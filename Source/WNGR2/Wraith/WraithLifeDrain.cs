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
        public int biomassGain;
        public bool fatalIfAlreadyLifeDrained;
        public HediffDef victimHediff;
        public HediffDef casterHediff;

        public CompProperties_AbilityDrainLife()
        {
            compClass = typeof(CompAbilityEffect_DrainLife);
        }
    }

    /// <summary>
    /// Shared Wraith feeding effect used by full Drain Life and the deliberately weaker Partial Feed.
    /// Both operate at touch range on a downed living biological pawn, shift biological age, refill
    /// the same native Life Force gene resource and yield cultured biomass. Full feeding can be made
    /// lethal when repeated before the victim's Life Drained state has cleared; partial feeding never
    /// uses that lethal-repeat rule.
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
                    "Wraith feeding requires a downed living biological pawn within reach.",
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

            bool lethalRepeat = DrainProps.fatalIfAlreadyLifeDrained
                && DrainProps.victimHediff != null
                && victim.health?.hediffSet?.GetFirstHediffOfDef(DrainProps.victimHediff) != null;

            ShiftBiologicalAge(victim, Math.Max(0, DrainProps.victimAgeYears), 0);
            ShiftBiologicalAge(caster, -Math.Max(0, DrainProps.casterRejuvenationYears), Math.Max(0, DrainProps.minimumCasterAgeYears));

            RefreshHediff(victim, DrainProps.victimHediff);
            RefreshHediff(caster, DrainProps.casterHediff);
            WraithLifeForceUtility.Offset(caster, Math.Max(0f, DrainProps.lifeForceGain));
            ProduceBiomass(caster, victim, Math.Max(0, DrainProps.biomassGain));

            if (DrainProps.sound != null && caster.Spawned && caster.Map != null)
                DrainProps.sound.PlayOneShot(new TargetInfo(caster.Position, caster.Map));

            if (lethalRepeat && !victim.Dead)
                victim.Kill(null);
        }

        private static void ProduceBiomass(Pawn caster, Pawn victim, int amount)
        {
            if (amount <= 0)
                return;

            ThingDef biomassDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            Map map = caster?.Map ?? victim?.Map;
            if (biomassDef == null || map == null)
                return;

            IntVec3 cell = caster != null && caster.Spawned ? caster.Position : victim.Position;
            Thing biomass = ThingMaker.MakeThing(biomassDef);
            biomass.stackCount = amount;
            GenPlace.TryPlaceThing(biomass, cell, map, ThingPlaceMode.Near);
        }

        private static bool IsSynthetic(Pawn pawn)
        {
            string xenotype = pawn?.genes?.Xenotype?.defName ?? string.Empty;
            string race = pawn?.def?.defName ?? string.Empty;
            string kind = pawn?.kindDef?.defName ?? string.Empty;
            return LooksSynthetic(xenotype) || LooksSynthetic(race) || LooksSynthetic(kind);
        }

        private static bool LooksSynthetic(string value)
        {
            if (value.NullOrEmpty())
                return false;
            return value.IndexOf("Replicator", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Asuran", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Nanite", StringComparison.OrdinalIgnoreCase) >= 0;
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
