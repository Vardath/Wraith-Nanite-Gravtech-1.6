using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Generates the complete currently-supported Mature Hive package or removes the entire attempt.
    /// No partial Hive is accepted: exact infrastructure, exact active founders, exact sleepers and
    /// finite exact feeding stock must all exist before the Hive Heart population anchor is initialized.
    /// </summary>
    public sealed class SitePartWorker_WraithMatureHive : SitePartWorker
    {
        private const int InfrastructureRadius = 22;
        private const int PlacementAttempts = 160;
        private const int FeedingNicheCount = 3;
        private const int HibernationPodCount = 2;
        private const int DormancyVaultCount = 2;
        private const int FeedingStockGenerationAttempts = 8;
        private const float DormantInitialLifeForce = 0.45f;

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
            if (map == null || site == null || faction == null || faction == Faction.OfPlayer || !WraithCaptivityRegistry.IsWraithFaction(faction))
                return;

            if (!TryResolveDefs(out HiveDefs defs))
                return;

            List<Thing> infrastructure = new List<Thing>();
            List<Pawn> activeFounders = new List<Pawn>();
            List<Pawn> dormantFounders = new List<Pawn>();
            List<Pawn> feedingStock = new List<Pawn>();

            Building heart = TrySpawnBuilding(defs.heart, faction, map, map.Center, infrastructure);
            int nichesSpawned = SpawnBuildingCount(defs.feedingNiche, FeedingNicheCount, faction, map, map.Center, infrastructure);
            int podsSpawned = SpawnBuildingCount(defs.hibernationPod, HibernationPodCount, faction, map, map.Center, infrastructure);
            int vaultsSpawned = SpawnBuildingCount(defs.dormancyVault, DormancyVaultCount, faction, map, map.Center, infrastructure);

            if (heart == null || nichesSpawned != FeedingNicheCount || podsSpawned != HibernationPodCount || vaultsSpawned != DormancyVaultCount)
            {
                Abort("required infrastructure", infrastructure, activeFounders, dormantFounders, feedingStock);
                return;
            }

            List<Building_Bed> niches = CollectBeds(infrastructure, defs.feedingNiche);
            List<Building_Bed> pods = CollectBeds(infrastructure, defs.hibernationPod);
            if (niches.Count != FeedingNicheCount || pods.Count != HibernationPodCount)
            {
                Abort("exact bed references", infrastructure, activeFounders, dormantFounders, feedingStock);
                return;
            }

            // Current fresh founder topology: Queen, Keeper, two Hunters and two Warriors active;
            // one Hunter and one Warrior ordinary hibernators. Numbers remain isolated here for tuning.
            if (!TrySpawnFounder(defs.queen, faction, map, heart.Position, activeFounders)
                || !TrySpawnFounder(defs.keeper, faction, map, heart.Position, activeFounders)
                || !TrySpawnFounder(defs.hunter, faction, map, heart.Position, activeFounders)
                || !TrySpawnFounder(defs.hunter, faction, map, heart.Position, activeFounders)
                || !TrySpawnFounder(defs.warrior, faction, map, heart.Position, activeFounders)
                || !TrySpawnFounder(defs.warrior, faction, map, heart.Position, activeFounders))
            {
                Abort("active founding population", infrastructure, activeFounders, dormantFounders, feedingStock);
                return;
            }

            if (!TrySpawnDormantFounder(defs.hunter, faction, map, pods[0], dormantFounders)
                || !TrySpawnDormantFounder(defs.warrior, faction, map, pods[1], dormantFounders))
            {
                Abort("ordinary hibernating founders", infrastructure, activeFounders, dormantFounders, feedingStock);
                return;
            }

            for (int i = 0; i < niches.Count; i++)
            {
                if (!TrySpawnFiniteFeedingStock(faction, map, niches[i], feedingStock))
                {
                    Abort("finite feeding stock", infrastructure, activeFounders, dormantFounders, feedingStock);
                    return;
                }
            }

            CompMatureWraithHivePopulation population = heart.GetComp<CompMatureWraithHivePopulation>();
            if (population == null || !population.InitializeGeneratedHive(activeFounders, dormantFounders, pods, feedingStock, niches))
            {
                Abort("Hive Heart population initialization", infrastructure, activeFounders, dormantFounders, feedingStock);
                return;
            }

            Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, heart.Position, 60000), map);
            foreach (Pawn pawn in activeFounders)
                lord.AddPawn(pawn);
        }

        private static bool TryResolveDefs(out HiveDefs defs)
        {
            defs = new HiveDefs
            {
                heart = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithHiveHeart"),
                feedingNiche = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithFeedingNiche"),
                hibernationPod = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithHibernationPod"),
                dormancyVault = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithDormancyVault"),
                queen = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithQueen"),
                keeper = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithKeeper"),
                hunter = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithHunter"),
                warrior = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithWarrior")
            };
            if (defs.AllPresent)
                return true;
            Log.Error("[WNG] Mature Hive generation aborted because a required fresh WNG Def is unavailable.");
            return false;
        }

        private static Building TrySpawnBuilding(ThingDef def, Faction faction, Map map, IntVec3 center, List<Thing> generated)
        {
            if (def == null || faction == null || map == null)
                return null;
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, InfrastructureRadius,
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
                    Log.Warning("[WNG] Mature Hive building placement retry for " + def.defName + " failed: " + ex.Message);
                }
                if (!thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }
            return null;
        }

        private static int SpawnBuildingCount(ThingDef def, int count, Faction faction, Map map, IntVec3 center, List<Thing> generated)
        {
            int result = 0;
            for (int i = 0; i < count; i++)
            {
                if (TrySpawnBuilding(def, faction, map, center, generated) == null)
                    break;
                result++;
            }
            return result;
        }

        private static List<Building_Bed> CollectBeds(List<Thing> generated, ThingDef def)
        {
            return generated.OfType<Building_Bed>().Where(b => b != null && b.Spawned && b.def == def).ToList();
        }

        private static bool TrySpawnFounder(PawnKindDef kind, Faction faction, Map map, IntVec3 center, List<Pawn> output)
        {
            if (kind == null || faction == null || map == null)
                return false;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 16, c => c.Standable(map) && !c.Fogged(map));
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
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                    return false;
                }
                output.Add(pawn);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Mature Hive founder spawn failed for " + kind.defName + ": " + ex.Message);
                if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                return false;
            }
        }

        private static bool TrySpawnDormantFounder(PawnKindDef kind, Faction faction, Map map, Building_Bed pod, List<Pawn> output)
        {
            if (pod == null || !pod.Spawned || pod.Map != map || pod.Faction != faction)
                return false;
            int oldCount = output.Count;
            if (!TrySpawnFounder(kind, faction, map, pod.Position, output) || output.Count != oldCount + 1)
                return false;

            Pawn pawn = output[output.Count - 1];
            Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(pawn);
            if (lifeForce != null)
                lifeForce.Value = Math.Min(lifeForce.Max, DormantInitialLifeForce);

            Job job = JobMaker.MakeJob(JobDefOf.LayDown, pod);
            job.expiryInterval = 60000;
            job.checkOverrideOnExpire = false;
            pawn.jobs?.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc);
            return true;
        }

        private static bool TrySpawnFiniteFeedingStock(Faction faction, Map map, Building_Bed niche, List<Pawn> output)
        {
            if (faction == null || map == null || niche == null || niche.Map != map || niche.Faction != faction)
                return false;

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("SpaceRefugee")
                ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("Villager");
            if (kind == null)
                return false;

            for (int attempt = 0; attempt < FeedingStockGenerationAttempts; attempt++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, null, map.Tile);
                if (pawn == null)
                    continue;
                if (!WraithCaptivityRegistry.IsValidBiologicalCaptive(pawn) || pawn.guest == null)
                {
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                    continue;
                }

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(niche.Position, map, 4, c => c.Standable(map) && !c.Fogged(map));
                if (!cell.IsValid)
                {
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                    continue;
                }

                try
                {
                    GenSpawn.Spawn(pawn, cell, map);
                    niche.ForOwnerType = BedOwnerType.Prisoner;
                    pawn.guest.SetGuestStatus(faction, GuestStatus.Prisoner);
                    if (!pawn.Spawned || pawn.Map != map || pawn.guest?.IsPrisoner != true || pawn.guest.HostFaction != faction)
                    {
                        if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                        continue;
                    }

                    Job job = JobMaker.MakeJob(JobDefOf.LayDown, niche);
                    job.expiryInterval = 60000;
                    job.checkOverrideOnExpire = false;
                    pawn.jobs?.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc);
                    output.Add(pawn);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Mature Hive feeding-stock generation retry failed: " + ex.Message);
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                }
            }
            return false;
        }

        private static void Abort(string stage, List<Thing> infrastructure, params List<Pawn>[] pawnLists)
        {
            Log.Error("[WNG] Mature Hive generation failed at " + stage + "; removing the incomplete generated Hive.");
            foreach (List<Pawn> list in pawnLists)
            {
                if (list == null) continue;
                foreach (Pawn pawn in list)
                    if (pawn != null && !pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
            }
            for (int i = infrastructure.Count - 1; i >= 0; i--)
            {
                Thing thing = infrastructure[i];
                if (thing != null && !thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }
        }

        private struct HiveDefs
        {
            public ThingDef heart;
            public ThingDef feedingNiche;
            public ThingDef hibernationPod;
            public ThingDef dormancyVault;
            public PawnKindDef queen;
            public PawnKindDef keeper;
            public PawnKindDef hunter;
            public PawnKindDef warrior;
            public bool AllPresent => heart != null && feedingNiche != null && hibernationPod != null && dormancyVault != null
                && queen != null && keeper != null && hunter != null && warrior != null;
        }
    }

    /// <summary>
    /// World discovery of established Mature Hives. This incident is independent of strategic
    /// Wraith hunger and merely exposes a bounded site belonging to an existing Wraith lineage.
    /// </summary>
    public sealed class IncidentWorker_WraithMatureHiveDiscovery : IncidentWorker
    {
        private const int MaxActiveMatureHives = 2;
        private const int MinSiteDistance = 8;
        private const int MaxSiteDistance = 20;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms) || Find.WorldObjects == null || Find.FactionManager == null || Faction.OfPlayer == null)
                return false;
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithMatureHive");
            return siteDef != null && ActiveSites(siteDef).Count < MaxActiveMatureHives && EligibleFactions(siteDef).Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithMatureHive");
            if (siteDef == null || ActiveSites(siteDef).Count >= MaxActiveMatureHives)
                return false;

            List<Faction> factions = EligibleFactions(siteDef);
            if (factions.Count == 0)
                return false;
            Faction faction = factions.RandomElement();

            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, MinSiteDistance, MaxSiteDistance, false, TileFinderMode.Near))
                return false;

            Site site = SiteMaker.MakeSite(siteDef, tile, faction, ifHostileThenMustRemainHostile: true, threatPoints: 0f);
            if (site?.parts == null || !site.parts.Any(p => p != null && p.def == siteDef))
            {
                site?.Destroy();
                return false;
            }

            site.customLabel = faction.Name + " mature hive";
            Find.WorldObjects.Add(site);
            Find.LetterStack.ReceiveLetter(
                "Mature Wraith Hive discovered",
                "Long-range reports have exposed an established mature Wraith Hive belonging to " + faction.Name + ". Its active Wraith, ordinary hibernators, feeding stock and sealed combat reserves are bounded rather than infinitely generated.",
                faction.HostileTo(Faction.OfPlayer) ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent,
                site);
            return true;
        }

        private static List<Site> ActiveSites(SitePartDef def)
        {
            return Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .Where(s => s != null && !s.Destroyed && s.parts != null && s.parts.Any(p => p != null && p.def == def))
                .ToList();
        }

        private static List<Faction> EligibleFactions(SitePartDef def)
        {
            HashSet<Faction> represented = new HashSet<Faction>(ActiveSites(def).Select(s => s.Faction).Where(f => f != null));
            return Find.FactionManager.AllFactionsListForReading
                .Where(f => f != null && f != Faction.OfPlayer && !f.defeated && WraithCaptivityRegistry.IsWraithFaction(f) && !represented.Contains(f))
                .ToList();
        }
    }
}
