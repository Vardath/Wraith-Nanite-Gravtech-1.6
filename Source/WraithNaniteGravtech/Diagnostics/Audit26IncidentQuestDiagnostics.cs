using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit26IncidentQuestDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 26 - incidents / quests",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            Map map = Find.CurrentMap;
            int incidents = 0;
            int sites = 0;
            int invalidFalse = 0;
            int currentMapCallable = 0;

            foreach (IncidentDef def in DefDatabase<IncidentDef>.AllDefsListForReading
                         .Where(d => d?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                         .OrderBy(d => d.defName))
            {
                incidents++;
                try
                {
                    IncidentWorker worker = def.Worker;
                    if (worker == null)
                    {
                        failures.Add(def.defName + " resolved a null IncidentWorker.");
                        continue;
                    }

                    IncidentParms invalid = new IncidentParms { target = null };
                    bool invalidResult = worker.CanFireNow(invalid);
                    if (invalidResult)
                        failures.Add(def.defName + " accepted a null incident target.");
                    else
                        invalidFalse++;

                    if (map != null)
                    {
                        IncidentParms current = StorytellerUtility.DefaultParmsNow(def.category, map);
                        current.target = map;
                        bool canNow = worker.CanFireNow(current);
                        currentMapCallable++;
                        sb.AppendLine(" - " + def.defName + ": CanFireNow(current map)=" + canNow);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(def.defName + " CanFireNow probe threw " +
                                 ex.GetType().Name + ": " + ex.Message);
                }
            }

            foreach (SitePartDef def in DefDatabase<SitePartDef>.AllDefsListForReading
                         .Where(d => d?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                         .OrderBy(d => d.defName))
            {
                sites++;
                if (def.workerClass == null)
                    failures.Add(def.defName + " has null SitePart workerClass at runtime.");
            }

            sb.Insert(0,
                "[WNG AUDIT 26] INCIDENT / QUEST\n" +
                "Loaded WNG IncidentDefs: " + incidents + "\n" +
                "Loaded WNG SitePartDefs: " + sites + "\n" +
                "Null-target incident checks returning false: " + invalidFalse + "\n" +
                "Current-map CanFireNow probes completed: " + currentMapCallable + "\n");

            sb.AppendLine("This probe is read-only: it does not execute incidents or generate sites.");
            sb.AppendLine("Valid-condition execution and SitePart map generation remain throwaway-save tests.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 26 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded IncidentDefs/SitePartDefs and CanFireNow guards resolve.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 26 automated checks PASS; live incident/site generation still pending.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
