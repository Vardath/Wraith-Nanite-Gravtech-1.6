using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Visible feedstock reserve for Asuran/human-form Replicator bodies.  Food remains a normal
    /// biological-style need, but positive nutrition gains are converted into nanite feedstock.
    /// Expensive reconstruction spends this reserve explicitly; the resource never drains merely
    /// because time passes.
    /// </summary>
    public sealed class Gene_Resource_NaniteReserve : Gene_Resource
    {
        public const float CopyPawnCost = 0.60f;
        public const float FastHealCost = 0.015f;
        public const float ReconstructPartCost = 0.25f;
        public const float ReserveGainPerFoodLevel = 0.35f;

        private float lastFoodLevel = -1f;

        public override float InitialResourceMax => 1f;
        public override float MinLevelForAlert => 0.10f;

        protected override Color BarColor => new Color(0.20f, 0.66f, 0.78f);
        protected override Color BarHighlightColor => new Color(0.43f, 0.88f, 0.98f);

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(60, delta))
                return;

            Need_Food food = pawn.needs?.food;
            if (food == null)
            {
                lastFoodLevel = -1f;
                return;
            }

            float current = food.CurLevel;
            if (lastFoodLevel >= 0f)
            {
                float gained = current - lastFoodLevel;
                if (gained > 0.001f && Value < Max)
                    Value = Math.Min(Max, Value + gained * ReserveGainPerFoodLevel);
            }
            lastFoodLevel = current;
        }

        public bool CanSpend(float amount)
        {
            return Active && amount >= 0f && Value + 0.0001f >= amount;
        }

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount))
                return false;
            Value = Math.Max(0f, Value - amount);
            return true;
        }

        public static Gene_Resource_NaniteReserve Get(Pawn pawn)
        {
            if (pawn?.genes == null)
                return null;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene is Gene_Resource_NaniteReserve reserve && gene.Active)
                    return reserve;
            }
            return null;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastFoodLevel, "wngNaniteLastFoodLevel", -1f);
        }
    }

    /// <summary>
    /// Core synthetic-body identity.  It gives WNG nanite bodies their food-poisoning immunity and
    /// performs a conservative one-shot old-save migration only when the rest of the established
    /// WNG nanite package is already present.
    /// </summary>
    public sealed class Gene_NaniteBody : Gene
    {
        private bool reserveMigrationChecked;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
                return;

            if (pawn.IsHashIntervalTick(60, delta))
            {
                HediffDef foodPoisoning = DefDatabase<HediffDef>.GetNamedSilentFail("FoodPoisoning");
                Hediff poisoning = foodPoisoning == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(foodPoisoning);
                if (poisoning != null)
                    pawn.health.RemoveHediff(poisoning);
            }

            if (reserveMigrationChecked || pawn.genes == null || !pawn.IsHashIntervalTick(600, delta))
                return;

            GeneDef reserve = DefDatabase<GeneDef>.GetNamedSilentFail("WNG_NaniteReserve");
            GeneDef reconstruction = DefDatabase<GeneDef>.GetNamedSilentFail("WNG_NaniteReconstruction");
            GeneDef neuralInterface = DefDatabase<GeneDef>.GetNamedSilentFail("WNG_NeuralInterface");
            if (reserve != null && reconstruction != null && neuralInterface != null
                && pawn.genes.GetGene(reserve) == null
                && pawn.genes.GetGene(reconstruction) != null
                && pawn.genes.GetGene(neuralInterface) != null)
            {
                bool asXenogene = pawn.genes.Xenogenes.Contains(this);
                pawn.genes.AddGene(reserve, asXenogene);
            }
            reserveMigrationChecked = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref reserveMigrationChecked, "wngNaniteReserveMigrationChecked", false);
        }
    }

    /// <summary>
    /// Fresh reserve-aware reconstruction loop.  Costs are paid only when a repair actually occurs:
    /// 1.5% reserve per 450-tick injury pulse and 25% reserve per 30,000-tick missing-part rebuild.
    /// With insufficient reserve the body falls back to the slower 1,800/90,000-tick emergency path.
    /// EMP disruption pauses both paths instead of silently consuming resources.
    /// </summary>
    public sealed class Gene_NaniteReconstruction : Gene
    {
        private const int FastInjuryTicks = 450;
        private const int EmergencyInjuryTicks = 1800;
        private const int FastPartTicks = 30000;
        private const int EmergencyPartTicks = 90000;

        private int nextInjuryTick;
        private int nextPartTick;

        public override void PostAdd()
        {
            base.PostAdd();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (nextInjuryTick <= 0)
                nextInjuryTick = now + FastInjuryTicks;
            if (nextPartTick <= 0)
                nextPartTick = now + FastPartTicks;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead || pawn.health?.hediffSet == null || IsDisrupted(pawn))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            Gene_Resource_NaniteReserve reserve = Gene_Resource_NaniteReserve.Get(pawn);

            if (now >= nextInjuryTick)
            {
                bool fast = reserve?.CanSpend(Gene_Resource_NaniteReserve.FastHealCost) == true;
                nextInjuryTick = now + (fast ? FastInjuryTicks : EmergencyInjuryTicks);

                Hediff_Injury injury = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .Where(x => !x.IsPermanent() && x.CanHealNaturally())
                    .OrderByDescending(x => x.Severity)
                    .FirstOrDefault();
                if (injury != null)
                {
                    if (fast)
                    {
                        if (reserve.TrySpend(Gene_Resource_NaniteReserve.FastHealCost))
                            injury.Heal(1.75f);
                    }
                    else
                    {
                        injury.Heal(1.25f);
                    }
                }
            }

            if (now >= nextPartTick)
            {
                bool fast = reserve?.CanSpend(Gene_Resource_NaniteReserve.ReconstructPartCost) == true;
                nextPartTick = now + (fast ? FastPartTicks : EmergencyPartTicks);
                BodyPartRecord missing = FindRestorableMissingPart();
                if (missing != null)
                {
                    if (!fast || reserve.TrySpend(Gene_Resource_NaniteReserve.ReconstructPartCost))
                        pawn.health.RestorePart(missing);
                }
            }
        }

        private BodyPartRecord FindRestorableMissingPart()
        {
            var present = pawn.health.hediffSet.GetNotMissingParts().ToList();
            return pawn.def.race.body.AllParts
                .Where(part => pawn.health.hediffSet.PartIsMissing(part)
                    && part.parent != null
                    && present.Contains(part.parent)
                    && !pawn.health.hediffSet.AncestorHasDirectlyAddedParts(part))
                .OrderByDescending(part => part.def.GetMaxHealth(pawn))
                .FirstOrDefault();
        }

        public static bool IsDisrupted(Pawn pawn)
        {
            HediffDef disruption = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_NaniteEMPDisruption");
            return disruption != null && pawn?.health?.hediffSet?.HasHediff(disruption) == true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextInjuryTick, "wngNaniteNextInjuryRepair", 0);
            Scribe_Values.Look(ref nextPartTick, "wngNaniteNextPartRepair", 0);
        }
    }

    public sealed class Gene_EMPSensitiveNanites : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            AddReceiverIfNeeded();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (Active && pawn != null && pawn.IsHashIntervalTick(600, delta))
                AddReceiverIfNeeded();
        }

        public override void PostRemove()
        {
            if (pawn?.health?.hediffSet != null)
            {
                HediffDef tracker = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_NaniteEMPReceiver");
                Hediff existing = tracker == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(tracker);
                if (existing != null)
                    pawn.health.RemoveHediff(existing);
            }
            base.PostRemove();
        }

        private void AddReceiverIfNeeded()
        {
            if (pawn?.health?.hediffSet == null)
                return;
            HediffDef tracker = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_NaniteEMPReceiver");
            if (tracker != null && !pawn.health.hediffSet.HasHediff(tracker))
                pawn.health.AddHediff(tracker);
        }
    }

    public sealed class HediffCompProperties_NaniteEMPReceiver : HediffCompProperties
    {
        public HediffCompProperties_NaniteEMPReceiver()
        {
            compClass = typeof(HediffComp_NaniteEMPReceiver);
        }
    }

    public sealed class HediffComp_NaniteEMPReceiver : HediffComp
    {
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            Pawn target = parent?.pawn;
            if (target?.health?.hediffSet == null || dinfo.Def != DamageDefOf.EMP)
                return;

            HediffDef disruption = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_NaniteEMPDisruption");
            if (disruption == null)
                return;

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(disruption);
            if (existing == null)
                target.health.AddHediff(disruption);
            else
                existing.Severity = Math.Max(existing.Severity, 1f);
        }
    }

    /// <summary>
    /// Unique sovereign identity marker.  It is deliberately absent from the ordinary human-form
    /// xenotype and is added only to the exact Replicator Queen pawn by Queen-generation logic.
    /// </summary>
    public sealed class Gene_ReplicatorQueenLink : Gene
    {
        private int nextAbilityCheck;

        public override void PostAdd()
        {
            base.PostAdd();
            EnsureDirective();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn?.abilities == null)
                return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextAbilityCheck)
                return;
            nextAbilityCheck = now + 300;
            EnsureDirective();
        }

        private void EnsureDirective()
        {
            if (!Active || pawn?.abilities == null)
                return;
            AbilityDef directive = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_SovereignLatticeDirective");
            if (directive != null && pawn.abilities.GetAbility(directive) == null)
                pawn.abilities.GainAbility(directive);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextAbilityCheck, "wngQueenDirectiveAbilityCheck", 0);
        }
    }
}