using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit21HumanFormAsuranCopyDiagnostics
    {
        [DebugAction("WNG", "Audit 21 - human-form / Asuran copy",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 21] HUMAN-FORM / ASURAN COPY");

            string[] kinds =
            {
                "WNG_PrecursorEngineer","WNG_PrecursorSoldier","WNG_PrecursorCommander",
                "WNG_HumanFormReplicator","WNG_HumanFormInfiltrator",
                "WNG_PlayerHumanFormReplicator","WNG_HumanFormCopy","WNG_ReplicatorQueenChild"
            };
            foreach (string name in kinds)
                if (DefDatabase<PawnKindDef>.GetNamedSilentFail(name) == null)
                    failures.Add("Missing PawnKindDef " + name);

            if (DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_NeuralInterface") == null)
                failures.Add("Missing WNG_NeuralInterface.");

            ThingDef archiveDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AsuranPatternArchive");
            if (archiveDef == null)
                failures.Add("Missing WNG_AsuranPatternArchive.");
            else if (archiveDef.GetCompProperties<CompProperties_AsuranPatternArchive>() == null)
                failures.Add("Pattern Archive missing continuity comp.");

            GameComponent_ReplicatorQueenState queenState = ReplicatorQueenUtility.State;
            Pawn exactQueen = queenState?.ExactQueen;
            sb.AppendLine(exactQueen == null
                ? "Exact Queen: not registered in this save."
                : "Exact Queen: " + exactQueen.LabelShortCap + " #" + exactQueen.thingIDNumber +
                  " released=" + queenState.Released + " captured=" + queenState.Captured);

            Map map = Find.CurrentMap;
            int synthetics = 0, copies = 0, queenChildren = 0, assignedRoles = 0, archives = 0;
            if (map?.mapPawns?.AllPawnsSpawned != null)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!AsuranCollectiveUtility.IsNaniteSynthetic(pawn))
                        continue;
                    synthetics++;
                    string kind = pawn.kindDef?.defName ?? "";
                    if (kind == "WNG_HumanFormCopy") copies++;
                    if (kind == "WNG_ReplicatorQueenChild") queenChildren++;

                    HumanFormSpecialistRole role = HumanFormSpecialistRoleUtility.RoleOf(pawn);
                    if (role != HumanFormSpecialistRole.Unassigned) assignedRoles++;
                    if ((ReplicatorQueenUtility.IsExactQueen(pawn) || kind == "WNG_ReplicatorQueenChild") &&
                        role != HumanFormSpecialistRole.Unassigned)
                        failures.Add(pawn.LabelShortCap + " has a specialist role despite Queen/child exclusion.");
                }
            }

            if (map != null && archiveDef != null)
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(archiveDef))
                {
                    if (thing == null || thing.Destroyed || !thing.Spawned) continue;
                    archives++;
                    CompAsuranPatternArchive comp = thing.TryGetComp<CompAsuranPatternArchive>();
                    if (comp == null)
                        failures.Add("Spawned Pattern Archive lacks runtime continuity comp.");
                    else
                        sb.AppendLine("Archive #" + thing.thingIDNumber + " faction=" +
                            (thing.Faction?.Name ?? "<none>") + " powered=" + comp.Powered);
                }
            }

            sb.AppendLine("Spawned nanite synthetics: " + synthetics);
            sb.AppendLine("Human-form copies: " + copies);
            sb.AppendLine("Replicator child bodies: " + queenChildren);
            sb.AppendLine("Assigned specialist roles: " + assignedRoles);
            sb.AppendLine("Pattern Archives: " + archives);
            sb.AppendLine("Read-only probe: actual copy creation, reconstruction and save/reload are manual live checks.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 21 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded copy/archive/Queen/role contracts resolve.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 21 automated checks PASS; live copy/save-load test pending.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
