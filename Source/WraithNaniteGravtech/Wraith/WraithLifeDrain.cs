using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityLifeDrain : CompProperties_AbilityEffect
    {
        public long victimAgeYears = 50L;
        public long casterRejuvenationYears = 5L;
        public long minimumCasterAgeYears = 18L;
        public float lifeForceGain = 1f;
        public bool killIfAlreadyDrained = true;
        public bool addLifeDrainedHediff = true;
        public bool addFedRecentlyHediff = true;

        public CompProperties_AbilityLifeDrain()
        {
            compClass = typeof(CompAbilityEffect_LifeDrain);
        }
    }

    /// <summary>
    /// One transaction owns both full Drain Life and Partial Feed. Strategic Wraith faction hunger
    /// is intentionally absent: ordinary pawn feeding only changes the two pawns and the caster's
    /// Life Force resource.
    /// </summary>
    public sealed class CompAbilityEffect_LifeDrain : CompAbilityEffect
    {
        private const long TicksPerYear = 3600000L;
        private const string LifeDrainedDefName = "WNG_LifeDrained";
        private const string FedRecentlyDefName = "WNG_FedRecently";

        public new CompProperties_AbilityLifeDrain Props => (CompProperties_AbilityLifeDrain)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn victim = target.Pawn;
            Pawn caster = parent?.pawn;
            if (!IsValidBiologicalTarget(caster, victim))
                return;

            Gene_Resource_LifeForce resource = caster.genes?.GetFirstGeneOfType<Gene_Resource_LifeForce>();
            if (resource == null || !resource.Active)
                return;

            // Hoffan protection intercepts before any feeding-side transaction commits: no victim
            // aging, no Life Force gain, no Wraith rejuvenation, no Fed Recently marker and no
            // completed-feeding Ideology event. The failed feed instead poisons the exact Wraith.
            if (HoffanSerumUtility.HasProtection(victim))
            {
                HoffanSerumUtility.TryPoisonFeeder(caster, victim);
                return;
            }

            HediffDef lifeDrainedDef = DefDatabase<HediffDef>.GetNamedSilentFail(LifeDrainedDefName);
            bool alreadyLifeDrained = lifeDrainedDef != null &&
                victim.health?.hediffSet?.GetFirstHediffOfDef(lifeDrainedDef) != null;

            // Full Drain Life is deliberately lethal when repeated before the victim's Life Drained
            // state has recovered. Partial Feed shares the transaction but opts out of this rule.
            if (alreadyLifeDrained && Props.killIfAlreadyDrained)
            {
                victim.Kill(null);
                return;
            }

            if (Props.victimAgeYears != 0L)
                AdjustBiologicalAge(victim, Props.victimAgeYears);
            if (Props.addLifeDrainedHediff)
                AddOrRefreshHediff(victim, LifeDrainedDefName);

            if (Props.casterRejuvenationYears != 0L)
                AdjustBiologicalAge(caster, -Props.casterRejuvenationYears, Props.minimumCasterAgeYears);
            if (Props.addFedRecentlyHediff)
                AddOrRefreshHediff(caster, FedRecentlyDefName);

            resource.AddLifeForce(Math.Max(0f, Props.lifeForceGain));

            // Ideology is observational here: only a completed feeding transaction emits the
            // history event, and belief handling can never cancel or alter the Life Force commit.
            WNGIdeologyEvents.RecordWraithFeeding(caster, victim);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn victim = target.Pawn;
            Pawn caster = parent?.pawn;
            if (!IsValidBiologicalTarget(caster, victim))
            {
                if (throwMessages && caster != null)
                {
                    Messages.Message(
                        "Drain Life requires another living biological pawn; mechanical and non-flesh targets contain no usable Life Force.",
                        caster,
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }
                return false;
            }

            Gene_Resource_LifeForce resource = caster.genes?.GetFirstGeneOfType<Gene_Resource_LifeForce>();
            if (resource == null || !resource.Active)
            {
                if (throwMessages)
                {
                    Messages.Message(
                        "This pawn has no active Wraith Life Force reserve.",
                        caster,
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        private static bool IsValidBiologicalTarget(Pawn caster, Pawn victim)
        {
            return caster != null &&
                   victim != null &&
                   victim != caster &&
                   !victim.Dead &&
                   victim.RaceProps != null &&
                   victim.RaceProps.IsFlesh &&
                   !victim.RaceProps.IsMechanoid;
        }

        private static void AddOrRefreshHediff(Pawn pawn, string defName)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (hediffDef == null)
                return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
                pawn.health.RemoveHediff(existing);
            pawn.health.AddHediff(hediffDef);
        }

        private static void AdjustBiologicalAge(Pawn pawn, long yearsDelta, long minimumYears = 0L)
        {
            if (pawn?.ageTracker == null || yearsDelta == 0L)
                return;

            long current = pawn.ageTracker.AgeBiologicalTicks;
            long next;
            try
            {
                checked
                {
                    next = current + yearsDelta * TicksPerYear;
                }
            }
            catch (OverflowException)
            {
                next = yearsDelta > 0L ? long.MaxValue : 0L;
            }

            long minimum;
            try
            {
                checked
                {
                    minimum = Math.Max(0L, minimumYears) * TicksPerYear;
                }
            }
            catch (OverflowException)
            {
                minimum = long.MaxValue;
            }

            if (next < minimum)
                next = minimum;
            if (next < 0L)
                next = 0L;
            pawn.ageTracker.AgeBiologicalTicks = next;
        }
    }

    public sealed class CompProperties_AbilityEnthrall : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityEnthrall()
        {
            compClass = typeof(CompAbilityEffect_Enthrall);
        }
    }

    /// <summary>
    /// Restored Wraith Enthrall contract: a Wraith may immediately enslave the exact targeted
    /// humanlike pawn when that pawn is downed or already a prisoner. Native guest/slave state owns
    /// the transition; WNG creates no proxy pawn and no parallel slavery system.
    /// </summary>
    public sealed class CompAbilityEffect_Enthrall : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn victim = target.Pawn;
            Pawn caster = parent?.pawn;
            if (!CanEnthrall(caster, victim))
                return;

            GenGuest.TryEnslavePrisoner(caster, victim);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn victim = target.Pawn;
            Pawn caster = parent?.pawn;
            if (!CanEnthrall(caster, victim))
            {
                if (throwMessages && caster != null)
                {
                    Messages.Message(
                        "Enthrall requires another living humanlike pawn who is downed or already a prisoner.",
                        caster,
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        private static bool CanEnthrall(Pawn caster, Pawn victim)
        {
            return caster != null &&
                   caster.Faction != null &&
                   victim != null &&
                   victim != caster &&
                   !victim.Dead &&
                   victim.RaceProps != null &&
                   victim.RaceProps.Humanlike &&
                   victim.guest != null &&
                   !victim.IsSlave &&
                   victim.Faction != caster.Faction &&
                   (victim.Downed || victim.IsPrisoner);
        }
    }
}
