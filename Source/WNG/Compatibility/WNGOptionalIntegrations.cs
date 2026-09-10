using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Dependency-free optional integration detection. WNG must remain loadable and playable
    /// without any third-party Stargate/Goa'uld/Jaffa mods installed.
    /// </summary>
    public static class WNGOptionalIntegrations
    {
        public const string StargatesPackageId = "ccyt.stargatesmod";
        public const string OnacPackageId = "idolord.onac";

        // Steam Workshop identities are documentation/validation anchors. RimWorld's active-mod
        // runtime API does not expose Workshop IDs directly in a stable cross-source way, so the
        // RimGate side is identified by the exact published Biotech title until its packageId is
        // verified from the actual mod's About.xml.
        public const ulong OnacWorkshopId = 3775612635UL;
        public const ulong RequiredRimGateBiotechWorkshopId = 3762118088UL;
        public const ulong WrongHarRimGate16WorkshopId = 3759441429UL;
        public const ulong WrongMlieContinuedWorkshopId = 2395671127UL;
        public const string RequiredRimGateBiotechName = "RimGate - Jaffa, Kree! (Biotech)";

        private static readonly string[] SystemLordTokens = { "apophis", "ra", "anubis" };

        public static bool StargatesActive => IsPackageActive(StargatesPackageId);
        public static bool OnacActive => IsPackageActive(OnacPackageId);

        /// <summary>
        /// ONAC explicitly requires the separate Biotech rewrite of RimGate - Jaffa, Kree!
        /// (Steam Workshop 3762118088). Do not accept the HAR 1.6 update or the older Mlie
        /// continuation as equivalent integrations even if they can coexist in a user's mod list.
        /// </summary>
        public static bool RequiredRimGateBiotechActive => FindRequiredRimGateContentPack() != null;

        public static bool RimGateJaffaActive => RequiredRimGateBiotechActive;
        public static bool GoauldJaffaIntegrationActive => OnacActive && RequiredRimGateBiotechActive;

        /// <summary>
        /// Resolve only faction Defs originating from the verified Biotech RimGate content pack.
        /// This intentionally avoids guessing the rewrite's internal DefNames. The Workshop page
        /// documents three hidden System Lord factions: Apophis, Ra and Anubis.
        /// </summary>
        public static IReadOnlyList<FactionDef> ResolveRimGateSystemLordFactionDefs()
        {
            ModContentPack pack = FindRequiredRimGateContentPack();
            if (pack == null)
                return Array.Empty<FactionDef>();

            return DefDatabase<FactionDef>.AllDefsListForReading
                .Where(def => def != null && def.modContentPack == pack)
                .Where(def => SystemLordTokens.Any(token => MatchesIdentity(def, token)))
                .Distinct()
                .ToList();
        }

        public static bool IsVerifiedRimGateSystemLordFaction(Faction faction)
            => faction?.def != null && ResolveRimGateSystemLordFactionDefs().Contains(faction.def);

        private static ModContentPack FindRequiredRimGateContentPack()
            => LoadedModManager.RunningModsListForReading.FirstOrDefault(mod =>
                string.Equals(mod?.Name?.Trim(), RequiredRimGateBiotechName, StringComparison.OrdinalIgnoreCase));

        private static bool MatchesIdentity(FactionDef def, string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            return Contains(def.defName, token) ||
                   Contains(def.label, token) ||
                   Contains(def.description, token);
        }

        private static bool IsPackageActive(string packageId)
            => !string.IsNullOrEmpty(packageId) && ModsConfig.IsActive(packageId);

        private static bool Contains(string value, string token)
            => !string.IsNullOrEmpty(value) && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
