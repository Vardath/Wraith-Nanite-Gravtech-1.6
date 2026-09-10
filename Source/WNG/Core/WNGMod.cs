using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WNGSettings : ModSettings
    {
        // Saved key retained for compatibility. The option now covers all explicit direct-build
        // bypasses that replace WNG's intended biological/nanite acquisition paths.
        public bool enableDirectWraithBootstrapCrafting;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableDirectWraithBootstrapCrafting, "enableDirectWraithBootstrapCrafting", false);
        }
    }

    public sealed class WNGMod : Mod
    {
        public static WNGSettings Settings { get; private set; }

        public WNGMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WNGSettings>();
            LongEventHandler.ExecuteWhenFinished(WNGDirectBootstrapRecipeGate.Apply);
        }

        public override string SettingsCategory() => "Wraith & Nanite Gravtech";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Technology acquisition bypasses");
            listing.GapLine();
            listing.CheckboxLabeled(
                "Enable direct workshop / grav-engine construction bypasses",
                ref Settings.enableDirectWraithBootstrapCrafting,
                "Off by default. Wraith workshop and grav-engine technology normally use host/corpse living-tech growth, while the Asuran workshop normally uses the Nanite Reserve assembly ability. When enabled, Wraith deployable workshop/grav-engine recipes are added and the Asuran workshop becomes directly buildable from the WNG Architect tab. Normal acquisition paths remain available. This never restores the obsolete WNG Gravcore.");
            listing.Gap();
            listing.Label("Changing this option updates WNG recipe and Architect visibility immediately. Existing structures are never removed when the option is disabled.");
            listing.End();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            WNGDirectBootstrapRecipeGate.Apply();
        }
    }

    public static class WNGDirectBootstrapRecipeGate
    {
        private static readonly string[] NormalWraithRecipeDefNames =
        {
            "WNG_CraftLivingForgeImplant",
            "WNG_CultureWraithBioSludge",
            "WNG_CraftWraithGravEngineImplant"
        };

        private static readonly string[] DirectWraithBypassRecipeDefNames =
        {
            "WNG_CraftDeployableLivingForge",
            "WNG_CraftDeployableWraithGravEngine"
        };

        public static bool Enabled => WNGMod.Settings?.enableDirectWraithBootstrapCrafting == true;

        public static void Apply()
        {
            ThingDef fabricationBench = DefDatabase<ThingDef>.GetNamedSilentFail("FabricationBench");
            ThingDef livingForge = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_LivingForge");

            // Wraith-owned production lives on the living forge. The first workshop-growth implant
            // retains its vanilla fabrication bootstrap route until the encounter/salvage acquisition
            // layer is rebuilt, so the player can never be trapped in a recipe deadlock.
            if (livingForge != null)
            {
                if (livingForge.recipes == null)
                    livingForge.recipes = new List<RecipeDef>();
                foreach (string recipeDefName in NormalWraithRecipeDefNames)
                    SetRecipeOnBench(livingForge, recipeDefName, true);
            }

            // The direct Wraith workshop/engine cores are a convenience bypass. They are visible on
            // the Wraith bench when enabled; FabricationBench also exposes them so the option can
            // genuinely bypass the very first host-grown workshop rather than requiring one already.
            foreach (string recipeDefName in DirectWraithBypassRecipeDefNames)
            {
                if (livingForge != null)
                    SetRecipeOnBench(livingForge, recipeDefName, Enabled);
                if (fabricationBench != null)
                    SetRecipeOnBench(fabricationBench, recipeDefName, Enabled);
            }

            ApplyArchitectVisibility();
        }

        private static void SetRecipeOnBench(ThingDef bench, string recipeDefName, bool enabled)
        {
            if (bench == null)
                return;
            RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipe == null)
                return;
            if (bench.recipes == null)
                bench.recipes = new List<RecipeDef>();

            bool contains = bench.recipes.Contains(recipe);
            if (enabled && !contains)
                bench.recipes.Add(recipe);
            else if (!enabled && contains)
                bench.recipes.Remove(recipe);
        }

        private static void ApplyArchitectVisibility()
        {
            DesignationCategoryDef wngCategory = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("WNG");
            ThingDef asuranWorkshop = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AsuranWorkshop");
            ThingDef wraithEngineSeed = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithGravEngineSeed");

            // The technical Wraith engine seed is never a normal Architect shortcut. Normal
            // acquisition is biological growth; the bypass produces a deployable core by recipe.
            if (wraithEngineSeed != null)
                wraithEngineSeed.designationCategory = null;

            // The Asuran workshop is normally assembled directly from Nanite Reserve. The user
            // option adds/removes a genuine WNG Architect designator instead of leaving a disabled
            // blueprint visible when the bypass is off.
            if (asuranWorkshop != null)
                asuranWorkshop.designationCategory = Enabled ? wngCategory : null;

            // ResolveDesignators is scheduled by ResolveReferences and rebuilds the category from the
            // current BuildableDef designationCategory assignments.
            wngCategory?.ResolveReferences();
        }
    }
}
