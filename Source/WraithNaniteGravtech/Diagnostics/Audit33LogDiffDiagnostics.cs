using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LudeonTK;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit33LogDiffDiagnostics
    {
        public const int MonitorTicks = 6000;
        public const string RuntimeBaselineStatus = "PROVISIONAL_AWAITING_CLEAN_REAL_STACK_PLAYER_LOG";

        // Deliberately empty until a real clean-stack Player.log has been reviewed and classified.
        // Audit 33 must not silently bless warnings/errors merely because the game reached a map.
        public static readonly HashSet<string> ApprovedRuntimeFingerprints =
            new HashSet<string>(StringComparer.Ordinal);

        [DebugAction(
            "WNG",
            "Audit 33 - log diff candidate",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                Messages.Message("WNG Audit 33 requires an active map.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            MapComponent_Audit33LogDiff monitor = map.GetComponent<MapComponent_Audit33LogDiff>();
            if (monitor == null)
            {
                Messages.Message("WNG Audit 33 monitor component is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            monitor.Begin();
        }

        public static string NormalizeFingerprint(LogType type, string condition)
        {
            string text = condition ?? string.Empty;
            int newline = text.IndexOfAny(new[] { '\r', '\n' });
            if (newline >= 0)
                text = text.Substring(0, newline);

            text = Regex.Replace(text, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>");
            text = Regex.Replace(text, @"0x[0-9a-fA-F]+", "<hex>");
            text = Regex.Replace(text, @"(?<![A-Za-z_])-?\d+(?:\.\d+)?", "#");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (text.Length > 500)
                text = text.Substring(0, 500);

            return type + "|" + text;
        }
    }

    public sealed class MapComponent_Audit33LogDiff : MapComponent
    {
        private bool active;
        private int startTick;
        private int endTick;
        private readonly HashSet<string> observed =
            new HashSet<string>(StringComparer.Ordinal);

        public MapComponent_Audit33LogDiff(Map map) : base(map)
        {
        }

        public void Begin()
        {
            if (active)
            {
                Application.logMessageReceived -= HandleLogMessage;
                active = false;
            }

            observed.Clear();
            startTick = Find.TickManager?.TicksGame ?? 0;
            endTick = startTick + Audit33LogDiffDiagnostics.MonitorTicks;
            active = true;
            Application.logMessageReceived += HandleLogMessage;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 33] LOG-DIFF CANDIDATE MONITOR ARMED");
            sb.AppendLine("Runtime baseline status: " + Audit33LogDiffDiagnostics.RuntimeBaselineStatus);
            sb.AppendLine("Start tick: " + startTick);
            sb.AppendLine("End tick: " + endTick);
            sb.AppendLine("Approved runtime fingerprints currently checked in: " +
                          Audit33LogDiffDiagnostics.ApprovedRuntimeFingerprints.Count);
            sb.AppendLine("All future Warning/Error/Assert/Exception messages during this 6000-tick window are normalized into stable fingerprints.");
            sb.AppendLine("Because the real-stack baseline is still provisional, completion produces a CANDIDATE rather than a release PASS. Startup warnings/errors before arming must be supplied from the complete Player.log.");
            Log.Message(sb.ToString());

            Messages.Message(
                "WNG Audit 33 armed for 6000 ticks. Exercise the normal stack, then save the full Player.log.",
                MessageTypeDefOf.NeutralEvent,
                false);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!active)
                return;

            int now = Find.TickManager?.TicksGame ?? startTick;
            if (map == null || map.Disposed)
            {
                Finish("map became unavailable before the capture completed");
                return;
            }

            if (now >= endTick)
                Finish(null);
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!active)
                return;

            if (type != LogType.Warning &&
                type != LogType.Error &&
                type != LogType.Assert &&
                type != LogType.Exception)
                return;

            string fingerprint = Audit33LogDiffDiagnostics.NormalizeFingerprint(type, condition);
            if (!fingerprint.NullOrEmpty())
                observed.Add(fingerprint);
        }

        private void Finish(string interruptedReason)
        {
            if (!active)
                return;

            Application.logMessageReceived -= HandleLogMessage;
            active = false;

            int now = Find.TickManager?.TicksGame ?? startTick;
            List<string> ordered = observed.OrderBy(x => x, StringComparer.Ordinal).ToList();
            List<string> approved = ordered
                .Where(Audit33LogDiffDiagnostics.ApprovedRuntimeFingerprints.Contains)
                .ToList();
            List<string> unclassified = ordered
                .Where(x => !Audit33LogDiffDiagnostics.ApprovedRuntimeFingerprints.Contains(x))
                .ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 33] LOG-DIFF CANDIDATE RESULT");
            sb.AppendLine("Runtime baseline status: " + Audit33LogDiffDiagnostics.RuntimeBaselineStatus);
            sb.AppendLine("Elapsed ticks: " + Math.Max(0, now - startTick));
            sb.AppendLine("Unique warning/error/assert/exception fingerprints: " + ordered.Count);
            sb.AppendLine("Already approved fingerprints: " + approved.Count);
            sb.AppendLine("NEW / UNCLASSIFIED FINGERPRINTS: " + unclassified.Count);

            if (!interruptedReason.NullOrEmpty())
                sb.AppendLine("INTERRUPTED: " + interruptedReason);

            if (unclassified.Count > 0)
            {
                sb.AppendLine("NEW / UNCLASSIFIED FINGERPRINTS:");
                foreach (string fingerprint in unclassified)
                    sb.AppendLine(" - " + fingerprint);
            }

            if (approved.Count > 0)
            {
                sb.AppendLine("APPROVED BASELINE FINGERPRINTS OBSERVED:");
                foreach (string fingerprint in approved)
                    sb.AppendLine(" - " + fingerprint);
            }

            bool fullWindow = interruptedReason.NullOrEmpty() &&
                              now - startTick >= Audit33LogDiffDiagnostics.MonitorTicks;

            if (!fullWindow)
            {
                sb.AppendLine("INCOMPLETE: repeat the 6000-tick capture.");
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 33 capture incomplete: see Player.log.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (Audit33LogDiffDiagnostics.RuntimeBaselineStatus.StartsWith("PROVISIONAL", StringComparison.Ordinal))
            {
                sb.AppendLine("BASELINE CANDIDATE: do not mark Audit 33 live-passed yet. Review the complete startup/runtime Player.log, classify every warning/error, then check the approved normalized fingerprints into the next build.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 33 candidate captured. Provide the full Player.log to establish the clean baseline.", MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            if (unclassified.Count > 0)
            {
                sb.AppendLine("FAIL: new warnings/errors differ from the approved runtime baseline.");
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 33 FAILED: new log signatures detected.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: runtime warning/error signature matches the approved baseline.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 33 runtime log signature PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
