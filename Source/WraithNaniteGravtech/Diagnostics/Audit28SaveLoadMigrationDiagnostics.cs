using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit28SaveLoadMigrationDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 28 - save / load / migration",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 28] SAVE / LOAD / MIGRATION");

            Map map = Find.CurrentMap;
            int wngPawns = 0, replicators = 0, archives = 0, gravEngines = 0;

            if (map?.mapPawns?.AllPawnsSpawned != null)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn?.kindDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                        continue;
                    wngPawns++;
                    if (ReplicatorAssimilationUtility.IsBlockReplicator(pawn))
                    {
                        replicators++;
                        CompReplicatorDomain domain = pawn.TryGetComp<CompReplicatorDomain>();
                        if (domain == null)
                            failures.Add(pawn.LabelShortCap + " has no Replicator domain comp after load.");
                    }
                }
            }

            ThingDef archiveDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AsuranPatternArchive");
            if (map != null && archiveDef != null)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(archiveDef))
                {
                    if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                    archives++;
                    if (thing.TryGetComp<CompAsuranPatternArchive>() == null)
                        failures.Add("Loaded Pattern Archive #" + thing.thingIDNumber + " lost its archive comp.");
                }
            }

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                if (def.thingClass == typeof(Building_GravEngine))
                {
                    if (map != null)
                        gravEngines += map.listerThings.ThingsOfDef(def).Count(t => t != null && t.Spawned);
                }
            }

            WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail("WNG_AsuranFabrication");
            WorkGiverDef workGiver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("WNG_DoAsuranFabrication");
            if (workType == null) failures.Add("WNG_AsuranFabrication WorkTypeDef missing after load.");
            if (workGiver == null || workGiver.workType != workType)
                failures.Add("WNG Asuran fabrication WorkGiver no longer points to its WorkType.");

            FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            if (swarmDef == null)
                failures.Add("WNG_ReplicatorSwarm FactionDef missing.");
            else if (Find.FactionManager?.FirstFactionOfDef(swarmDef) == null)
                sb.AppendLine("Replicator Swarm faction instance: absent in this save (allowed; FactionDef loaded).");
            else
                sb.AppendLine("Replicator Swarm faction instance: present.");

            GameComponent_ReplicatorQueenState queenState = ReplicatorQueenUtility.State;
            sb.AppendLine("Exact Queen reference: " +
                (queenState?.ExactQueen == null ? "<none>" : queenState.ExactQueen.LabelShortCap + " #" + queenState.ExactQueen.thingIDNumber));
            sb.AppendLine("Spawned WNG pawns: " + wngPawns);
            sb.AppendLine("Spawned block Replicators: " + replicators);
            sb.AppendLine("Spawned Pattern Archives: " + archives);
            sb.AppendLine("Spawned WNG grav engines: " + gravEngines);
            sb.AppendLine("Current save tick: " + (Find.TickManager?.TicksGame ?? -1));
            sb.AppendLine("Read-only probe: persistence across an actual save/reload/build-upgrade must be checked by the user.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 28 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded migration-critical defs, factions and current-map state resolve.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 28 automated checks PASS; save/reload/upgrade test still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
