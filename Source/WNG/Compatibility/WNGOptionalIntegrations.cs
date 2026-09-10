using System;
using System.Linq;
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

        public static bool StargatesActive => IsPackageActive(StargatesPackageId);
        public static bool OnacActive => IsPackageActive(OnacPackageId);

        /// <summary>
        /// ONAC explicitly requires the separate Biotech rewrite of RimGate - Jaffa, Kree!
        /// (Steam Workshop 3762118088). Do not accept the HAR 1.6 update or the older Mlie
        /// continuation as equivalent integrations even if they can coexist in a user's mod list.
        /// </summary>
        public static bool RequiredRimGateBiotechActive => LoadedModManager.RunningModsListForReading.Any(mod =>
            string.Equals(mod?.Name?.Trim(), RequiredRimGateBiotechName, StringComparison.OrdinalIgnoreCase));

        public static bool RimGateJaffaActive => RequiredRimGateBiotechActive;
        public static bool GoauldJaffaIntegrationActive => OnacActive && RequiredRimGateBiotechActive;

        private static bool IsPackageActive(string packageId)
            => !string.IsNullOrEmpty(packageId) && ModsConfig.IsActive(packageId);
    }
}
