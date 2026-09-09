using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ChildsToyFeralTransformation : CompProperties
    {
        public CompProperties_ChildsToyFeralTransformation()
        {
            compClass = typeof(CompChildsToyFeralTransformation);
        }
    }

    /// <summary>
    /// The Child's Toy is deliberately not an ordinary Replicator Drone while controlled.
    /// If it remains outside valid colony/Queen/lattice control for the configured feral delay,
    /// it physically transforms into a hostile WNG_ReplicatorDrone. The replacement is spawned
    /// successfully before the toy is removed, so a failed transformation cannot delete the toy.
    /// </summary>
    public sealed class CompChildsToyFeralTransformation : ThingComp
    {
        private const int CheckIntervalTicks = 250;
        private int uncontrolledTicks;
        private bool transformationCommitted;

        private Pawn Pawn => parent as Pawn;

        public override void CompTick()
        {
            base.CompTick();

            Pawn toy = Pawn;
            if (transformationCommitted || toy == null || toy.Dead || !toy.Spawned || toy.Map == null)
                return;

            if ((Find.TickManager.TicksGame + toy.thingIDNumber) % CheckIntervalTicks != 0)
                return;

            if (HasValidControl(toy))
            {
                uncontrolledTicks = 0;
                return;
            }

            uncontrolledTicks += CheckIntervalTicks;
            if (uncontrolledTicks < WNG_Config.ReplicatorFeralDelayTicks)
                return;

            TryTransform(toy);
        }

        private static bool HasValidControl(Pawn toy)
        {
            if (toy.Faction != Faction.OfPlayer)
                return false;

            return toy.IsColonyMechPlayerControlled
                || ReplicatorQueenUtility.HasSovereignForFaction(toy.Map, toy.Faction)
                || ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(toy);
        }

        private void TryTransform(Pawn toy)
        {
            FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            PawnKindDef droneKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            Faction swarm = swarmDef == null
                ? null
                : Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.def == swarmDef);

            if (swarm == null || droneKind == null || toy.Map == null)
                return;

            Map map = toy.Map;
            IntVec3 origin = toy.Position;
            CompReplicatorMaterial material = toy.TryGetComp<CompReplicatorMaterial>();

            Pawn drone = PawnGenerator.GeneratePawn(droneKind, swarm);
            if (drone == null)
                return;

            drone.TryGetComp<CompReplicatorMaterial>()?.CopyFrom(material);

            if (!GenPlace.TryPlaceThing(drone, origin, map, ThingPlaceMode.Near))
            {
                if (!drone.Destroyed)
                    drone.Destroy(DestroyMode.Vanish);
                return;
            }

            transformationCommitted = true;
            toy.Destroy(DestroyMode.Vanish);

            Messages.Message(
                "The Child's Toy has gone feral and unfolded into a hostile Replicator!",
                drone,
                MessageTypeDefOf.ThreatSmall,
                historical: true);
        }

        public override string CompInspectStringExtra()
        {
            Pawn toy = Pawn;
            if (toy == null || toy.Faction != Faction.OfPlayer || HasValidControl(toy))
                return "Child's Toy: controlled and stable.";

            int remaining = System.Math.Max(0, WNG_Config.ReplicatorFeralDelayTicks - uncontrolledTicks);
            return $"Child's Toy: uncontrolled. Feral transformation in {remaining / 2500f:0.0} in-game hour(s).";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref uncontrolledTicks, "wngChildsToyUncontrolledTicks", 0);
            Scribe_Values.Look(ref transformationCommitted, "wngChildsToyTransformationCommitted", false);
        }
    }
}
