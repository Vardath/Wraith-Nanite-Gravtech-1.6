using System;
using RimWorld;
using Verse;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityDrainLife : CompProperties_AbilityEffect
    {
        public int victimAgeYears = 50;
        public int casterRejuvenationYears = 5;
        public int minimumCasterAgeYears = 18;
        public float lifeForceGain = 1f;
        public HediffDef victimHediff;
        public HediffDef casterHediff;

        public CompProperties_AbilityDrainLife()
        {
            compClass = typeof(CompAbilityEffect_DrainLife);
        }
    }

    /// <summary>
    /// The single full Wraith feeding/withering path. It only works at touch range against a
    /// downed biological pawn. A successful feed ages the victim by fifty biological years,
    /// rejuvenates the Wraith by five years without ever crossing age eighteen, refreshes the
    /// temporary victim/caster states, and replenishes the visible Life Force resource.
    /// </summary>
    public sealed class CompAbilityEffect_DrainLife : CompAbilityEffect
    {
        private const long TicksPerYear = 3600000L;
        private CompProperties_AbilityDrainLife DrainProps => (CompProperties_AbilityDrainLife)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn victim = target.Pawn;
            bool valid = victim != null
                && victim != parent.pawn
                && !victim.Dead
                && victim.Downed
                && victim.RaceProps != null
                && victim.RaceProps.IsFlesh
                && !victim.RaceProps.IsMechanoid
                && !IsSynthetic(victim);

            if (!valid && throwMessages)
            {
                Messages.Message(
                    "Drain Life requires a downed living biological pawn within reach.",
                    parent.pawn,
                    MessageTypeDefOf.RejectInput,
                    historical: false);
            }

            return valid && base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            if (caster == null || victim == null || victim.Dead || !victim.Downed)
                return;

            ShiftBiologicalAge(victim, Math.Max(0, DrainProps.victimAgeYears), 0);
            ShiftBiologicalAge(caster, -Math.Max(0, DrainProps.casterRejuvenationYears), Math.Max(0, DrainProps.minimumCasterAgeYears));

            RefreshHediff(victim, DrainProps.victimHediff);
            RefreshHediff(caster, DrainProps.casterHediff);
            WraithLifeForceUtility.Offset(caster, Math.Max(0f, DrainProps.lifeForceGain));

            if (DrainProps.sound != null && caster.Spawned && caster.Map != null)
                DrainProps.sound.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
        }

        private static bool IsSynthetic(Pawn pawn)
        {
            string xenotype = pawn?.genes?.Xenotype?.defName ?? string.Empty;
            return xenotype == "WNG_HumanFormReplicator" || xenotype == "WNG_NanitePrecursor";
        }

        private static void ShiftBiologicalAge(Pawn pawn, int years, int minimumYears)
        {
            if (pawn?.ageTracker == null || years == 0)
                return;

            long delta;
            try
            {
                checked { delta = (long)years * TicksPerYear; }
            }
            catch (OverflowException)
            {
                delta = years > 0 ? long.MaxValue : long.MinValue;
            }

            long next;
            try
            {
                checked { next = pawn.ageTracker.AgeBiologicalTicks + delta; }
            }
            catch (OverflowException)
            {
                next = years > 0 ? long.MaxValue : 0L;
            }

            long minimum = Math.Max(0L, (long)minimumYears * TicksPerYear);
            if (next < minimum)
                next = minimum;
            if (next < 0L)
                next = 0L;

            pawn.ageTracker.AgeBiologicalTicks = next;
        }

        private static void RefreshHediff(Pawn pawn, HediffDef def)
        {
            if (pawn?.health == null || def == null)
                return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
                pawn.health.RemoveHediff(existing);
            pawn.health.AddHediff(def);
        }
    }
}
