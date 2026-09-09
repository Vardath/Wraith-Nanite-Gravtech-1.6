using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorContainmentProjector : CompProperties
    {
        public float radius = 8.5f;
        public CompProperties_ReplicatorContainmentProjector() { compClass = typeof(CompReplicatorContainmentProjector); }
    }

    public sealed class CompReplicatorContainmentProjector : ThingComp
    {
        public CompProperties_ReplicatorContainmentProjector Props => (CompProperties_ReplicatorContainmentProjector)props;
        public float Radius => Props.radius;
        public bool Active
        {
            get
            {
                if (parent == null || !parent.Spawned) return false;
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                return power != null && power.PowerOn;
            }
        }
        public override string CompInspectStringExtra()
        {
            if (!Active) return $"Replicator containment field: offline (radius {Radius:0.0}).";
            return $"Replicator containment field: active (radius {Radius:0.0}). Replicator Matter cannot reassemble and uncontrolled block Replicators cannot complete assimilation or recombination while inside the field.";
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
                if (comp == null || !comp.Active) continue;
                float radius = comp.Radius;
                if (cell.DistanceToSquared(thing.Position) <= radius * radius) return true;
            }
            return false;
        }

        private void RefreshIfNeeded()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextRefreshTick) return;
            nextRefreshTick = now + RefreshTicks;
            projectors.Clear();
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorContainmentProjector");
            if (def == null) return;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            if (things == null) return;
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && !thing.Destroyed && thing.Spawned) projectors.Add(thing);
            }
        }
    }

    public static class ReplicatorContainmentUtility
    {
        public static bool IsContained(Map map, IntVec3 cell) => map?.GetComponent<MapComponent_ReplicatorContainment>()?.IsContained(cell) == true;
        public static bool BlocksAssimilation(Pawn pawn, Thing target)
        {
            Map map = pawn?.Map;
            if (map == null) return false;
            if (IsContained(map, pawn.Position)) return true;
            return target != null && target.Spawned && target.Map == map && IsContained(map, target.Position);
        }
    }
}
