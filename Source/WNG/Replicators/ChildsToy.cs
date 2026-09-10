using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ChildsToy : CompProperties
    {
        public int controlCheckTicks = 250;
        public int feralDelayTicks = 60000;
        public string feralPawnKind = "WNG_ReplicatorDrone";
        public string feralFactionDef = "WNG_ReplicatorSwarm";

        public CompProperties_ChildsToy() => compClass = typeof(CompChildsToy);
    }

    /// <summary>
    /// Player-owned Child's Toy control state. A Toy that remains outside valid player
    /// mech control for the configured interval unfolds into a normal hostile Drone.
    /// The replacement is spawned before the Toy is consumed so a failed transaction
    /// cannot silently delete the player's pawn.
    /// </summary>
    public sealed class CompChildsToy : ThingComp
    {
        private int nextCheckTick;
        private int uncontrolledSinceTick = -1;
        private bool transformationCommitted;

        private CompProperties_ChildsToy Props => (CompProperties_ChildsToy)props;
        private Pawn Pawn => parent as Pawn;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextCheckTick <= 0)
                nextCheckTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.controlCheckTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn toy = Pawn;
            if (transformationCommitted || toy == null || toy.Dead || !toy.Spawned || toy.Map == null) return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextCheckTick) return;
            nextCheckTick = now + Math.Max(60, Props.controlCheckTicks);

            if (HasPlayerControl(toy))
            {
                uncontrolledSinceTick = -1;
                return;
            }

            if (uncontrolledSinceTick < 0)
                uncontrolledSinceTick = now;

            if (now - uncontrolledSinceTick >= Math.Max(0, Props.feralDelayTicks))
                TryBecomeDrone(toy);
        }

        private static bool HasPlayerControl(Pawn toy)
        {
            if (toy?.Faction != Faction.OfPlayer) return false;
            return toy.IsColonyMechPlayerControlled;
        }

        private void TryBecomeDrone(Pawn toy)
        {
            PawnKindDef droneKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.feralPawnKind);
            FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail(Props.feralFactionDef);
            Faction swarm = swarmDef == null
                ? null
                : Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.def == swarmDef);
            if (droneKind == null || swarm == null || toy.Map == null) return;

            Map map = toy.Map;
            IntVec3 origin = toy.Position;
            Pawn drone = PawnGenerator.GeneratePawn(droneKind, swarm);
            if (drone == null) return;

            drone.TryGetComp<CompReplicatorState>()?.CopyFrom(toy.TryGetComp<CompReplicatorState>());

            if (!GenPlace.TryPlaceThing(drone, origin, map, ThingPlaceMode.Near))
            {
                if (!drone.Destroyed) drone.Destroy(DestroyMode.Vanish);
                return;
            }

            transformationCommitted = true;
            toy.Destroy(DestroyMode.Vanish);
            Messages.Message(
                "The Child's Toy has gone feral and unfolded into a Replicator Drone!",
                drone,
                MessageTypeDefOf.ThreatSmall,
                historical: true);
        }

        public override string CompInspectStringExtra()
        {
            Pawn toy = Pawn;
            if (toy == null) return null;
            if (HasPlayerControl(toy)) return "Child's Toy: controlled.";

            int now = Find.TickManager?.TicksGame ?? 0;
            int start = uncontrolledSinceTick < 0 ? now : uncontrolledSinceTick;
            int remaining = Math.Max(0, Math.Max(0, Props.feralDelayTicks) - (now - start));
            return $"Child's Toy: control lost. Feral transformation in {remaining / (float)GenDate.TicksPerHour:0.0} in-game hour(s).";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextCheckTick, "wngChildsToyNextControlCheck", 0);
            Scribe_Values.Look(ref uncontrolledSinceTick, "wngChildsToyUncontrolledSince", -1);
            Scribe_Values.Look(ref transformationCommitted, "wngChildsToyTransformationCommitted", false);
        }
    }
}
