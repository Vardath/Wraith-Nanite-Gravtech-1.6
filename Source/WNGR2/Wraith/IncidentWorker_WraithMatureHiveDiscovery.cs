using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Discovers established mature Wraith Hives on the world map. This is deliberately independent
    /// of strategic faction hunger: feeding requests and hunger-weighted raids remain separate systems.
    /// Discovery is bounded to two active mature-Hive sites globally and one per Wraith lineage.
    /// Tile distances are fresh public test-balance values rather than recovered historical constants.
    /// </summary>
    public sealed class IncidentWorker_WraithMatureHiveDiscovery : IncidentWorker
    {
        private const int MaxActiveMatureHives = 2;
        private const int MinSiteDistance = 8;
        private const int MaxSiteDistance = 20;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;
            if (Find.WorldObjects == null || Find.FactionManager == null || Faction.OfPlayer == null)
                return false;

            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithMatureHive");
            if (siteDef == null)
                return false;
            if (ActiveMatureHives(siteDef).Count >= MaxActiveMatureHives)
                return false;

            return EligibleFactions(siteDef).Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithMatureHive");
            if (siteDef == null || Find.WorldObjects == null || Find.FactionManager == null)
                return false;

            List<Site> active = ActiveMatureHives(siteDef);
            if (active.Count >= MaxActiveMatureHives)
                return false;

            List<Faction> candidates = EligibleFactions(siteDef);
            if (candidates.Count == 0)
                return false;

            Faction faction = candidates[Rand.Range(0, candidates.Count)];
            if (!TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    MinSiteDistance,
                    MaxSiteDistance,
                    allowCaravans: false,
                    tileFinderMode: TileFinderMode.Near))
                return false;

            Site site = SiteMaker.MakeSite(
                siteDef,
                tile,
                faction,
                ifHostileThenMustRemainHostile: true,
                threatPoints: 0f);
            if (site?.parts == null || site.parts.Count == 0)
                return false;

            SitePart part = site.parts.FirstOrDefault(p => p != null && p.def == siteDef);
            if (part == null)
            {
                site.Destroy();
                return false;
            }

            site.customLabel = faction.Name + " mature hive";
            Find.WorldObjects.Add(site);

            string stance = faction.HostileTo(Faction.OfPlayer)
                ? "The lineage controlling it is already hostile."
                : "The lineage controlling it is not currently hostile, but the Hive is still defended sovereign territory.";
            Find.LetterStack.ReceiveLetter(
                "Mature Wraith Hive discovered",
                "Long-range reports have exposed an established Wraith Hive belonging to " + faction.Name + ". "
                + "Its population and sealed reserves are finite and materially supported rather than endlessly generated. "
                + stance,
                faction.HostileTo(Faction.OfPlayer) ? LetterDefOf.ThreatBig : LetterDefOf.NeutralEvent,
                site);
            return true;
        }

        private static List<Site> ActiveMatureHives(SitePartDef siteDef)
        {
            if (Find.WorldObjects?.AllWorldObjects == null || siteDef == null)
                return new List<Site>();

            return Find.WorldObjects.AllWorldObjects
                .OfType<Site>()
                .Where(site => site != null && !site.Destroyed && site.parts != null
                    && site.parts.Any(part => part != null && part.def == siteDef))
                .ToList();
        }

        private static List<Faction> EligibleFactions(SitePartDef siteDef)
        {
            List<Site> active = ActiveMatureHives(siteDef);
            HashSet<Faction> represented = new HashSet<Faction>(active
                .Select(site => site.Faction)
                .Where(faction => faction != null));

            return Find.FactionManager.AllFactionsListForReading
                .Where(faction => faction != null
                    && faction != Faction.OfPlayer
                    && !faction.defeated
                    && WraithCaptureUtility.IsWraithCaptor(faction)
                    && !represented.Contains(faction))
                .ToList();
        }
    }
}
