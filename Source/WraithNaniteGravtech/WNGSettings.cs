using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WNGSettings : ModSettings
    {
        public bool enableUniversalCrafting = false;
        public int livingForgeRawMeatCost = 20;
        public int livingForgeBiomassYield = 16;
        public int livingForgeBiomassWorkAmount = 900;
        public int wraithGravEngineBiomassCost = 120;
        public int wraithGravEngineSeedWorkAmount = 7000;
        public float wraithGravEngineIncubationDays = 1f;
        public float livingForgeIncubationDays = 1f;
        public float wraithLivingEquipmentMaturationDays = 1f;
        public int wraithBioelectricPowerOutput = 1400;
        public float wraithBioelectricBiomassPerDay = 18f;
        public int wraithLivingPowerCellCapacity = 750;
        public int hostileReplicatorCap = 120;
        public float childsToyFeralDelayDays = 1f;
        public bool enableReplicatorStoryEvents = true;
        public float replicatorQueenRecurringRecoveryDays = 4f;
        public int sovereignLatticeControlCap = 30;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableUniversalCrafting, "enableUniversalCrafting", false);
            Scribe_Values.Look(ref livingForgeRawMeatCost, "livingForgeRawMeatCost", 20);
            Scribe_Values.Look(ref livingForgeBiomassYield, "livingForgeBiomassYield", 16);
            Scribe_Values.Look(ref livingForgeBiomassWorkAmount, "livingForgeBiomassWorkAmount", 900);
            Scribe_Values.Look(ref wraithGravEngineBiomassCost, "wraithGravEngineBiomassCost", 120);
            Scribe_Values.Look(ref wraithGravEngineSeedWorkAmount, "wraithGravEngineSeedWorkAmount", 7000);
            Scribe_Values.Look(ref wraithGravEngineIncubationDays, "wraithGravEngineIncubationDays", 1f);
            Scribe_Values.Look(ref livingForgeIncubationDays, "livingForgeIncubationDays", 1f);
            Scribe_Values.Look(ref wraithLivingEquipmentMaturationDays, "wraithLivingEquipmentMaturationDays", 1f);
            Scribe_Values.Look(ref wraithBioelectricPowerOutput, "wraithBioelectricPowerOutput", 1400);
            Scribe_Values.Look(ref wraithBioelectricBiomassPerDay, "wraithBioelectricBiomassPerDay", 18f);
            Scribe_Values.Look(ref wraithLivingPowerCellCapacity, "wraithLivingPowerCellCapacity", 750);
            Scribe_Values.Look(ref hostileReplicatorCap, "hostileReplicatorCap", 120);
            Scribe_Values.Look(ref childsToyFeralDelayDays, "childsToyFeralDelayDays", 1f);
            Scribe_Values.Look(ref enableReplicatorStoryEvents, "enableReplicatorStoryEvents", true);
            Scribe_Values.Look(ref replicatorQueenRecurringRecoveryDays, "replicatorQueenRecurringRecoveryDays", 4f);
            Scribe_Values.Look(ref sovereignLatticeControlCap, "sovereignLatticeControlCap", 30);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                ClampValues();
        }

        internal void ClampValues()
        {
            livingForgeRawMeatCost = Mathf.Clamp(livingForgeRawMeatCost, 1, 500);
            livingForgeBiomassYield = Mathf.Clamp(livingForgeBiomassYield, 1, 500);
            livingForgeBiomassWorkAmount = Mathf.Clamp(livingForgeBiomassWorkAmount, 100, 100000);
            wraithGravEngineBiomassCost = Mathf.Clamp(wraithGravEngineBiomassCost, 1, 1000);
            wraithGravEngineSeedWorkAmount = Mathf.Clamp(wraithGravEngineSeedWorkAmount, 100, 100000);
            wraithGravEngineIncubationDays = Mathf.Clamp(wraithGravEngineIncubationDays, 0.1f, 10f);
            livingForgeIncubationDays = Mathf.Clamp(livingForgeIncubationDays, 0.1f, 10f);
            wraithLivingEquipmentMaturationDays = Mathf.Clamp(wraithLivingEquipmentMaturationDays, 0.1f, 10f);
            wraithBioelectricPowerOutput = Mathf.Clamp(wraithBioelectricPowerOutput, 100, 10000);
            wraithBioelectricBiomassPerDay = Mathf.Clamp(wraithBioelectricBiomassPerDay, 0.1f, 100f);
            wraithLivingPowerCellCapacity = Mathf.Clamp(wraithLivingPowerCellCapacity, 100, 10000);
            hostileReplicatorCap = Mathf.Clamp(hostileReplicatorCap, 20, 300);
            childsToyFeralDelayDays = Mathf.Clamp(childsToyFeralDelayDays, 0.1f, 5f);
            replicatorQueenRecurringRecoveryDays = Mathf.Clamp(replicatorQueenRecurringRecoveryDays, 1f, 10f);
            sovereignLatticeControlCap = Mathf.Clamp(sovereignLatticeControlCap, 20, 50);
        }
    }

    public sealed class WNGMod : Mod
    {
        internal static WNGSettings Settings;

        public WNGMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WNGSettings>();
            Settings?.ClampValues();
            LongEventHandler.ExecuteWhenFinished(WNGSettingsUtility.ApplyRuntimeDefSettings);
        }

        public override string SettingsCategory() => "Wraith & Nanite Gravtech";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            WNGSettings settings = Settings;
            if (settings == null)
                return;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("Acquisition overrides");
            listing.CheckboxLabeled(
                "Enable universal WNG crafting",
                ref settings.enableUniversalCrafting,
                "Off by default. When enabled, WNG fallback recipes marked for universal crafting become available even when their normal faction, event, or external-mod acquisition route is unavailable. This does not change the normal default progression.");
            listing.GapLine();

            listing.Label("Wraith living technology");
            settings.livingForgeIncubationDays = DrawDaysSlider(listing, "Living Forge host incubation", settings.livingForgeIncubationDays);
            settings.livingForgeRawMeatCost = DrawIntSlider(listing, "Living Forge biomass raw-meat cost", settings.livingForgeRawMeatCost, 1, 100, 1);
            settings.livingForgeBiomassYield = DrawIntSlider(listing, "Living Forge biomass yield", settings.livingForgeBiomassYield, 1, 100, 1);
            settings.livingForgeBiomassWorkAmount = DrawIntSlider(listing, "Living Forge biomass work", settings.livingForgeBiomassWorkAmount, 100, 10000, 100);
            settings.wraithGravEngineBiomassCost = DrawIntSlider(listing, "Wraith grav-engine seed biomass cost", settings.wraithGravEngineBiomassCost, 1, 500, 1);
            settings.wraithGravEngineSeedWorkAmount = DrawIntSlider(listing, "Wraith grav-engine seed work", settings.wraithGravEngineSeedWorkAmount, 100, 20000, 100);
            settings.wraithGravEngineIncubationDays = DrawDaysSlider(listing, "Wraith grav-engine host incubation", settings.wraithGravEngineIncubationDays);
            settings.wraithLivingEquipmentMaturationDays = DrawDaysSlider(listing, "Wraith living-equipment maturation", settings.wraithLivingEquipmentMaturationDays);
            settings.wraithBioelectricPowerOutput = DrawIntSlider(listing, "Wraith bioelectric-organ power output", settings.wraithBioelectricPowerOutput, 100, 5000, 100);
            settings.wraithBioelectricBiomassPerDay = DrawFloatSlider(listing, "Wraith bioelectric-organ biomass/day", settings.wraithBioelectricBiomassPerDay, 0.5f, 60f, 0.5f);
            settings.wraithLivingPowerCellCapacity = DrawIntSlider(listing, "Wraith living-power-cell capacity", settings.wraithLivingPowerCellCapacity, 100, 5000, 50);

            listing.GapLine();
            listing.Label("Block Replicators");
            listing.CheckboxLabeled(
                "Enable Replicator storyteller events",
                ref settings.enableReplicatorStoryEvents,
                "Controls WNG storyteller-driven block-Replicator discoveries and outbreak-style events. Existing Replicators, dangerous loose Blocks, player-created Child's Toys and Queen/controller systems are not deleted or disabled by this switch.");
            settings.hostileReplicatorCap = DrawIntSlider(listing, "Maximum hostile block Replicators per map", settings.hostileReplicatorCap, 20, 300, 5);
            settings.childsToyFeralDelayDays = DrawFloatSlider(listing, "Child's Toy uncontrolled feral delay (days)", settings.childsToyFeralDelayDays, 0.1f, 5f, 0.1f);

            listing.GapLine();
            listing.Label("Replicator Queen story pacing");
            settings.replicatorQueenRecurringRecoveryDays = DrawFloatSlider(listing, "Queen recovery retry cadence", settings.replicatorQueenRecurringRecoveryDays, 1f, 10f, 0.5f);

            listing.GapLine();
            listing.Label("Sovereign Neural Lattice");
            settings.sovereignLatticeControlCap = DrawIntSlider(listing, "Maximum controlled block Replicators per implanted bearer", settings.sovereignLatticeControlCap, 20, 50, 1);

            listing.Gap();
            listing.Label("Defaults preserve the designed progression. Additional subsystem variables are routed into this settings surface as those systems are rebuilt.");
            listing.End();

            settings.ClampValues();
            WNGSettingsUtility.ApplyRuntimeDefSettings();
        }

        private static float DrawDaysSlider(Listing_Standard listing, string label, float value)
        {
            listing.Label(label + ": " + value.ToString("0.0") + " days");
            float raw = listing.Slider(value, 0.1f, 10f);
            return Mathf.Round(raw * 10f) / 10f;
        }

        private static int DrawIntSlider(Listing_Standard listing, string label, int value, int min, int max, int step)
        {
            listing.Label(label + ": " + value);
            float raw = listing.Slider(value, min, max);
            int rounded = Mathf.RoundToInt(raw / step) * step;
            return Mathf.Clamp(rounded, min, max);
        }

        private static float DrawFloatSlider(Listing_Standard listing, string label, float value, float min, float max, float step)
        {
            listing.Label(label + ": " + value.ToString("0.0"));
            float raw = listing.Slider(value, min, max);
            float rounded = Mathf.Round(raw / step) * step;
            return Mathf.Clamp(rounded, min, max);
        }
    }

    public static class WNGSettingsUtility
    {
        public const int TicksPerDay = 60000;

        public static bool UniversalCraftingEnabled => WNGMod.Settings?.enableUniversalCrafting ?? false;
        public static int LivingForgeRawMeatCost => WNGMod.Settings?.livingForgeRawMeatCost ?? 20;
        public static int LivingForgeBiomassYield => WNGMod.Settings?.livingForgeBiomassYield ?? 16;
        public static int LivingForgeBiomassWorkAmount => WNGMod.Settings?.livingForgeBiomassWorkAmount ?? 900;
        public static int WraithGravEngineBiomassCost => WNGMod.Settings?.wraithGravEngineBiomassCost ?? 120;
        public static int WraithGravEngineSeedWorkAmount => WNGMod.Settings?.wraithGravEngineSeedWorkAmount ?? 7000;
        public static int WraithGravEngineIncubationTicks => DaysToTicks(WNGMod.Settings?.wraithGravEngineIncubationDays ?? 1f);
        public static int LivingForgeIncubationTicks => DaysToTicks(WNGMod.Settings?.livingForgeIncubationDays ?? 1f);
        public static int WraithLivingEquipmentMaturationTicks => DaysToTicks(WNGMod.Settings?.wraithLivingEquipmentMaturationDays ?? 1f);
        public static int WraithBioelectricPowerOutput => WNGMod.Settings?.wraithBioelectricPowerOutput ?? 1400;
        public static float WraithBioelectricBiomassPerDay => WNGMod.Settings?.wraithBioelectricBiomassPerDay ?? 18f;
        public static int WraithLivingPowerCellCapacity => WNGMod.Settings?.wraithLivingPowerCellCapacity ?? 750;
        public static int HostileReplicatorCap => Mathf.Clamp(WNGMod.Settings?.hostileReplicatorCap ?? 120, 20, 300);
        public static int ChildsToyFeralDelayTicks => DaysToTicks(Mathf.Clamp(WNGMod.Settings?.childsToyFeralDelayDays ?? 1f, 0.1f, 5f));
        public static bool ReplicatorStoryEventsEnabled => WNGMod.Settings?.enableReplicatorStoryEvents ?? true;
        public static int ReplicatorQueenRecurringRecoveryTicks => DaysToTicks(WNGMod.Settings?.replicatorQueenRecurringRecoveryDays ?? 4f);
        public static int SovereignLatticeControlCap => Mathf.Clamp(WNGMod.Settings?.sovereignLatticeControlCap ?? 30, 20, 50);

        public static int DaysToTicks(float days)
        {
            return Math.Max(250, Mathf.RoundToInt(Mathf.Clamp(days, 0.1f, 10f) * TicksPerDay));
        }

        public static void ApplyRuntimeDefSettings()
        {
            try
            {
                RecipeDef biomassRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail("WNG_CultureBiomass");
                if (biomassRecipe != null)
                {
                    biomassRecipe.workAmount = LivingForgeBiomassWorkAmount;
                    if (biomassRecipe.ingredients != null && biomassRecipe.ingredients.Count > 0)
                        biomassRecipe.ingredients[0].SetBaseCount(LivingForgeRawMeatCost);
                    if (biomassRecipe.products != null && biomassRecipe.products.Count > 0)
                        biomassRecipe.products[0].count = LivingForgeBiomassYield;
                }

                RecipeDef engineRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail("WNG_GrowWraithGravEngineSeed");
                if (engineRecipe != null)
                {
                    engineRecipe.workAmount = WraithGravEngineSeedWorkAmount;
                    if (engineRecipe.ingredients != null && engineRecipe.ingredients.Count > 0)
                        engineRecipe.ingredients[0].SetBaseCount(WraithGravEngineBiomassCost);
                }

                ThingDef bioelectric = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_BioelectricOrgan");
                if (bioelectric?.comps != null)
                {
                    foreach (CompProperties comp in bioelectric.comps)
                    {
                        CompProperties_Power power = comp as CompProperties_Power;
                        if (power != null && power.compClass == typeof(CompPowerPlant))
                            typeof(CompProperties_Power)
                                .GetField("basePowerConsumption", BindingFlags.Instance | BindingFlags.NonPublic)
                                ?.SetValue(power, (float)-WraithBioelectricPowerOutput);
                        CompProperties_Refuelable refuel = comp as CompProperties_Refuelable;
                        if (refuel != null)
                            refuel.fuelConsumptionRate = WraithBioelectricBiomassPerDay;
                    }
                }

                ThingDef livingCell = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_LivingPowerCell");
                if (livingCell?.comps != null)
                {
                    foreach (CompProperties comp in livingCell.comps)
                    {
                        CompProperties_Battery battery = comp as CompProperties_Battery;
                        if (battery != null)
                            battery.storedEnergyMax = WraithLivingPowerCellCapacity;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[WNG] Could not apply runtime Wraith living-technology recipe settings: " + ex.Message, 0x574E4701);
            }
        }
    }

    // Recipes using this worker are deliberately hidden in normal progression and become available
    // only when the player enables the global WNG fallback-crafting option. Keep those recipes free
    // of normal research gates so the worker remains the single authority for the override path.
    public sealed class RecipeWorker_UniversalCraftOnly : RecipeWorker
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return WNGSettingsUtility.UniversalCraftingEnabled && base.AvailableOnNow(thing, part);
        }
    }
}
