using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithDartAbduction : CompProperties
    {
        public int maxCaptives = 3;
        public int scanIntervalTicks = 500;
        public float acquisitionRadius = 28f;
        public int stunTicks = 1200;
        public int retreatDelayTicks = 600;
        public int emptyScansBeforeRetreat = 3;

        public CompProperties_WraithDartAbduction()
        {
            compClass = typeof(CompWraithDartAbduction);
        }
    }

    /// <summary>
    /// Hostile Wraith Dart capture mission. The Dart scans a bounded area, targets only valid
    /// biological colony prey, stuns the selected pawn, stores that exact pawn in its transporter,
    /// then transfers the same pawn to the persistent world-pawn pool when the Dart withdraws.
    /// It never fabricates replacement captives.
    /// </summary>
    public sealed class CompWraithDartAbduction : ThingComp
    {
        private int capturedCount;
        private int nextScanTick;
        private int retreatTick = -1;
        private int consecutiveEmptyScans;

        private CompProperties_WraithDartAbduction Props => (CompProperties_WraithDartAbduction)props;
        private CompTransporter Transporter => parent?.GetComp<CompTransporter>();

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Find.TickManager != null)
                nextScanTick = Find.TickManager.TicksGame + Math.Max(1, Props.scanIntervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null || !parent.Spawned || parent.Map == null || Find.TickManager == null)
                return;

            Faction faction = parent.Faction;
            if (!WraithCaptureUtility.IsWraithCaptor(faction) || !faction.HostileTo(Faction.OfPlayer))
                return;

            if (Transporter == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (retreatTick >= 0)
            {
                if (now >= retreatTick)
                    Withdraw();
                return;
            }

            if (capturedCount >= Math.Max(1, Props.maxCaptives))
            {
                ScheduleRetreat(now);
                return;
            }

            if (now < nextScanTick)
                return;

            nextScanTick = now + Math.Max(1, Props.scanIntervalTicks);
            Pawn target = FindBestTarget();
            if (target == null)
            {
                consecutiveEmptyScans++;
                if (consecutiveEmptyScans >= Math.Max(1, Props.emptyScansBeforeRetreat))
                    ScheduleRetreat(now);
                return;
            }

            consecutiveEmptyScans = 0;
            TryAbsorb(target, now);
        }

        private Pawn FindBestTarget()
        {
            Map map = parent.Map;
            if (map?.mapPawns?.AllPawnsSpawned == null)
                return null;

            float radiusSquared = Props.acquisitionRadius * Props.acquisitionRadius;
            IEnumerable<Pawn> candidates = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null
                    && WraithCaptureUtility.IsValidAbductionTarget(p)
                    && p.Position.DistanceToSquared(parent.Position) <= radiusSquared);

            return candidates
                .OrderByDescending(p => p.Downed)
                .ThenBy(p => p.Position.DistanceToSquared(parent.Position))
                .FirstOrDefault();
        }

        private void TryAbsorb(Pawn target, int now)
        {
            CompTransporter transporter = Transporter;
            if (target == null || transporter == null || !WraithCaptureUtility.IsValidAbductionTarget(target))
                return;

            float targetMass = target.GetStatValue(StatDefOf.Mass);
            if (transporter.MassUsage + targetMass > transporter.MassCapacity)
            {
                ScheduleRetreat(now);
                return;
            }

            Map originalMap = target.Map;
            IntVec3 originalPosition = target.Position;
            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            if (registry == null || !WraithCaptureUtility.TryRegisterAbduction(target, parent.Faction))
                return;

            if (target.stances?.stunner != null)
                target.stances.stunner.StunFor(Math.Max(1, Props.stunTicks), parent);

            target.DeSpawn();
            if (!transporter.innerContainer.TryAdd(target))
            {
                registry.ReleaseExactPawn(target);
                if (originalMap != null)
                    GenSpawn.Spawn(target, originalPosition, originalMap);
                return;
            }

            capturedCount++;
            if (capturedCount >= Math.Max(1, Props.maxCaptives))
                ScheduleRetreat(now);
        }

        private void ScheduleRetreat(int now)
        {
            if (retreatTick >= 0)
                return;
            retreatTick = SafeFutureTick(now, Math.Max(1, Props.retreatDelayTicks));
        }

        private void Withdraw()
        {
            if (parent == null || parent.Destroyed)
                return;

            CompTransporter transporter = Transporter;
            if (transporter != null)
            {
                List<Pawn> captives = transporter.innerContainer.OfType<Pawn>().ToList();
                foreach (Pawn captive in captives)
                {
                    if (captive == null)
                        continue;
                    transporter.innerContainer.Remove(captive);
                    if (!captive.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(captive, PawnDiscardDecideMode.KeepForever);
                }
            }

            parent.Destroy(DestroyMode.Vanish);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(0, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref capturedCount, "wngDartCapturedCount", 0);
            Scribe_Values.Look(ref nextScanTick, "wngDartNextScanTick", 0);
            Scribe_Values.Look(ref retreatTick, "wngDartRetreatTick", -1);
            Scribe_Values.Look(ref consecutiveEmptyScans, "wngDartEmptyScans", 0);
        }
    }
}
