using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit37EconomyDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 37 - economy",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            int buildables = 0;
            int buildablesWithCost = 0;
            int recipes = 0;
            int recipesWithIngredients = 0;
            int products = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true ||
                    def.designationCategory == null)
                    continue;

                buildables++;
                bool positiveCost = def.costList != null && def.costList.Any(c => c != null && c.count > 0);
                if (positiveCost)
                    buildablesWithCost++;
                else
                    failures.Add(def.defName + " has no positive runtime construction cost.");

                float work = def.GetStatValueAbstract(StatDefOf.WorkToBuild);
                if (work <= 0f)
                    failures.Add(def.defName + " has non-positive runtime WorkToBuild=" + work);
            }

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe == null || recipe.products == null || recipe.products.Count == 0)
                    continue;

                bool wngRecipe =
                    recipe.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true ||
                    recipe.products.Any(p =>
                        p?.thingDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true);
                if (!wngRecipe)
                    continue;

                recipes++;
                if (recipe.workAmount <= 0f)
                    failures.Add(recipe.defName + " has non-positive runtime workAmount=" + recipe.workAmount);

                if (recipe.ingredients != null && recipe.ingredients.Count > 0)
                    recipesWithIngredients++;
                else
                    failures.Add(recipe.defName + " produces physical output but has no runtime ingredients.");

                foreach (ThingDefCountClass product in recipe.products)
                {
                    products++;
                    if (product == null || product.thingDef == null || product.count <= 0)
                        failures.Add(recipe.defName + " contains a null/non-positive runtime product.");
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 37] ECONOMY");
            sb.AppendLine("WNG runtime buildables: " + buildables);
            sb.AppendLine("Buildables with positive construction cost: " + buildablesWithCost);
            sb.AppendLine("WNG-output production recipes: " + recipes);
            sb.AppendLine("Production recipes with ingredient lists: " + recipesWithIngredients);
            sb.AppendLine("Runtime product entries checked: " + products);
            sb.AppendLine();
            sb.AppendLine("MANUAL ECONOMY SPOT-CHECK:");
            sb.AppendLine(" - In normal play, place representative Wraith/Asuran/Goa'uld/gravship buildings and confirm blueprints reserve the displayed materials and work.");
            sb.AppendLine(" - Open representative WNG production bills and confirm ingredient selectors require the intended real materials.");
            sb.AppendLine(" - Complete several representative bills and confirm the bill consumes ingredients once and creates only the displayed product count.");
            sb.AppendLine(" - Keep God Mode OFF during this audit; direct dev spawning is not evidence of normal economic cost.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 37 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: effective runtime WNG build/recipe economy has positive construction cost, work, ingredients and products.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 37 structural runtime PASS; representative live crafting/building spot-check still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
