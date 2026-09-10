using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

        public CompProperties_WraithDartRaidMission()
        {
            compClass = typeof(CompWraithDartRaidMission);
        }
    }

    /// <summary>
    /// Mission-state layer for one real Wraith Dart. Physical attack passes carry this exact shuttle
    /// inside WNG skyfallers; CompShuttle, CompTransporter, native boarding/launch and CatCraft remain
    /// authoritative for their own responsibilities.
    /// </summary>
    public sealed class CompWraithDartRaidMission : ThingComp
    {
        private WNGShuttleRaidPhase phase = WNGShuttleRaidPhase.Idle;
        private int completedPasses;
        private int nextPhaseTick;
        private int totalCapturedDuringPasses;

        private CompProperties_WraithDartRaidMission Props => (CompProperties_WraithDartRaidMission)props;
        private CompWraithDartCulling Culling => parent?.TryGetComp<CompWraithDartCulling>();

        public WNGShuttleRaidPhase Phase => phase;
        public int CompletedPasses => completedPasses;
        public bool IsOperationalMission => phase != WNGShuttleRaidPhase.Escaped && parent != null && !parent.Destroyed;

        public void BeginTwoPassRaid()
        {
            if (parent?.Spawned != true || parent.Map == null || parent.Faction == null || !WraithCaptivityRegistry.IsWraithFaction(parent.Faction))
                return;

            Map map = parent.Map;
            IntVec3 firstPassCell = WNGShuttleFlightUtility.FindAttackPassCell(parent, map, parent.Position, oppositeSide: false);

            completedPasses = 0;
            totalCapturedDuringPasses = 0;
            phase = WNGShuttleRaidPhase.AttackPasses;
            nextPhaseTick = int.MaxValue;

            if (!WNGShuttleFlightUtility.TryBeginPhysicalPasses(parent, map, firstPassCell))
            {
                // Do not pretend a grounded craft completed a flyby. Leave it present and mark the
                // mission stranded so a later live fix/retreat can act on the same physical craft.
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
            nextPhaseTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.landedRaidDelayTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Destroyed != false || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextPhaseTick)
                return;

            if (phase == WNGShuttleRaidPhase.RetreatRequested)
            {
                phase = WNGShuttleRaidPhase.StargateEscapePending;
                nextPhaseTick = int.MaxValue;
            }
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
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return false;
            if (WNGStargateTransitPolicy.EvaluateLocalDeparture(direction) != WNGStargateTransitDecision.Allowed)
                return false;
            nextPhaseTick = int.MaxValue;
            return true;
        }

        public void NotifyStargateEscapeCompleted()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;
            Culling?.CommitNativeEscapeWithCaptives();
            phase = WNGShuttleRaidPhase.Escaped;
            nextPhaseTick = int.MaxValue;
        }

        public void NotifyStargateUnavailableUseNativeFallback()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;
            phase = WNGShuttleRaidPhase.NativeEscapePending;
            nextPhaseTick = int.MaxValue;
        }

        public void NotifyNativeEscapeCompleted()
        {
            if (phase != WNGShuttleRaidPhase.NativeEscapePending)
                return;
            Culling?.CommitNativeEscapeWithCaptives();
            phase = WNGShuttleRaidPhase.Escaped;
            nextPhaseTick = int.MaxValue;
        }

        public void NotifyEscapeFailedOrAbandoned()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending && phase != WNGShuttleRaidPhase.NativeEscapePending && phase != WNGShuttleRaidPhase.RetreatRequested)
                return;
            phase = WNGShuttleRaidPhase.Stranded;
            nextPhaseTick = int.MaxValue;
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
        }
    }
}
