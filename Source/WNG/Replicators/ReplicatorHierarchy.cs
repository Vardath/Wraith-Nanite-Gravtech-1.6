using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    internal static class ReplicatorHierarchyTransaction
    {
        private static readonly HashSet<int> consumedForUpgrade = new HashSet<int>();
        public static void BeginConsume(Pawn pawn) { if (pawn != null) consumedForUpgrade.Add(pawn.thingIDNumber); }
        public static void EndConsume(Pawn pawn) { if (pawn != null) consumedForUpgrade.Remove(pawn.thingIDNumber); }
        public static bool IsUpgradeConsumption(Pawn pawn) => pawn != null && consumedForUpgrade.Contains(pawn.thingIDNumber);
    }

    public sealed class CompProperties_ReplicatorHierarchy : CompProperties
    {
        public string upgradePawnKind;
        public int unitsRequired;
        public float assemblyRadius = 7f;
        public int assemblyCheckTicks = 2500;
        public string splitChildPawnKind;
        public int splitCount;
        public int splitRecombineDelayTicks = 2500;
        public CompProperties_ReplicatorHierarchy() => compClass = typeof(CompReplicatorHierarchy);
    }

    public sealed class CompReplicatorHierarchy : ThingComp
    {
        private int nextAssemblyTick;
        private int recombineBlockedUntil;
        private bool deathSplitHandled;
        private IntVec3 lastPosition = IntVec3.Invalid;
        private static bool assembling;
        private CompProperties_ReplicatorHierarchy Props => (CompProperties_ReplicatorHierarchy)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true) lastPosition = pawn.Position;
            if (!respawningAfterLoad && CanUpgrade)
                nextAssemblyTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(250, Props.assemblyCheckTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true) lastPosition = pawn.Position;
            if (pawn == null || pawn.Dead || !pawn.Spawned || !CanUpgrade || assembling || ReplicatorEMP.IsSuppressed(pawn)) return;

            int now = Find.TickManager.TicksGame;
            if (now < nextAssemblyTick || now < recombineBlockedUntil) return;
            nextAssemblyTick = now + Math.Max(250, Props.assemblyCheckTicks);

            Map map = pawn.Map;
            float radiusSq = Props.assemblyRadius * Props.assemblyRadius;
            List<Pawn> candidates = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned && p.def == pawn.def && p.Faction == pawn.Faction
                    && p.Position.DistanceToSquared(pawn.Position) <= radiusSq
                    && !ReplicatorEMP.IsSuppressed(p)
                    && (p.TryGetComp<CompReplicatorHierarchy>()?.CanParticipate(now) ?? true))
                .OrderBy(p => p.thingIDNumber)
                .ToList();

            if (candidates.Count < Props.unitsRequired || candidates[0] != pawn) return;
            PawnKindDef upgradeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.upgradePawnKind);
            if (upgradeKind == null) return;

            List<Pawn> sources = candidates.Take(Props.unitsRequired).ToList();
            Pawn upgraded = null;
            try
            {
                assembling = true;
                upgraded = PawnGenerator.GeneratePawn(upgradeKind, pawn.Faction);
                CompReplicatorState upgradedState = upgraded.TryGetComp<CompReplicatorState>();
                foreach (Pawn source in sources)
                    upgradedState?.MergeFrom(source.TryGetComp<CompReplicatorState>());
                GenSpawn.Spawn(upgraded, pawn.Position, map);

                foreach (Pawn source in sources)
                {
                    if (source == null || source.Destroyed) continue;
                    ReplicatorHierarchyTransaction.BeginConsume(source);
                    try { source.Destroy(DestroyMode.Vanish); }
                    finally { ReplicatorHierarchyTransaction.EndConsume(source); }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[WNG] Replicator recombination failed: {ex}");
                if (upgraded != null && !upgraded.Destroyed)
                {
                    ReplicatorHierarchyTransaction.BeginConsume(upgraded);
                    try { upgraded.Destroy(DestroyMode.Vanish); }
                    finally { ReplicatorHierarchyTransaction.EndConsume(upgraded); }
                }
            }
            finally { assembling = false; }
        }

        private bool CanUpgrade => Props.unitsRequired >= 2 && !string.IsNullOrEmpty(Props.upgradePawnKind);
        private bool CanParticipate(int now) => now >= recombineBlockedUntil;

        public void DelayRecombination(int ticks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            recombineBlockedUntil = Math.Max(recombineBlockedUntil, now + Math.Max(0, ticks));
            nextAssemblyTick = Math.Max(nextAssemblyTick, recombineBlockedUntil);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeath = mode == DestroyMode.KillFinalize || pawn?.Dead == true;
            if (genuineDeath && !ReplicatorHierarchyTransaction.IsUpgradeConsumption(pawn))
                Split(previousMap, pawn);
            base.PostDestroy(mode, previousMap);
        }

        private void Split(Map map, Pawn source)
        {
            if (deathSplitHandled || map == null || source == null || Props.splitCount <= 0 || string.IsNullOrEmpty(Props.splitChildPawnKind)) return;
            PawnKindDef childKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.splitChildPawnKind);
            if (childKind == null) return;
            IntVec3 origin = lastPosition;
            if (!origin.IsValid || !origin.InBounds(map)) return;

            // Latch before emission so a death transaction can never duplicate children on re-entry.
            deathSplitHandled = true;
            for (int i = 0; i < Props.splitCount; i++)
            {
                try
                {
                    Pawn child = PawnGenerator.GeneratePawn(childKind, source.Faction);
                    child.TryGetComp<CompReplicatorState>()?.CopyFrom(source.TryGetComp<CompReplicatorState>());
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(origin, map, 2);
                    GenSpawn.Spawn(child, cell, map);
                    child.TryGetComp<CompReplicatorHierarchy>()?.DelayRecombination(Props.splitRecombineDelayTicks);
                }
                catch (Exception ex)
                {
                    Log.Error($"[WNG] Replicator death split failed: {ex}");
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAssemblyTick, "wngReplicatorNextAssembly", 0);
            Scribe_Values.Look(ref recombineBlockedUntil, "wngReplicatorRecombineBlockedUntil", 0);
            Scribe_Values.Look(ref deathSplitHandled, "wngReplicatorDeathSplitHandled", false);
        }
    }
}
