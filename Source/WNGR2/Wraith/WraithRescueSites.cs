using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Turns due captivity traces into world rescue sites without ever generating a substitute pawn.
    /// The exact captive is placed into the SitePart ThingOwner and the site's world-object ID is
    /// persisted back onto that captive's record.
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
            {
                if (record?.pawn == null || record.pawn.Dead || record.pawn.Spawned)
                    continue;
                TryCreateSite(registry, record, now);
            }
        }

        private static void MaintainActiveSite(WraithCaptivityRegistry registry, WraithCaptivityRecord record, int now)
        {
            Site site = Find.WorldObjects.AllWorldObjects
                .OfType<Site>()
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

            // Never expire a rescue opportunity out from under the player while its map is open.
            if (site.HasMap)
            {
                record.activeRescueSiteExpiryTick = SafeFutureTick(now, ActiveMapExpiryDeferralTicks);
                return;
            }

            // SitePart.PostDestroy passes held pawns back to the world-pawn pool rather than replacing them.
            // Mark the same captive as missed, then remove the expired world marker.
            registry.MarkRescueSiteMissed(record.pawn, now);
            site.Destroy();
        }

        private static bool TryCreateSite(WraithCaptivityRegistry registry, WraithCaptivityRecord record, int now)
        {
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithHoldingSite");
            Faction captor = ResolveFaction(record.captorFactionDefName);
            if (siteDef == null || captor == null)
                return false;

            if (!TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    SiteMinDistance,
                    SiteMaxDistance,
                    allowCaravans: false,
                    tileFinderMode: TileFinderMode.Near))
                return false;

            float threatPoints = 450f
                + 175f
                + 250f * Math.Max(0, record.rescueFailures)
                + 125f * Math.Max(0, (int)record.stage);

            Site site = SiteMaker.MakeSite(siteDef, tile, captor, ifHostileThenMustRemainHostile: true, threatPoints: threatPoints);
            if (site?.parts == null || site.parts.Count == 0)
                return false;

            SitePart part = site.parts.FirstOrDefault(p => p != null && p.def == siteDef);
            if (part == null)
                return false;

            part.things = new ThingOwner<Pawn>(part, oneStackOnly: true);
            if (!part.things.TryAdd(record.pawn))
                return false;

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
                + ". This is the exact pawn who was abducted. The opportunity will eventually be lost if no rescue is attempted.",
                LetterDefOf.NeutralEvent,
                site);
            return true;
        }

        private static void ResolveSuccessfulRecovery(WraithCaptivityRegistry registry, WraithCaptivityRecord record)
        {
            Site site = Find.WorldObjects.AllWorldObjects
                .OfType<Site>()
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
            if (defName.NullOrEmpty())
                return null;
            return Find.FactionManager.AllFactions
                .FirstOrDefault(f => f != null && !f.defeated && f.def?.defName == defName);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(0, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
