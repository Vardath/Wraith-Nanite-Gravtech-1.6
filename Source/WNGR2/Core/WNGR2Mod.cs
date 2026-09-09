using UnityEngine;
using Verse;

namespace WNGR2
{
    public sealed class WNGR2Settings : ModSettings
    {
        public int maxHostileReplicatorsPerMap = 120;
        public bool replicatorOutbreaksEnabled = true;
        public float replicatorFeralDelayDays = 1f;
        public float stargateIncursionFrequency = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxHostileReplicatorsPerMap, "maxHostileReplicatorsPerMap", 120);
            Scribe_Values.Look(ref replicatorOutbreaksEnabled, "replicatorOutbreaksEnabled", true);
            Scribe_Values.Look(ref replicatorFeralDelayDays, "replicatorFeralDelayDays", 1f);
            Scribe_Values.Look(ref stargateIncursionFrequency, "stargateIncursionFrequency", 1f);
            base.ExposeData();
        }
    }

    public sealed class WNGR2Mod : Mod
    {
        public static WNGR2Settings Settings { get; private set; }

        public WNGR2Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WNGR2Settings>();
        }

        public override string SettingsCategory() => "Wraith & Nanite Gravtech";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Maximum hostile block Replicators per map: {Settings.maxHostileReplicatorsPerMap}");
            Settings.maxHostileReplicatorsPerMap = Mathf.RoundToInt(listing.Slider(Settings.maxHostileReplicatorsPerMap, 20, 500));

            listing.Label($"Uncontrolled Child's Toy feral delay: {Settings.replicatorFeralDelayDays:0.0} day(s)");
            Settings.replicatorFeralDelayDays = listing.Slider(Settings.replicatorFeralDelayDays, 0.1f, 5f);

            listing.CheckboxLabeled("Enable storyteller Replicator outbreaks", ref Settings.replicatorOutbreaksEnabled,
                "If disabled, the storyteller will not begin new Replicator outbreaks. Existing Replicators and player-built Replicators are unaffected.");

            listing.Label($"Optional Stargate incursion frequency: {Settings.stargateIncursionFrequency:0.00}x");
            Settings.stargateIncursionFrequency = listing.Slider(Settings.stargateIncursionFrequency, 0f, 2f);

            listing.End();
        }
    }
}

namespace WraithNaniteGravtech
{
    internal static class WNG_Config
    {
        public static float WraithRegenerationMultiplier => 1f;
        public static float NaniteRegenerationMultiplier => 1f;
        public static int MaxHostileReplicatorsPerMap => WNGR2.WNGR2Mod.Settings?.maxHostileReplicatorsPerMap ?? 120;
        public static bool ReplicatorOutbreaksEnabled => WNGR2.WNGR2Mod.Settings?.replicatorOutbreaksEnabled ?? true;
        public static int ReplicatorFeralDelayTicks => Mathf.Max(6000, Mathf.RoundToInt((WNGR2.WNGR2Mod.Settings?.replicatorFeralDelayDays ?? 1f) * 60000f));
        public static float StargateIncursionFrequency => Mathf.Clamp(WNGR2.WNGR2Mod.Settings?.stargateIncursionFrequency ?? 1f, 0f, 2f);
    }
}
