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
            AppendAvailabilityDiagnostics(sb, recipe, "  ");
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

            int deepAvailabilitySamples = 0;
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

                if (deepAvailabilitySamples < 5 &&
                    users.Any(u => u != null && string.Equals(u.defName, "Nanofabricator", StringComparison.OrdinalIgnoreCase)))
                {
                    AppendAvailabilityDiagnostics(sb, recipe, "    ");
                    deepAvailabilitySamples++;
                }
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

        private static void AppendAvailabilityDiagnostics(StringBuilder sb, RecipeDef recipe, string indent)
        {
            if (recipe == null)
                return;

            try
            {
                string researchList = recipe.researchPrerequisites == null
                    ? "<none>"
                    : string.Join(",", recipe.researchPrerequisites.Where(r => r != null)
                        .Select(r => r.defName + ":" + r.IsFinished));
                string memes = recipe.memePrerequisitesAny == null
                    ? "<none>"
                    : string.Join(",", recipe.memePrerequisitesAny.Where(m => m != null).Select(m => m.defName));
                string factionTags = recipe.factionPrerequisiteTags == null
                    ? "<none>"
                    : string.Join(",", recipe.factionPrerequisiteTags);

                sb.AppendLine(indent + "VANILLA GATES researchPrerequisite=" +
                              (recipe.researchPrerequisite == null ? "<none>" : recipe.researchPrerequisite.defName + ":" + recipe.researchPrerequisite.IsFinished) +
                              " researchPrerequisites=[" + researchList + "]" +
                              " memes=[" + memes + "]" +
                              " factionTags=[" + factionTags + "]" +
                              " fromIdeoBuildingPreceptOnly=" + recipe.fromIdeoBuildingPreceptOnly +
                              " playerFaction=" + (Faction.OfPlayer?.def?.defName ?? "<null>"));
            }
            catch (Exception ex)
            {
                sb.AppendLine(indent + "VANILLA GATES THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            AppendHarmonyAvailabilityOwners(sb, indent);
            AppendDiscoveriesDiagnostics(sb, recipe, indent);
        }

        private static void AppendHarmonyAvailabilityOwners(StringBuilder sb, string indent)
        {
            try
            {
                Type harmonyType = FindLoadedType("HarmonyLib.Harmony");
                if (harmonyType == null)
                {
                    sb.AppendLine(indent + "AVAILABLE NOW PATCHES Harmony=<absent>");
                    return;
                }

                MethodInfo getter = typeof(RecipeDef).GetProperty(
                    nameof(RecipeDef.AvailableNow),
                    BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod();

                MethodInfo getPatchInfo = harmonyType.GetMethod(
                    "GetPatchInfo",
                    BindingFlags.Static | BindingFlags.Public,
                    null,
                    new[] { typeof(MethodBase) },
                    null);

                object patchInfo = getPatchInfo?.Invoke(null, new object[] { getter });
                if (patchInfo == null)
                {
                    sb.AppendLine(indent + "AVAILABLE NOW PATCHES <none>");
                    return;
                }

                HashSet<string> owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                PropertyInfo ownersProperty = patchInfo.GetType().GetProperty("Owners", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object ownersValue = ownersProperty?.GetValue(patchInfo, null);
                if (ownersValue is System.Collections.IEnumerable enumerableOwners)
                {
                    foreach (object owner in enumerableOwners)
                        if (owner != null) owners.Add(owner.ToString());
                }

                foreach (string groupName in new[] { "Prefixes", "Postfixes", "Transpilers", "Finalizers" })
                {
                    PropertyInfo group = patchInfo.GetType().GetProperty(groupName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    object groupValue = group?.GetValue(patchInfo, null);
                    if (!(groupValue is System.Collections.IEnumerable patches))
                        continue;
                    foreach (object patch in patches)
                    {
                        if (patch == null) continue;
                        PropertyInfo ownerProp = patch.GetType().GetProperty("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?? patch.GetType().GetProperty("Owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        FieldInfo ownerField = patch.GetType().GetField("owner", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        object owner = ownerProp?.GetValue(patch, null) ?? ownerField?.GetValue(patch);
                        if (owner != null) owners.Add(owner.ToString());
                    }
                }

                sb.AppendLine(indent + "AVAILABLE NOW PATCHES owners=[" + string.Join(",", owners.OrderBy(x => x)) + "]");
            }
            catch (Exception ex)
            {
                sb.AppendLine(indent + "AVAILABLE NOW PATCHES THREW " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void AppendDiscoveriesDiagnostics(StringBuilder sb, RecipeDef recipe, string indent)
        {
            Type tracker = FindLoadedType("Discoveries.DiscoveryTracker");
            Type mod = FindLoadedType("Discoveries.DiscoveriesMod");
            if (tracker == null || mod == null)
            {
                sb.AppendLine(indent + "DISCOVERIES <not loaded>");
                return;
            }

            try
            {
                object settings =
                    mod.GetField("settings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null) ??
                    mod.GetProperty("settings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null, null);

                bool? discoveryEnabled = ReadBoolMember(settings, "discoveryEnabled");
                bool? enableThings = ReadBoolMember(settings, "enableDiscoveryForThings");
                bool? hideIngredients = ReadBoolMember(settings, "hideRecipesWithUndiscoveredIngredients");
                bool? excludeStarting = ReadBoolMember(settings, "excludeStartingScenario");

                MethodInfo lockedMethod = tracker.GetMethod(
                    "IsRecipeLockedByDiscovery",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(RecipeDef) },
                    null);
                object lockedValue = lockedMethod?.Invoke(null, new object[] { recipe });

                FieldInfo discoveredField = tracker.GetField(
                    "discoveredThingDefNames",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                object discoveredValue = discoveredField?.GetValue(null);
                int discoveredCount = CollectionCount(discoveredValue);

                sb.AppendLine(indent + "DISCOVERIES loaded=True locked=" + (lockedValue ?? "<unknown>") +
                              " discoveryEnabled=" + NullableBool(discoveryEnabled) +
                              " enableDiscoveryForThings=" + NullableBool(enableThings) +
                              " hideRecipesWithUndiscoveredIngredients=" + NullableBool(hideIngredients) +
                              " excludeStartingScenario=" + NullableBool(excludeStarting) +
                              " discoveredThingCount=" + discoveredCount);

                MethodInfo getSlots = tracker.GetMethod(
                    "GetIngredientSlots",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(RecipeDef) },
                    null);
                MethodInfo isDiscovered = tracker.GetMethod(
                    "IsIngredientDiscovered",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(ThingDef) },
                    null);

                object slotsValue = getSlots?.Invoke(null, new object[] { recipe });
                if (slotsValue is System.Collections.IEnumerable slots)
                {
                    int slotIndex = 0;
                    foreach (object slotObj in slots)
                    {
                        IEnumerable<ThingDef> defs = (slotObj as IEnumerable<ThingDef>) ?? Enumerable.Empty<ThingDef>();
                        List<ThingDef> slotDefs = defs.Where(d => d != null).ToList();
                        List<string> discovered = new List<string>();
                        List<string> undiscovered = new List<string>();

                        foreach (ThingDef def in slotDefs)
                        {
                            bool known = false;
                            try
                            {
                                object knownValue = isDiscovered?.Invoke(null, new object[] { def });
                                known = knownValue is bool b && b;
                            }
                            catch
                            {
                            }

                            if (known)
                            {
                                if (discovered.Count < 12) discovered.Add(def.defName);
                            }
                            else
                            {
                                if (undiscovered.Count < 20) undiscovered.Add(def.defName);
                            }
                        }

                        sb.AppendLine(indent + "DISCOVERIES SLOT " + slotIndex +
                                      " allowed=" + slotDefs.Count +
                                      " discoveredSample=[" + string.Join(",", discovered) + "]" +
                                      " undiscoveredSample=[" + string.Join(",", undiscovered) + "]" +
                                      " anyDiscovered=" + (discovered.Count > 0));
                        slotIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine(indent + "DISCOVERIES THREW " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool? ReadBoolMember(object obj, string name)
        {
            if (obj == null)
                return null;
            try
            {
                Type type = obj.GetType();
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field?.GetValue(obj) is bool fb) return fb;
                PropertyInfo prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop?.GetValue(obj, null) is bool pb) return pb;
            }
            catch
            {
            }
            return null;
        }

        private static int CollectionCount(object value)
        {
            if (value == null)
                return -1;
            if (value is System.Collections.ICollection collection)
                return collection.Count;

            int count = 0;
            if (value is System.Collections.IEnumerable enumerable)
                foreach (object ignored in enumerable) count++;
            return count;
        }

        private static string NullableBool(bool? value)
        {
            return value.HasValue ? value.Value.ToString() : "<unknown>";
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
