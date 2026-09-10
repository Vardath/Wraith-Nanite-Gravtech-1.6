using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WNGFuelPipe : CompProperties
    {
        public WNGGravshipTheme family = WNGGravshipTheme.None;

        public CompProperties_WNGFuelPipe()
        {
            compClass = typeof(CompWNGFuelPipe);
        }
    }

    /// <summary>
    /// Marker for a one-cell WNG gravship fuel pipe. Fuel itself remains stored in the native
    /// CompRefuelable on the tank; these pipe Things only provide physical connectivity. This keeps
    /// Odyssey's own fuel accounting and consumption authoritative.
    /// </summary>
    public sealed class CompWNGFuelPipe : ThingComp
    {
        private CompProperties_WNGFuelPipe Props => (CompProperties_WNGFuelPipe)props;
        public WNGGravshipTheme Family => Props.family;

        public override string CompInspectStringExtra()
        {
            switch (Family)
            {
                case WNGGravshipTheme.Wraith:
                    return "Fuel network: Wraith bio-sludge";
                case WNGGravshipTheme.Asuran:
                    return "Fuel network: Asuran nanite sludge";
                case WNGGravshipTheme.Goauld:
                    return "Fuel network: Goa'uld";
                default:
                    return "Fuel network: unassigned";
            }
        }
    }

    public sealed class CompProperties_WNGFuelTankFacility : CompProperties_GravshipFacility
    {
        public WNGGravshipTheme family = WNGGravshipTheme.None;
        public int connectivityCheckIntervalTicks = 60;

        public CompProperties_WNGFuelTankFacility()
        {
            compClass = typeof(CompWNGFuelTankFacility);
        }
    }

    /// <summary>
    /// Native Odyssey fuel-storage facility with one extra condition: the tank must be physically
    /// connected to its same-family themed GravEngine through WNG fuel pipes. Building_GravEngine
    /// still reads this facility through CanBeActive and still sums/consumes CompRefuelable fuel.
    /// </summary>
    public sealed class CompWNGFuelTankFacility : CompGravshipFacility
    {
        private int nextConnectivityCheckTick;
        private bool cachedConnected;

        private CompProperties_WNGFuelTankFacility FuelProps => (CompProperties_WNGFuelTankFacility)props;

        public override bool CanBeActive
        {
            get
            {
                if (!base.CanBeActive || engine == null || parent?.Spawned != true)
                    return false;

                int now = Find.TickManager.TicksGame;
                if (now >= nextConnectivityCheckTick)
                {
                    nextConnectivityCheckTick = now + Math.Max(1, FuelProps.connectivityCheckIntervalTicks);
                    cachedConnected = WNGFuelNetworkUtility.HasPipePath(parent, engine, FuelProps.family);
                }
                return cachedConnected;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            nextConnectivityCheckTick = 0;
            cachedConnected = false;
        }

        public override string CompInspectStringExtra()
        {
            string baseText = base.CompInspectStringExtra();
            string networkText;
            if (engine == null)
                networkText = "Fuel feed: no grav engine link";
            else
                networkText = CanBeActive ? "Fuel feed: connected" : "Fuel feed: pipe disconnected";

            if (baseText.NullOrEmpty())
                return networkText;
            StringBuilder builder = new StringBuilder(baseText);
            builder.AppendLineIfNotEmpty();
            builder.Append(networkText);
            return builder.ToString();
        }
    }

    public static class WNGFuelNetworkUtility
    {
        private static readonly Queue<IntVec3> Open = new Queue<IntVec3>();
        private static readonly HashSet<IntVec3> Seen = new HashSet<IntVec3>();

        public static bool HasPipePath(Thing tank, Building_GravEngine engine, WNGGravshipTheme family)
        {
            if (tank?.Spawned != true || engine?.Spawned != true || tank.Map != engine.Map || family == WNGGravshipTheme.None)
                return false;

            CompWNGGravEngineTheme engineTheme = engine.TryGetComp<CompWNGGravEngineTheme>();
            if (engineTheme == null || engineTheme.Theme != family)
                return false;

            Map map = tank.Map;
            CellRect engineConnectionArea = engine.OccupiedRect().ExpandedBy(1).ClipInsideMap(map);

            Open.Clear();
            Seen.Clear();

            foreach (IntVec3 cell in tank.OccupiedRect().ExpandedBy(1).ClipInsideMap(map))
            {
                if (TryGetPipe(cell, map, family, engine, out _))
                    Enqueue(cell);
            }

            while (Open.Count > 0)
            {
                IntVec3 current = Open.Dequeue();
                if (engineConnectionArea.Contains(current))
                    return true;

                for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
                {
                    IntVec3 next = current + GenAdj.CardinalDirections[i];
                    if (!next.InBounds(map) || Seen.Contains(next))
                        continue;
                    if (!TryGetPipe(next, map, family, engine, out _))
                        continue;
                    Enqueue(next);
                }
            }

            return false;
        }

        private static void Enqueue(IntVec3 cell)
        {
            if (Seen.Add(cell))
                Open.Enqueue(cell);
        }

        private static bool TryGetPipe(IntVec3 cell, Map map, WNGGravshipTheme family, Building_GravEngine engine, out CompWNGFuelPipe pipe)
        {
            pipe = null;
            if (!engine.ValidSubstructureAt(cell))
                return false;

            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                CompWNGFuelPipe candidate = things[i].TryGetComp<CompWNGFuelPipe>();
                if (candidate != null && candidate.Family == family)
                {
                    pipe = candidate;
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class PlaceWorker_WNGFuelPipe : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (map == null || !loc.InBounds(map))
                return false;

            List<Thing> things = loc.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing existing = things[i];
                if (existing == thingToIgnore)
                    continue;

                ThingDef existingBuildable = existing.def?.entityDefToBuild as ThingDef;
                bool existingFuelPipe = existing.TryGetComp<CompWNGFuelPipe>() != null ||
                                        existingBuildable?.GetCompProperties<CompProperties_WNGFuelPipe>() != null;
                if (!existingFuelPipe)
                    continue;

                if (!GenConstruct.CanReplace(checkingDef, existing.def))
                    return "Another WNG fuel pipe is already planned or built in this cell.";
            }
            return true;
        }
    }
}
