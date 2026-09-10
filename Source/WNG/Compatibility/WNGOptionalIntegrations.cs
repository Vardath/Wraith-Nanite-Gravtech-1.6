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

        public static bool StargatesActive => IsPackageActive(StargatesPackageId);
        public static bool OnacActive => IsPackageActive(OnacPackageId);

        /// <summary>
        /// RimGate/Jaffa package identity must be verified from the actual installed mod before
        /// WNG hard-codes a package id. Until then detect the ecosystem conservatively by active
        /// mod metadata and, later, verified faction Defs.
        /// </summary>
        public static bool RimGateJaffaActive => LoadedModManager.RunningModsListForReading.Any(mod =>
        {
            string packageId = mod?.PackageIdPlayerFacing ?? string.Empty;
            string name = mod?.Name ?? string.Empty;
            return Contains(packageId, "rimgate") ||
                   (Contains(name, "rimgate") && Contains(name, "jaffa")) ||
                   Contains(packageId, "jaffa");
        });

        public static bool GoauldJaffaIntegrationActive => OnacActive && RimGateJaffaActive;

        private static bool IsPackageActive(string packageId)
            => !string.IsNullOrEmpty(packageId) && ModsConfig.IsActive(packageId);

        private static bool Contains(string value, string fragment)
            => value?.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
