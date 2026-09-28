using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit36ResearchProgressionDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 36 - research progression",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void Run()
        {
            List<ResearchProjectDef> projects =
                DefDatabase<ResearchProjectDef>.AllDefsListForReading
                    .Where(r => r?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                    .OrderBy(r => r.defName, StringComparer.Ordinal)
                    .ToList();

            Dictionary<ResearchProjectDef, int> buildables =
                projects.ToDictionary(p => p, p => 0);
            Dictionary<ResearchProjectDef, int> recipes =
                projects.ToDictionary(p => p, p => 0);

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.researchPrerequisites == null)
                    continue;
                foreach (ResearchProjectDef project in def.researchPrerequisites)
                    if (project != null && buildables.ContainsKey(project))
                        buildables[project]++;
            }

            foreach (TerrainDef def in DefDatabase<TerrainDef>.AllDefsListForReading)
            {
                if (def?.researchPrerequisites == null)
                    continue;
                foreach (ResearchProjectDef project in def.researchPrerequisites)
                    if (project != null && buildables.ContainsKey(project))
                        buildables[project]++;
            }

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                ResearchProjectDef project = recipe?.researchPrerequisite;
                if (project != null && recipes.ContainsKey(project))
                    recipes[project]++;
            }

            int finished = 0;
            int withBuildables = 0;
            int withRecipes = 0;
            int roots = 0;
            int blockedByWngPrereq = 0;
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("[WNG AUDIT 36] RESEARCH PROGRESSION");
            sb.AppendLine("WNG projects resolved: " + projects.Count);

            foreach (ResearchProjectDef project in projects)
            {
                if (project.IsFinished)
                    finished++;

                List<ResearchProjectDef> prereqList = project.prerequisites ?? new List<ResearchProjectDef>();
                List<ResearchProjectDef> wngPrereqs = prereqList
                    .Where(p => p?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                    .ToList();
                if (wngPrereqs.Count == 0)
                    roots++;

                List<string> unfinishedWngPrereqs = wngPrereqs
                    .Where(p => !p.IsFinished)
                    .Select(p => p.defName)
                    .ToList();
                if (unfinishedWngPrereqs.Count > 0)
                    blockedByWngPrereq++;

                int b = buildables[project];
                int r = recipes[project];
                if (b > 0) withBuildables++;
                if (r > 0) withRecipes++;

                sb.AppendLine(
                    " - " + project.defName +
                    ": finished=" + project.IsFinished +
                    ", prereqs=" + prereqList.Count +
                    ", unfinishedWNGPrereqs=" +
                    (unfinishedWngPrereqs.Count == 0 ? "<none>" : string.Join(",", unfinishedWngPrereqs)) +
                    ", buildables=" + b +
                    ", recipes=" + r);

                if (project.prerequisites != null && project.prerequisites.Any(p => p == null))
                    failures.Add(project.defName + " has a null resolved prerequisite.");
            }

            sb.AppendLine();
            sb.AppendLine("Finished WNG projects: " + finished + " / " + projects.Count);
            sb.AppendLine("WNG graph roots at runtime: " + roots);
            sb.AppendLine("Projects currently blocked by unfinished WNG prerequisite: " + blockedByWngPrereq);
            sb.AppendLine("Projects with runtime buildable unlocks: " + withBuildables);
            sb.AppendLine("Projects with runtime recipe unlocks: " + withRecipes);
            sb.AppendLine();
            sb.AppendLine("FRESH-COLONY LIVE WALK REQUIRED:");
            sb.AppendLine(" - Use a throwaway fresh colony with research completion reset.");
            sb.AppendLine(" - Confirm each WNG root appears when its vanilla/DLC/analyzed gate is legitimately satisfied.");
            sb.AppendLine(" - Progress each WNG branch in prerequisite order and verify newly unlocked buildings/recipes/features appear.");
            sb.AppendLine(" - For every requiredAnalyzed project, obtain/analyze the artifact through its intended gameplay route rather than dev-completing the project.");
            sb.AppendLine(" - A project that cannot be started after all legitimate prerequisites/gates are satisfied, or that completes with no intended unlock, is a live failure.");

            if (projects.Count == 0)
                failures.Add("No WNG ResearchProjectDefs resolved at runtime.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 36 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: runtime WNG research Defs and resolved prerequisite/buildable/recipe links are structurally intact. Fresh-colony progression remains a live/manual requirement.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 36 structural runtime PASS; fresh-colony progression walk still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
