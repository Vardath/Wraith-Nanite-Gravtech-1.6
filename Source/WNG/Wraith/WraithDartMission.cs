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
    /// Mission-state layer for one real Wraith Dart. It does not replace CompShuttle,
    /// CompTransporter, native boarding jobs, or native launch. It records the two-pass culling
    /// sequence and the escape preference so CatCraft/native shuttle integrations can complete
    /// the physical route without the craft being despawned by WNG.
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

            completedPasses = 0;
            totalCapturedDuringPasses = 0;
            phase = WNGShuttleRaidPhase.AttackPasses;
            nextPhaseTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.passIntervalTicks);
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
                case WNGShuttleRaidPhase.AttackPasses:
                    ExecuteCullingPass();
                    completedPasses++;
                    if (completedPasses >= Math.Max(1, Props.attackPasses))
                    {
                        phase = WNGShuttleRaidPhase.LandedRaid;
                        nextPhaseTick = now + Math.Max(60, Props.landedRaidDelayTicks);
                    }
                    else
                    {
                        nextPhaseTick = now + Math.Max(60, Props.passIntervalTicks);
                    }
                    break;

                case WNGShuttleRaidPhase.RetreatRequested:
                    // Route selection is intentionally separate from route execution. CatCraft owns
                    // Stargate dialing/traversal; native RimWorld/Odyssey owns shuttle launch.
                    // Until one of those integrations confirms a viable route, the real craft stays.
                    phase = WNGShuttleRaidPhase.StargateEscapePending;
                    nextPhaseTick = int.MaxValue;
                    break;
            }
        }

        private void ExecuteCullingPass()
        {
            if (parent?.Spawned != true || parent.Map == null || Culling == null || Culling.CapacityRemaining <= 0)
                return;

            float radius = Math.Max(1f, Props.acquisitionRadius);
            float radiusSq = radius * radius;
            int limit = Math.Min(Math.Max(1, Props.maxCapturesPerPass), Culling.CapacityRemaining);

            List<Pawn> targets = parent.Map.mapPawns.AllPawnsSpawned
                .Where(IsValidPassTarget)
                .Where(p => p.Position.DistanceToSquared(parent.Position) <= radiusSq)
                .OrderBy(p => p.Position.DistanceToSquared(parent.Position))
                .ThenBy(p => p.thingIDNumber)
                .Take(limit * 3)
                .ToList();

            int captured = 0;
            foreach (Pawn target in targets)
            {
                if (captured >= limit || Culling.CapacityRemaining <= 0)
                    break;
                if (Culling.TryAbsorbExact(target))
                {
                    captured++;
                    totalCapturedDuringPasses++;
                }
            }
        }

        private bool IsValidPassTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn == null || pawn.Map != parent.Map)
                return false;
            if (!WraithCaptivityRegistry.IsValidBiologicalCaptive(pawn))
                return false;
            return parent.Faction == null || pawn.Faction != parent.Faction;
        }

        public void RequestRetreat()
        {
            if (phase == WNGShuttleRaidPhase.Escaped || parent?.Destroyed != false)
                return;
            phase = WNGShuttleRaidPhase.RetreatRequested;
            nextPhaseTick = Find.TickManager?.TicksGame ?? 0;
        }

        /// <summary>
        /// Called only by CatCraft-compatible integration when an actual accessible Stargate route
        /// has been established for this same Dart. WNG does not dial or despawn the craft here.
        /// </summary>
        public void NotifyStargateRouteAvailable()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;
            nextPhaseTick = int.MaxValue;
        }

        /// <summary>
        /// Called by the Stargate integration after the same craft has physically completed gate
        /// traversal. Exact buffered captives then become off-map Wraith captives.
        /// </summary>
        public void NotifyStargateEscapeCompleted()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;
            Culling?.CommitNativeEscapeWithCaptives();
            phase = WNGShuttleRaidPhase.Escaped;
            nextPhaseTick = int.MaxValue;
        }

        /// <summary>
        /// CatCraft calls this when no usable outbound gate can be established. The mission then
        /// permits the native shuttle route to be attempted; native code still performs boarding
        /// and launch.
        /// </summary>
        public void NotifyStargateUnavailableUseNativeFallback()
        {
            if (phase != WNGShuttleRaidPhase.StargateEscapePending)
                return;
            phase = WNGShuttleRaidPhase.NativeEscapePending;
            nextPhaseTick = int.MaxValue;
        }

        /// <summary>
        /// Called only after native RimWorld/Odyssey reports that this actual shuttle has completed
        /// its successful escape. It must not be called when launch was merely requested.
        /// </summary>
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
            // Deliberately no despawn, destruction or captive transfer. The real Dart stays on map.
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
