using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Fresh mature-Hive map generator. The exact infrastructure and founding Wraiths created here
    /// are passed directly into the Hive Heart population tracker; there is no map-wide adoption.
    ///
    /// Founder and stock counts in this first public pass are fresh test-balance values, not copied
    /// historical constants: Queen 1, Keeper 1, Hunter 2, Warrior 2, 420 cultured biomass.
    /// Hibernation Pods are generated physically, but are not preloaded until a safe RimWorld 1.6
    /// bed-occupancy initialization path is separately verified.
    /// </summary>
    public sealed class SitePartWorker_WraithMatureHive : SitePartWorker
    {
        private const int InfrastructureRadius = 22;
        private const int PlacementAttempts = 160;
        private const int FeedingNicheCount = 3;
        private const int HibernationPodCount = 2;
        private const int DormancyVaultCount = 2;
        private const int StoredBiomass = 420;

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
            if (map == null || site == null || faction == null || faction == Faction.OfPlayer)
                return;
            if (!WraithCaptureUtility.IsWraithCaptor(faction))
                return;

            if (!TryResolveRequiredDefs(out HiveDefs defs))
                return;

            List<Thing> infrastructure = new List<Thing>();
            Building heart = TrySpawnBuilding(defs.heart, faction, map, map.Center, infrastructure);
            Building chamber = TrySpawnBuilding(defs.growthChamber, faction, map, map.Center, infrastructure);
            Building bioelectric = TrySpawnBuilding(defs.bioelectricOrgan, faction, map, map.Center, infrastructure);

            int niches = SpawnBuildingCount(defs.feedingNiche, FeedingNicheCount, faction, map, map.Center, infrastructure);
            int pods = SpawnBuildingCount(defs.hibernationPod, HibernationPodCount, faction, map, map.Center, infrastructure);
            int vaults = SpawnBuildingCount(defs.dormancyVault, DormancyVaultCount, faction, map, map.Center, infrastructure);

            if (heart == null || chamber == null || bioelectric == null || niches != FeedingNicheCount
                || pods != HibernationPodCount || vaults != DormancyVaultCount)
            {
                Log.Error("[WNG] Mature Wraith Hive generation could not establish its complete required infrastructure; generated Hive infrastructure is being removed rather than leaving a partial mature Hive.");
                DestroyGeneratedInfrastructure(infrastructure);
                return;
            }

            SpawnBiomass(defs.biomass, StoredBiomass, heart.Position, map);

            List<Pawn> founders = new List<Pawn>(6);
            if (!TrySpawnFounder(defs.queen, faction, map, heart.Position, founders)
                || !TrySpawnFounder(defs.keeper, faction, map, heart.Position, founders)
                || !TrySpawnFounder(defs.hunter, faction, map, heart.Position, founders)
                || !TrySpawnFounder(defs.hunter, faction, map, heart.Position, founders)
                || !TrySpawnFounder(defs.warrior, faction, map, heart.Position, founders)
                || !TrySpawnFounder(defs.warrior, faction, map, heart.Position, founders))
            {
                Log.Error("[WNG] Mature Wraith Hive generation could not establish the complete six-Wraith founding population; generated founders and infrastructure are being removed rather than initializing an invalid demographic cap.");
                DestroyGeneratedPawns(founders);
                DestroyGeneratedInfrastructure(infrastructure);
                return;
            }

            CompMatureWraithHivePopulation population = heart.GetComp<CompMatureWraithHivePopulation>();
            if (population == null || !population.InitializeGeneratedHive(founders, chamber))
            {
                Log.Error("[WNG] Mature Wraith Hive Heart rejected explicit demographic initialization; generated founders and infrastructure are being removed rather than leaving an unbounded Hive.");
                DestroyGeneratedPawns(founders);
                DestroyGeneratedInfrastructure(infrastructure);
                return;
            }

            Lord lord = LordMaker.MakeNewLord(
                faction,
                new LordJob_DefendBase(faction, heart.Position, 60000),
                map);
            for (int i = 0; i < founders.Count; i++)
                lord.AddPawn(founders[i]);
        }

        private static bool TryResolveRequiredDefs(out HiveDefs defs)
        {
            defs = new HiveDefs
            {
                heart = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_HiveHeart"),
                growthChamber = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GrowthChamber"),
                bioelectricOrgan = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_BioelectricOrgan"),
                feedingNiche = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_FeedingNiche"),
                hibernationPod = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_HibernationPod"),
                dormancyVault = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_DormancyVault"),
                biomass = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass"),
                queen = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithQueen"),
                keeper = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithKeeper"),
                hunter = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithHunter"),
                warrior = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithWarrior")
            };

            if (defs.AllPresent)
                return true;

            Log.Error("[WNG] Mature Wraith Hive generation aborted because one or more required fresh WNG defs are unavailable.");
            return false;
        }

        private static Building TrySpawnBuilding(ThingDef def, Faction faction, Map map, IntVec3 center, List<Thing> generated)
        {
            if (def == null || faction == null || map == null)
                return null;

            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                    center,
                    map,
                    InfrastructureRadius,
                    c => c.InBounds(map) && !c.Fogged(map) && GenSpawn.CanSpawnAt(def, c, map));
                if (!cell.IsValid || !GenSpawn.CanSpawnAt(def, cell, map))
                    continue;

                Thing thing = ThingMaker.MakeThing(def);
                if (thing == null)
                    return null;

                try
                {
                    thing.SetFaction(faction);
                    GenSpawn.Spawn(thing, cell, map);
                    Building building = thing as Building;
                    if (building != null && building.Spawned)
                    {
                        generated.Add(building);
                        return building;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Mature Hive infrastructure placement retry failed for " + def.defName + ": " + ex.Message);
                }

                if (!thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }

            return null;
        }

        private static int SpawnBuildingCount(ThingDef def, int count, Faction faction, Map map, IntVec3 center, List<Thing> generated)
        {
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (TrySpawnBuilding(def, faction, map, center, generated) == null)
                    break;
                spawned++;
            }
            return spawned;
        }

        private static void SpawnBiomass(ThingDef biomass, int count, IntVec3 near, Map map)
        {
            if (biomass == null || map == null || count <= 0)
                return;

            int remaining = count;
            int stackLimit = Math.Max(1, biomass.stackLimit);
            while (remaining > 0)
            {
                Thing stack = ThingMaker.MakeThing(biomass);
                stack.stackCount = Math.Min(remaining, stackLimit);
                remaining -= stack.stackCount;
                if (!GenPlace.TryPlaceThing(stack, near, map, ThingPlaceMode.Near) && !stack.Destroyed)
                    stack.Destroy(DestroyMode.Vanish);
            }
        }

        private static bool TrySpawnFounder(PawnKindDef kind, Faction faction, Map map, IntVec3 center, List<Pawn> founders)
        {
            if (kind == null || faction == null || map == null)
                return false;

            IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                center,
                map,
                16,
                c => c.Standable(map) && !c.Fogged(map));
            if (!cell.IsValid)
                return false;

            Pawn pawn = PawnGenerator.GeneratePawn(kind, faction, map.Tile);
            if (pawn == null)
                return false;

            try
            {
                GenSpawn.Spawn(pawn, cell, map);
                if (!pawn.Spawned || pawn.Map != map)
                {
                    if (!pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                    return false;
                }

                founders.Add(pawn);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Mature Hive founder spawn failed for " + kind.defName + ": " + ex);
                if (!pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);
                return false;
            }
        }

        private static void DestroyGeneratedPawns(List<Pawn> pawns)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && !pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);
            }
        }

        private static void DestroyGeneratedInfrastructure(List<Thing> things)
        {
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (thing != null && !thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }
        }

        private struct HiveDefs
        {
            public ThingDef heart;
            public ThingDef growthChamber;
            public ThingDef bioelectricOrgan;
            public ThingDef feedingNiche;
            public ThingDef hibernationPod;
            public ThingDef dormancyVault;
            public ThingDef biomass;
            public PawnKindDef queen;
            public PawnKindDef keeper;
            public PawnKindDef hunter;
            public PawnKindDef warrior;

            public bool AllPresent => heart != null && growthChamber != null && bioelectricOrgan != null
                && feedingNiche != null && hibernationPod != null && dormancyVault != null && biomass != null
                && queen != null && keeper != null && hunter != null && warrior != null;
        }
    }
}
