using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    /// <summary>
    /// Live completion layer for Audit 3. It never edits recipe ownership.
    /// It enumerates every non-WNG workbench in the active mod stack and gives
    /// special regression coverage to Nanotech Overpower's Nanofabricator.
    /// </summary>
    public static class Audit3ThirdPartyRecipeDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 3 - third-party recipe preservation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            List<string> notes = new List<string>();
            int benches = 0;
            int recipeLinks = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || def.category != ThingCategory.Building)
                    continue;
                if (def.defName != null && def.defName.StartsWith("WNG_", StringComparison.Ordinal))
                    continue;

                List<RecipeDef> recipes;
                try
                {
                    recipes = def.AllRecipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
                }
                catch (Exception ex)
                {
                    failures.Add(def.defName + " AllRecipes threw " + ex.GetType().Name + ": " + ex.Message);
                    continue;
                }

                if (recipes.Count == 0)
                    continue;

                benches++;
                recipeLinks += recipes.Count;

                // Availability of any single WNG recipe must not prevent enumeration of
                // unrelated recipes on an external bench.
                foreach (RecipeDef recipe in recipes)
                {
                    try
                    {
                        bool ignored = recipe.AvailableNow;
                    }
                    catch (Exception ex)
                    {
                        failures.Add(def.defName + " -> " + recipe.defName +
                                     " AvailableNow threw " + ex.GetType().Name + ": " + ex.Message);
                    }
                }

                string owner = def.modContentPack?.PackageId ?? "<unknown>";
                notes.Add(owner + " :: " + def.defName + " = " + recipes.Count + " recipe(s)");
            }

            ThingDef nano = DefDatabase<ThingDef>.GetNamedSilentFail("Nanofabricator");
            if (nano != null)
            {
                string owner = nano.modContentPack?.PackageId ?? "<unknown>";
                List<RecipeDef> recipes = null;
                try
                {
                    recipes = nano.AllRecipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
                }
                catch (Exception ex)
                {
                    failures.Add("Nanofabricator enumeration threw " + ex.GetType().Name + ": " + ex.Message);
                }

                if (recipes != null)
                {
                    notes.Add("HISTORICAL REGRESSION: " + owner + " :: Nanofabricator = " +
                              recipes.Count + " recipe(s)");

                    // Nanotech Overpower explicitly defines the Nanofabricator as a multi-recipe
                    // resource fabricator. A one-recipe result reproduces the historical failure.
                    if (recipes.Count <= 1)
                        failures.Add("Nanofabricator has only " + recipes.Count +
                                     " recipe(s); historical third-party truncation regression reproduced.");
                }
            }
            else
            {
                notes.Add("Nanofabricator not installed in this active mod set; specific Lyn.NTO regression skipped.");
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 3] THIRD-PARTY RECIPE PRESERVATION");
            sb.AppendLine("External benches with recipes: " + benches);
            sb.AppendLine("External recipe links enumerated: " + recipeLinks);
            sb.AppendLine("Failures: " + failures.Count);
            foreach (string note in notes)
                sb.AppendLine(" - " + note);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 3 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: third-party recipe enumeration survived WNG.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 3 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
