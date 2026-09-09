using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Generates a bounded Wraith guard detail for an exact-captive holding site.
    /// Keeper always acts as custodian; Hunter/Warrior fill the detail; Commander appears only
    /// after repeated/later rescue attempts. Queens are never generated as holding-site guards.
    /// </summary>
    public sealed class SitePartWorker_WraithHoldingSite : SitePartWorker
    {
        private const int MinDefenders = 3;
        private const int MaxDefenders = 10;
        private const int SpawnRadius = 18;

        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
            parms.threatPoints = Math.Max(0f, myThreatPoints);
            return parms;
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Site site = map?.Parent as Site;
            Faction faction = site?.Faction;
            if (map == null || site == null || faction == null || !WraithCaptureUtility.IsWraithCaptor(faction))
                return;

            WraithCaptivityRecord record = WraithCaptivityRegistry.Current?.Records
                .FirstOrDefault(r => r != null && r.activeRescueSiteId == site.ID);
            if (record == null)
            {
                Log.Error("[WNG] Holding site generated without its exact captivity record: site " + site.ID);
                return;
            }

            int defenderCount = Math.Max(
                MinDefenders,
                Math.Min(MaxDefenders, 3 + Math.Max(0, record.rescueFailures) + ((int)record.stage / 2)));

            int spawned = 0;
            if (TrySpawn("WNG_WraithKeeper", faction, map))
                spawned++;

            bool harderAttempt = record.stage >= WraithCaptiveTreatmentStage.Conditioning || record.rescueFailures >= 2;
            if (harderAttempt && spawned < defenderCount && TrySpawn("WNG_WraithCommander", faction, map))
                spawned++;

            bool hunterNext = true;
            while (spawned < defenderCount)
            {
                string kindName = hunterNext ? "WNG_WraithHunter" : "WNG_WraithWarrior";
                hunterNext = !hunterNext;
                if (!TrySpawn(kindName, faction, map))
                {
                    Log.Error("[WNG] Could not complete Wraith holding-site guard detail because " + kindName + " is unavailable or could not spawn.");
                    break;
                }
                spawned++;
            }
        }

        private static bool TrySpawn(string pawnKindDefName, Faction faction, Map map)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindDefName);
            if (kind == null)
            {
                Log.Error("[WNG] Missing holding-site defender PawnKindDef: " + pawnKindDefName);
                return false;
            }

            IntVec3 center = map.Center;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                center,
                map,
                SpawnRadius,
                c => c.Standable(map) && !c.Fogged(map));
            if (!cell.IsValid)
                return false;

            Pawn pawn = PawnGenerator.GeneratePawn(kind, faction, map.Tile);
            if (pawn == null)
                return false;

            GenSpawn.Spawn(pawn, cell, map);
            return pawn.Spawned;
        }
    }
}
