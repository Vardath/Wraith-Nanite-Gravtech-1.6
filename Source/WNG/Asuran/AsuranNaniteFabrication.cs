using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Visible finite fabrication/repair feedstock for player Asurans and human-form Replicators.
    /// This is not biological hunger: food/feedstock intake can replenish the reserve, while
    /// fabrication and reconstruction explicitly spend it.
    /// </summary>
    public sealed class Gene_Resource_NaniteReserve : Gene_Resource
    {
        private float lastFoodLevel = -1f;
        public const float ReserveGainPerFoodLevel = 0.35f;

        public override float InitialResourceMax => 1f;
        public override float MinLevelForAlert => 0.10f;
        protected override Color BarColor => new Color(0.20f, 0.66f, 0.78f);
        protected override Color BarHighlightColor => new Color(0.43f, 0.88f, 0.98f);

        public bool CanSpend(float amount) => Active && amount >= 0f && Value + 0.0001f >= amount;

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
                if (gene is Gene_Resource_NaniteReserve reserve && reserve.Active)
                    return reserve;
            return null;
        }

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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lastFoodLevel, "wngNaniteReserveLastFoodLevel", -1f);
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
    /// Reconstructs the Asuran production workshop directly from a finite Nanite Reserve.
    /// The reserve is spent only after the target cell is still valid and the workshop Thing has
    /// been successfully created, so cancelled/invalid casts never consume the resource.
    /// </summary>
    public sealed class CompAbilityEffect_AssembleAsuranWorkshop : CompAbilityEffect
    {
        private CompProperties_AbilityAssembleAsuranWorkshop Props =>
            (CompProperties_AbilityAssembleAsuranWorkshop)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Dead || !caster.IsColonistPlayerControlled)
                return Reject("Only a player-controlled nanite humanoid can assemble an Asuran workshop.", throwMessages);

            if (Props.workshopDef == null)
                return Reject("The Asuran workshop definition is unavailable.", throwMessages);

            if (Props.researchPrerequisite != null && !Props.researchPrerequisite.IsFinished)
                return Reject($"Research {Props.researchPrerequisite.LabelCap} first.", throwMessages);

            Gene_Resource_NaniteReserve reserve = Gene_Resource_NaniteReserve.Get(caster);
            if (reserve == null || !reserve.CanSpend(Math.Max(0f, Props.reserveCost)))
                return Reject($"Requires {Math.Max(0f, Props.reserveCost):P0} Nanite Reserve.", throwMessages);

            if (!target.Cell.IsValid || caster.Map == null || !target.Cell.InBounds(caster.Map))
                return Reject("Choose a valid construction cell.", throwMessages);

            AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(
                Props.workshopDef,
                target.Cell,
                Props.workshopDef.defaultPlacingRot,
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

            Gene_Resource_NaniteReserve reserve = Gene_Resource_NaniteReserve.Get(caster);
            float cost = Math.Max(0f, Props.reserveCost);
            if (reserve == null || !reserve.CanSpend(cost))
                return;

            Thing workshop = ThingMaker.MakeThing(Props.workshopDef);
            if (workshop == null)
                return;

            if (Props.workshopDef.CanHaveFaction)
                workshop.SetFactionDirect(Faction.OfPlayer);

            Thing spawned = GenSpawn.Spawn(
                workshop,
                target.Cell,
                caster.Map,
                Props.workshopDef.defaultPlacingRot,
                WipeMode.Vanish);

            if (spawned == null)
                return;

            // Spend only after successful assembly. If a future spawn path can fail after returning a
            // non-null Thing, it must roll the structure back before charging the reserve.
            if (!reserve.TrySpend(cost))
            {
                spawned.Destroy(DestroyMode.Vanish);
                return;
            }

            Messages.Message(
                $"{caster.LabelShort} assembled {Props.workshopDef.label} from Nanite Reserve.",
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
