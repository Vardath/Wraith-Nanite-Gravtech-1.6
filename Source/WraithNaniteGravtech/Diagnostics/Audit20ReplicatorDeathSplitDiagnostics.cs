using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit20ReplicatorDeathSplitDiagnostics
    {
        private sealed class Expected
        {
            public string Parent;
            public string Child;
            public int Count;
            public Expected(string parent, string child, int count)
            {
                Parent = parent;
                Child = child;
                Count = count;
            }
        }

        private static readonly Expected[] Contracts =
        {
            new Expected("WNG_ReplicatorHunter", "WNG_ReplicatorDrone", 2),
            new Expected("WNG_ReplicatorBulwark", "WNG_ReplicatorHunter", 2),
            new Expected("WNG_ReplicatorTitan", "WNG_ReplicatorBulwark", 2),
            new Expected("WNG_ReplicatorSiegeMass", "WNG_ReplicatorTitan", 2),
            new Expected("WNG_ReplicatorRepairer", "WNG_ReplicatorDrone", 2),
            new Expected("WNG_ReplicatorBurrower", "WNG_ReplicatorDrone", 2),
            new Expected("WNG_ReplicatorArtillery", "WNG_ReplicatorHunter", 2)
        };

        [DebugAction(
            "WNG",
            "Audit 20 - Replicator death / split",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 20] REPLICATOR DEATH / SPLIT");

            foreach (Expected expected in Contracts)
            {
                ThingDef parent = DefDatabase<ThingDef>.GetNamedSilentFail(expected.Parent);
                PawnKindDef childKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(expected.Child);
                if (parent == null)
                {
                    failures.Add("Missing parent race " + expected.Parent);
                    continue;
                }
                if (childKind == null)
                    failures.Add("Missing child PawnKind " + expected.Child);

                CompProperties_ReplicatorHierarchy props =
                    parent.GetCompProperties<CompProperties_ReplicatorHierarchy>();
                if (props == null)
                {
                    failures.Add(expected.Parent + " has no hierarchy comp.");
                    continue;
                }

                if (!string.Equals(props.splitChildPawnKind, expected.Child, StringComparison.Ordinal))
                    failures.Add(expected.Parent + " split child=" +
                                 (props.splitChildPawnKind ?? "<none>") +
                                 ", expected " + expected.Child);
                if (props.splitCount != expected.Count)
                    failures.Add(expected.Parent + " splitCount=" + props.splitCount +
                                 ", expected " + expected.Count);
                if (props.splitRecombineDelayTicks <= 0)
                    failures.Add(expected.Parent + " has no split-born recombination delay.");

                sb.AppendLine(" - " + expected.Parent + " death -> " +
                              expected.Count + " x " + expected.Child +
                              " (recombine lock " + props.splitRecombineDelayTicks + " ticks)");
            }

            ThingDef drone = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            CompProperties_ReplicatorHierarchy droneProps =
                drone?.GetCompProperties<CompProperties_ReplicatorHierarchy>();
            if (drone == null)
                failures.Add("Missing WNG_ReplicatorDrone race.");
            else if (droneProps != null && !string.IsNullOrEmpty(droneProps.splitChildPawnKind))
                failures.Add("Drone unexpectedly has a death-split child.");

            ThingDef controller = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorController");
            CompProperties_ReplicatorHierarchy controllerProps =
                controller?.GetCompProperties<CompProperties_ReplicatorHierarchy>();
            if (controller == null)
                failures.Add("Missing WNG_ReplicatorController race.");
            else if (controllerProps != null && !string.IsNullOrEmpty(controllerProps.splitChildPawnKind))
                failures.Add("Controller unexpectedly has a death-split child.");

            sb.AppendLine("This action is read-only and does NOT kill pawns.");
            sb.AppendLine("Manual throwaway-save test remains required for melee, bullet, explosion, fire and dev-kill paths.");
            sb.AppendLine("Non-death despawn/map-transfer must not emit split children; recombination uses DestroyMode.Vanish intentionally.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 20 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded death/split contracts resolve correctly.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 20 automated checks PASS; destructive live test still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
