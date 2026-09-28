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
    /// Live in-game completion layer for Audit 1 (Bill / Recipe Enumeration).
    /// It is intentionally a developer action only: it does not patch or mutate recipe lists.
    /// Run from the RimWorld debug actions menu while a map is loaded.
    /// </summary>
    public static class Audit1RuntimeDiagnostics
    {
        private sealed class AuditResult
        {
            public int Checks;
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Notes = new List<string>();

            public void Check(string label, Action probe)
            {
                Checks++;
                try
                {
                    probe();
                }
                catch (Exception ex)
                {
                    Failures.Add(label + " -> " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            public void Expect(string label, bool condition, string detail)
            {
                Checks++;
                if (!condition)
                    Failures.Add(label + " -> " + detail);
            }
        }

        [DebugAction(
            "WNG",
            "Audit 1 - live bill / recipe runtime probes",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunAudit1()
        {
            AuditResult result = new AuditResult();
            Map map = Find.CurrentMap;

            Log.Message("[WNG AUDIT 1] START - live bill / recipe runtime probes");

            SnapshotBenchOwnership(result, "FueledStove");
            SnapshotBenchOwnership(result, "ElectricStove");
            SnapshotBenchOwnership(result, "FabricationBench");
            SnapshotBenchOwnership(result, "DrugLab");
            SnapshotBenchOwnership(result, "TableMachining");
            SnapshotBenchOwnership(result, "Nanofabricator");

            ProbeEveryWngWorker(result);
            ProbeEveryInstalledWorkbench(result, map);
            ProbeRepresentativeBenches(result, map);
            ProbeWngProductionLifecycleCases(result, map);

            StringBuilder report = new StringBuilder();
            report.AppendLine("[WNG AUDIT 1] LIVE BILL / RECIPE RUNTIME REPORT");
            report.AppendLine("Checks: " + result.Checks);
            report.AppendLine("Failures: " + result.Failures.Count);

            if (result.Notes.Count > 0)
            {
                report.AppendLine("Notes:");
                foreach (string note in result.Notes)
                    report.AppendLine(" - " + note);
            }

            if (result.Failures.Count > 0)
            {
                report.AppendLine("FAILURES:");
                foreach (string failure in result.Failures)
                    report.AppendLine(" - " + failure);
                Log.Error(report.ToString());
                Messages.Message(
                    "WNG Audit 1 FAILED: " + result.Failures.Count + " runtime probe(s). See Player.log.",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
            }
            else
            {
                report.AppendLine("PASS: live recipe enumeration and availability probes completed without exceptions.");
                Log.Message(report.ToString());
                Messages.Message(
                    "WNG Audit 1 PASS: " + result.Checks + " live recipe probes completed.",
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
        }

        private static void ProbeEveryWngWorker(AuditResult result)
        {
            Thing wrongType = null;
            result.Check("create generic wrong-type item", delegate
            {
                ThingDef steel = DefDatabase<ThingDef>.GetNamedSilentFail("Steel");
                if (steel != null)
                    wrongType = ThingMaker.MakeThing(steel);
            });

            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                if (recipe == null || recipe.workerClass == null)
                    continue;

                string ns = recipe.workerClass.Namespace ?? string.Empty;
                if (!ns.StartsWith("WraithNaniteGravtech", StringComparison.Ordinal))
                    continue;

                RecipeDef localRecipe = recipe;

                result.Check(localRecipe.defName + " AvailableNow", delegate
                {
                    bool ignored = localRecipe.AvailableNow;
                });

                result.Check(localRecipe.defName + " AvailableOnNow(null)", delegate
                {
                    bool ignored = localRecipe.AvailableOnNow(null);
                });

                if (wrongType != null)
                {
                    result.Check(localRecipe.defName + " AvailableOnNow(wrong Thing type)", delegate
                    {
                        bool ignored = localRecipe.AvailableOnNow(wrongType);
                    });
                }

                IEnumerable<ThingDef> users = Enumerable.Empty<ThingDef>();
                result.Check(localRecipe.defName + " enumerate AllRecipeUsers", delegate
                {
                    users = localRecipe.AllRecipeUsers.ToList();
                });

                foreach (ThingDef userDef in users ?? Enumerable.Empty<ThingDef>())
                {
                    if (userDef == null || userDef.category == ThingCategory.Pawn)
                        continue;

                    // Do not instantiate arbitrary external benches here. The two WNG production
                    // workers receive focused lifecycle probes below.
                    if (!userDef.defName.StartsWith("WNG_", StringComparison.Ordinal))
                        continue;

                    ThingDef localUser = userDef;
                    result.Check(localRecipe.defName + " unspawned WNG user " + localUser.defName, delegate
                    {
                        Thing thing = ThingMaker.MakeThing(localUser);
                        bool ignored = localRecipe.AvailableOnNow(thing);
                    });
                }
            }
        }

        private static void ProbeEveryInstalledWorkbench(AuditResult result, Map map)
        {
            int benchCount = 0;
            int recipeCount = 0;
            int externalBenchCount = 0;

            foreach (ThingDef benchDef in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (benchDef == null || benchDef.category != ThingCategory.Building)
                    continue;

                List<RecipeDef> recipes = null;
                result.Check("enumerate " + benchDef.defName + ".AllRecipes", delegate
                {
                    recipes = benchDef.AllRecipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
                });

                if (recipes == null || recipes.Count == 0)
                    continue;

                benchCount++;
                recipeCount += recipes.Count;
                if (!benchDef.defName.StartsWith("WNG_", StringComparison.Ordinal))
                    externalBenchCount++;

                // Do NOT instantiate every third-party workbench here. Some benches run side-effectful
                // PostMake/comp initialization and made-from-stuff benches also spam MakeThing warnings
                // when created without stuff. Audit 1 is an observer only.
                if (map != null)
                {
                    foreach (Building spawned in map.listerBuildings.AllBuildingsColonistOfDef(benchDef))
                    {
                        Building localSpawned = spawned;
                        foreach (RecipeDef recipe in recipes)
                        {
                            RecipeDef localRecipe = recipe;
                            result.Check(benchDef.defName + " -> " + localRecipe.defName + " spawned availability", delegate
                            {
                                bool ignored = localRecipe.AvailableOnNow(localSpawned);
                            });
                        }
                    }
                }
            }

            result.Notes.Add(
                "Installed workbench enumeration: " + benchCount + " bench defs, " +
                recipeCount + " recipe links, " + externalBenchCount + " non-WNG/mod-or-vanilla benches.");
        }

        private static void SnapshotBenchOwnership(AuditResult result, string defName)
        {
            ThingDef bench = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (bench == null)
            {
                result.Notes.Add("Snapshot bench not present: " + defName);
                return;
            }

            List<RecipeDef> all = null;
            result.Check("snapshot " + defName + " AllRecipes", delegate
            {
                all = bench.AllRecipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
            });

            List<RecipeDef> reverse = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r?.recipeUsers != null && r.recipeUsers.Contains(bench))
                .ToList();

            int directCount = bench.recipes?.Count ?? 0;
            result.Notes.Add(
                "SNAPSHOT " + defName +
                ": directRecipes=" + directCount +
                ", reverseRecipeUsers=" + reverse.Count +
                ", AllRecipes=" + (all?.Count ?? -1));

            if (all != null)
            {
                string owners = string.Join(", ",
                    all.GroupBy(r => r.modContentPack?.Name ?? "<unknown>")
                       .OrderBy(g => g.Key)
                       .Select(g => g.Key + "=" + g.Count()));
                result.Notes.Add("SNAPSHOT " + defName + " owners: " + owners);

                string names = string.Join(", ", all.Take(120).Select(r => r.defName));
                result.Notes.Add("SNAPSHOT " + defName + " recipes: " + names);
            }

            if (reverse.Count > 0)
            {
                string reverseNames = string.Join(", ", reverse.Take(120).Select(r => r.defName));
                result.Notes.Add("SNAPSHOT " + defName + " reverse-owned recipes: " + reverseNames);
            }
        }

        private static void ProbeRepresentativeBenches(AuditResult result, Map map)
        {
            // Def names are resolved dynamically because several vanilla production benches have
            // changed labels while keeping stable def names across versions.
            string[] representative =
            {
                "FueledStove",
                "ElectricStove",
                "HandTailoringBench",
                "ElectricTailoringBench",
                "TableMachining",
                "FueledSmithy",
                "ElectricSmithy",
                "FabricationBench",
                "DrugLab",
                "ElectricSmelter",
                "TableButcher",
                "TableStonecutter",
                "BioferriteShaper",
                "MechGestator"
            };

            foreach (string defName in representative)
            {
                ThingDef benchDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (benchDef == null)
                {
                    result.Notes.Add("Representative bench not present in this active mod/DLC set: " + defName);
                    continue;
                }

                List<RecipeDef> recipes = null;
                result.Check("representative " + defName + " enumerate AllRecipes", delegate
                {
                    recipes = benchDef.AllRecipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
                });

                result.Expect(
                    "representative " + defName + " has recipes",
                    recipes != null && recipes.Count > 0,
                    "recipe list is empty");

                if (recipes != null)
                    result.Notes.Add(defName + ": " + recipes.Count + " recipe(s) visible through ThingDef.AllRecipes.");
            }
        }

        private static void ProbeWngProductionLifecycleCases(AuditResult result, Map map)
        {
            RecipeDef universal = DefDatabase<RecipeDef>.GetNamedSilentFail("WNG_Universal_AncientDrone")
                                  ?? DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(
                                      r => r?.workerClass == typeof(RecipeWorker_UniversalCraftOnly));

            if (universal != null)
            {
                result.Check("Universal worker null probe", delegate
                {
                    bool ignored = universal.AvailableOnNow(null);
                });

                ThingDef fabricator = DefDatabase<ThingDef>.GetNamedSilentFail("FabricationBench");
                if (fabricator != null)
                {
                    Thing unspawned = null;
                    result.Check("Universal worker unspawned fabrication bench", delegate
                    {
                        unspawned = ThingMaker.MakeThing(fabricator);
                        bool ignored = universal.AvailableOnNow(unspawned);
                    });

                    if (unspawned != null)
                    {
                        result.Check("Universal worker destroyed fabrication bench", delegate
                        {
                            unspawned.Destroy(DestroyMode.Vanish);
                            bool ignored = universal.AvailableOnNow(unspawned);
                        });
                    }
                }
            }

            RecipeDef pattern = DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(
                r => r?.workerClass == typeof(RecipeWorker_AsuranPatternLocked));

            if (pattern != null)
            {
                result.Check("Asuran pattern worker null probe", delegate
                {
                    bool ignored = pattern.AvailableOnNow(null);
                });

                ThingDef precursor = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_PrecursorFabricator");
                if (precursor != null)
                {
                    Thing unspawned = null;
                    result.Check("Asuran pattern worker missing-map/missing-faction probe", delegate
                    {
                        unspawned = ThingMaker.MakeThing(precursor);
                        bool ignored = pattern.AvailableOnNow(unspawned);
                    });

                    if (unspawned != null)
                    {
                        result.Check("Asuran pattern worker destroyed probe", delegate
                        {
                            unspawned.Destroy(DestroyMode.Vanish);
                            bool ignored = pattern.AvailableOnNow(unspawned);
                        });
                    }
                }

                if (map != null)
                {
                    Building spawned = map.listerBuildings
                        .AllBuildingsColonistOfDef(DefDatabase<ThingDef>.GetNamedSilentFail("WNG_PrecursorFabricator"))
                        ?.FirstOrDefault();
                    if (spawned != null)
                    {
                        result.Check("Asuran pattern worker spawned-map probe", delegate
                        {
                            bool ignored = pattern.AvailableOnNow(spawned);
                        });
                    }
                }
            }
        }
    }
}
