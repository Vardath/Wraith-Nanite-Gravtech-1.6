using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Map-level accounting for a block-Replicator consumption event. The initial eligible matter
    /// is captured once an uncontrolled swarm is present. The terminal state latches when the
    /// configured fraction of that baseline has disappeared. This is consumption progress, not
    /// hunger or fuel.
    /// </summary>
    public sealed class MapComponent_ReplicatorConsumption : MapComponent
    {
        private const int RecountIntervalTicks = 2500;
        private const float DefaultTerminalConsumedFraction = 0.925f;

        private float initialMatter = -1f;
        private float remainingMatter = -1f;
        private bool terminalConsumption;
        private int nextRecountTick;
        private bool dirty = true;

        public MapComponent_ReplicatorConsumption(Map map) : base(map) { }

        public bool TerminalConsumption => terminalConsumption;
        public float InitialMatter => Math.Max(0f, initialMatter);
        public float RemainingMatter => Math.Max(0f, remainingMatter);
        public float ConsumedFraction => initialMatter <= 0.001f ? 0f : Math.Max(0f, Math.Min(1f, 1f - remainingMatter / initialMatter));

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (!HasAutonomousReplicators())
            {
                // A latched terminal state belongs to the current swarm encounter. Once every
                // uncontrolled Replicator has gone, a future infestation receives a fresh baseline.
                if (initialMatter >= 0f || terminalConsumption)
                {
                    initialMatter = -1f;
                    remainingMatter = -1f;
                    terminalConsumption = false;
                    dirty = true;
                }
                return;
            }

            if (!dirty && now < nextRecountTick) return;
            nextRecountTick = now + RecountIntervalTicks;
            dirty = false;

            float current = CountEligibleMatter();
            remainingMatter = current;
            if (initialMatter < 0f)
            {
                initialMatter = Math.Max(1f, current);
                return;
            }

            // New construction or deliveries do not inflate the original invasion baseline.
            // They remain consumable, but cannot move the swarm backwards from a latched state.
            if (!terminalConsumption && ConsumedFraction >= DefaultTerminalConsumedFraction)
                terminalConsumption = true;
        }

        public void NotifyConsumptionChanged()
        {
            dirty = true;
        }

        private bool HasAutonomousReplicators()
        {
            return map?.mapPawns?.AllPawnsSpawned?.Any(p =>
                p != null && !p.Dead && p.TryGetComp<CompReplicatorState>() != null
                && p.Faction != Faction.OfPlayer && !p.IsColonyMechPlayerControlled) == true;
        }

        private float CountEligibleMatter()
        {
            if (map == null) return 0f;
            float total = 0f;

            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (!IsEligibleThing(thing)) continue;
                if (thing.def.category == ThingCategory.Item)
                    total += Math.Max(1, thing.stackCount);
                else
                    total += 1f;
            }

            foreach (IntVec3 cell in map.AllCells)
            {
                if (CompReplicatorCellConsumption.HasConsumableTopTerrain(map, cell)) total += 1f;
                if (CompReplicatorCellConsumption.FoundationAt(map, cell) != null) total += 1f;
                if (map.roofGrid.RoofAt(cell) != null) total += 1f;
            }
            return total;
        }

        private static bool IsEligibleThing(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.def == null) return false;
            if (thing is Pawn || thing is Corpse) return false;
            if (thing.TryGetComp<CompReplicatorState>() != null) return false;
            if (thing.def.defName == "WNG_ReplicatorMatter" || thing.def.defName == "WNG_ReplicatorCoreFragment") return false;
            if (thing is Plant) return true;
            if (thing.def.category == ThingCategory.Item) return thing.def.EverHaulable;
            return thing.def.category == ThingCategory.Building && thing.def.destroyable;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialMatter, "wngReplicatorInitialConsumableMatter", -1f);
            Scribe_Values.Look(ref remainingMatter, "wngReplicatorRemainingConsumableMatter", -1f);
            Scribe_Values.Look(ref terminalConsumption, "wngReplicatorTerminalConsumption", false);
            Scribe_Values.Look(ref nextRecountTick, "wngReplicatorNextMatterRecount", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) dirty = true;
        }
    }

    public sealed class JobGiver_ReplicatorTerminalAttack : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || ReplicatorEMP.IsSuppressed(pawn)) return null;
            if (pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled) return null;
            if (pawn.Map.GetComponent<MapComponent_ReplicatorConsumption>()?.TerminalConsumption != true) return null;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned
                    && p.TryGetComp<CompReplicatorState>() == null
                    && pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
            return target == null ? null : JobMaker.MakeJob(JobDefOf.AttackMelee, target);
        }
    }

    internal static class ReplicatorTerminalUtility
    {
        public static bool IsTerminal(Pawn pawn)
            => pawn?.Map?.GetComponent<MapComponent_ReplicatorConsumption>()?.TerminalConsumption == true;
    }
}
