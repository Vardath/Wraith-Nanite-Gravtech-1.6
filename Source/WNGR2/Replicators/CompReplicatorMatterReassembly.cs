using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorMatterReassembly : CompProperties
    {
        public int minimumStack = 10;
        public int consumePerReplicator = 10;
        public int dormantTicks = 30000;
        public float baseChancePerRareTick = 0.0007f;

        public CompProperties_ReplicatorMatterReassembly()
        {
            compClass = typeof(CompReplicatorMatterReassembly);
        }
    }

    public sealed class CompReplicatorMatterReassembly : ThingComp
    {
        private int exposedTicks;

        private CompProperties_ReplicatorMatterReassembly Props => (CompProperties_ReplicatorMatterReassembly)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref exposedTicks, "exposedTicks", 0);
        }

        public override void PostSplitOff(Thing piece)
        {
            base.PostSplitOff(piece);
            CompReplicatorMatterReassembly splitComp = piece?.TryGetComp<CompReplicatorMatterReassembly>();
            if (splitComp != null)
                splitComp.exposedTicks = exposedTicks;
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            base.PreAbsorbStack(otherStack, count);
            CompReplicatorMatterReassembly otherComp = otherStack?.TryGetComp<CompReplicatorMatterReassembly>();
            if (otherComp != null)
                exposedTicks = Math.Max(exposedTicks, otherComp.exposedTicks);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (parent == null || !parent.Spawned || parent.Map == null)
                return;

            if (ReplicatorContainmentUtility.IsContained(parent.Map, parent.Position))
                return;

            exposedTicks += 250;
            if (exposedTicks < Props.dormantTicks || parent.stackCount < Props.minimumStack)
                return;

            Faction swarm = ResolveSwarmFaction();
            if (swarm == null)
                return;

            if (ReplicatorUtility.CountBlockReplicatorsForFaction(parent.Map, swarm) >= WNG_Config.MaxHostileReplicatorsPerMap)
                return;

            float stackScale = Math.Max(0.5f, parent.stackCount / 20f);
            float chance = Math.Min(0.01f, Props.baseChancePerRareTick * stackScale);
            if (!Rand.Chance(chance))
                return;

            Reassemble(parent.Map, parent.Position, swarm);
        }

        public override string CompInspectStringExtra()
        {
            if (parent == null || parent.stackCount < Props.minimumStack)
                return null;

            if (parent.Spawned && parent.Map != null && ReplicatorContainmentUtility.IsContained(parent.Map, parent.Position))
                return "Contained replicator substrate: powered suppression field is preventing self-assembly. Reassembly risk returns if containment power is lost.";

            if (exposedTicks < Props.dormantTicks)
            {
                float days = (Props.dormantTicks - exposedTicks) / 60000f;
                return $"Dormant replicator substrate ({days:0.0} day(s) until reassembly risk).";
            }

            return "Unstable replicator substrate: may self-assemble into a hostile Replicator. Reprocess, destroy or place it under a powered containment field.";
        }

        private static Faction ResolveSwarmFaction()
        {
            FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            return swarmDef == null
                ? null
                : Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.def == swarmDef);
        }

        private void Reassemble(Map map, IntVec3 position, Faction swarm)
        {
            PawnKindDef pawnKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (swarm == null || pawnKind == null)
                return;

            if (ReplicatorUtility.CountBlockReplicatorsForFaction(map, swarm) >= WNG_Config.MaxHostileReplicatorsPerMap)
                return;

            Pawn pawn = PawnGenerator.GeneratePawn(pawnKind, swarm);
            if (pawn == null)
                return;

            if (!GenPlace.TryPlaceThing(pawn, position, map, ThingPlaceMode.Near))
            {
                if (!pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);
                return;
            }

            int consume = Math.Min(Props.consumePerReplicator, parent.stackCount);
            if (consume >= parent.stackCount)
                parent.Destroy(DestroyMode.Vanish);
            else
                parent.stackCount -= consume;

            Messages.Message(
                "Dormant Replicator Matter has reassembled itself into a hostile Replicator!",
                pawn,
                MessageTypeDefOf.ThreatSmall,
                historical: true);

            exposedTicks = 0;
        }
    }
}
