using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit23SurgeryDiagnostics
    {
        private static bool IsSurgery(RecipeDef recipe)
        {
            Type t = recipe?.workerClass;
            return t != null &&
                   (typeof(Recipe_Surgery).IsAssignableFrom(t) ||
                    typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(t));
        }

        private static void Probe(List<string> failures, RecipeDef recipe, Thing thing, string label)
        {
            try
            {
                bool ignored = recipe.AvailableOnNow(thing);
            }
            catch (Exception ex)
            {
                failures.Add(recipe.defName + " AvailableOnNow(" + label + ") threw " +
                             ex.GetType().Name + ": " + ex.Message);
            }
        }

        [DebugAction(
            "WNG",
            "Audit 23 - surgery",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            List<RecipeDef> surgeries = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true && IsSurgery(r))
                .OrderBy(r => r.defName)
                .ToList();

            sb.AppendLine("[WNG AUDIT 23] SURGERY");
            sb.AppendLine("Loaded WNG surgery recipes: " + surgeries.Count);

            Pawn livingHuman = Find.CurrentMap?.mapPawns?.AllPawnsSpawned
                .FirstOrDefault(p => p != null && !p.Dead && p.RaceProps?.Humanlike == true);
            Pawn wrongSpecies = Find.CurrentMap?.mapPawns?.AllPawnsSpawned
                .FirstOrDefault(p => p != null && !p.Dead && p.RaceProps?.Animal == true);
            Corpse corpse = Find.CurrentMap?.listerThings?.ThingsInGroup(ThingRequestGroup.Corpse)
                ?.OfType<Corpse>().FirstOrDefault();

            foreach (RecipeDef recipe in surgeries)
            {
                Probe(failures, recipe, null, "null");
                if (livingHuman != null)
                    Probe(failures, recipe, livingHuman, "living humanlike");
                if (wrongSpecies != null)
                    Probe(failures, recipe, wrongSpecies, "wrong species / animal");
                if (corpse != null)
                    Probe(failures, recipe, corpse, "corpse");

                try
                {
                    bool availableNow = recipe.AvailableNow;
                    string research = recipe.researchPrerequisite?.defName ?? "<none>";
                    string researchState = recipe.researchPrerequisite == null
                        ? "n/a"
                        : (recipe.researchPrerequisite.IsFinished ? "finished" : "missing/not finished");
                    sb.AppendLine(" - " + recipe.defName +
                                  " worker=" + (recipe.workerClass?.Name ?? "<none>") +
                                  " AvailableNow=" + availableNow +
                                  " research=" + research + " (" + researchState + ")");
                }
                catch (Exception ex)
                {
                    failures.Add(recipe.defName + " global AvailableNow probe threw " +
                                 ex.GetType().Name + ": " + ex.Message);
                }
            }

            sb.AppendLine("Living-human probe present: " + (livingHuman != null));
            sb.AppendLine("Wrong-species probe present: " + (wrongSpecies != null));
            sb.AppendLine("Corpse probe present: " + (corpse != null));
            sb.AppendLine("Read-only: no research, health state, bills, pawns or corpses were changed.");
            sb.AppendLine("A false availability result is normal for the wrong patient/state; only exceptions are failures.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 23 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: every loaded WNG surgery survived operation-menu availability probes.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 23 PASS; manual valid-patient operation-menu check still required.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
