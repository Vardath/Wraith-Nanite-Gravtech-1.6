using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WNGSettings : ModSettings
    {
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
            listing.Label("Wraith living-technology bootstrap");
            listing.GapLine();
            listing.CheckboxLabeled(
                "Enable direct Wraith workshop / grav-engine crafting bypass",
                ref Settings.enableDirectWraithBootstrapCrafting,
                "Off by default. When enabled, fabrication-bench recipes become available after research for deployable Wraith workshop and Wraith grav-engine growth cores. This skips using a living pawn or corpse as the biological growth host. The grav-engine result is still the real Odyssey grav engine; this does not restore the obsolete WNG Gravcore.");
            listing.Gap();
            listing.Label("Changing this option updates recipe visibility immediately. Existing bills already queued before disabling the option are not silently deleted.");
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
        private static readonly string[] RecipeDefNames =
        {
            "WNG_CraftDeployableLivingForge",
            "WNG_CraftDeployableWraithGravEngine"
        };

        public static bool Enabled => WNGMod.Settings?.enableDirectWraithBootstrapCrafting == true;

        public static void Apply()
        {
            ThingDef fabricationBench = DefDatabase<ThingDef>.GetNamedSilentFail("FabricationBench");
            if (fabricationBench == null)
            {
                Log.Warning("[WNG] Could not find vanilla FabricationBench while applying direct Wraith bootstrap recipe setting.");
                return;
            }

            if (fabricationBench.recipes == null)
                fabricationBench.recipes = new List<RecipeDef>();

            foreach (string recipeDefName in RecipeDefNames)
            {
                RecipeDef recipe = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
                if (recipe == null)
                    continue;

                bool contains = fabricationBench.recipes.Contains(recipe);
                if (Enabled && !contains)
                    fabricationBench.recipes.Add(recipe);
                else if (!Enabled && contains)
                    fabricationBench.recipes.Remove(recipe);
            }
        }
    }
}
