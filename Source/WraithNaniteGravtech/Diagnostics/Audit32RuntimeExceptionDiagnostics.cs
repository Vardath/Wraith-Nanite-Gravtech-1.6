using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit32RuntimeExceptionDiagnostics
    {
        public const int MonitorTicks = 6000;

        [DebugAction(
            "WNG",
            "Audit 32 - runtime exception monitor",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("WNG Audit 32 requires an active map.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            MapComponent_Audit32RuntimeExceptionMonitor monitor =
                map.GetComponent<MapComponent_Audit32RuntimeExceptionMonitor>();
            if (monitor == null)
            {
                Messages.Message("WNG Audit 32 monitor component is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            monitor.Begin();
        }
    }

    public sealed class MapComponent_Audit32RuntimeExceptionMonitor : MapComponent
    {
        private bool active;
        private int startTick;
        private int endTick;
        private int wngPawnsAtStart;
        private int wngThingsAtStart;
        private readonly List<string> wngFailures = new List<string>();
        private readonly List<string> externalWarnings = new List<string>();

        public MapComponent_Audit32RuntimeExceptionMonitor(Map map) : base(map)
        {
        }

        public void Begin()
        {
            if (active)
            {
                Application.logMessageReceived -= HandleLogMessage;
                active = false;
            }

            wngFailures.Clear();
            externalWarnings.Clear();
            startTick = Find.TickManager?.TicksGame ?? 0;
            endTick = startTick + Audit32RuntimeExceptionDiagnostics.MonitorTicks;

            wngPawnsAtStart = map?.mapPawns?.AllPawnsSpawned?.Count(p =>
                p?.kindDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true) ?? 0;
            wngThingsAtStart = map?.listerThings?.AllThings?.Count(t =>
                t?.def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true) ?? 0;

            active = true;
            Application.logMessageReceived += HandleLogMessage;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 32] RUNTIME EXCEPTION MONITOR ARMED");
            sb.AppendLine("Start tick: " + startTick);
            sb.AppendLine("End tick: " + endTick);
            sb.AppendLine("Monitor duration: " + Audit32RuntimeExceptionDiagnostics.MonitorTicks + " game ticks");
            sb.AppendLine("Representative WNG pawns currently spawned: " + wngPawnsAtStart);
            sb.AppendLine("Representative WNG things currently spawned: " + wngThingsAtStart);
            sb.AppendLine("This monitor records FUTURE Unity/RimWorld log errors during the soak. Startup/config errors that occurred before arming must still be checked in Player.log.");
            sb.AppendLine("For a useful soak, spawn/use representative Wraith, Asuran/human-form, Replicator, gravship/shuttle, building/bill and optional-integration content before or during the run.");
            Log.Message(sb.ToString());
            Messages.Message("WNG Audit 32 armed for 6000 game ticks. Run the game and representative WNG content.", MessageTypeDefOf.NeutralEvent, false);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!active)
                return;

            int now = Find.TickManager?.TicksGame ?? startTick;
            if (map == null || map.Disposed)
            {
                Finish("map became unavailable before the soak completed");
                return;
            }

            if (now >= endTick)
                Finish(null);
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!active)
                return;

            string conditionText = condition ?? string.Empty;
            string stackText = stackTrace ?? string.Empty;
            string combined = conditionText + "\n" + stackText;

            bool severe = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            bool suspiciousKeyword =
                combined.IndexOf("NullReferenceException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("config error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("cross-reference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("could not resolve", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("HarmonyException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0 &&
                combined.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!severe && !suspiciousKeyword)
                return;

            bool wngOrigin =
                combined.IndexOf("WraithNaniteGravtech", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("[WNG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("WNG_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                combined.IndexOf("vardath.wraithnanitegravtech", StringComparison.OrdinalIgnoreCase) >= 0;

            string compact = Compact(conditionText, stackText, type);
            if (wngOrigin)
                AddUniqueCapped(wngFailures, compact);
            else
                AddUniqueCapped(externalWarnings, compact);
        }

        private static string Compact(string condition, string stackTrace, LogType type)
        {
            string firstStackLine = string.Empty;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int newline = stackTrace.IndexOf('\n');
                firstStackLine = newline >= 0 ? stackTrace.Substring(0, newline) : stackTrace;
            }

            string text = "[" + type + "] " + (condition ?? string.Empty).Replace("\n", " ").Replace("\r", " ");
            if (!string.IsNullOrEmpty(firstStackLine))
                text += " | " + firstStackLine.Trim();
            return text.Length <= 900 ? text : text.Substring(0, 900);
        }

        private static void AddUniqueCapped(List<string> list, string value)
        {
            if (list == null || value.NullOrEmpty() || list.Contains(value) || list.Count >= 40)
                return;
            list.Add(value);
        }

        private void Finish(string interruptedReason)
        {
            if (!active)
                return;

            Application.logMessageReceived -= HandleLogMessage;
            active = false;

            int now = Find.TickManager?.TicksGame ?? startTick;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 32] RUNTIME EXCEPTION MONITOR RESULT");
            sb.AppendLine("Start tick: " + startTick);
            sb.AppendLine("Finish tick: " + now);
            sb.AppendLine("Elapsed ticks: " + Math.Max(0, now - startTick));
            sb.AppendLine("WNG pawns at start: " + wngPawnsAtStart);
            sb.AppendLine("WNG things at start: " + wngThingsAtStart);
            sb.AppendLine("WNG-origin suspicious log entries: " + wngFailures.Count);
            sb.AppendLine("external/stack suspicious log entries: " + externalWarnings.Count);

            if (!interruptedReason.NullOrEmpty())
                sb.AppendLine("INTERRUPTED: " + interruptedReason);

            if (externalWarnings.Count > 0)
            {
                sb.AppendLine("EXTERNAL / STACK ENTRIES:");
                foreach (string warning in externalWarnings)
                    sb.AppendLine(" - " + warning);
            }

            if (wngFailures.Count > 0)
            {
                sb.AppendLine("WNG FAILURES:");
                foreach (string failure in wngFailures)
                    sb.AppendLine(" - " + failure);
            }

            bool passed = interruptedReason.NullOrEmpty() &&
                          now - startTick >= Audit32RuntimeExceptionDiagnostics.MonitorTicks &&
                          wngFailures.Count == 0;

            if (passed)
            {
                sb.AppendLine("PASS: 6000-tick monitor completed with no captured WNG-origin exception/config/cross-reference/Harmony failure.");
                Log.Message(sb.ToString());
                Messages.Message(
                    externalWarnings.Count == 0
                        ? "WNG Audit 32 6000-tick soak PASS. Save Player.log."
                        : "WNG Audit 32 WNG soak PASS with external/stack log entries. Save Player.log.",
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }
            else
            {
                sb.AppendLine("FAIL/INCOMPLETE: inspect Player.log and repeat after correcting any WNG-origin issue.");
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 32 FAILED or was interrupted: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}
