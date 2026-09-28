using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit34PerformanceDiagnostics
    {
        public const int SampleTicks = 600;
        public const string BenchmarkPawnKindDefName = "WNG_ReplicatorDrone";
        private static readonly int[] RequiredCounts = { 1, 10, 50, 100 };

        public sealed class SampleResult
        {
            public int Count;
            public double MeanMs;
            public double MedianMs;
            public double P95Ms;
            public int Intervals;
        }

        private static readonly Dictionary<int, SampleResult> Results =
            new Dictionary<int, SampleResult>();

        public static bool IsRequiredCount(int count)
        {
            return RequiredCounts.Contains(count);
        }

        public static void Store(SampleResult result)
        {
            if (result != null)
                Results[result.Count] = result;
        }

        public static string BuildTable()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("count | mean ms/tick | median | p95 | intervals");
            foreach (int count in RequiredCounts)
            {
                if (Results.TryGetValue(count, out SampleResult r))
                    sb.AppendLine(count + " | " + r.MeanMs.ToString("0.000") + " | " +
                                  r.MedianMs.ToString("0.000") + " | " +
                                  r.P95Ms.ToString("0.000") + " | " + r.Intervals);
                else
                    sb.AppendLine(count + " | PENDING | PENDING | PENDING | 0");
            }
            return sb.ToString().TrimEnd();
        }

        public static string ScalingAssessment()
        {
            if (!RequiredCounts.All(Results.ContainsKey))
                return "INCOMPLETE: samples for 1, 10, 50 and 100 drones are required.";

            SampleResult r1 = Results[1];
            SampleResult r10 = Results[10];
            SampleResult r50 = Results[50];
            SampleResult r100 = Results[100];

            double s1 = Math.Max(0d, r10.MeanMs - r1.MeanMs) / 9d;
            double s2 = Math.Max(0d, r50.MeanMs - r10.MeanMs) / 40d;
            double s3 = Math.Max(0d, r100.MeanMs - r50.MeanMs) / 50d;
            double earlier = Math.Max(0.001d, Math.Max(s1, s2));

            if (s3 > earlier * 3d && s3 > 0.02d)
                return "FLAG: 50->100 marginal tick cost is more than 3x the earlier per-entity slope; inspect for superlinear work/full-map scans.";

            return "NO SUPERLINEAR FLAG: whole-stack timing did not trip the conservative 50->100 scaling threshold. Review absolute timings and Player.log before live PASS.";
        }

        [DebugAction(
            "WNG",
            "Audit 34 - performance sample",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("WNG Audit 34 requires an active map.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            MapComponent_Audit34PerformanceSampler sampler =
                map.GetComponent<MapComponent_Audit34PerformanceSampler>();
            if (sampler == null)
            {
                Messages.Message("WNG Audit 34 sampler component is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            sampler.Begin();
        }

        [DebugAction(
            "WNG",
            "Audit 34 - clear performance samples",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Clear()
        {
            Results.Clear();
            Messages.Message("WNG Audit 34 performance samples cleared.", MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public sealed class MapComponent_Audit34PerformanceSampler : MapComponent
    {
        private bool active;
        private int targetCount;
        private int startTick;
        private int endTick;
        private long lastTimestamp;
        private readonly List<double> intervals = new List<double>();

        public MapComponent_Audit34PerformanceSampler(Map map) : base(map)
        {
        }

        private int CountBenchmarkDrones()
        {
            if (map?.mapPawns?.AllPawnsSpawned == null)
                return 0;

            return map.mapPawns.AllPawnsSpawned.Count(p =>
                p?.kindDef?.defName == Audit34PerformanceDiagnostics.BenchmarkPawnKindDefName);
        }

        public void Begin()
        {
            if (active)
            {
                active = false;
                intervals.Clear();
            }

            int count = CountBenchmarkDrones();
            if (!Audit34PerformanceDiagnostics.IsRequiredCount(count))
            {
                Messages.Message(
                    "WNG Audit 34 needs exactly 1, 10, 50 or 100 spawned WNG_ReplicatorDrone pawns. Current count: " + count + ".",
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            targetCount = count;
            startTick = Find.TickManager?.TicksGame ?? 0;
            endTick = startTick + Audit34PerformanceDiagnostics.SampleTicks;
            intervals.Clear();
            lastTimestamp = Stopwatch.GetTimestamp();
            active = true;

            Log.Message(
                "[WNG AUDIT 34] PERFORMANCE SAMPLE ARMED\n" +
                "Benchmark family: " + Audit34PerformanceDiagnostics.BenchmarkPawnKindDefName + "\n" +
                "Entity count: " + targetCount + "\n" +
                "Sample window: " + Audit34PerformanceDiagnostics.SampleTicks + " game ticks\n" +
                "Game speed at start: " + (Find.TickManager?.CurTimeSpeed.ToString() ?? "<unknown>") + "\n" +
                "This measures whole-stack wall-clock interval between map ticks while holding the benchmark-drone count constant.");
            Messages.Message(
                "WNG Audit 34 sampling " + targetCount + " drones for 600 ticks. Do not add/remove benchmark drones during the sample.",
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!active)
                return;

            long nowStamp = Stopwatch.GetTimestamp();
            if (lastTimestamp != 0L)
            {
                double ms = (nowStamp - lastTimestamp) * 1000d / Stopwatch.Frequency;
                if (ms >= 0d && ms < 5000d)
                    intervals.Add(ms);
            }
            lastTimestamp = nowStamp;

            int nowTick = Find.TickManager?.TicksGame ?? startTick;
            if ((nowTick - startTick) % 60 == 0 && CountBenchmarkDrones() != targetCount)
            {
                Finish("benchmark-drone count changed during sample");
                return;
            }

            if (nowTick >= endTick)
                Finish(null);
        }

        private void Finish(string interruptedReason)
        {
            if (!active)
                return;
            active = false;

            if (!interruptedReason.NullOrEmpty())
            {
                Log.Error("[WNG AUDIT 34] PERFORMANCE SAMPLE INCOMPLETE: " + interruptedReason);
                Messages.Message("WNG Audit 34 sample invalid: " + interruptedReason + ".", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (intervals.Count < Audit34PerformanceDiagnostics.SampleTicks / 2)
            {
                Log.Error("[WNG AUDIT 34] PERFORMANCE SAMPLE INCOMPLETE: too few measured tick intervals (" + intervals.Count + ").");
                Messages.Message("WNG Audit 34 sample incomplete: too few intervals.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            List<double> ordered = intervals.OrderBy(x => x).ToList();
            double mean = ordered.Average();
            double median = Percentile(ordered, 0.50d);
            double p95 = Percentile(ordered, 0.95d);

            Audit34PerformanceDiagnostics.SampleResult result =
                new Audit34PerformanceDiagnostics.SampleResult
                {
                    Count = targetCount,
                    MeanMs = mean,
                    MedianMs = median,
                    P95Ms = p95,
                    Intervals = ordered.Count
                };
            Audit34PerformanceDiagnostics.Store(result);

            string report =
                "[WNG AUDIT 34] PERFORMANCE SAMPLE RESULT\n" +
                "Entity count: " + targetCount + "\n" +
                "Intervals: " + ordered.Count + "\n" +
                "Mean ms/tick: " + mean.ToString("0.000") + "\n" +
                "Median ms/tick: " + median.ToString("0.000") + "\n" +
                "P95 ms/tick: " + p95.ToString("0.000") + "\n\n" +
                Audit34PerformanceDiagnostics.BuildTable() + "\n\n" +
                Audit34PerformanceDiagnostics.ScalingAssessment();

            Log.Message(report);
            Messages.Message(
                "WNG Audit 34 sample " + targetCount + " complete. See Player.log for the table.",
                MessageTypeDefOf.PositiveEvent,
                false);
        }

        private static double Percentile(List<double> ordered, double p)
        {
            if (ordered == null || ordered.Count == 0)
                return 0d;
            int index = (int)Math.Ceiling(p * ordered.Count) - 1;
            index = Math.Max(0, Math.Min(index, ordered.Count - 1));
            return ordered[index];
        }
    }
}
