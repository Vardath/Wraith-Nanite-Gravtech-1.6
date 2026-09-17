using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Bounded first-contact route for the autonomous block-Replicator ecosystem.
    /// This deliberately reuses the current Drone and physical loose-Block systems rather than
    /// reviving historical cargo proxies, ruin-husk Defs, hidden matter accounts or a second
    /// reproduction model.
    /// </summary>
    public static class ReplicatorFirstContactUtility
    {
        public const string SitePartDefName = "WNG_ReplicatorConsumedRuin";
        private const string SwarmFactionDefName = "WNG_ReplicatorSwarm";

        public static SitePartDef SitePartDef => DefDatabase<SitePartDef>.GetNamedSilentFail(SitePartDefName);

        public static Faction SwarmFaction
        {
            get
            {
                FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(SwarmFactionDefName);
                return def == null ? null : Find.FactionManager?.FirstFactionOfDef(def);
            }
        }

        public static bool HasLiveConsumedRuin()
        {
            SitePartDef part = SitePartDef;
            if (part == null || Find.WorldObjects?.AllWorldObjects == null)
                return false;

            return Find.WorldObjects.AllWorldObjects
                .OfType<Site>()
                .Any(site => site?.parts != null && site.parts.Any(p => p?.def == part));
        }
    }

    public sealed class IncidentWorker_ReplicatorConsumedRuinDiscovery : IncidentWorker
    {
        private const int MinSiteDistance = 8;
        private const int MaxSiteDistance = 24;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            return WNGSettingsUtility.ReplicatorStoryEventsEnabled
                && map != null
                && map.IsPlayerHome
                && ReplicatorFirstContactUtility.SitePartDef != null
                && ReplicatorFirstContactUtility.SwarmFaction != null
                && !ReplicatorFirstContactUtility.HasLiveConsumedRuin();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            SitePartDef part = ReplicatorFirstContactUtility.SitePartDef;
            if (!WNGSettingsUtility.ReplicatorStoryEventsEnabled || map == null || !map.IsPlayerHome || part == null || ReplicatorFirstContactUtility.SwarmFaction == null)
                return false;
            if (ReplicatorFirstContactUtility.HasLiveConsumedRuin())
                return false;

            PlanetTile tile;
            if (!TileFinder.TryFindNewSiteTile(out tile, MinSiteDistance, MaxSiteDistance, allowCaravans: false))
                return false;

            Site site = SiteMaker.MakeSite(
                part,
                tile,
                faction: null,
                ifHostileThenMustRemainHostile: false,
                threatPoints: 0f);
            if (site == null)
                return false;

            site.customLabel = "Replicator-consumed ruin";
            try
            {
                Find.WorldObjects.Add(site);
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Failed to add Replicator first-contact site: " + ex);
                return false;
            }

            Find.LetterStack.ReceiveLetter(
                "Replicator trace located",
                "A stripped ruin has been located nearby. Reconnaissance shows autonomous Replicator activity and loose self-organizing blocks among the remains. The blocks are physical salvage, but leaving enough of them exposed can reconstruct new Replicator Drones.",
                LetterDefOf.ThreatSmall,
                site);
            return true;
        }
    }

    public sealed class SitePartWorker_ReplicatorConsumedRuin : SitePartWorker
    {
        private const string DroneKindDefName = "WNG_ReplicatorDrone";
        private const string MatterDefName = "WNG_ReplicatorMatter";
        private const int StartingDroneCount = 2;
        private const int StartingBlockCount = 20;

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Site site = map?.Parent as Site;
            if (map == null || site == null)
                return;

            Faction swarm = ReplicatorFirstContactUtility.SwarmFaction;
            PawnKindDef droneKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(DroneKindDefName);
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail(MatterDefName);
            if (swarm == null || droneKind == null || matterDef == null)
            {
                Log.Warning("[WNG] Replicator consumed-ruin map generated without its required swarm/Drone/Block Defs.");
                return;
            }

            // Twenty real Blocks make the salvage itself a meaningful hazard: after the established
            // exposure delay, the same stack can pay for two physical Drone reconstructions unless
            // the player removes or contains it. No Core Fragment is granted by first contact.
            Thing matter = ThingMaker.MakeThing(matterDef);
            matter.stackCount = StartingBlockCount;
            if (!GenPlace.TryPlaceThing(matter, map.Center, map, ThingPlaceMode.Near) && !matter.Destroyed)
                matter.Destroy(DestroyMode.Vanish);

            string sharedDomain = "ruin:" + site.ID;
            for (int i = 0; i < StartingDroneCount; i++)
            {
                Pawn drone = null;
                try
                {
                    drone = PawnGenerator.GeneratePawn(droneKind, swarm);
                    drone.TryGetComp<CompReplicatorDomain>()?.AssignAutonomousDomain(sharedDomain);

                    IntVec3 preferred = map.Center + new IntVec3(i == 0 ? -4 : 4, 0, i == 0 ? -2 : 2);
                    if (!GenPlace.TryPlaceThing(drone, preferred, map, ThingPlaceMode.Near))
                    {
                        if (!drone.Destroyed)
                            drone.Destroy(DestroyMode.Vanish);
                    }
                }
                catch (Exception ex)
                {
                    if (drone != null && !drone.Destroyed)
                        drone.Destroy(DestroyMode.Vanish);
                    Log.Error("[WNG] Failed to place Replicator first-contact Drone: " + ex);
                }
            }
        }
    }
}
