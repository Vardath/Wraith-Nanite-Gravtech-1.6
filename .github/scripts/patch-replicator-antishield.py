from pathlib import Path

p = Path('Source/WNG/Replicators/ReplicatorAdaptationEffects.cs')
s = p.read_text(encoding='utf-8')

marker = '''        public float PowerHealingMultiplier => State?.HasAdaptation(ReplicatorAdaptation.Power) == true
            ? Math.Max(1f, Props.powerHealingMultiplier)
            : 1f;
'''
insert = marker + '''
        internal int AdaptiveProjectileBodyDamage => Math.Max(1, (int)Math.Round(Math.Max(1f, Props.rangedDamage)));
        internal float AdaptiveProjectileArmorPenetration => Math.Max(0f, Props.rangedArmorPenetration);
        internal float AdaptiveAntiShieldMultiplier => Math.Max(1f, Props.antiShieldDamageMultiplier);
'''
if 'internal int AdaptiveProjectileBodyDamage' not in s:
    if marker not in s:
        raise SystemExit('Projectile accessor insertion point missing')
    s = s.replace(marker, insert, 1)

old = '''            float damage = Math.Max(1f, Props.rangedDamage);
            CompReplicatorAdaptationEffects targetEffects = target.TryGetComp<CompReplicatorAdaptationEffects>();
            if (State.HasAdaptation(ReplicatorAdaptation.AntiShield)
                && targetEffects?.State?.HasAdaptation(ReplicatorAdaptation.Shield) == true)
                damage *= Math.Max(1f, Props.antiShieldDamageMultiplier);

            target.TakeDamage(new DamageInfo(
                DamageDefOf.Bullet,
                damage,
                Math.Max(0f, Props.rangedArmorPenetration),
                instigator: pawn));
'''
new = '''            TryLaunchAdaptiveProjectile(pawn, target);
'''
if old in s:
    s = s.replace(old, new, 1)
elif 'TryLaunchAdaptiveProjectile(pawn, target);' not in s:
    raise SystemExit('Direct ranged damage block not found')

insert_point = '''        private void TryAutonomousGravReposition(Pawn pawn)
'''
helper = '''        private bool TryLaunchAdaptiveProjectile(Pawn pawn, Pawn target)
        {
            if (pawn == null || target == null || pawn.Map == null || !pawn.Spawned || !target.Spawned)
                return false;

            ThingDef projectileDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorAdaptiveBolt");
            Projectile projectile = projectileDef == null ? null : ThingMaker.MakeThing(projectileDef) as Projectile;
            if (projectile == null)
            {
                Log.Error("[WNG] Replicator adaptive ranged fire could not create WNG_ReplicatorAdaptiveBolt.");
                return false;
            }

            GenSpawn.Spawn(projectile, pawn.Position, pawn.Map);
            projectile.Launch(
                pawn,
                target,
                target,
                ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld,
                preventFriendlyFire: true);
            return true;
        }

'''
if 'private bool TryLaunchAdaptiveProjectile' not in s:
    if insert_point not in s:
        raise SystemExit('Projectile helper insertion point missing')
    s = s.replace(insert_point, helper + insert_point, 1)

p.write_text(s, encoding='utf-8')
