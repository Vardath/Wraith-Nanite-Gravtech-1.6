using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class AsuranInfiltrationExtension : DefModExtension
    {
        public float meaningfulDamageThreshold = 8f;
        public float selfRepairRevealPoints = 1f;
    }

    /// <summary>
    /// Marker physiology used only while an Asuran infiltrator is concealed. The pawn keeps the
    /// ordinary Food need/UI as its cover reserve until a permanent reveal converts this exact pawn
    /// to the public WNG nanite-humanoid xenotype.
    /// </summary>
    public sealed class Gene_AsuranInfiltratorPhysiology : Gene_AsuranNanitePhysiology
    {
        public override void PostAdd()
        {
            EnsureInfiltrationState();
            base.PostAdd();
        }

        private void EnsureInfiltrationState()
        {
            if (pawn?.health?.hediffSet == null)
                return;
            HediffDef stateDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_AsuranInfiltrationState");
            if (stateDef != null && !pawn.health.hediffSet.HasHediff(stateDef))
                pawn.health.AddHediff(stateDef);
        }
    }

    /// <summary>
    /// Save-persistent conceal/reveal state on the exact pawn. A covert visitor may temporarily
    /// assume a non-hostile human faction identity, but its exact true Asuran source faction is
    /// retained here and restored on exposure/activation. The pawn itself is never recreated.
    /// </summary>
    public sealed class Hediff_AsuranInfiltration : Hediff
    {
        private bool revealed;
        private float suspicion;
        private int revealedTick = -1;
        private string revealReason;

        private Faction trueFaction;
        private Faction coverFaction;
        private bool covertPresence;
        private bool activationDeferred;

        public bool Revealed => revealed;
        public float Suspicion => suspicion;
        public Faction TrueFaction => trueFaction;
        public Faction CoverFaction => coverFaction;
        public bool CovertPresence => covertPresence;
        public bool ActivationDeferred => activationDeferred;
        public override bool Visible => revealed && base.Visible;

        public override string LabelInBrackets
        {
            get
            {
                if (!revealed)
                    return null;
                return revealReason.NullOrEmpty() ? "revealed" : "revealed: " + revealReason;
            }
        }

        public void BeginCovertPresence(Faction exactTrueFaction, Faction assumedCoverFaction)
        {
            if (revealed || exactTrueFaction == null || assumedCoverFaction == null)
                return;

            trueFaction = exactTrueFaction;
            coverFaction = assumedCoverFaction;
            covertPresence = true;
            activationDeferred = false;
        }

        public void MarkCoverBroken()
        {
            covertPresence = false;
            activationDeferred = false;
        }

        public void DeferActivation()
        {
            if (trueFaction != null)
                activationDeferred = true;
        }

        public void AddRepairSuspicion(float healedAmount)
        {
            if (revealed || healedAmount <= 0f)
                return;

            AsuranInfiltrationExtension ext = AsuranInfiltrationUtility.ExtensionFor(pawn);
            float threshold = Math.Max(0.05f, ext?.selfRepairRevealPoints ?? 1f);
            suspicion += healedAmount;
            if (suspicion + 0.0001f >= threshold)
                Reveal("unnatural self-repair");
        }

        public bool Reveal(string reason, Pawn detector = null)
        {
            if (revealed || pawn == null || pawn.Dead)
                return false;

            revealed = true;
            revealReason = reason.NullOrEmpty() ? "synthetic identity exposed" : reason;
            revealedTick = Find.TickManager?.TicksGame ?? 0;
            AsuranInfiltrationUtility.CommitRevealedIdentity(pawn);
            AsuranCovertPresenceUtility.TryActivateRevealedPawn(pawn, this);

            if (pawn.Spawned)
            {
                string detectorText = detector == null ? string.Empty : $" by {detector.LabelShortCap}";
                Messages.Message(
                    $"{pawn.LabelShortCap} has been exposed{detectorText} as an Asuran synthetic infiltrator ({revealReason}).",
                    pawn,
                    MessageTypeDefOf.ThreatSmall,
                    historical: false);
            }
            return true;
        }

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (!revealed || !activationDeferred || pawn == null || pawn.Dead)
                return;
            if (pawn.IsHashIntervalTick(250, delta))
                AsuranCovertPresenceUtility.TryActivateRevealedPawn(pawn, this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref revealed, "wngAsuranInfiltratorRevealed", false);
            Scribe_Values.Look(ref suspicion, "wngAsuranInfiltratorSuspicion", 0f);
            Scribe_Values.Look(ref revealedTick, "wngAsuranInfiltratorRevealedTick", -1);
            Scribe_Values.Look(ref revealReason, "wngAsuranInfiltratorRevealReason");
            Scribe_References.Look(ref trueFaction, "wngAsuranInfiltratorTrueFaction");
            Scribe_References.Look(ref coverFaction, "wngAsuranInfiltratorCoverFaction");
            Scribe_Values.Look(ref covertPresence, "wngAsuranInfiltratorCovertPresence", false);
            Scribe_Values.Look(ref activationDeferred, "wngAsuranInfiltratorActivationDeferred", false);
        }
    }

    /// <summary>
    /// Synthetic status hediffs that are physically active while an infiltrator is concealed but
    /// are intentionally omitted from the player's health readout until reveal.
    /// </summary>
    public sealed class Hediff_AsuranSyntheticStatus : Hediff
    {
        public override bool Visible => base.Visible && !AsuranInfiltrationUtility.IsConcealed(pawn);
    }

    public static class AsuranInfiltrationUtility
    {
        public static Hediff_AsuranInfiltration State(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return null;
            return pawn.health.hediffSet.hediffs.OfType<Hediff_AsuranInfiltration>().FirstOrDefault();
        }

        public static bool IsConcealed(Pawn pawn)
        {
            Hediff_AsuranInfiltration state = State(pawn);
            return state != null && !state.Revealed;
        }

        public static bool IsRevealedInfiltrator(Pawn pawn)
        {
            Hediff_AsuranInfiltration state = State(pawn);
            return state != null && state.Revealed;
        }

        public static bool IsActiveCovertPresence(Pawn pawn)
        {
            Hediff_AsuranInfiltration state = State(pawn);
            return state != null && !state.Revealed && state.CovertPresence && state.TrueFaction != null;
        }

        public static AsuranInfiltrationExtension ExtensionFor(Pawn pawn)
        {
            Gene mask = GetMaskGene(pawn);
            return mask?.def?.GetModExtension<AsuranInfiltrationExtension>();
        }

        public static bool Reveal(Pawn pawn, string reason, Pawn detector = null)
        {
            return State(pawn)?.Reveal(reason, detector) == true;
        }

        public static void NotifyDamage(Pawn pawn, DamageInfo dinfo, float totalDamageDealt)
        {
            if (!IsConcealed(pawn) || pawn == null || pawn.Dead)
                return;

            if (dinfo.Def == DamageDefOf.EMP)
            {
                Reveal(pawn, "EMP exposed the nanite lattice");
                return;
            }

            float threshold = Math.Max(1f, ExtensionFor(pawn)?.meaningfulDamageThreshold ?? 8f);
            if (totalDamageDealt + 0.0001f >= threshold)
                Reveal(pawn, "injury exposed synthetic structure");
        }

        public static void NotifySelfRepair(Pawn pawn, float healedAmount)
        {
            State(pawn)?.AddRepairSuspicion(healedAmount);
        }

        public static void CommitRevealedIdentity(Pawn pawn)
        {
            if (pawn?.genes == null)
                return;

            XenotypeDef realXenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail("WNG_NaniteHumanoid");
            if (realXenotype == null)
                return;

            Gene mask = GetMaskGene(pawn);
            if (mask != null)
                pawn.genes.RemoveGene(mask);

            foreach (GeneDef geneDef in realXenotype.AllGenes)
            {
                if (geneDef == null || pawn.genes.GetGene(geneDef) != null)
                    continue;
                pawn.genes.AddGene(geneDef, xenogene: true);
            }

            pawn.genes.SetXenotypeDirect(realXenotype);
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static Gene GetMaskGene(Pawn pawn)
        {
            if (pawn?.genes == null)
                return null;
            return pawn.genes.GenesListForReading
                .FirstOrDefault(g => g?.def?.defName == "WNG_AsuranInfiltratorPhysiologyMask");
        }
    }
}
