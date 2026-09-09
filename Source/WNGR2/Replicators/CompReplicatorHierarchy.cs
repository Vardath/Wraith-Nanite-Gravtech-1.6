using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    internal static class ReplicatorDestructionIntent
    {
        private static readonly HashSet<int> intentionalHierarchyConsumption = new HashSet<int>();

        public static void MarkIntentionalHierarchyConsumption(Pawn pawn)
        {
            if (pawn != null)
                intentionalHierarchyConsumption.Add(pawn.thingIDNumber);
        }

        public static bool IsIntentionalHierarchyConsumption(Pawn pawn)
        {
            return pawn != null && intentionalHierarchyConsumption.Contains(pawn.thingIDNumber);
        }

        public static void ClearIntentionalHierarchyConsumption(Pawn pawn)
        {
            if (pawn != null)
                intentionalHierarchyConsumption.Remove(pawn.thingIDNumber);
        }
    }

    public sealed class CompProperties_ReplicatorHierarchy : CompProperties
    {
        public string upgradePawnKind;
        public int unitsRequired = 0;
        public float assemblyRadius = 7f;
        public int assemblyIntervalTicks = 2500;
        public string splitChildPawnKind;
        public int splitCount = 0;

        public CompProperties_ReplicatorHierarchy()
        {
            compClass = typeof(CompReplicatorHierarchy);
        }
    }

    public sealed class CompReplicatorHierarchy : ThingComp
    {
        // Split-born forms must remain separated long enough for the breakup to matter,
        // but the locked WNG rule is one in-game hour rather than one day.
        public const int DeathBreakupRecombinationCooldownTicks = 2500;

        private int nextAssemblyTick;
        private int recombinationBlockedUntilTick;
        private bool deathSplitEmitted;
        private IntVec3 lastKnownPosition = IntVec3.Invalid;
        private static bool assemblyInProgress;

        public CompProperties_ReplicatorHierarchy Props => (CompProperties_ReplicatorHierarchy)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;

            if (!respawningAfterLoad && nextAssemblyTick <= 0 && Props.unitsRequired >= 2 && !string.IsNullOrEmpty(Props.upgradePawnKind))
                nextAssemblyTick = Find.TickManager.TicksGame + Math.Max(250, Props.assemblyIntervalTicks);

            if (pawn?.health == null || !HasDeathSplitFor(pawn.def?.defName))
                return;

            HediffDef marker = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_ReplicatorHierarchyMarker");
            if (marker != null && !pawn.health.hediffSet.HasHediff(marker))
                pawn.health.AddHediff(marker);
        }

        public void BlockRecombinationForTicks(int ticks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int until = now + Math.Max(0, ticks);
            if (until > recombinationBlockedUntilTick)
                recombinationBlockedUntilTick = until;
            if (until > nextAssemblyTick)
                nextAssemblyTick = until;
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;

            if (pawn == null || pawn.Dead || !pawn.Spawned || assemblyInProgress)
                return;
            if (Props.unitsRequired < 2 || string.IsNullOrEmpty(Props.upgradePawnKind))
                return;

            int now = Find.TickManager.TicksGame;
            if (now < recombinationBlockedUntilTick || now < nextAssemblyTick)
                return;

            if (ReplicatorEMPSuppressionUtility.IsSuppressed(pawn) ||
                ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(pawn))
            {
                nextAssemblyTick = now + 250;
                return;
            }

            Map map = pawn.Map;
            MapComponent_ReplicatorContainment containment = map?.GetComponent<MapComponent_ReplicatorContainment>();
            if (containment?.IsContained(pawn.Position) == true)
            {
                nextAssemblyTick = now + 250;
                return;
            }

            float adaptationFactor = pawn.TryGetComp<CompReplicatorAdaptation>()?.AssemblyIntervalFactor ?? 1f;
            float coordinationFactor = map?.GetComponent<MapComponent_ReplicatorCoordination>()?.AssemblyFactorFor(pawn) ?? 1f;
            float postureFactor = map?.GetComponent<MapComponent_ReplicatorSwarmBehavior>()?.AssemblyFactorFor(pawn) ?? 1f;
            float intervalFactor = adaptationFactor * coordinationFactor * postureFactor;
            nextAssemblyTick = now + Math.Max(250, (int)Math.Round(Props.assemblyIntervalTicks * intervalFactor));

            float radiusSq = Props.assemblyRadius * Props.assemblyRadius;
            List<Pawn> candidates = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.def == pawn.def && p.Faction == pawn.Faction &&
                            p.Position.DistanceToSquared(pawn.Position) <= radiusSq &&
                            containment?.IsContained(p.Position) != true &&
                            !ReplicatorEMPSuppressionUtility.IsSuppressed(p) &&
                            !ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(p) &&
                            !IsDeathBreakupCoolingDown(p))
                .OrderBy(p => p.thingIDNumber)
                .ToList();

            if (candidates.Count < Props.unitsRequired || candidates[0] != pawn)
                return;

            PawnKindDef upgradeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.upgradePawnKind);
            if (upgradeKind == null)
                return;

            IntVec3 spawnCell = pawn.Position;
            Faction faction = pawn.Faction;
            List<Pawn> consumed = candidates.Take(Props.unitsRequired).ToList();
            CompReplicatorMaterial materialDonor = consumed
                .Select(p => p.TryGetComp<CompReplicatorMaterial>())
                .FirstOrDefault(c => c?.SourceDef != null);
            ReplicatorAdaptationType mergedRole = ResolveMergedSpecialization(consumed, map);

            Pawn upgraded = null;
            bool sourceCommitStarted = false;
            try
            {
                assemblyInProgress = true;
                upgraded = PawnGenerator.GeneratePawn(request: new PawnGenerationRequest(
                    kind: upgradeKind,
                    faction: faction,
                    context: PawnGenerationContext.NonPlayer,
                    tile: map.Tile));
                upgraded.TryGetComp<CompReplicatorMaterial>()?.CopyFrom(materialDonor);
                upgraded.TryGetComp<CompReplicatorAdaptation>()?.InheritExact(mergedRole);

                ReplicatorRecombinationVisualUtility.PlayConvergence(consumed, spawnCell, map);
                GenSpawn.Spawn(upgraded, spawnCell, map);

                sourceCommitStarted = true;
                foreach (Pawn unit in consumed)
                {
                    if (unit == null || unit.Destroyed)
                        continue;

                    ReplicatorDestructionIntent.MarkIntentionalHierarchyConsumption(unit);
                    try
                    {
                        unit.Destroy(DestroyMode.Vanish);
                    }
                    finally
                    {
                        ReplicatorDestructionIntent.ClearIntentionalHierarchyConsumption(unit);
                    }
                }
                ReplicatorRecombinationVisualUtility.PlayAssemblyLock(upgraded);
            }
            catch (Exception ex)
            {
                if (!sourceCommitStarted && upgraded != null && !upgraded.Destroyed)
                {
                    try
                    {
                        ReplicatorDestructionIntent.MarkIntentionalHierarchyConsumption(upgraded);
                        upgraded.Destroy(DestroyMode.Vanish);
                    }
                    catch (Exception cleanupEx)
                    {
                        Log.Warning($"[WNG] Replicator recombination rollback cleanup failed for {pawn.def?.defName}: {cleanupEx.Message}");
                    }
                    finally
                    {
                        ReplicatorDestructionIntent.ClearIntentionalHierarchyConsumption(upgraded);
                    }
                }
                Log.Error($"[WNG] Replicator recombination failed for {pawn.def?.defName}: {ex}");
            }
            finally
            {
                assemblyInProgress = false;
            }
        }

        private static bool IsDeathBreakupCoolingDown(Pawn pawn)
        {
            CompReplicatorHierarchy hierarchy = pawn?.TryGetComp<CompReplicatorHierarchy>();
            if (hierarchy == null)
                return false;
            int now = Find.TickManager?.TicksGame ?? 0;
            return now < hierarchy.recombinationBlockedUntilTick;
        }

        public void TryEmitDeathSplit(Map map, IntVec3 origin)
        {
            if (deathSplitEmitted)
                return;

            Pawn pawn = parent as Pawn;
            if (pawn == null || map == null || ReplicatorDestructionIntent.IsIntentionalHierarchyConsumption(pawn))
                return;

            if (!TryResolveDeathSplit(pawn.def?.defName, out PawnKindDef childKind, out int count))
                return;

            if (!origin.IsValid || !origin.InBounds(map))
                origin = lastKnownPosition;
            if (!origin.IsValid || !origin.InBounds(map))
            {
                Log.Error($"[WNG] Replicator death split could not recover a valid map position for {pawn.def?.defName}.");
                return;
            }

            if (childKind?.race == null)
            {
                Log.Error($"[WNG] Replicator death split child kind is missing for {pawn.def?.defName}.");
                return;
            }

            if (childKind.race == pawn.def || childKind.defName == pawn.kindDef?.defName)
            {
                Log.Error($"[WNG] Replicator death split rejected self-clone mapping {pawn.def?.defName} -> {childKind.defName}/{childKind.race.defName}.");
                return;
            }

            Faction faction = pawn.Faction;
            CompReplicatorMaterial material = pawn.TryGetComp<CompReplicatorMaterial>();
            CompReplicatorLatticeOverride latticeOverride = pawn.TryGetComp<CompReplicatorLatticeOverride>();
            ReplicatorAdaptationType role = pawn.TryGetComp<CompReplicatorAdaptation>()?.Specialization ?? ReplicatorAdaptationType.Primitive;

            ReplicatorSplitVisualUtility.PlayBreakup(origin, map, count);
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                Pawn child = null;
                try
                {
                    child = PawnGenerator.GeneratePawn(request: new PawnGenerationRequest(
                        kind: childKind,
                        faction: faction,
                        context: PawnGenerationContext.NonPlayer,
                        tile: map.Tile));

                    if (child == null || child.kindDef != childKind || child.def != childKind.race)
                    {
                        string actual = child == null
                            ? "null"
                            : $"kind={child.kindDef?.defName ?? "null"}, race={child.def?.defName ?? "null"}";
                        if (child != null && !child.Destroyed)
                            child.Destroy(DestroyMode.Vanish);
                        Log.Error($"[WNG] Replicator death split generation mismatch. Requested kind={childKind.defName}, race={childKind.race.defName}; got {actual}.");
                        continue;
                    }

                    child.TryGetComp<CompReplicatorMaterial>()?.CopyFrom(material);
                    child.TryGetComp<CompReplicatorAdaptation>()?.InheritExact(role);
                    latticeOverride?.CopyTemporaryOverrideTo(child);
                    child.TryGetComp<CompReplicatorHierarchy>()?.BlockRecombinationForTicks(DeathBreakupRecombinationCooldownTicks);

                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(origin, map, 2);
                    GenSpawn.Spawn(child, cell, map);
                    ReplicatorSplitVisualUtility.PlayChildEmergence(child);
                    spawned++;
                }
                catch (Exception ex)
                {
                    if (child != null && !child.Destroyed && !child.Spawned)
                    {
                        try { child.Destroy(DestroyMode.Vanish); } catch { }
                    }
                    Log.Error($"[WNG] Replicator death split failed for {pawn.def?.defName} -> {childKind.defName}: {ex}");
                }
            }

            if (spawned == count)
            {
                deathSplitEmitted = true;
                Log.Message($"[WNG] Replicator death split completed: {pawn.def?.defName} -> {count}x {childKind.defName}; split-born forms cannot recombine for one in-game hour.");
            }
            else
            {
                Log.Error($"[WNG] Replicator death split for {pawn.def?.defName} spawned {spawned}/{count}; latch remains open for fallback.");
            }
        }

        private static bool HasDeathSplitFor(string parentDefName)
        {
            return parentDefName == "WNG_ReplicatorSiegeMass" ||
                   parentDefName == "WNG_ReplicatorTitan" ||
                   parentDefName == "WNG_ReplicatorBulwark" ||
                   parentDefName == "WNG_ReplicatorHunter";
        }

        private static bool TryResolveDeathSplit(string parentDefName, out PawnKindDef childKind, out int count)
        {
            string childDefName;
            count = 2;
            switch (parentDefName)
            {
                case "WNG_ReplicatorSiegeMass": childDefName = "WNG_ReplicatorTitan"; break;
                case "WNG_ReplicatorTitan": childDefName = "WNG_ReplicatorBulwark"; break;
                case "WNG_ReplicatorBulwark": childDefName = "WNG_ReplicatorHunter"; break;
                case "WNG_ReplicatorHunter": childDefName = "WNG_ReplicatorDrone"; break;
                default:
                    childKind = null;
                    count = 0;
                    return false;
            }

            childKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(childDefName);
            return childKind != null;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeathCleanup = mode == DestroyMode.KillFinalize || (mode == DestroyMode.Vanish && pawn?.Dead == true);
            if (genuineDeathCleanup && !ReplicatorDestructionIntent.IsIntentionalHierarchyConsumption(pawn))
                TryEmitDeathSplit(previousMap, lastKnownPosition);

            base.PostDestroy(mode, previousMap);
        }

        private static ReplicatorAdaptationType ResolveMergedSpecialization(List<Pawn> consumed, Map map)
        {
            Dictionary<ReplicatorAdaptationType, int> counts = new Dictionary<ReplicatorAdaptationType, int>();
            foreach (Pawn unit in consumed)
            {
                ReplicatorAdaptationType role = unit.TryGetComp<CompReplicatorAdaptation>()?.Specialization ?? ReplicatorAdaptationType.Primitive;
                if (role == ReplicatorAdaptationType.Primitive)
                    continue;
                counts.TryGetValue(role, out int count);
                counts[role] = count + 1;
            }

            if (counts.Count > 0)
            {
                return counts
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => (int)kv.Key)
                    .First().Key;
            }

            return map?.GetComponent<MapComponent_ReplicatorAdaptation>()?.SelectSpecialization() ?? ReplicatorAdaptationType.Primitive;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAssemblyTick, "nextAssemblyTick", 0);
            Scribe_Values.Look(ref recombinationBlockedUntilTick, "recombinationBlockedUntilTick", 0);
            Scribe_Values.Look(ref deathSplitEmitted, "deathSplitEmitted", false);
        }
    }

    internal static class ReplicatorRecombinationVisualUtility
    {
        private const int MaximumConvergenceFlashes = 6;

        public static void PlayConvergence(List<Pawn> consumed, IntVec3 origin, Map map)
        {
            if (map == null || consumed == null || !origin.InBounds(map))
                return;

            int emitted = 0;
            foreach (Pawn unit in consumed
                         .Where(p => p != null && p.Spawned && p.Map == map)
                         .OrderBy(p => p.thingIDNumber))
            {
                FleckMaker.Static(unit.TrueCenter(), map, FleckDefOf.ExplosionFlash, 0.30f);
                emitted++;
                if (emitted >= MaximumConvergenceFlashes)
                    return;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 1.9f, false)
                         .Where(c => c.InBounds(map) && c != origin)
                         .OrderByDescending(c => c.DistanceToSquared(origin)))
            {
                FleckMaker.Static(cell.ToVector3Shifted(), map, FleckDefOf.ExplosionFlash, 0.20f);
                emitted++;
                if (emitted >= MaximumConvergenceFlashes)
                    break;
            }
        }

        public static void PlayAssemblyLock(Pawn upgraded)
        {
            if (upgraded?.Map == null || !upgraded.Spawned)
                return;
            FleckMaker.Static(upgraded.TrueCenter(), upgraded.Map, FleckDefOf.ExplosionFlash, 0.78f);
        }
    }

    internal static class ReplicatorSplitVisualUtility
    {
        private const int MinimumFragmentFlashes = 3;
        private const int MaximumFragmentFlashes = 6;

        public static void PlayBreakup(IntVec3 origin, Map map, int childCount)
        {
            if (map == null || !origin.InBounds(map))
                return;

            FleckMaker.Static(origin.ToVector3Shifted(), map, FleckDefOf.ExplosionFlash, 0.72f);

            int wanted = Math.Max(MinimumFragmentFlashes, Math.Min(MaximumFragmentFlashes, Math.Max(1, childCount) * 2));
            int emitted = 0;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 2.4f, false)
                         .Where(c => c.InBounds(map) && c != origin)
                         .OrderBy(_ => Rand.Value))
            {
                FleckMaker.Static(cell.ToVector3Shifted(), map, FleckDefOf.ExplosionFlash, 0.28f);
                emitted++;
                if (emitted >= wanted)
                    break;
            }
        }

        public static void PlayChildEmergence(Pawn child)
        {
            if (child?.Map == null || !child.Spawned)
                return;
            FleckMaker.Static(child.TrueCenter(), child.Map, FleckDefOf.ExplosionFlash, 0.38f);
        }
    }
}
