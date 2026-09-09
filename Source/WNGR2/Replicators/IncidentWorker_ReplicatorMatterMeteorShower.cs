using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public sealed class IncidentWorker_ReplicatorMatterMeteorShower : IncidentWorker
    {
        private const int MinimumClusters = 3;
        private const int MaximumClusters = 4;
        private const int MinimumMatterPerCluster = 12;
        private const int MaximumMatterPerCluster = 18;
        private const int MaximumExistingMatter = 60;
        private const int CellSearchAttempts = 120;
        private const int EdgeMargin = 7;
        private const int MinimumClusterSeparationSquared = 64;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!WNG_Config.ReplicatorOutbreaksEnabled || !(parms.target is Map map))
                return false;
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorMatter");
            ThingDef meteorDef = DefDatabase<ThingDef>.GetNamedSilentFail("MeteoriteIncoming");
            if (matterDef == null || meteorDef == null)
                return false;
            if (ReplicatorCrisisUtility.CountHostileBlocks(map) > 0)
                return false;
            int existingMatter = map.listerThings.ThingsOfDef(matterDef)
                .Where(t => t != null && !t.Destroyed && t.Spawned)
                .Sum(t => Math.Max(0, t.stackCount));
            if (existingMatter >= MaximumExistingMatter)
                return false;
            return base.CanFireNowSub(parms) && CanFindImpactCell(map, null, out _);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!WNG_Config.ReplicatorOutbreaksEnabled || !(parms.target is Map map))
                return false;
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorMatter");
            ThingDef meteorDef = DefDatabase<ThingDef>.GetNamedSilentFail("MeteoriteIncoming");
            if (matterDef == null || meteorDef == null || ReplicatorCrisisUtility.CountHostileBlocks(map) > 0)
                return false;
            int existingMatter = map.listerThings.ThingsOfDef(matterDef)
                .Where(t => t != null && !t.Destroyed && t.Spawned)
                .Sum(t => Math.Max(0, t.stackCount));
            if (existingMatter >= MaximumExistingMatter)
                return false;
            int clusterCount = Rand.RangeInclusive(MinimumClusters, MaximumClusters);
            List<IntVec3> impactCells = new List<IntVec3>(clusterCount);
            for (int i = 0; i < clusterCount; i++)
            {
                if (!CanFindImpactCell(map, impactCells, out IntVec3 cell))
                    return false;
                impactCells.Add(cell);
            }
            int totalMatter = 0;
            foreach (IntVec3 cell in impactCells)
            {
                Thing matter = ThingMaker.MakeThing(matterDef);
                if (matter == null)
                    return false;
                matter.stackCount = Rand.RangeInclusive(MinimumMatterPerCluster, MaximumMatterPerCluster);
                totalMatter += matter.stackCount;
                SkyfallerMaker.SpawnSkyfaller(meteorDef, matter, cell, map);
            }
            SoundDef warning = DefDatabase<SoundDef>.GetNamedSilentFail("WNG_ReplicatorMatterFall");
            warning?.PlayOneShot(new TargetInfo(impactCells[0], map));
            string text = def.letterText
                + "\n\nApproximately " + totalMatter
                + " units of Replicator Matter are inbound in " + impactCells.Count
                + " separated impacts. The material is initially dormant, but after roughly half a RimWorld day exposed stacks can begin rebuilding themselves into hostile Replicators. Reprocess it, destroy it, or move it under powered nanite containment before the lattice wakes.";
            SendStandardLetter(def.letterLabel, text, def.letterDef, parms, new TargetInfo(impactCells[0], map));
            return true;
        }

        private static bool CanFindImpactCell(Map map, List<IntVec3> existing, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            if (map == null || map.Size.x <= EdgeMargin * 2 + 2 || map.Size.z <= EdgeMargin * 2 + 2)
                return false;
            for (int attempt = 0; attempt < CellSearchAttempts; attempt++)
            {
                IntVec3 cell = new IntVec3(
                    Rand.RangeInclusive(EdgeMargin, map.Size.x - EdgeMargin - 1),
                    0,
                    Rand.RangeInclusive(EdgeMargin, map.Size.z - EdgeMargin - 1));
                if (!cell.InBounds(map) || !cell.Standable(map) || cell.Roofed(map))
                    continue;
                if (cell.GetFirstBuilding(map) != null)
                    continue;
                if (existing != null && existing.Any(other => other.DistanceToSquared(cell) < MinimumClusterSeparationSquared))
                    continue;
                result = cell;
                return true;
            }
            return false;
        }
    }
}
