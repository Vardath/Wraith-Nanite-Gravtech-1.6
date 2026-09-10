using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Converts due captivity traces into world rescue sites containing the exact abducted pawn.
    /// The captive is transferred into the SitePart ThingOwner; no substitute pawn is generated.
    /// Missed sites return that same pawn to the world-pawn pool and schedule another trace later.
    /// </summary>
    public sealed class WraithRescueSiteManager : GameComponent
    {
        private const int TickInterval = 600;
        private const int SiteMinDistance = 6;
        private const int SiteMaxDistance = 18;
        private const int ActiveMapExpiryDeferralTicks = 2500;

        public WraithRescueSiteManager(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.WorldObjects == null || Find.FactionManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now % TickInterval != 0)
                return;

            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            if (registry == null)
                return;

            foreach (WraithCaptivityRecord record in registry.Records.ToList())
            {
                if (record?.pawn == null || record.pawn.Dead)
                    continue;

                if (IsPawnAtPlayerSafety(record.pawn))
                {
                    ResolveSuccessfulRecovery(registry, record);
                    continue;
                }

                if (record.activeRescueSiteId >= 0)
                    MaintainActiveSite(registry, record, now);
            }

            foreach (WraithCaptivityRecord record in registry.DueForRescueTrace(now).ToList())
                TryCreateSite(registry, record, now);
        }

        private static void MaintainActiveSite(WraithCaptivityRegistry registry, WraithCaptivityRecord record, int now)
        {
            Site site = Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .FirstOrDefault(s => s != null && !s.Destroyed && s.ID == record.activeRescueSiteId);

            if (site == null)
            {
                if (IsPawnAtPlayerSafety(record.pawn))
                    registry.ReleaseExactPawn(record.pawn);
                else
                    registry.MarkRescueSiteMissed(record.pawn, now);
                return;
            }

            if (!registry.IsRescueSiteExpired(record.pawn, now))
                return;

            if (site.HasMap)
            {
                record.activeRescueSiteExpiryTick = SafeFutureTick(now, ActiveMapExpiryDeferralTicks);
                return;
            }

            registry.MarkRescueSiteMissed(record.pawn, now);
            site.Destroy();
        }

        private static bool TryCreateSite(WraithCaptivityRegistry registry, WraithCaptivityRecord record, int now)
        {
            if (record?.pawn == null || record.pawn.Dead || record.pawn.Spawned)
                return false;

            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithHoldingSite");
            Faction captor = ResolveFaction(record.captorFactionDefName);
            if (siteDef == null || captor == null || !WraithCaptivityRegistry.IsWraithFaction(captor))
                return false;

            if (!TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    SiteMinDistance,
                    SiteMaxDistance,
                    allowCaravans: false,
                    tileFinderMode: TileFinderMode.Near))
                return false;

            float threatPoints = 500f + Math.Max(0, record.rescueAttempts) * 250f;
            Site site = SiteMaker.MakeSite(siteDef, tile, captor, ifHostileThenMustRemainHostile: true, threatPoints: threatPoints);
            SitePart part = site?.parts?.FirstOrDefault(p => p != null && p.def == siteDef);
            if (site == null || part == null)
            {
                site?.Destroy();
                return false;
            }

            part.things = new ThingOwner<Pawn>(part, oneStackOnly: true);
            if (!part.things.TryAdd(record.pawn))
            {
                site.Destroy();
                return false;
            }

            site.customLabel = "Wraith holding site — " + record.pawn.LabelShortCap;
            Find.WorldObjects.Add(site);
            if (!registry.MarkRescueSiteOpened(record.pawn, site.ID, now))
            {
                site.Destroy();
                return false;
            }

            Find.LetterStack.ReceiveLetter(
                "Wraith captive located",
                "A trace has revealed a Wraith holding site containing " + record.pawn.LabelShortCap
                    + ". This is the exact pawn who was abducted, not a generated replacement.",
                LetterDefOf.NeutralEvent,
                site);
            return true;
        }

        private static void ResolveSuccessfulRecovery(WraithCaptivityRegistry registry, WraithCaptivityRecord record)
        {
            Site site = Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .FirstOrDefault(s => s != null && !s.Destroyed && s.ID == record.activeRescueSiteId);
            registry.ReleaseExactPawn(record.pawn);
            if (site != null && !site.HasMap)
                site.Destroy();
        }

        private static bool IsPawnAtPlayerSafety(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;
            if (pawn.Spawned && pawn.Map != null && pawn.Map.IsPlayerHome)
                return true;
            return Find.WorldObjects?.Caravans != null
                && Find.WorldObjects.Caravans.Any(c => c != null && c.IsPlayerControlled && c.PawnsListForReading.Contains(pawn));
        }

        private static Faction ResolveFaction(string defName)
        {
            if (defName.NullOrEmpty() || Find.FactionManager == null)
                return null;
            return Find.FactionManager.AllFactions.FirstOrDefault(f => f != null && !f.defeated && f.def?.defName == defName);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }

    /// <summary>
    /// Generates a bounded Wraith guard detail around the exact captive supplied in the SitePart.
    /// Keeper acts as custodian; repeated rescue attempts increase Hunter/Warrior numbers and can
    /// add a Commander. Queens are never generated merely as holding-site guards.
    /// </summary>
    public sealed class SitePartWorker_WraithHoldingSite : SitePartWorker
    {
        private const int MinDefenders = 3;
        private const int MaxDefenders = 9;
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
            if (map == null || site == null || faction == null || !WraithCaptivityRegistry.IsWraithFaction(faction))
                return;

            WraithCaptivityRecord record = WraithCaptivityRegistry.Current?.Records
                .FirstOrDefault(r => r != null && r.activeRescueSiteId == site.ID);
            if (record == null)
            {
                Log.Error("[WNG] Holding site generated without its exact captivity record: site " + site.ID);
                return;
            }

            Pawn captive = site.parts.SelectMany(p => p?.things?.OfType<Pawn>() ?? Enumerable.Empty<Pawn>())
                .FirstOrDefault(p => p == record.pawn);
            if (captive == null)
            {
                Log.Error("[WNG] Holding site lost the exact captive reference for site " + site.ID);
                return;
            }

            IntVec3 captiveCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 8, c => c.Standable(map) && !c.Fogged(map));
            if (!captiveCell.IsValid)
                captiveCell = map.Center;
            GenSpawn.Spawn(captive, captiveCell, map);
            captive.guest?.SetGuestStatus(faction, GuestStatus.Prisoner);

            int defenderCount = Math.Max(MinDefenders, Math.Min(MaxDefenders, 3 + Math.Max(0, record.rescueAttempts)));
            List<Pawn> defenders = new List<Pawn>();
            TrySpawn("WNG_WraithKeeper", faction, map, defenders);
            if (record.rescueAttempts >= 2)
                TrySpawn("WNG_WraithCommander", faction, map, defenders);

            bool hunterNext = true;
            while (defenders.Count < defenderCount)
            {
                string kind = hunterNext ? "WNG_WraithHunter" : "WNG_WraithWarrior";
                hunterNext = !hunterNext;
                if (!TrySpawn(kind, faction, map, defenders))
                    break;
            }

            if (defenders.Count > 0)
            {
                Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, captive.Position, 60000), map);
                foreach (Pawn pawn in defenders)
                    lord.AddPawn(pawn);
            }
        }

        private static bool TrySpawn(string defName, Faction faction, Map map, List<Pawn> output)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            if (kind == null)
                return false;
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, SpawnRadius, c => c.Standable(map) && !c.Fogged(map));
            if (!cell.IsValid)
                return false;
            Pawn pawn = PawnGenerator.GeneratePawn(kind, faction, map.Tile);
            GenSpawn.Spawn(pawn, cell, map);
            if (!pawn.Spawned)
            {
                if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                return false;
            }
            output.Add(pawn);
            return true;
        }
    }
}
