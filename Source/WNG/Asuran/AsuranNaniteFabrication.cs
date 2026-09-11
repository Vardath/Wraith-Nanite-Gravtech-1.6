using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class AsuranNanitePhysiologyExtension : DefModExtension
    {
        public int reconcileIntervalTicks = 120;
        public int healIntervalTicks = 250;
        public float baseHealAmount = 0.12f;
        public float reserveCostPerHealPoint = 0.004f;
        public float depletionThreshold = 0.15f;
        public int empSuppressionTicks = 1800;
    }

    /// <summary>
    /// Human-form Replicators/Asurans use RimWorld's exact Need_Food machinery as a matter reserve.
    /// The custom NeedDef remains Need_Food, so native eating/caravan/feeding behavior stays intact;
    /// only its WNG meaning and synthetic consequences change. Block Replicators do not use this.
    /// </summary>
    public static class AsuranNaniteUtility
    {
        public static Gene_AsuranNanitePhysiology GetPhysiology(Pawn pawn)
        {
            if (pawn?.genes == null)
                return null;
            return pawn.genes.GenesListForReading
                .OfType<Gene_AsuranNanitePhysiology>()
                .FirstOrDefault(g => g.Active);
        }

        public static bool IsNaniteHumanoid(Pawn pawn) => GetPhysiology(pawn) != null;

        public static Need_Food Reserve(Pawn pawn)
        {
            if (!IsNaniteHumanoid(pawn))
                return null;
            Need_Food reserve = pawn.needs?.food;
            return reserve?.def?.defName == "WNG_NaniteMatterReserve" ? reserve : null;
        }

        public static float ReservePercent(Pawn pawn)
        {
            Need_Food reserve = Reserve(pawn);
            if (reserve == null || reserve.MaxLevel <= 0f)
                return 0f;
            return Mathf.Clamp01(reserve.CurLevel / reserve.MaxLevel);
        }

        public static bool CanSpendFraction(Pawn pawn, float fraction)
        {
            Need_Food reserve = Reserve(pawn);
            if (reserve == null)
                return false;
            float cost = reserve.MaxLevel * Mathf.Clamp01(fraction);
            return reserve.CurLevel + 0.0001f >= cost;
        }

        public static bool TrySpendFraction(Pawn pawn, float fraction)
        {
            Need_Food reserve = Reserve(pawn);
            if (reserve == null)
                return false;
            float cost = reserve.MaxLevel * Mathf.Clamp01(fraction);
            if (reserve.CurLevel + 0.0001f < cost)
                return false;
            reserve.CurLevel = Math.Max(0f, reserve.CurLevel - cost);
            return true;
        }

        public static bool TrySpendForRepair(Pawn pawn, float healAmount, float reserveFractionPerHealPoint, out float affordableHeal)
        {
            affordableHeal = 0f;
            Need_Food reserve = Reserve(pawn);
            if (reserve == null || healAmount <= 0f)
                return false;

            float costPerPoint = Math.Max(0f, reserveFractionPerHealPoint) * reserve.MaxLevel;
            if (costPerPoint <= 0f)
            {
                affordableHeal = healAmount;
                return true;
            }

            affordableHeal = Math.Min(healAmount, reserve.CurLevel / costPerPoint);
            if (affordableHeal <= 0.0001f)
                return false;

            reserve.CurLevel = Math.Max(0f, reserve.CurLevel - affordableHeal * costPerPoint);
            return true;
        }
    }

    public sealed class Gene_AsuranNanitePhysiology : Gene
    {
        private int nextReconcileTick;

        public AsuranNanitePhysiologyExtension Extension => def?.GetModExtension<AsuranNanitePhysiologyExtension>();

        public override void PostAdd()
        {
            base.PostAdd();
            ReconcilePhysiology();
        }

        public override void PostRemove()
        {
            RemoveNamedHediff("WNG_AsuranNaniteLattice");
            RemoveNamedHediff("WNG_NaniteDepletion");
            RemoveNamedHediff("WNG_AsuranEMPDisrupted");
            pawn?.needs?.AddOrRemoveNeedsAsAppropriate();
            base.PostRemove();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextReconcileTick)
                return;
            nextReconcileTick = now + Math.Max(30, Extension?.reconcileIntervalTicks ?? 120);
            ReconcilePhysiology();
        }

        private void ReconcilePhysiology()
        {
            if (!Active || pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
                return;

            // The gene disables vanilla Food and enables WNG_NaniteMatterReserve. Both use the exact
            // Need_Food class, so this rebinding preserves native eating/caravan behavior.
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();

            EnsureNamedHediff("WNG_AsuranNaniteLattice");

            // Need_Food normally expresses starvation as biological malnutrition. Nanite humanoids
            // instead weaken/shut down through WNG_NaniteDepletion, so never leave Malnutrition on them.
            Hediff malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
                pawn.health.RemoveHediff(malnutrition);

            Need_Food reserve = AsuranNaniteUtility.Reserve(pawn);
            float threshold = Math.Max(0.001f, Extension?.depletionThreshold ?? 0.15f);
            float reservePct = reserve == null || reserve.MaxLevel <= 0f ? 0f : Mathf.Clamp01(reserve.CurLevel / reserve.MaxLevel);
            HediffDef depletionDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_NaniteDepletion");
            if (depletionDef == null)
                return;

            Hediff depletion = pawn.health.hediffSet.GetFirstHediffOfDef(depletionDef);
            if (reservePct >= threshold)
            {
                if (depletion != null)
                    pawn.health.RemoveHediff(depletion);
                return;
            }

            if (depletion == null)
            {
                depletion = HediffMaker.MakeHediff(depletionDef, pawn);
                pawn.health.AddHediff(depletion);
            }
            if (depletion != null)
                depletion.Severity = Mathf.Clamp01((threshold - reservePct) / threshold);
        }

        private void EnsureNamedHediff(string defName)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (hediffDef == null || pawn.health.hediffSet.HasHediff(hediffDef))
                return;
            pawn.health.AddHediff(hediffDef);
        }

        private void RemoveNamedHediff(string defName)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (hediffDef == null || pawn?.health?.hediffSet == null)
                return;
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextReconcileTick, "wngAsuranPhysiologyReconcileTick", 0);
        }
    }

    /// <summary>
    /// Persistent nanite-lattice state for a human-form Replicator/Asuran. EMP disruption and
    /// self-repair are attached to the exact pawn and survive save/load. Repair spends the same
    /// native Need_Food matter reserve that ordinary ingestion refills.
    /// </summary>
    public sealed class Hediff_AsuranNaniteLattice : Hediff
    {
        private int suppressedUntil;
        private int nextHealTick;

        private Gene_AsuranNanitePhysiology Physiology => AsuranNaniteUtility.GetPhysiology(pawn);
        private AsuranNanitePhysiologyExtension Extension => Physiology?.Extension;
        private bool EmpSuppressed => (Find.TickManager?.TicksGame ?? 0) < suppressedUntil;

        public override string LabelInBrackets
        {
            get
            {
                if (!EmpSuppressed)
                    return null;
                int remaining = Math.Max(0, suppressedUntil - (Find.TickManager?.TicksGame ?? 0));
                return $"EMP disrupted {remaining / 2500f:0.0}h";
            }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (dinfo.Def != DamageDefOf.EMP || pawn == null || pawn.Dead)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            int duration = Math.Max(60, Extension?.empSuppressionTicks ?? 1800);
            long until = (long)now + duration;
            suppressedUntil = Math.Max(suppressedUntil, until >= int.MaxValue ? int.MaxValue : (int)until);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn == null || pawn.Dead || Physiology == null || pawn.health?.hediffSet == null)
                return;

            SyncEmpDisruptionHediff();

            int now = Find.TickManager?.TicksGame ?? 0;
            if (EmpSuppressed)
            {
                nextHealTick = Math.Max(nextHealTick, suppressedUntil);
                return;
            }
            if (now < nextHealTick)
                return;

            nextHealTick = now + Math.Max(30, Extension?.healIntervalTicks ?? 250);
            Hediff_Injury injury = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.CanHealNaturally() && x.Severity > 0f)
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();
            if (injury == null)
                return;

            float desiredHeal = Math.Min(injury.Severity, Math.Max(0f, Extension?.baseHealAmount ?? 0.12f));
            if (!AsuranNaniteUtility.TrySpendForRepair(
                    pawn,
                    desiredHeal,
                    Math.Max(0f, Extension?.reserveCostPerHealPoint ?? 0.004f),
                    out float affordableHeal))
                return;

            if (affordableHeal > 0f)
                injury.Heal(affordableHeal);
        }

        private void SyncEmpDisruptionHediff()
        {
            HediffDef disruptedDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_AsuranEMPDisrupted");
            if (disruptedDef == null)
                return;

            Hediff disrupted = pawn.health.hediffSet.GetFirstHediffOfDef(disruptedDef);
            if (EmpSuppressed)
            {
                if (disrupted == null)
                    pawn.health.AddHediff(disruptedDef);
            }
            else if (disrupted != null)
            {
                pawn.health.RemoveHediff(disrupted);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref suppressedUntil, "wngAsuranEmpUntil", 0);
            Scribe_Values.Look(ref nextHealTick, "wngAsuranNextHealTick", 0);
        }
    }

    public sealed class CompProperties_AbilityAssembleAsuranWorkshop : CompProperties_AbilityEffect
    {
        public ThingDef workshopDef;
        public ResearchProjectDef researchPrerequisite;
        public float reserveCost = 0.60f;

        public CompProperties_AbilityAssembleAsuranWorkshop()
        {
            compClass = typeof(CompAbilityEffect_AssembleAsuranWorkshop);
        }
    }

    /// <summary>
    /// Reconstructs the Asuran production workshop directly from a finite personal matter reserve.
    /// The cost is a fraction of the caster's native Need_Food-derived nanite reserve and is charged
    /// only after valid placement succeeds.
    /// </summary>
    public sealed class CompAbilityEffect_AssembleAsuranWorkshop : CompAbilityEffect
    {
        private CompProperties_AbilityAssembleAsuranWorkshop WorkshopProps =>
            (CompProperties_AbilityAssembleAsuranWorkshop)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Dead || !caster.IsColonistPlayerControlled)
                return Reject("Only a player-controlled nanite humanoid can assemble an Asuran workshop.", throwMessages);

            if (WorkshopProps.workshopDef == null)
                return Reject("The Asuran workshop definition is unavailable.", throwMessages);

            if (WorkshopProps.researchPrerequisite != null && !WorkshopProps.researchPrerequisite.IsFinished)
                return Reject($"Research {WorkshopProps.researchPrerequisite.LabelCap} first.", throwMessages);

            if (!AsuranNaniteUtility.CanSpendFraction(caster, Math.Max(0f, WorkshopProps.reserveCost)))
                return Reject($"Requires {Math.Max(0f, WorkshopProps.reserveCost):P0} Nanite Reserve.", throwMessages);

            if (!target.Cell.IsValid || caster.Map == null || !target.Cell.InBounds(caster.Map))
                return Reject("Choose a valid construction cell.", throwMessages);

            AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(
                WorkshopProps.workshopDef,
                target.Cell,
                WorkshopProps.workshopDef.defaultPlacingRot,
                caster.Map,
                godMode: true);
            if (!report.Accepted)
                return Reject(report.Reason.NullOrEmpty() ? "The Asuran workshop cannot assemble there." : report.Reason, throwMessages);

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !Valid(target, true))
                return;

            float cost = Math.Max(0f, WorkshopProps.reserveCost);
            if (!AsuranNaniteUtility.CanSpendFraction(caster, cost))
                return;

            Thing workshop = ThingMaker.MakeThing(WorkshopProps.workshopDef);
            if (workshop == null)
                return;

            if (WorkshopProps.workshopDef.CanHaveFaction)
                workshop.SetFactionDirect(Faction.OfPlayer);

            Thing spawned = GenSpawn.Spawn(
                workshop,
                target.Cell,
                caster.Map,
                WorkshopProps.workshopDef.defaultPlacingRot,
                WipeMode.Vanish);

            if (spawned == null)
                return;

            if (!AsuranNaniteUtility.TrySpendFraction(caster, cost))
            {
                spawned.Destroy(DestroyMode.Vanish);
                return;
            }

            Messages.Message(
                $"{caster.LabelShort} converted stored matter into {WorkshopProps.workshopDef.label}.",
                spawned,
                MessageTypeDefOf.PositiveEvent,
                historical: false);
        }

        private bool Reject(string reason, bool throwMessages)
        {
            if (throwMessages && parent?.pawn != null)
                Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }
}
