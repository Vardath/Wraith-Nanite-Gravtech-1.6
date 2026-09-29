using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit11WorkGiverWorkTypeDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 11 - WorkGiver / WorkType",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            List<string> notes = new List<string>();

            WorkTypeDef workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail("WNG_AsuranFabrication");
            WorkGiverDef giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("WNG_DoAsuranFabrication");

            if (workType == null)
                failures.Add("WNG_AsuranFabrication WorkTypeDef missing at runtime.");
            else
            {
                if (workType.visible)
                    failures.Add("WNG_AsuranFabrication unexpectedly visible in normal work UI.");
                if (!workType.alwaysStartActive)
                    failures.Add("WNG_AsuranFabrication is not alwaysStartActive.");
            }

            if (giver == null)
                failures.Add("WNG_DoAsuranFabrication WorkGiverDef missing at runtime.");
            else
            {
                if (giver.workType != workType)
                    failures.Add("WNG_DoAsuranFabrication does not resolve to WNG_AsuranFabrication.");
                if (!(giver.Worker is WorkGiver_DoAsuranFabrication))
                    failures.Add("WNG_DoAsuranFabrication runtime worker is not WorkGiver_DoAsuranFabrication.");
                if (giver.fixedBillGiverDefs == null ||
                    !giver.fixedBillGiverDefs.Any(d => d?.defName == "TableSculpting"))
                {
                    failures.Add("WNG_DoAsuranFabrication no longer targets TableSculpting.");
                }
            }

            List<RecipeDef> gated = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r?.defName != null &&
                            (r.defName.StartsWith("WNG_", StringComparison.Ordinal) ||
                             r.defName.StartsWith("Make_WNG_", StringComparison.Ordinal)) &&
                            r.requiredGiverWorkType == workType)
                .ToList();

            HashSet<string> expected = new HashSet<string>
            {
                "Make_WNG_AsuranSleeperStatue",
                "Make_WNG_AsuranFeederStatue",
                "Make_WNG_AsuranReplicatorReliquary"
            };

            HashSet<string> actual = new HashSet<string>(gated.Select(r => r.defName));
            if (!actual.SetEquals(expected))
                failures.Add("Runtime Asuran-only recipe gate differs from expected: " + string.Join(", ", actual.OrderBy(x => x)));

            foreach (RecipeDef recipe in gated)
            {
                List<ThingDef> users = recipe.AllRecipeUsers?.ToList() ?? new List<ThingDef>();
                if (!users.Any(u => u?.defName == "TableSculpting"))
                    failures.Add(recipe.defName + " is gated to Asuran fabrication but is not available at TableSculpting.");
            }

            if (Find.CurrentMap != null && workType != null)
            {
                int eligibleAsurans = 0;
                int activeAsurans = 0;
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned)
                {
                    if (!AsuranCollectiveUtility.IsNaniteSynthetic(pawn) ||
                        pawn.workSettings == null ||
                        !pawn.workSettings.EverWork ||
                        pawn.story == null ||
                        pawn.WorkTypeIsDisabled(workType))
                        continue;

                    eligibleAsurans++;
                    if (pawn.workSettings.GetPriority(workType) > 0)
                        activeAsurans++;
                }

                notes.Add("Eligible spawned Asurans: " + eligibleAsurans);
                notes.Add("Eligible Asurans with active fabrication work: " + activeAsurans);
                if (activeAsurans != eligibleAsurans)
                    failures.Add("One or more eligible Asurans have WNG_AsuranFabrication priority 0.");
            }

            foreach (JobDef job in DefDatabase<JobDef>.AllDefsListForReading)
            {
                if (job?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                if (job.driverClass == null)
                    failures.Add(job.defName + " has null driverClass at runtime.");
                else if (!job.driverClass.FullName.StartsWith("WraithNaniteGravtech.", StringComparison.Ordinal))
                    failures.Add(job.defName + " resolves to non-WNG driver " + job.driverClass.FullName);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 11] WORKGIVER / WORKTYPE");
            sb.AppendLine("Asuran-gated recipes: " + gated.Count);
            foreach (string note in notes) sb.AppendLine(" - " + note);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 11 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: work routing and WNG job-driver resolution are intact.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 11 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
