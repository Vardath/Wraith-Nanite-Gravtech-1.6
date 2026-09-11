using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Native projectile used by learned Replicator ranged adaptation. Native projectile
    /// interceptors see this shot normally. AntiShield increases the damage value presented to
    /// those shield interceptors, while ordinary body impact keeps the configured base damage.
    /// Existing WNG block adaptive shields retain their prior AntiShield countermeasure behavior.
    /// </summary>
    public sealed class Projectile_ReplicatorAdaptiveBolt : Bullet
    {
        private bool resolvingOrdinaryBodyImpact;

        private CompReplicatorAdaptationEffects LauncherEffects
            => (Launcher as Pawn)?.TryGetComp<CompReplicatorAdaptationEffects>();

        private bool LauncherHasAntiShield
            => LauncherEffects?.State?.HasAdaptation(ReplicatorAdaptation.AntiShield) == true;

        private int NormalBodyDamage
            => Math.Max(1, LauncherEffects?.AdaptiveProjectileBodyDamage ?? base.DamageAmount);

        public override int DamageAmount
        {
            get
            {
                int normal = NormalBodyDamage;
                if (resolvingOrdinaryBodyImpact || !LauncherHasAntiShield)
                    return normal;
                float multiplier = LauncherEffects?.AdaptiveAntiShieldMultiplier ?? 1f;
                return Math.Max(normal, (int)Math.Ceiling(normal * Math.Max(1f, multiplier)));
            }
        }

        public override float ArmorPenetration
            => Math.Max(0f, LauncherEffects?.AdaptiveProjectileArmorPenetration ?? base.ArmorPenetration);

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            bool targetHasWngAdaptiveShield = !blockedByShield && LauncherHasAntiShield &&
                hitThing is Pawn target &&
                target.TryGetComp<CompReplicatorAdaptationEffects>()?.State?.HasAdaptation(ReplicatorAdaptation.Shield) == true;

            // Native interceptor shields have already consumed DamageAmount before this call and
            // invoke Impact(... blockedByShield:true). For a genuine unshielded body impact use
            // normal body damage. WNG block adaptive shields preserve their existing amplified
            // AntiShield interaction by leaving the amplified amount visible to their damage comp.
            resolvingOrdinaryBodyImpact = !targetHasWngAdaptiveShield;
            try
            {
                base.Impact(hitThing, blockedByShield);
            }
            finally
            {
                resolvingOrdinaryBodyImpact = false;
            }
        }
    }
}
