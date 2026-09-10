using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
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
    /// A successful native RimWorld hack opens the capture buffer and returns those exact pawns
    /// to the map before escape. Destroying/interruption also releases any still-buffered captivity
    /// records so a pawn dropped by the transporter is never left falsely marked as off-map captive.
    /// It never fabricates replacement captives.
    /// </summary>
    public sealed class CompWraithDartAbduction : ThingComp
    {
        private int capturedCount;
        private int nextScanTick;
        private int retreatTick = -1;
        private int consecutiveEmptyScans;
        private List<Pawn> bufferedCaptives = new List<Pawn>();
        private bool intentionalWithdrawal;

        private CompProperties_WraithDartAbduction Props => (CompProperties_WraithDartAbduction)props;
        private CompTransporter Transporter => parent?.GetComp<CompTransporter>();
        private CompHackable Hackable => parent?.GetComp<CompHackable>();

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            bufferedCaptives ??= new List<Pawn>();
            if (!respawningAfterLoad && Find.TickManager != null)
                nextScanTick = Find.TickManager.TicksGame + Math.Max(1, Props.scanIntervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null || !parent.Spawned || parent.Map == null || Find.TickManager == null)
                return;

            if (Hackable?.IsHacked == true)
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

            if (!bufferedCaptives.Contains(target))
                bufferedCaptives.Add(target);
            capturedCount = bufferedCaptives.Count;
            if (capturedCount >= Math.Max(1, Props.maxCaptives))
                ScheduleRetreat(now);
        }

        public override void Notify_Hacked(Pawn hacker)
        {
            base.Notify_Hacked(hacker);
            retreatTick = -1;
            consecutiveEmptyScans = 0;
            RecoverBufferedCaptives();
        }

        private void RecoverBufferedCaptives()
        {
            CompTransporter transporter = Transporter;
            Map map = parent?.Map;
            if (transporter == null || map == null || parent == null || !parent.Spawned)
                return;

            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            List<Pawn> captives = bufferedCaptives.Where(p => p != null).ToList();
            foreach (Pawn captive in captives)
            {
                if (transporter.innerContainer.Contains(captive))
                    transporter.innerContainer.Remove(captive);
                registry?.ReleaseExactPawn(captive);

                if (captive.IsWorldPawn())
                    Find.WorldPawns.RemovePawn(captive);

                if (!captive.Spawned)
                {
                    IntVec3 releaseCell = CellFinder.RandomClosewalkCellNear(parent.Position, map, 4);
                    GenSpawn.Spawn(captive, releaseCell, map);
                }
            }

            bufferedCaptives.Clear();
            capturedCount = 0;
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            if (intentionalWithdrawal || bufferedCaptives == null || bufferedCaptives.Count == 0)
                return;

            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            foreach (Pawn captive in bufferedCaptives.Where(p => p != null).ToList())
                registry?.ReleaseExactPawn(captive);

            bufferedCaptives.Clear();
            capturedCount = 0;
            retreatTick = -1;
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
            if (transporter == null)
                return;

            List<Pawn> captives = bufferedCaptives.Where(p => p != null).ToList();
            foreach (Pawn captive in captives)
            {
                if (!transporter.innerContainer.Contains(captive))
                    continue;

                transporter.innerContainer.Remove(captive);
                if (!captive.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(captive, PawnDiscardDecideMode.KeepForever);
            }

            bufferedCaptives.Clear();
            capturedCount = 0;
            intentionalWithdrawal = true;
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
            Scribe_Collections.Look(ref bufferedCaptives, "wngDartBufferedCaptives", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                bufferedCaptives = bufferedCaptives?.Where(p => p != null).Distinct().ToList() ?? new List<Pawn>();
                capturedCount = bufferedCaptives.Count;
                intentionalWithdrawal = false;
            }
        }
    }
}
