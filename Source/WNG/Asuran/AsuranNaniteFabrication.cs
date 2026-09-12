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
        public int healIntervalTicks = 450;
        public float baseHealAmount = 0.12f;
        public float reserveFractionPerHealPulse = 0.015f;
        public int depletedHealIntervalTicks = 1800;
        public int missingPartIntervalTicks = 30000;
        public float reserveFractionPerRestoredPart = 0.25f;
        public int depletedMissingPartIntervalTicks = 90000;
        public float depletionThreshold = 0.15f;
        public int empSuppressionTicks = 1800;
    }

    /// <summary>
    /// Human-form Replicators/Asurans use RimWorld's exact Need_Food machinery as a matter reserve.
    /// The custom NeedDef remains Need_Food, so native eating/caravan/feeding behavior stays intact;
    /// only its WNG meaning and synthetic consequences change. Block Replicators do not use this.
    /// A concealed infiltrator deliberately keeps ordinary Food as the visible cover UI until reveal.
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
            if (AsuranInfiltrationUtility.IsConcealed(pawn))
                return reserve;

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
    }

    public class Gene_AsuranNanitePhysiology : Gene
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

            // Pawn tick order runs Need_Food before genes. Remove vanilla biological starvation in
            // the same pawn tick it can be created so a nanite humanoid never carries Malnutrition
            // as its actual depletion consequence.
            RemoveBiologicalMalnutrition();

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

            // Normal nanite humanoids replace vanilla Food with WNG_NaniteMatterReserve. Concealed
            // infiltrators use a mask GeneDef that deliberately leaves Food active until reveal.
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();

            EnsureNamedHediff("WNG_AsuranNaniteLattice");
            RemoveBiologicalMalnutrition();

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

        private void RemoveBiologicalMalnutrition()
        {
            if (pawn?.health?.hediffSet == null)
                return;
            Hediff malnutrition = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (malnutrition != null)
                pawn.health.RemoveHediff(malnutrition);
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
    /// self-repair are attached to the exact pawn and survive save/load. Canonical WNG repair uses
    /// fixed reserve costs per successful repair transaction rather than scaling the reserve charge
    /// with HP healed. If the reserve cannot pay, much slower emergency repair/reconstruction remains
    /// possible without a reserve charge.
    /// </summary>
    public sealed class Hediff_AsuranNaniteLattice : Hediff
    {
        private int suppressedUntil;
        private int nextHealTick;
        private int nextMissingPartTick;

        private Gene_AsuranNanitePhysiology Physiology => AsuranNaniteUtility.GetPhysiology(pawn);
        private AsuranNanitePhysiologyExtension Extension => Physiology?.Extension;
        private bool EmpSuppressed => (Find.TickManager?.TicksGame ?? 0) < suppressedUntil;

        public override bool Visible => base.Visible && !AsuranInfiltrationUtility.IsConcealed(pawn);

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

        public void ApplyEmpSuppression(int durationTicks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int duration = Math.Max(60, durationTicks);
            long until = (long)now + duration;
            suppressedUntil = Math.Max(suppressedUntil, until >= int.MaxValue ? int.MaxValue : (int)until);
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (pawn == null || pawn.Dead)
                return;

            bool wasConcealed = AsuranInfiltrationUtility.IsConcealed(pawn);
            int empDuration = Math.Max(60, Extension?.empSuppressionTicks ?? 1800);
            if (dinfo.Def == DamageDefOf.EMP)
                ApplyEmpSuppression(empDuration);

            AsuranInfiltrationUtility.NotifyDamage(pawn, dinfo, totalDamageDealt);

            // Revealing removes the masking physiology gene and adds the public nanite physiology,
            // which rebuilds the lattice hediff. Carry EMP suppression onto that replacement state.
            if (wasConcealed && dinfo.Def == DamageDefOf.EMP && !AsuranInfiltrationUtility.IsConcealed(pawn))
            {
                Hediff_AsuranNaniteLattice replacement = pawn.health?.hediffSet?.hediffs
                    .OfType<Hediff_AsuranNaniteLattice>()
                    .FirstOrDefault();
                if (replacement != null && replacement != this)
                    replacement.ApplyEmpSuppression(empDuration);
            }
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
                nextMissingPartTick = Math.Max(nextMissingPartTick, suppressedUntil);
                return;
            }

            TickInjuryRepair(now);
            TickMissingPartReconstruction(now);
        }

        private void TickInjuryRepair(int now)
        {
            Hediff_Injury injury = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.CanHealNaturally() && x.Severity > 0f)
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();

            if (injury == null)
            {
                nextHealTick = 0;
                return;
            }

            float reserveCost = Mathf.Clamp01(Extension?.reserveFractionPerHealPulse ?? 0.015f);
            bool reservePowered = AsuranNaniteUtility.CanSpendFraction(pawn, reserveCost);
            int interval = reservePowered
                ? Math.Max(30, Extension?.healIntervalTicks ?? 450)
                : Math.Max(30, Extension?.depletedHealIntervalTicks ?? 1800);

            if (nextHealTick <= 0)
            {
                nextHealTick = SafeFutureTick(now, interval);
                return;
            }
            if (now < nextHealTick)
                return;

            float healAmount = Math.Min(injury.Severity, Math.Max(0f, Extension?.baseHealAmount ?? 0.12f));
            if (healAmount <= 0f)
            {
                nextHealTick = 0;
                return;
            }

            if (reservePowered && !AsuranNaniteUtility.TrySpendFraction(pawn, reserveCost))
            {
                reservePowered = false;
                interval = Math.Max(30, Extension?.depletedHealIntervalTicks ?? 1800);
            }

            injury.Heal(healAmount);
            AsuranInfiltrationUtility.NotifySelfRepair(pawn, healAmount);
            nextHealTick = SafeFutureTick(now, interval);
        }

        private void TickMissingPartReconstruction(int now)
        {
            Hediff_MissingPart missing = pawn.health.hediffSet
                .GetMissingPartsCommonAncestors()
                .FirstOrDefault(x => x?.Part != null && !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(x.Part));

            if (missing == null)
            {
                nextMissingPartTick = 0;
                return;
            }

            float reserveCost = Mathf.Clamp01(Extension?.reserveFractionPerRestoredPart ?? 0.25f);
            bool reservePowered = AsuranNaniteUtility.CanSpendFraction(pawn, reserveCost);
            int interval = reservePowered
                ? Math.Max(60, Extension?.missingPartIntervalTicks ?? 30000)
                : Math.Max(60, Extension?.depletedMissingPartIntervalTicks ?? 90000);

            if (nextMissingPartTick <= 0)
            {
                nextMissingPartTick = SafeFutureTick(now, interval);
                return;
            }
            if (now < nextMissingPartTick)
                return;

            if (reservePowered && !AsuranNaniteUtility.TrySpendFraction(pawn, reserveCost))
            {
                reservePowered = false;
                interval = Math.Max(60, Extension?.depletedMissingPartIntervalTicks ?? 90000);
            }

            BodyPartRecord part = missing.Part;
            pawn.health.RestorePart(part);
            AsuranInfiltrationUtility.Reveal(pawn, "nanite reconstruction exposed synthetic structure");
            nextMissingPartTick = SafeFutureTick(now, interval);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
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
            Scribe_Values.Look(ref nextMissingPartTick, "wngAsuranNextMissingPartTick", 0);
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
