using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityDrainLife : CompProperties_AbilityEffect
    {
        public int victimAgeYears = 50;
        public int casterRejuvenationYears = 5;
        public int minimumCasterAgeYears = 18;
        public float lifeForceGain = 1f;
        public bool fatalIfAlreadyLifeDrained = true;
        public HediffDef victimHediff;
        public HediffDef casterHediff;
        public CompProperties_AbilityDrainLife() => compClass = typeof(CompAbilityEffect_DrainLife);
    }

    public sealed class CompAbilityEffect_DrainLife : CompAbilityEffect
    {
        private const long TicksPerYear = 3600000L;
        private CompProperties_AbilityDrainLife DrainProps => (CompProperties_AbilityDrainLife)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn victim = target.Pawn;
            bool valid = victim != null && victim != parent.pawn && !victim.Dead && victim.Downed
                && victim.RaceProps != null && victim.RaceProps.IsFlesh && !victim.RaceProps.IsMechanoid
                && !IsSynthetic(victim);
            if (!valid && throwMessages)
                Messages.Message("Wraith feeding requires a downed living biological pawn within reach.", parent.pawn, MessageTypeDefOf.RejectInput, false);
            return valid && base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            Pawn victim = target.Pawn;
            if (caster == null || victim == null || victim.Dead || !victim.Downed) return;

            bool lethalRepeat = DrainProps.fatalIfAlreadyLifeDrained && DrainProps.victimHediff != null
                && victim.health?.hediffSet?.HasHediff(DrainProps.victimHediff) == true;

            ShiftBiologicalAge(victim, Math.Max(0, DrainProps.victimAgeYears), 0);
            ShiftBiologicalAge(caster, -Math.Max(0, DrainProps.casterRejuvenationYears), Math.Max(0, DrainProps.minimumCasterAgeYears));
            RefreshHediff(victim, DrainProps.victimHediff);
            RefreshHediff(caster, DrainProps.casterHediff);
            WraithLifeForceUtility.Offset(caster, Math.Max(0f, DrainProps.lifeForceGain));

            if (lethalRepeat && !victim.Dead) victim.Kill(null);
        }

        private static bool IsSynthetic(Pawn pawn)
        {
            string identity = ((pawn?.genes?.Xenotype?.defName ?? string.Empty) + " " + (pawn?.def?.defName ?? string.Empty) + " " + (pawn?.kindDef?.defName ?? string.Empty)).ToLowerInvariant();
            return identity.Contains("replicator") || identity.Contains("asuran") || identity.Contains("nanite");
        }

        private static void ShiftBiologicalAge(Pawn pawn, int years, int minimumYears)
        {
            if (pawn?.ageTracker == null || years == 0) return;
            long delta = (long)years * TicksPerYear;
            long next;
            try { checked { next = pawn.ageTracker.AgeBiologicalTicks + delta; } }
            catch (OverflowException) { next = years > 0 ? long.MaxValue : 0L; }
            long minimum = Math.Max(0L, (long)minimumYears * TicksPerYear);
            pawn.ageTracker.AgeBiologicalTicks = Math.Max(minimum, Math.Max(0L, next));
        }

        private static void RefreshHediff(Pawn pawn, HediffDef def)
        {
            if (pawn?.health == null || def == null) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null) pawn.health.RemoveHediff(existing);
            pawn.health.AddHediff(def);
        }
    }

    public sealed class CompProperties_AbilityWraithHibernate : CompProperties_AbilityEffect
    {
        public HediffDef hibernatingHediff;
        public CompProperties_AbilityWraithHibernate() => compClass = typeof(CompAbilityEffect_WraithHibernate);
    }

    public sealed class CompAbilityEffect_WraithHibernate : CompAbilityEffect
    {
        private CompProperties_AbilityWraithHibernate HibernateProps => (CompProperties_AbilityWraithHibernate)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            bool valid = caster != null && !caster.Dead && WraithLifeForceUtility.IsWraith(caster)
                && HibernateProps.hibernatingHediff != null && caster.health?.hediffSet != null
                && !caster.health.hediffSet.HasHediff(HibernateProps.hibernatingHediff);
            if (!valid && throwMessages && caster != null)
                Messages.Message("Only an active Wraith that is not already hibernating can enter hibernation.", caster, MessageTypeDefOf.RejectInput, false);
            return valid && base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Dead || !WraithLifeForceUtility.IsWraith(caster) || HibernateProps.hibernatingHediff == null) return;
            if (caster.health?.hediffSet?.HasHediff(HibernateProps.hibernatingHediff) == true) return;
            caster.health?.AddHediff(HibernateProps.hibernatingHediff);
        }
    }
}
