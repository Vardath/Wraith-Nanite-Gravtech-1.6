using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    public sealed class CompProperties_ReplicatorHierarchy : CompProperties
    {
        public string upgradePawnKind;
        public int unitsRequired;
        public float assemblyRadius = 7f;
        public int assemblyIntervalTicks = 2500;
        public string splitChildPawnKind;
        public int splitCount;

        public CompProperties_ReplicatorHierarchy()
        {
            compClass = typeof(CompReplicatorHierarchy);
        }
    }

    /// <summary>
    /// Fresh modular Replicator hierarchy implementation.
    /// Larger bodies split downward on genuine death; smaller forms may recombine upward.
    /// Intentional recombination consumption is explicitly distinguished from death so the same
    /// transaction cannot both assemble a larger body and immediately emit split children.
    /// </summary>
    public sealed class CompReplicatorHierarchy : ThingComp
    {
        public const int SplitBornRecombinationLockTicks = 2500;

        private static readonly HashSet<int> IntentionalHierarchyConsumption = new HashSet<int>();
        private static bool assemblyTransactionActive;

        private int nextAssemblyTick;
        private int recombinationBlockedUntilTick;
        private bool deathSplitEmitted;
        private IntVec3 lastKnownPosition = IntVec3.Invalid;

        public CompProperties_ReplicatorHierarchy Props => (CompProperties_ReplicatorHierarchy)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;

            if (!respawningAfterLoad && nextAssemblyTick <= 0 && CanRecombineUpward)
                nextAssemblyTick = CurrentTick + Math.Max(250, Props.assemblyIntervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;

            if (pawn == null || pawn.Dead || !pawn.Spawned || !CanRecombineUpward || assemblyTransactionActive)
                return;

            int now = CurrentTick;
            if (now < recombinationBlockedUntilTick || now < nextAssemblyTick)
                return;

            nextAssemblyTick = now + Math.Max(250, Props.assemblyIntervalTicks);

            Map map = pawn.Map;
            if (map == null)
                return;

            float radiusSq = Props.assemblyRadius * Props.assemblyRadius;
            List<Pawn> eligible = map.mapPawns.AllPawnsSpawned
                .Where(other => other != null &&
                                !other.Dead &&
                                other.def == pawn.def &&
                                other.Faction == pawn.Faction &&
                                other.Position.DistanceToSquared(pawn.Position) <= radiusSq &&
                                !IsRecombinationLocked(other))
                .OrderBy(other => other.thingIDNumber)
                .ToList();

            if (eligible.Count < Props.unitsRequired || eligible[0] != pawn)
                return;

            PawnKindDef upgradeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.upgradePawnKind);
            if (upgradeKind == null)
            {
                Log.Error($"[WNG] Replicator hierarchy could not resolve upgrade PawnKind '{Props.upgradePawnKind}'.");
                return;
            }

            List<Pawn> consumed = eligible.Take(Props.unitsRequired).ToList();
            IntVec3 spawnCell = pawn.Position;
            Faction faction = pawn.Faction;
            CompReplicatorState donorState = SelectStateDonor(consumed);
            Pawn upgraded = null;

            try
            {
                assemblyTransactionActive = true;
                upgraded = PawnGenerator.GeneratePawn(upgradeKind, faction);
                upgraded.TryGetComp<CompReplicatorState>()?.CopyFrom(donorState);
                GenSpawn.Spawn(upgraded, spawnCell, map);

                foreach (Pawn source in consumed)
                {
                    if (source == null || source.Destroyed)
                        continue;

                    MarkIntentionalConsumption(source);
                    try
                    {
                        source.Destroy(DestroyMode.Vanish);
                    }
                    finally
                    {
                        ClearIntentionalConsumption(source);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[WNG] Replicator recombination failed for {pawn.def?.defName}: {ex}");

                if (upgraded != null && !upgraded.Destroyed)
                {
                    try
                    {
                        MarkIntentionalConsumption(upgraded);
                        upgraded.Destroy(DestroyMode.Vanish);
                    }
                    catch
                    {
                        // Preserve the original recombination exception as the actionable failure.
                    }
                    finally
                    {
                        ClearIntentionalConsumption(upgraded);
                    }
                }
            }
            finally
            {
                assemblyTransactionActive = false;
            }
        }

        public void BlockRecombinationForTicks(int ticks)
        {
            int until = CurrentTick + Math.Max(0, ticks);
            if (until > recombinationBlockedUntilTick)
                recombinationBlockedUntilTick = until;
            if (until > nextAssemblyTick)
                nextAssemblyTick = until;
        }

        public void TryEmitDeathSplit(Map map, IntVec3 origin)
        {
            if (deathSplitEmitted)
                return;

            Pawn parentPawn = parent as Pawn;
            if (parentPawn == null || map == null || IsIntentionalConsumption(parentPawn))
                return;

            if (Props.splitCount <= 0 || string.IsNullOrEmpty(Props.splitChildPawnKind))
                return;

            PawnKindDef childKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.splitChildPawnKind);
            if (childKind == null)
            {
                Log.Error($"[WNG] Replicator hierarchy could not resolve split child PawnKind '{Props.splitChildPawnKind}'.");
                return;
            }

            if (!origin.IsValid || !origin.InBounds(map))
                origin = lastKnownPosition;
            if (!origin.IsValid || !origin.InBounds(map))
            {
                Log.Error($"[WNG] Replicator split could not recover a valid map cell for {parentPawn.def?.defName}.");
                return;
            }

            CompReplicatorState parentState = parentPawn.TryGetComp<CompReplicatorState>();
            int spawned = 0;

            for (int i = 0; i < Props.splitCount; i++)
            {
                Pawn child = null;
                try
                {
                    child = PawnGenerator.GeneratePawn(childKind, parentPawn.Faction);
                    child.TryGetComp<CompReplicatorState>()?.CopyFrom(parentState);
                    child.TryGetComp<CompReplicatorHierarchy>()?.BlockRecombinationForTicks(SplitBornRecombinationLockTicks);

                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(origin, map, 2);
                    GenSpawn.Spawn(child, cell, map);
                    spawned++;
                }
                catch (Exception ex)
                {
                    if (child != null && !child.Destroyed && !child.Spawned)
                    {
                        try { child.Destroy(DestroyMode.Vanish); } catch { }
                    }
                    Log.Error($"[WNG] Replicator split failed for {parentPawn.def?.defName} -> {Props.splitChildPawnKind}: {ex}");
                }
            }

            if (spawned == Props.splitCount)
            {
                deathSplitEmitted = true;
                Log.Message($"[WNG] Replicator split completed: {parentPawn.def?.defName} -> {spawned}x {Props.splitChildPawnKind}; split-born recombination locked for {SplitBornRecombinationLockTicks} ticks.");
            }
            else
            {
                Log.Error($"[WNG] Replicator split for {parentPawn.def?.defName} produced {spawned}/{Props.splitCount} children. The split latch remains open for a later fallback attempt.");
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeath = mode == DestroyMode.KillFinalize || pawn?.Dead == true;

            if (genuineDeath && !IsIntentionalConsumption(pawn))
                TryEmitDeathSplit(previousMap, lastKnownPosition);

            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAssemblyTick, "nextAssemblyTick", 0);
            Scribe_Values.Look(ref recombinationBlockedUntilTick, "recombinationBlockedUntilTick", 0);
            Scribe_Values.Look(ref deathSplitEmitted, "deathSplitEmitted", false);
        }

        private bool CanRecombineUpward => Props.unitsRequired >= 2 && !string.IsNullOrEmpty(Props.upgradePawnKind);

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        private static CompReplicatorState SelectStateDonor(IEnumerable<Pawn> pawns)
        {
            return pawns
                .Select(pawn => pawn?.TryGetComp<CompReplicatorState>())
                .FirstOrDefault(state => state != null);
        }

        private static bool IsRecombinationLocked(Pawn pawn)
        {
            CompReplicatorHierarchy hierarchy = pawn?.TryGetComp<CompReplicatorHierarchy>();
            return hierarchy != null && CurrentTick < hierarchy.recombinationBlockedUntilTick;
        }

        private static void MarkIntentionalConsumption(Pawn pawn)
        {
            if (pawn != null)
                IntentionalHierarchyConsumption.Add(pawn.thingIDNumber);
        }

        private static bool IsIntentionalConsumption(Pawn pawn)
        {
            return pawn != null && IntentionalHierarchyConsumption.Contains(pawn.thingIDNumber);
        }

        private static void ClearIntentionalConsumption(Pawn pawn)
        {
            if (pawn != null)
                IntentionalHierarchyConsumption.Remove(pawn.thingIDNumber);
        }
    }
}
