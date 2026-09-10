using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    public enum WNGShuttleRaidPhase
    {
        Idle,
        AttackPasses,
        LandedRaid,
        RetreatRequested,
        StargateEscapePending,
        NativeEscapePending,
        Stranded,
        Escaped
    }

    public sealed class CompProperties_WraithDartRaidMission : CompProperties
    {
        public int attackPasses = 2;
        public int passIntervalTicks = 900;
        public int maxCapturesPerPass = 2;
        public float acquisitionRadius = 38f;
        public int landedRaidDelayTicks = 1800;
        public int stargateDecisionTimeoutTicks = 1200;
        public int stargateTransitTimeoutTicks = 6000;

        public CompProperties_WraithDartRaidMission()
        {
            compClass = typeof(CompWraithDartRaidMission);
        }
    }

    /// <summary>
    /// Mission-state layer for one real Wraith Dart. Physical attack passes carry this exact shuttle
    /// inside WNG skyfallers. After landing, hostile retreat prefers the optional Stargate route;
    /// if that route is absent/inaccessible it falls back to RimWorld's native TransportShip /
    /// ShipJob_FlyAway path. Native player boarding/loading/launch remain untouched after capture.
    /// </summary>
    public sealed class CompWraithDartRaidMission : ThingComp
    {
        private WNGShuttleRaidPhase phase = WNGShuttleRaidPhase.Idle;
        private int completedPasses;
        private int nextPhaseTick;
        private int totalCapturedDuringPasses;
        private bool nativeLaunchIssued;

        private CompProperties_WraithDartRaidMission Props => (CompProperties_WraithDartRaidMission)props;
        private CompWraithDartCulling Culling => parent?.TryGetComp<CompWraithDartCulling>();
        private CompWraithDartPilot Pilot => parent?.TryGetComp<CompWraithDartPilot>();

        public WNGShuttleRaidPhase Phase => phase;
        public int CompletedPasses => completedPasses;
        public bool IsOperationalMission => phase != WNGShuttleRaidPhase.Escaped && parent != null && !parent.Destroyed;

        public void BeginTwoPassRaid()
        {
            if (parent?.Spawned != true || parent.Map == null || parent.Faction == null || !WraithCaptivityRegistry.IsWraithFaction(parent.Faction))
                return;

            // A Dart is a piloted craft in Stargate. Keep the exact Wraith pilot in the native
            // transporter instead of treating the shuttle as an autonomous mech.
            if (Pilot == null || !Pilot.EnsureHostilePilot(parent.Faction))
            {
                phase = WNGShuttleRaidPhase.Stranded;
                nextPhaseTick = int.MaxValue;
                return;
            }

            Map map = parent.Map;
            IntVec3 firstPassCell = WNGShuttleFlightUtility.FindAttackPassCell(parent, map, parent.Position, oppositeSide: false);

            completedPasses = 0;
            totalCapturedDuringPasses = 0;
            nativeLaunchIssued = false;
            phase = WNGShuttleRaidPhase.AttackPasses;
            nextPhaseTick = int.MaxValue;

            if (!WNGShuttleFlightUtility.TryBeginPhysicalPasses(parent, map, firstPassCell))
            {
                // Do not pretend a grounded craft completed a flyby. Leave the same craft present.
                phase = WNGShuttleRaidPhase.Stranded;
                nextPhaseTick = int.MaxValue;
            }
        }

        /// <summary>
        /// Invoked by the skyfaller carrying this exact Dart when a physical pass reaches its attack
        /// line. Returns true when another physical pass is still required.
        /// </summary>
        public bool ExecutePhysicalPass(Map map, IntVec3 passCell)
        {
            if (phase != WNGShuttleRaidPhase.AttackPasses || map == null || parent == null || parent.Destroyed)
                return false;

            ExecuteCullingPass(map, passCell);
            completedPasses++;
            return completedPasses < Math.Max(1, Props.attackPasses);
        }

        public void NotifyPhysicallyLanded()
        {
            if (phase != WNGShuttleRaidPhase.AttackPasses || parent?.Spawned != true)
                return;

            phase = WNGShuttleRaidPhase.LandedRaid;
            nextPhaseTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, Math.Max(60, Props.landedRaidDelayTicks));
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Destroyed != false || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextPhaseTick)
                return;

            switch (phase)
            {
                case WNGShuttleRaidPhase.LandedRaid:
                    RequestRetreat();
                    break;

                case WNGShuttleRaidPhase.RetreatRequested:
                    BeginPreferredEscape(now);
                    break;

                case WNGShuttleRaidPhase.StargateEscapePending:
                    // The optional CatCraft bridge gets a bounded window to find/redial/use a valid
                    // outbound gate. An active inbound gate can never be reused in reverse.
                    NotifyStargateUnavailableUseNativeFallback();
                    break;
            }
        }

        private void BeginPreferredEscape(int now)
        {
            if (Pilot?.HasOperationalHostilePilot != true || parent?.Faction == null || !WraithCaptivityRegistry.IsWraithFaction(parent.Faction))
            {
                NotifyEscapeFailedOrAbandoned();
                return;
            }

            if (WNGOptionalIntegrations.StargatesActive)
            {
                phase = WNGShuttleRaidPhase.StargateEscapePending;
                nextPhaseTick = SafeFutureTick(now, Math.Max(60, Props.stargateDecisionTimeoutTicks));
                return;
            }

            phase = WNGShuttleRaidPhase.NativeEscapePending;
            nextPhaseTick = int.MaxValue;
            TryBeginNativeFallbackEscape();
        }

        private void ExecuteCullingPass(Map map, IntVec3 passCell)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null || Culling == null || Culling.CapacityRemaining <= 0)
                return;

            float radius = Math.Max(1f, Props.acquisitionRadius);
            float radiusSq = radius * radius;
            int limit = Math.Min(Math.Max(1, Props.maxCapturesPerPass), Culling.CapacityRemaining);

            List<Pawn> targets = map.mapPawns.AllPawnsSpawned
                .Where(p => IsValidPassTarget(p, map))
                .Where(p => p.Position.DistanceToSquared(passCell) <= radiusSq)
                .OrderBy(p => p.Position.DistanceToSquared(passCell))
                .ThenBy(p => p.thingIDNumber)
                .Take(limit * 3)
                .ToList();

            int captured = 0;
            foreach (Pawn target in targets)
            {
                if (captured >= limit || Culling.CapacityRemaining <= 0)
                    break;
                if (Culling.TryAbsorbExact(target, map))
                {
                    captured++;
                    totalCapturedDuringPasses++;
                }
            }
        }

        private bool IsValidPassTarget(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map)
                return false;
            if (!WraithCaptivityRegistry.IsValidBiologicalCaptive(pawn))
                return false;
            return parent?.Faction == null || pawn.Faction != parent.Faction;
        }

        public void RequestRetreat()
        {
            if (phase == WNGShuttleRaidPhase.Escaped || parent?.Destroyed != false)
                return;

            phase = WNGShuttleRaidPhase.RetreatRequested;
            nextPhaseTick = Find.TickManager?.TicksGame ?? 0;
        }

        public WNGStargateTransitDecision EvaluateStargateDeparture(WNGStargateConnectionDirection direction)
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return WNGStargateTransitDecision.Unusable;
            return WNGStargateTransitPolicy.EvaluateLocalDeparture(direction);
        }

        public bool NotifyStargateRouteAvailable(WNGStargateConnectionDirection direction)
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending || Pilot?.HasOperationalHostilePilot != true)
                return false;
            if (WNGStargateTransitPolicy.EvaluateLocalDeparture(direction) != WNGStargateTransitDecision.Allowed)
                return false;

            nextPhaseTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, Math.Max(300, Props.stargateTransitTimeoutTicks));
            return true;
        }

        public void NotifyStargateEscapeCompleted()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;

            Culling?.CommitStargateEscapeWithCaptives();
            phase = WNGShuttleRaidPhase.Escaped;
            nextPhaseTick = int.MaxValue;
            nativeLaunchIssued = false;
        }

        public void NotifyStargateUnavailableUseNativeFallback()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;

            phase = WNGShuttleRaidPhase.NativeEscapePending;
            nextPhaseTick = int.MaxValue;
            TryBeginNativeFallbackEscape();
        }

        /// <summary>
        /// Hostile NPC escape uses RimWorld's TransportShip / ShipJob_FlyAway machinery rather than
        /// the player CompLaunchable command path (whose pilot validation intentionally requires a
        /// free colonist). A valid adjacent world tile keeps the exact shuttle inside the native
        /// ActiveTransporter so the leaving-skyfaller callback can resolve exact captives correctly.
        /// </summary>
        private bool TryBeginNativeFallbackEscape()
        {
            if (nativeLaunchIssued)
                return true;
            if (phase != WNGShuttleRaidPhase.NativeEscapePending || parent?.Spawned != true || parent.Map == null)
                return false;
            if (Pilot?.HasOperationalHostilePilot != true)
            {
                NotifyEscapeFailedOrAbandoned();
                return false;
            }

            CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
            TransportShip transportShip = shuttle?.shipParent;
            if (transportShip == null || transportShip.Disposed)
            {
                NotifyEscapeFailedOrAbandoned();
                return false;
            }

            CompLaunchable launchable = parent.TryGetComp<CompLaunchable>();
            CompRefuelable refuelable = parent.TryGetComp<CompRefuelable>();
            float minimumFuel = Math.Max(0f, launchable?.Props?.minFuelCost ?? 0f);
            if (refuelable != null && refuelable.Fuel < minimumFuel)
            {
                NotifyEscapeFailedOrAbandoned();
                return false;
            }

            PlanetTile destination = FindNativeEscapeDestination(parent.Tile);
            if (!destination.Valid)
            {
                NotifyEscapeFailedOrAbandoned();
                return false;
            }

            ShipJob_FlyAway flyAway = (ShipJob_FlyAway)ShipJobMaker.MakeShipJob(ShipJobDefOf.FlyAway);
            flyAway.destinationTile = destination;
            flyAway.arrivalAction = new WNGHostileShuttleEscapeArrivalAction();
            flyAway.dropMode = TransportShipDropMode.None;

            nativeLaunchIssued = true;
            transportShip.ForceJob(flyAway);

            // ShipJob_FlyAway is immediate. If the same craft is still spawned, native launch did
            // not occur; leave it on-map as salvage/raid material rather than silently deleting it.
            if (parent.Spawned)
            {
                nativeLaunchIssued = false;
                NotifyEscapeFailedOrAbandoned();
                return false;
            }

            if (refuelable != null && minimumFuel > 0f)
                refuelable.ConsumeFuel(minimumFuel);
            return true;
        }

        private static PlanetTile FindNativeEscapeDestination(PlanetTile origin)
        {
            if (!origin.Valid || Find.WorldGrid == null)
                return PlanetTile.Invalid;

            List<PlanetTile> neighbors = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(origin, neighbors);
            if (neighbors.Count > 0)
                return neighbors.RandomElement();

            return origin;
        }

        public void NotifyNativeEscapeCompleted(ThingOwner transitContainer)
        {
            if (phase != WNGShuttleRaidPhase.NativeEscapePending)
                return;

            Culling?.CommitNativeEscapeWithCaptives(transitContainer);
            phase = WNGShuttleRaidPhase.Escaped;
            nextPhaseTick = int.MaxValue;
            nativeLaunchIssued = false;
        }

        public void NotifyEscapeFailedOrAbandoned()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending &&
                phase != WNGShuttleRaidPhase.NativeEscapePending &&
                phase != WNGShuttleRaidPhase.RetreatRequested &&
                phase != WNGShuttleRaidPhase.LandedRaid)
                return;

            phase = WNGShuttleRaidPhase.Stranded;
            nextPhaseTick = int.MaxValue;
            nativeLaunchIssued = false;
        }

        public override void Notify_Hacked(Pawn hacker)
        {
            // Hacking ends the hostile mission. The culling and pilot comps separately release
            // exact captives/eject the Wraith pilot and transfer ownership to the player.
            phase = WNGShuttleRaidPhase.Idle;
            nextPhaseTick = int.MaxValue;
            nativeLaunchIssued = false;
            base.Notify_Hacked(hacker);
        }

        public override string CompInspectStringExtra()
        {
            if (phase == WNGShuttleRaidPhase.Idle) return null;
            string passes = completedPasses + "/" + Math.Max(1, Props.attackPasses);
            return "Wraith Dart mission: " + phase + "\nCulling passes: " + passes + "\nPass captures: " + totalCapturedDuringPasses;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref phase, "wngDartRaidPhase", WNGShuttleRaidPhase.Idle);
            Scribe_Values.Look(ref completedPasses, "wngDartCompletedPasses", 0);
            Scribe_Values.Look(ref nextPhaseTick, "wngDartNextPhaseTick", 0);
            Scribe_Values.Look(ref totalCapturedDuringPasses, "wngDartTotalPassCaptures", 0);
            Scribe_Values.Look(ref nativeLaunchIssued, "wngDartNativeLaunchIssued", false);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
