using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WNGSettings : ModSettings
    {
        public int maxHostileReplicators = 120;
        public bool replicatorOutbreaksEnabled = true;
        public int toyFeralDelayDays = 1;
        public float stargateEventFrequency = 1f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxHostileReplicators, "maxHostileReplicators", 120);
            Scribe_Values.Look(ref replicatorOutbreaksEnabled, "replicatorOutbreaksEnabled", true);
            Scribe_Values.Look(ref toyFeralDelayDays, "toyFeralDelayDays", 1);
            Scribe_Values.Look(ref stargateEventFrequency, "stargateEventFrequency", 1f);
        }
    }

    public sealed class WNGMod : Mod
    {
        public static WNGSettings Settings { get; private set; }

        public WNGMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WNGSettings>();
        }

        public override string SettingsCategory() => "Wraith & Nanite Gravtech";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Maximum hostile block Replicators: {Settings.maxHostileReplicators}");
            Settings.maxHostileReplicators = (int)listing.Slider(Settings.maxHostileReplicators, 20f, 300f);

            listing.CheckboxLabeled(
                "Enable Replicator outbreak incidents",
                ref Settings.replicatorOutbreaksEnabled,
                "Allows WNG Replicator outbreak incidents when their normal incident conditions are met.");

            listing.Label($"Child's Toy feral delay: {Settings.toyFeralDelayDays} day(s)");
            Settings.toyFeralDelayDays = (int)listing.Slider(Settings.toyFeralDelayDays, 0f, 30f);

            listing.Label($"Optional Stargate event frequency: {Settings.stargateEventFrequency:0.00}x");
            Settings.stargateEventFrequency = listing.Slider(Settings.stargateEventFrequency, 0f, 3f);

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
