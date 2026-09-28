using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit19ReplicatorStateMachineDiagnostics
    {
        private sealed class ExpectedState
        {
            public string Race;
            public string Upgrade;
            public string Split;
            public ExpectedState(string race, string upgrade, string split)
            {
                Race = race;
                Upgrade = upgrade;
                Split = split;
            }
        }

        private static readonly ExpectedState[] States =
        {
            new ExpectedState("WNG_ReplicatorDrone", "WNG_ReplicatorHunter", null),
            new ExpectedState("WNG_ReplicatorHunter", "WNG_ReplicatorBulwark", "WNG_ReplicatorDrone"),
            new ExpectedState("WNG_ReplicatorBulwark", "WNG_ReplicatorTitan", "WNG_ReplicatorHunter"),
            new ExpectedState("WNG_ReplicatorTitan", "WNG_ReplicatorSiegeMass", "WNG_ReplicatorBulwark"),
            new ExpectedState("WNG_ReplicatorSiegeMass", null, "WNG_ReplicatorTitan")
        };

        [DebugAction(
            "WNG",
            "Audit 19 - Replicator state machine",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 19] REPLICATOR STATE MACHINE");

            foreach (ExpectedState expected in States)
            {
                ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail(expected.Race);
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(expected.Race);
                if (race == null)
                {
                    failures.Add("Missing race ThingDef " + expected.Race);
                    continue;
                }
                if (kind == null)
                    failures.Add("Missing PawnKindDef " + expected.Race);
                else if (kind.race != race)
                    failures.Add(expected.Race + " PawnKind points to a different race.");

                CompProperties_ReplicatorHierarchy props =
                    race.GetCompProperties<CompProperties_ReplicatorHierarchy>();
                if (props == null)
                {
                    failures.Add(expected.Race + " has no hierarchy comp.");
                    continue;
                }

                if (!string.Equals(props.upgradePawnKind, expected.Upgrade, StringComparison.Ordinal))
                    failures.Add(expected.Race + " upgrade=" + (props.upgradePawnKind ?? "<none>") +
                                 ", expected " + (expected.Upgrade ?? "<none>"));
                if (!string.Equals(props.splitChildPawnKind, expected.Split, StringComparison.Ordinal))
                    failures.Add(expected.Race + " split=" + (props.splitChildPawnKind ?? "<none>") +
                                 ", expected " + (expected.Split ?? "<none>"));

                if (expected.Upgrade != null && props.unitsRequired != 2)
                    failures.Add(expected.Race + " requires " + props.unitsRequired + " units instead of 2.");
                if (expected.Split != null && props.splitCount != 2)
                    failures.Add(expected.Race + " splits into " + props.splitCount + " instead of 2.");

                sb.AppendLine(" - " + expected.Race +
                              " -> " + (expected.Upgrade ?? "[top]") +
                              "; death split -> " + (expected.Split ?? "[none]"));
            }

            FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            if (swarmDef == null)
                failures.Add("Missing WNG_ReplicatorSwarm FactionDef.");

            Map map = Find.CurrentMap;
            MapComponent_ReplicatorConsumption consumption =
                map?.GetComponent<MapComponent_ReplicatorConsumption>();
            if (consumption == null)
            {
                failures.Add("MapComponent_ReplicatorConsumption unavailable.");
            }
            else
            {
                sb.AppendLine("Initial consumable cells: " + consumption.InitialConsumableCellCount);
                sb.AppendLine("Stripped cells: " + consumption.StrippedCellCount);
                sb.AppendLine("Stripped fraction: " + consumption.StrippedFraction.ToString("P2"));
                sb.AppendLine("Biological predation unlocked now: " + consumption.BiologicalPredationUnlocked);
                if (MapComponent_ReplicatorConsumption.BiologicalPredationThreshold != 0.95f)
                    failures.Add("Compiled biological threshold is not 0.95.");
            }

            int hostileBlocks = 0;
            int contained = 0;
            int emp = 0;
            if (map?.mapPawns?.AllPawnsSpawned != null)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!ReplicatorAssimilationUtility.IsBlockReplicator(pawn))
                        continue;

                    if (pawn.Faction != null &&
                        Faction.OfPlayer != null &&
                        pawn.Faction.HostileTo(Faction.OfPlayer))
                        hostileBlocks++;
                    if (ReplicatorContainmentUtility.IsContained(map, pawn.Position))
                        contained++;
                    if (ReplicatorInterferenceUtility.IsEmpDisrupted(pawn))
                        emp++;
                }
            }

            sb.AppendLine("Spawned hostile block Replicators: " + hostileBlocks);
            sb.AppendLine("Contained block Replicators: " + contained);
            sb.AppendLine("EMP-disrupted block Replicators: " + emp);
            sb.AppendLine("Current swarm posture: " +
                (map?.GetComponent<MapComponent_ReplicatorSwarmBehavior>()?.CurrentPosture.ToString() ?? "<unavailable>"));
            sb.AppendLine("NOTE: this diagnostic is read-only. Physical recombination/splitting and consumption are confirmed by the recorded user-side live test.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 19 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded hierarchy and current map Replicator state resolve correctly.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 19 automated checks PASS; live behavior test still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
