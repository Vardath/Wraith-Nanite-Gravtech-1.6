using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorCellConsumption : CompProperties
    {
        public float searchRadius = 45f;
        public int searchIntervalTicks = 600;
        public int workTicks = 300;
        public float floorMatter = 3f;
        public float foundationMatter = 5f;
        public float thinRoofMatter = 2f;
        public float thickRoofMatter = 8f;
        public CompProperties_ReplicatorCellConsumption() => compClass = typeof(CompReplicatorCellConsumption);
    }

    public sealed class CompReplicatorCellConsumption : ThingComp
    {
        private int nextSearchTick;
        private IntVec3 cachedCell = IntVec3.Invalid;
        private CompProperties_ReplicatorCellConsumption Props => (CompProperties_ReplicatorCellConsumption)props;
        public int WorkTicks => Math.Max(60, Props.workTicks);

        public bool CanAct
        {
            get
            {
                Pawn pawn = parent as Pawn;
                return pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map != null
                    && pawn.Faction != Faction.OfPlayer && !pawn.IsColonyMechPlayerControlled
                    && !ReplicatorEMP.IsSuppressed(pawn)
                    && !ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position);
            }
        }

        public IntVec3 GetTargetCell()
        {
            Pawn pawn = parent as Pawn;
            if (!CanAct || pawn == null) return IntVec3.Invalid;
            if (cachedCell.IsValid && IsConsumableCell(pawn.Map, cachedCell) && pawn.CanReach(cachedCell, PathEndMode.Touch, Danger.Deadly))
                return cachedCell;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextSearchTick) return IntVec3.Invalid;
            nextSearchTick = now + Math.Max(120, Props.searchIntervalTicks);

            float radius = Math.Max(1f, Props.searchRadius);
            cachedCell = GenRadial.RadialCellsAround(pawn.Position, radius, true)
                .Where(c => c.InBounds(pawn.Map) && IsConsumableCell(pawn.Map, c) && pawn.CanReach(c, PathEndMode.Touch, Danger.Deadly))
                .OrderByDescending(c => CellPriority(pawn.Map, c))
                .ThenBy(c => c.DistanceToSquared(pawn.Position))
                .FirstOrDefault(IntVec3.Invalid);
            return cachedCell;
        }

        public void FinishCell(IntVec3 cell)
        {
            Pawn pawn = parent as Pawn;
            if (!CanAct || pawn == null || !cell.IsValid || !cell.InBounds(pawn.Map) || !IsConsumableCell(pawn.Map, cell)) return;

            Map map = pawn.Map;
            CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
            CompReplicatorAssimilation matter = pawn.TryGetComp<CompReplicatorAssimilation>();
            float gained = 0f;

            TerrainDef foundation = FoundationAt(map, cell);
            if (foundation != null)
            {
                RecordTerrainFeedstock(state, foundation);
                if (RemoveFoundation(map, cell)) gained += Math.Max(0f, Props.foundationMatter);
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (IsConsumableTopTerrain(terrain))
            {
                RecordTerrainFeedstock(state, terrain);
                map.terrainGrid.RemoveTopLayer(cell, false);
                gained += Math.Max(0f, Props.floorMatter);
            }

            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof != null)
            {
                bool thick = roof.isThickRoof;
                RecordRoofFeedstock(state, roof);
                map.roofGrid.SetRoof(cell, null);
                gained += thick ? Math.Max(0f, Props.thickRoofMatter) : Math.Max(0f, Props.thinRoofMatter);
            }

            if (gained > 0f) matter?.AddStoredMatter(gained);
            cachedCell = IntVec3.Invalid;
            map.GetComponent<MapComponent_ReplicatorConsumption>()?.NotifyConsumptionChanged();
        }

        public static bool IsConsumableCell(Map map, IntVec3 cell)
        {
            if (map == null || !cell.InBounds(map) || ReplicatorContainmentUtility.IsContained(map, cell)) return false;
            if (FoundationAt(map, cell) != null) return true;
            if (IsConsumableTopTerrain(map.terrainGrid.TerrainAt(cell))) return true;
            return map.roofGrid.RoofAt(cell) != null;
        }

        private static bool IsConsumableTopTerrain(TerrainDef terrain)
        {
            if (terrain == null || terrain.isFoundation) return false;
            // Layerable terrains are constructed/removable floor layers. Costed terrain is also
            // treated as built material, while natural soil/water/rough ground is left alone.
            return terrain.layerable || (terrain.costList != null && terrain.costList.Count > 0);
        }

        private static float CellPriority(Map map, IntVec3 cell)
        {
            float score = 0f;
            if (FoundationAt(map, cell) != null) score += 300f;
            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (IsConsumableTopTerrain(terrain)) score += 200f;
            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof != null) score += roof.isThickRoof ? 160f : 100f;
            return score;
        }

        private static readonly MethodInfo FoundationAtMethod = typeof(TerrainGrid).GetMethod("FoundationAt", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(IntVec3) }, null);
        private static readonly MethodInfo RemoveFoundationMethod = typeof(TerrainGrid).GetMethod("RemoveFoundation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(IntVec3) }, null);

        internal static TerrainDef FoundationAt(Map map, IntVec3 cell)
        {
            if (map?.terrainGrid == null || FoundationAtMethod == null) return null;
            try { return FoundationAtMethod.Invoke(map.terrainGrid, new object[] { cell }) as TerrainDef; }
            catch { return null; }
        }

        private static bool RemoveFoundation(Map map, IntVec3 cell)
        {
            if (map?.terrainGrid == null || RemoveFoundationMethod == null) return false;
            try
            {
                RemoveFoundationMethod.Invoke(map.terrainGrid, new object[] { cell });
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Could not remove consumed foundation at " + cell + ": " + ex.Message);
                return false;
            }
        }

        private static void RecordTerrainFeedstock(CompReplicatorState state, TerrainDef terrain)
        {
            if (state == null || terrain == null) return;
            List<ThingDefCountClass> costs = terrain.costList;
            if (costs != null && costs.Count > 0)
            {
                foreach (ThingDefCountClass cost in costs)
                    if (cost?.thingDef != null)
                        state.RecordFeedstockDef(cost.thingDef, Math.Max(1, cost.count));
                return;
            }
            state.RecordEnvironmentalFeedstock(terrain.defName, terrain.label, terrain.isFoundation ? 1f : 0f, 1);
        }

        private static void RecordRoofFeedstock(CompReplicatorState state, RoofDef roof)
        {
            if (state == null || roof == null) return;
            string id = (roof.defName ?? string.Empty).ToLowerInvariant();
            float quality = roof.isThickRoof || id.Contains("rock") ? 1f : 0f;
            state.RecordEnvironmentalFeedstock(roof.defName, roof.label, quality, roof.isThickRoof ? 3 : 1);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextSearchTick, "wngReplicatorCellSearch", 0);
            Scribe_Values.Look(ref cachedCell, "wngReplicatorCellTarget", IntVec3.Invalid);
        }
    }

    public sealed class JobGiver_ReplicatorConsumeCell : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompReplicatorCellConsumption comp = pawn?.TryGetComp<CompReplicatorCellConsumption>();
            IntVec3 cell = comp?.GetTargetCell() ?? IntVec3.Invalid;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorConsumeCell");
            return !cell.IsValid || def == null ? null : JobMaker.MakeJob(def, cell);
        }
    }

    public sealed class JobDriver_ReplicatorConsumeCell : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA.Cell, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !job.targetA.Cell.IsValid || !CompReplicatorCellConsumption.IsConsumableCell(pawn.Map, job.targetA.Cell));
            this.FailOn(() => ReplicatorEMP.IsSuppressed(pawn) || ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position));
            yield return Toils_Goto.GotoCell(Target, PathEndMode.Touch);
            CompReplicatorCellConsumption comp = pawn.TryGetComp<CompReplicatorCellConsumption>();
            yield return Toils_General.Wait(comp?.WorkTicks ?? 300);
            Toil finish = ToilMaker.MakeToil("WNGReplicatorConsumeCellFinish");
            finish.initAction = () => pawn.TryGetComp<CompReplicatorCellConsumption>()?.FinishCell(job.targetA.Cell);
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
