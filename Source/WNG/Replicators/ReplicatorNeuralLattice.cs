using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class SovereignNeuralLatticeExtension : DefModExtension
    {
        public float directControlRange = 24f;
        public int maxControlledBlocks = 3;
        public int empSuppressionTicks = 1800;
    }

    /// <summary>
    /// A physical brain implant which gives a non-Queen bearer bounded target-specific access to the
    /// same persistent block-controller architecture used by the exact Queen. It does not create
    /// Queen identity and does not provide the Queen's broad nearby-swarm command.
    /// </summary>
    public sealed class Hediff_SovereignNeuralLattice : Hediff
    {
        private int suppressedUntil;

        private SovereignNeuralLatticeExtension Extension => def?.GetModExtension<SovereignNeuralLatticeExtension>();

        public bool SignalDisrupted => (Find.TickManager?.TicksGame ?? 0) < suppressedUntil;
        public float DirectControlRange => Math.Max(1f, Extension?.directControlRange ?? 24f);
        public int MaxControlledBlocks => Math.Max(1, Extension?.maxControlledBlocks ?? 3);

        public override string LabelInBrackets
        {
            get
            {
                if (!SignalDisrupted)
                    return null;
                int remaining = Math.Max(0, suppressedUntil - (Find.TickManager?.TicksGame ?? 0));
                return $"EMP disrupted {remaining / (float)GenDate.TicksPerHour:0.0}h";
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                yield return gizmo;

            if (pawn == null || pawn.Dead || pawn.Faction != Faction.OfPlayer || !pawn.Spawned || pawn.Map == null)
                yield break;

            if (ReplicatorSovereigntyUtility.IsExactQueen(pawn))
            {
                yield return new Command_Action
                {
                    defaultLabel = "Neural lattice control",
                    defaultDesc = "This pawn is the exact Replicator Queen and already possesses broader innate sovereign authority. The implant does not create a second overlapping control domain.",
                    disabled = true,
                    disabledReason = "The exact Queen uses her innate sovereign lattice."
                };
                yield break;
            }

            bool contained = ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position);
            bool blocked = SignalDisrupted || contained;
            string blockedReason = SignalDisrupted
                ? "The Sovereign Neural Lattice is disrupted by EMP."
                : "An active Replicator containment field is blocking the implant's control signal.";

            TargetingParameters acquireParams = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetAnimals = false,
                canTargetHumans = false,
                canTargetMechs = true,
                validator = info => info.Thing is Pawn target &&
                    ReplicatorSovereigntyUtility.IsBlockReplicator(target) && target != pawn
            };

            Command_Target acquire = new Command_Target
            {
                defaultLabel = "Neural lattice control",
                defaultDesc = $"Take bounded target-specific control of one exact block Replicator within {DirectControlRange:0} cells. This implant can maintain up to {MaxControlledBlocks} controlled block bodies while physically present with them.",
                targetingParams = acquireParams,
                action = target =>
                {
                    Pawn block = target.Pawn;
                    if (!ReplicatorSovereigntyUtility.TryAcquireForNeuralLattice(
                            pawn,
                            block,
                            DirectControlRange,
                            MaxControlledBlocks,
                            out string rejection))
                    {
                        if (!rejection.NullOrEmpty())
                            Messages.Message(rejection, pawn, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Messages.Message(
                        $"{block.LabelShort} is now linked to {pawn.LabelShort}'s Sovereign Neural Lattice.",
                        block,
                        MessageTypeDefOf.PositiveEvent,
                        historical: false);
                }
            };
            if (blocked)
                acquire.Disable(blockedReason);
            yield return acquire;

            yield return new Command_Action
            {
                defaultLabel = "Release neural lattice domain",
                defaultDesc = "Release all currently present block Replicators controlled by this implant bearer and restore their pre-control faction/domain.",
                action = delegate
                {
                    int released = ReplicatorSovereigntyUtility.ReleaseControllerDomain(
                        pawn,
                        ReplicatorControlAuthority.NeuralLattice);
                    Messages.Message(
                        $"Released neural-lattice control of {released} block Replicator(s).",
                        pawn,
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
            };
        }

        public override void PostRemoved()
        {
            if (pawn != null)
            {
                ReplicatorSovereigntyUtility.ReleaseControllerDomain(
                    pawn,
                    ReplicatorControlAuthority.NeuralLattice);
            }
            base.PostRemoved();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref suppressedUntil, "wngSovereignNeuralLatticeEmpUntil", 0);
        }
    }
}
