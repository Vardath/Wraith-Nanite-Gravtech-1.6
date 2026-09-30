using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{    /// <summary>
    /// Narrow live diagnostic for the external/vanilla recipe-loss regression.
    /// It is deliberately read-only: no recipe, bench, filter, category or Def is modified.
    /// </summary>
    public static class RecipeLossProbe
    {
        private static string latestStartupSnapshot;
        private static string latestManualProbe;
        private static bool rimDoctorBridgeAttempted;
        private static bool rimDoctorBridgeInstalled;
        private const string RimDoctorHarmonyId = "vardath.wraithnanitegravtech.recipeprobe.rimdoctor";
        [DebugAction(
            "WNG",
            "Recipe loss probe (vanilla + Nanotech Overpower)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            StringBuilder sb = new StringBuilder(32768);
            sb.AppendLine("[WNG RECIPE LOSS PROBE] START");
            sb.AppendLine("This probe is read-only.");

            Map map = Find.CurrentMap;

            Thing selected = Find.Selector?.SingleSelectedThing;
            if (selected != null)
            {
                sb.AppendLine("SELECTED THING " + selected.def?.defName +
                              " [" + (selected.LabelCap ?? "<no label>") + "] owner=" + Owner(selected.def));
                if (selected.def != null)
                    ProbeBench(sb, selected.def, map);
            }
            else
            {
                sb.AppendLine("SELECTED THING <none>");
            }

            ProbeNamedRecipe(sb, "CookMealSimple", map);
            ProbeNamedRecipe(sb, "CookMealFine", map);
            ProbeNamedRecipe(sb, "CookMealLavish", map);
            ProbeNamedRecipe(sb, "CookMealSimpleBulk", map);
            ProbeNamedRecipe(sb, "CookMealFineBulk", map);
            ProbeNamedRecipe(sb, "CookMealLavishBulk", map);
            ProbeNamedRecipe(sb, "Make_Pemmican", map);

            HashSet<ThingDef> benches = new HashSet<ThingDef>();
            AddBench(benches, "FueledStove");
            AddBench(benches, "ElectricStove");
            AddBench(benches, "Nanofabricator");

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null)
                    continue;

                string name = def.defName ?? string.Empty;
                string label = def.label ?? string.Empty;
                if (name.IndexOf("stove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("stove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("nanofabric", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("nanofabric", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    benches.Add(def);
                }
            }

            foreach (ThingDef bench in benches.OrderBy(b => b.defName))
                ProbeBench(sb, bench, map);

            ProbePackageRecipes(sb, "hye.nto", "Nanotech Overpower", benches, map);
            ProbeWngMarketValues(sb);

            sb.AppendLine("[WNG RECIPE LOSS PROBE] END");
            latestManualProbe = sb.ToString();
            Log.Message(latestManualProbe);
            InstallOptionalRimDoctorBridge();
            Messages.Message(
                "WNG recipe loss probe complete. Send the new Player.log; search for [WNG RECIPE LOSS PROBE].",
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }

        public static void RunStartupSnapshot()
        {
            StringBuilder sb = new StringBuilder(8192);
            sb.AppendLine("[WNG RECIPE STARTUP SNAPSHOT] START");
            sb.AppendLine("RecipeDefCount=" + DefDatabase<RecipeDef>.AllDefsListForReading.Count);

            ProbeNamedRecipe(sb, "CookMealSimple", null);
            ProbeNamedRecipe(sb, "CookMealFine", null);
            ProbeNamedRecipe(sb, "CookMealLavish", null);
            ProbeNamedRecipe(sb, "Make_Pemmican", null);

            foreach (string benchName in new[] { "FueledStove", "ElectricStove", "Nanofabricator" })
            {
                ThingDef bench = DefDatabase<ThingDef>.GetNamedSilentFail(benchName);
                if (bench != null)
                    ProbeBench(sb, bench, null);
                else
                    sb.AppendLine("STARTUP BENCH " + benchName + ": ABSENT");
            }

            int ntoCount = DefDatabase<RecipeDef>.AllDefsListForReading.Count(
                r => r != null && IsOwner(r, "hye.nto", "Nanotech Overpower"));
            sb.AppendLine("NanotechOverpowerRecipeDefCount=" + ntoCount);
            sb.AppendLine("[WNG RECIPE STARTUP SNAPSHOT] END");
            latestStartupSnapshot = sb.ToString();
            Log.Message(latestStartupSnapshot);
            InstallOptionalRimDoctorBridge();
        }

        /// <summary>
        /// Optional RimDoctor bridge. There is deliberately no compile-time reference to
        /// RimDoctor or Harmony. If both assemblies are already present in the user's mod
        /// stack, WNG patches RimDoctor's report builder by reflection and appends a fresh,
        /// read-only recipe snapshot. If either assembly is absent, WNG continues normally.
        /// </summary>
        public static void InstallOptionalRimDoctorBridge()
        {
            if (rimDoctorBridgeAttempted)
                return;
            rimDoctorBridgeAttempted = true;

            try
            {
                Type reportBuilderType = FindLoadedType("RimDoctor.ReportBuilder");
                MethodInfo build = reportBuilderType?.GetMethod(
                    "Build",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);

                if (build == null)
                {
                    Log.Message("[WNG RECIPE PROBE] RimDoctor not detected; Player.log diagnostics remain active.");
                    return;
                }

                Type harmonyType = FindLoadedType("HarmonyLib.Harmony");
                Type harmonyMethodType = FindLoadedType("HarmonyLib.HarmonyMethod");
                if (harmonyType == null || harmonyMethodType == null)
                {
                    Log.Warning("[WNG RECIPE PROBE] RimDoctor detected but Harmony is not loaded. RimDoctor report append is unavailable; Player.log diagnostics remain active.");
                    return;
                }

                object harmony = Activator.CreateInstance(harmonyType, new object[] { RimDoctorHarmonyId });
                MethodInfo postfixMethod = typeof(RecipeLossProbe).GetMethod(
                    nameof(RimDoctorBuildPostfix),
                    BindingFlags.Static | BindingFlags.Public);

                object harmonyMethod;
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (hmCtor != null)
                {
                    harmonyMethod = hmCtor.Invoke(new object[] { postfixMethod });
                }
                else
                {
                    harmonyMethod = Activator.CreateInstance(harmonyMethodType);
                    FieldInfo methodField = harmonyMethodType.GetField("method", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    PropertyInfo methodProperty = harmonyMethodType.GetProperty("method", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (methodField != null) methodField.SetValue(harmonyMethod, postfixMethod);
                    else if (methodProperty != null && methodProperty.CanWrite) methodProperty.SetValue(harmonyMethod, postfixMethod, null);
                    else throw new MissingMemberException("HarmonyMethod.method");
                }

                MethodInfo patch = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "Patch")
                            return false;
                        ParameterInfo[] ps = m.GetParameters();
                        return ps.Length >= 3 &&
                               typeof(MethodBase).IsAssignableFrom(ps[0].ParameterType) &&
                               ps.Any(x => string.Equals(x.Name, "postfix", StringComparison.OrdinalIgnoreCase));
                    });

                if (patch == null)
                    throw new MissingMethodException("Harmony.Patch");

                ParameterInfo[] parameters = patch.GetParameters();
                object[] args = new object[parameters.Length];
                args[0] = build;
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        args[i] = harmonyMethod;
                    else
                        args[i] = null;
                }

                patch.Invoke(harmony, args);
                rimDoctorBridgeInstalled = true;
                Log.Message("[WNG RECIPE PROBE] RimDoctor detected. A fresh WNG recipe-loss section will be appended to Diagnostics -> Save report.");
            }
            catch (Exception ex)
            {
                rimDoctorBridgeInstalled = false;
                Log.Warning("[WNG RECIPE PROBE] Optional RimDoctor bridge could not be installed. This does not affect WNG gameplay: " + ex);
            }
        }

        public static void RimDoctorBuildPostfix(ref string __result)
        {
            try
            {
                StringBuilder report = new StringBuilder(65536);
                report.AppendLine();
                report.AppendLine("## WNG recipe-loss probe");
                report.AppendLine();
                report.AppendLine("Collected by Wraith & Nanite Gravtech through an optional reflection-only RimDoctor bridge. RimDoctor is not a WNG dependency.");
                report.AppendLine();
                report.AppendLine("### Fresh report-time snapshot");
                report.AppendLine();
                report.AppendLine(BuildReportTimeSnapshot().TrimEnd());

                if (!string.IsNullOrEmpty(latestManualProbe))
                {
                    report.AppendLine();
                    report.AppendLine("### Latest manual in-game probe");
                    report.AppendLine();
                    report.AppendLine(latestManualProbe.TrimEnd());
                }
                else if (!string.IsNullOrEmpty(latestStartupSnapshot))
                {
                    report.AppendLine();
                    report.AppendLine("### Earlier startup snapshot");
                    report.AppendLine();
                    report.AppendLine(latestStartupSnapshot.TrimEnd());
                }

                __result = (__result ?? string.Empty) + report;
            }
            catch (Exception ex)
            {
                __result = (__result ?? string.Empty) +
                           "\n\n## WNG recipe-loss probe\n(RimDoctor append failed safely: " +
                           ex.GetType().Name + ": " + ex.Message + ")\n";
            }
        }

        private static string BuildReportTimeSnapshot()
        {
            StringBuilder sb = new StringBuilder(32768);
            sb.AppendLine("[WNG RIMDOCTOR RECIPE SNAPSHOT] START");
            sb.AppendLine("RimDoctorBridgeInstalled=" + rimDoctorBridgeInstalled);
            sb.AppendLine("RecipeDefCount=" + DefDatabase<RecipeDef>.AllDefsListForReading.Count);

            Map map = Find.CurrentMap;

            ProbeNamedRecipe(sb, "CookMealSimple", map);
            ProbeNamedRecipe(sb, "CookMealFine", map);
            ProbeNamedRecipe(sb, "CookMealLavish", map);
            ProbeNamedRecipe(sb, "CookMealSimpleBulk", map);
            ProbeNamedRecipe(sb, "CookMealFineBulk", map);
            ProbeNamedRecipe(sb, "CookMealLavishBulk", map);
            ProbeNamedRecipe(sb, "Make_Pemmican", map);
            ProbeNamedRecipe(sb, "Make_Kibble", map);

            HashSet<ThingDef> benches = new HashSet<ThingDef>();
            AddBench(benches, "FueledStove");
            AddBench(benches, "ElectricStove");
            AddBench(benches, "TableButcher");
            AddBench(benches, "ElectricSmelter");
            AddBench(benches, "DrugLab");
            AddBench(benches, "FabricationBench");
            AddBench(benches, "Nanofabricator");

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null)
                    continue;
                string name = def.defName ?? string.Empty;
                string label = def.label ?? string.Empty;
                if (name.IndexOf("stove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("stove", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("butcher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("butcher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("nanofabric", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("nanofabric", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    benches.Add(def);
                }
            }

            foreach (ThingDef bench in benches.OrderBy(b => b.defName))
                ProbeBench(sb, bench, map);

            ProbePackageRecipes(sb, "hye.nto", "Nanotech Overpower", benches, map);

            sb.AppendLine("[WNG RIMDOCTOR RECIPE SNAPSHOT] END");
            return sb.ToString();
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(fullName, false);
                    if (type != null)
                        return type;
                }
                catch
                {
                }
            }
            return null;
        }
        private static void AddBench(HashSet<ThingDef> benches, string defName)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
                benches.Add(def);
        }

        private static void ProbeNamedRecipe(StringBuilder sb, string defName, Map map)
        {
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(defName);
            if (recipe == null)
            {
                sb.AppendLine("NAMED RECIPE " + defName + ": ABSENT FROM DefDatabase<RecipeDef>");
                return;
            }

            sb.AppendLine("NAMED RECIPE " + defName + ": PRESENT owner=" + Owner(recipe));
            AppendRecipeState(sb, recipe, null, map, "  ");
        }

        private static void ProbeBench(StringBuilder sb, ThingDef bench, Map map)
        {
            sb.AppendLine("BENCH " + bench.defName + " [" + (bench.label ?? "<no label>") + "] owner=" + Owner(bench));

            List<RecipeDef> direct = bench.recipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
            List<RecipeDef> reverse = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r?.recipeUsers != null && r.recipeUsers.Contains(bench))
                .ToList();

            List<RecipeDef> all = null;
            try
            {
                all = bench.AllRecipes?.Where(r => r != null).ToList() ?? new List<RecipeDef>();
                sb.AppendLine("  directRecipes=" + direct.Count +
                              " reverseRecipeUsers=" + reverse.Count +
                              " AllRecipes=" + all.Count);
            }
            catch (Exception ex)
            {
                sb.AppendLine("  AllRecipes THREW: " + ex);
                all = new List<RecipeDef>();
            }

            AppendNames(sb, "  DIRECT", direct);
            AppendNames(sb, "  REVERSE", reverse);
            AppendNames(sb, "  ALL", all);

            List<RecipeDef> union = direct.Concat(reverse).Concat(all).Where(r => r != null).Distinct().ToList();
            foreach (RecipeDef recipe in union)
            {
                bool inAll = all.Contains(recipe);
                bool inDirect = direct.Contains(recipe);
                bool inReverse = reverse.Contains(recipe);
                sb.AppendLine("  RECIPE " + recipe.defName +
                              " owner=" + Owner(recipe) +
                              " direct=" + inDirect +
                              " reverse=" + inReverse +
                              " all=" + inAll);
                AppendRecipeState(sb, recipe, bench, map, "    ");
            }
        }

        private static void ProbePackageRecipes(
            StringBuilder sb,
            string packageId,
            string displayNameToken,
            HashSet<ThingDef> knownBenches,
            Map map)
        {
            List<RecipeDef> owned = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(r => r != null && IsOwner(r, packageId, displayNameToken))
                .OrderBy(r => r.defName)
                .ToList();

            sb.AppendLine("PACKAGE RECIPES " + packageId + ": count=" + owned.Count);

            foreach (RecipeDef recipe in owned)
            {
                List<ThingDef> users = SafeUsers(recipe);
                string userNames = string.Join(",", users.Where(u => u != null).Select(u => u.defName));
                sb.AppendLine("  " + recipe.defName + " users=[" + userNames + "] AvailableNow=" + SafeAvailableNow(recipe));

                foreach (ThingDef user in users.Where(u => u != null))
                {
                    knownBenches.Add(user);
                    bool contains = false;
                    string allState;
                    try
                    {
                        contains = user.AllRecipes != null && user.AllRecipes.Contains(recipe);
                        allState = contains ? "YES" : "NO";
                    }
                    catch (Exception ex)
                    {
                        allState = "THREW " + ex.GetType().Name + ": " + ex.Message;
                    }

                    sb.AppendLine("    user " + user.defName + " AllRecipesContains=" + allState);
                }

                ProbeRecipeMarketSurface(sb, recipe, "    ");
            }
        }

        private static void AppendRecipeState(
            StringBuilder sb,
            RecipeDef recipe,
            ThingDef bench,
            Map map,
            string indent)
        {
            bool inDatabase = false;
            try
            {
                inDatabase = DefDatabase<RecipeDef>.GetNamedSilentFail(recipe.defName) == recipe;
            }
            catch
            {
                inDatabase = false;
            }

            sb.AppendLine(indent + "worker=" + (recipe.workerClass?.FullName ?? "<null>") +
                          " AvailableNow=" + SafeAvailableNow(recipe) +
                          " research=" + ResearchState(recipe) +
                          " inDefDatabase=" + inDatabase);

            List<ThingDef> users = SafeUsers(recipe);
            sb.AppendLine(indent + "users=[" + string.Join(",", users.Where(u => u != null).Select(u => u.defName)) + "]");
            sb.AppendLine(indent + "products=[" + ProductNames(recipe) + "]");

            if (bench != null)
            {
                Building spawned = map?.listerBuildings?.AllBuildingsColonistOfDef(bench)?.FirstOrDefault();
                if (spawned != null)
                {
                    try
                    {
                        sb.AppendLine(indent + "AvailableOnNow(spawned " + bench.defName + ")=" + recipe.AvailableOnNow(spawned));
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine(indent + "AvailableOnNow(spawned " + bench.defName + ") THREW: " + ex);
                    }
                }
            }

            ProbeRecipeMarketSurface(sb, recipe, indent);
        }

        private static List<ThingDef> SafeUsers(RecipeDef recipe)
        {
            try
            {
                return recipe.AllRecipeUsers?.Where(u => u != null).ToList() ?? new List<ThingDef>();
            }
            catch
            {
                return new List<ThingDef>();
            }
        }

        private static string ProductNames(RecipeDef recipe)
        {
            if (recipe?.products == null || recipe.products.Count == 0)
                return string.Empty;
            return string.Join(",", recipe.products
                .Where(p => p?.thingDef != null)
                .Select(p => p.thingDef.defName + "x" + p.count));
        }

        private static string SafeAvailableNow(RecipeDef recipe)
        {
            try
            {
                return recipe.AvailableNow.ToString();
            }
            catch (Exception ex)
            {
                return "THREW " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static string ResearchState(RecipeDef recipe)
        {
            try
            {
                if (recipe.researchPrerequisite == null)
                    return "<none>";
                return recipe.researchPrerequisite.defName + ":" + recipe.researchPrerequisite.IsFinished;
            }
            catch (Exception ex)
            {
                return "THREW " + ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void ProbeRecipeMarketSurface(StringBuilder sb, RecipeDef recipe, string indent)
        {
            HashSet<ThingDef> candidates = new HashSet<ThingDef>();

            try
            {
                if (recipe.fixedIngredientFilter != null)
                {
                    foreach (ThingDef def in recipe.fixedIngredientFilter.AllowedThingDefs)
                        if (def != null) candidates.Add(def);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine(indent + "fixedIngredientFilter enumeration THREW: " + ex);
            }

            if (recipe.ingredients != null)
            {
                foreach (IngredientCount ingredient in recipe.ingredients)
                {
                    if (ingredient?.filter == null)
                        continue;
                    try
                    {
                        foreach (ThingDef def in ingredient.filter.AllowedThingDefs)
                            if (def != null) candidates.Add(def);
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine(indent + "ingredient filter enumeration THREW: " + ex);
                    }
                }
            }

            if (recipe.products != null)
            {
                foreach (ThingDefCountClass product in recipe.products)
                    if (product?.thingDef != null) candidates.Add(product.thingDef);
            }

            int tested = 0;
            int failed = 0;
            foreach (ThingDef def in candidates)
            {
                tested++;
                try
                {
                    float value = def.BaseMarketValue;
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        sb.AppendLine(indent + "MARKET INVALID " + def.defName + " = " + value);
                }
                catch (Exception ex)
                {
                    failed++;
                    sb.AppendLine(indent + "MARKET THROW " + def.defName +
                                  " owner=" + Owner(def) +
                                  " categories=[" + CategoryNames(def) + "] -> " + ex);
                }
            }

            sb.AppendLine(indent + "marketCandidates=" + tested + " marketThrows=" + failed);
        }

        private static void ProbeWngMarketValues(StringBuilder sb)
        {
            int tested = 0;
            int throws = 0;
            int buildingsCategory = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || string.IsNullOrEmpty(def.defName) ||
                    !def.defName.StartsWith("WNG_", StringComparison.Ordinal))
                    continue;

                bool inBuildings = def.thingCategories != null &&
                    def.thingCategories.Any(c => c != null && c.defName == "Buildings");
                if (inBuildings)
                    buildingsCategory++;

                tested++;
                try
                {
                    float value = def.BaseMarketValue;
                    if (inBuildings)
                    {
                        int costCount = def.CostList?.Count ?? 0;
                        sb.AppendLine("WNG BUILDINGS MARKET " + def.defName +
                                      " value=" + value +
                                      " costCount=" + costCount +
                                      " category=" + def.category +
                                      " thingClass=" + (def.thingClass?.FullName ?? "<null>"));
                    }
                }
                catch (Exception ex)
                {
                    throws++;
                    sb.AppendLine("WNG MARKET THROW " + def.defName +
                                  " categories=[" + CategoryNames(def) + "] -> " + ex);
                }
            }

            sb.AppendLine("WNG MARKET SUMMARY tested=" + tested +
                          " throws=" + throws +
                          " BuildingsMembers=" + buildingsCategory);
        }

        private static void AppendNames(StringBuilder sb, string label, List<RecipeDef> recipes)
        {
            sb.AppendLine(label + " [" + string.Join(", ", recipes.Select(r => r.defName + "@" + Owner(r))) + "]");
        }

        private static string Owner(Def def)
        {
            if (def?.modContentPack == null)
                return "<unknown>";
            return (def.modContentPack.PackageId ?? "<no package>") + "/" + (def.modContentPack.Name ?? "<no name>");
        }

        private static bool IsOwner(Def def, string packageId, string displayNameToken)
        {
            if (def?.modContentPack == null)
                return false;
            string id = def.modContentPack.PackageId ?? string.Empty;
            string name = def.modContentPack.Name ?? string.Empty;
            return id.Equals(packageId, StringComparison.OrdinalIgnoreCase) ||
                   name.IndexOf(displayNameToken, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CategoryNames(ThingDef def)
        {
            if (def?.thingCategories == null)
                return string.Empty;
            return string.Join(",", def.thingCategories.Where(c => c != null).Select(c => c.defName));
        }
    }
}
