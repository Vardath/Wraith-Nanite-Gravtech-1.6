using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorContainmentProjector : CompProperties
    {
        public float radius = 8.5f;
        public bool roomWide;
        public bool requireEnclosedRoom = true;
        public CompProperties_ReplicatorContainmentProjector() => compClass = typeof(CompReplicatorContainmentProjector);
    }

    public sealed class CompReplicatorContainmentProjector : ThingComp
    {
        private CompProperties_ReplicatorContainmentProjector Props => (CompProperties_ReplicatorContainmentProjector)props;
        public float Radius => Props.radius;
        public bool RoomWide => Props.roomWide;
        public bool RequireEnclosedRoom => Props.requireEnclosedRoom;

        public bool Active
        {
            get
            {
                if (parent == null || !parent.Spawned) return false;
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                if (power == null || !power.PowerOn) return false;
                if (!RoomWide || !RequireEnclosedRoom) return true;
                Room room = parent.Position.GetRoom(parent.Map);
                return room != null && !room.PsychologicallyOutdoors;
            }
        }

        public bool Contains(IntVec3 cell)
        {
            if (!Active || parent?.Map == null || !cell.InBounds(parent.Map)) return false;
            if (!RoomWide)
            {
                float radius = Radius;
                return cell.DistanceToSquared(parent.Position) <= radius * radius;
            }

            Room sourceRoom = parent.Position.GetRoom(parent.Map);
            Room targetRoom = cell.GetRoom(parent.Map);
            return sourceRoom != null && targetRoom == sourceRoom
                && (!RequireEnclosedRoom || !sourceRoom.PsychologicallyOutdoors);
        }

        public override string CompInspectStringExtra()
        {
            if (RoomWide)
            {
                if (!Active) return "Replicator EMP containment: offline. A powered enclosed room is required.";
                return "Replicator EMP containment: active throughout this room. Loose Replicator blocks cannot reform and uncontrolled Replicators cannot assimilate or recombine here.";
            }

            if (!Active) return $"Replicator containment field: offline (radius {Radius:0.0}).";
            return $"Replicator containment field: active (radius {Radius:0.0}). Replicator blocks cannot reform and uncontrolled block Replicators cannot assimilate or recombine inside the field.";
        }
    }

    public sealed class MapComponent_ReplicatorContainment : MapComponent
    {
        private const int RefreshTicks = 250;
        private readonly List<Thing> projectors = new List<Thing>();
        private int nextRefreshTick;

        public MapComponent_ReplicatorContainment(Map map) : base(map) { }

        public bool IsContained(IntVec3 cell)
        {
            RefreshIfNeeded();
            for (int i = 0; i < projectors.Count; i++)
            {
                Thing thing = projectors[i];
                CompReplicatorContainmentProjector comp = thing?.TryGetComp<CompReplicatorContainmentProjector>();
                if (comp?.Contains(cell) == true) return true;
            }
            return false;
        }

        private void RefreshIfNeeded()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextRefreshTick) return;
            nextRefreshTick = now + RefreshTicks;
            projectors.Clear();

            List<Thing> all = map?.listerThings?.AllThings;
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                Thing thing = all[i];
                if (thing != null && !thing.Destroyed && thing.Spawned
                    && thing.TryGetComp<CompReplicatorContainmentProjector>() != null)
                    projectors.Add(thing);
            }
        }
    }

    public static class ReplicatorContainmentUtility
    {
        public static bool IsContained(Map map, IntVec3 cell)
            => map?.GetComponent<MapComponent_ReplicatorContainment>()?.IsContained(cell) == true;

        public static bool BlocksAssimilation(Pawn pawn, Thing target)
        {
            Map map = pawn?.Map;
            if (map == null) return false;
            if (IsContained(map, pawn.Position)) return true;
            return target != null && target.Spawned && target.Map == map && IsContained(map, target.Position);
        }
    }
}
