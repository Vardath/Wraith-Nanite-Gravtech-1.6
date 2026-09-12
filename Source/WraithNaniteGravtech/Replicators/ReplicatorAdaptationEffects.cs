using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorAdaptationEffects : CompProperties
    {
        public float armorDeflectionChance = 0.12f;
        public int shieldRechargeTicks = 1800;

        public CompProperties_ReplicatorAdaptationEffects()
        {
            compClass = typeof(CompReplicatorAdaptationEffects);
        }
    }

    /// <summary>
    /// Turns learned flags into local, save-safe body behavior. Material learning is consumed by
    /// the assimilation job, Power by regeneration/specialist timing, Ranged by artillery,
    /// Grav by hierarchy reach, and this comp supplies the Armor and Shield body responses.
    /// </summary>
    public sealed class CompReplicatorAdaptationEffects : ThingComp
    {
        private int shieldReadyTick;
        private CompProperties_ReplicatorAdaptationEffects Props => (CompProperties_ReplicatorAdaptationEffects)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            if (state == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (dinfo.Def == DamageDefOf.EMP)
            {
                shieldReadyTick = Math.Max(shieldReadyTick, now + Math.Max(60, Props.shieldRechargeTicks));
                return;
            }

            if (state.EMPSuppressed)
                return;

            ReplicatorAdaptationFlags learned = state.Adaptations;
            if ((learned & ReplicatorAdaptationFlags.Shield) != 0 && now >= shieldReadyTick)
            {
                absorbed = true;
                shieldReadyTick = now + Math.Max(60, Props.shieldRechargeTicks);
                return;
            }

            if ((learned & ReplicatorAdaptationFlags.Armor) != 0 && IsPhysicalDamage(dinfo.Def) &&
                Rand.Chance(Math.Max(0f, Math.Min(0.45f, Props.armorDeflectionChance))))
            {
                absorbed = true;
            }
        }

        private static bool IsPhysicalDamage(DamageDef def)
        {
            return def == DamageDefOf.Bullet || def == DamageDefOf.Cut || def == DamageDefOf.Blunt;
        }

        public override string CompInspectStringExtra()
        {
            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            if (state == null || state.Adaptations == ReplicatorAdaptationFlags.None)
                return null;

            return "Replicator adaptations: " + state.Adaptations;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shieldReadyTick, "wngReplicatorShieldReadyTick", 0);
        }
    }
}
