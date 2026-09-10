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
    /// Exact identities below were verified from the user-supplied Workshop archives.
    /// </summary>
    public static class WNGOptionalIntegrations
    {
        public const string StargatesPackageId = "ccyt.stargatesmod";
        public const string OnacPackageId = "idolord.ONAC";
        public const string RimGateBiotechPackageId = "CraveMode.RimGateJaffaKreeBiotech";

        public const ulong StargatesWorkshopId = 2831698056UL;
        public const ulong OnacWorkshopId = 3775612635UL;
        public const ulong RequiredRimGateBiotechWorkshopId = 3762118088UL;
        public const ulong HumanoidAlienRacesWorkshopId = 839005762UL;
        public const ulong WrongHarRimGate16WorkshopId = 3759441429UL;
        public const ulong WrongMlieContinuedWorkshopId = 2395671127UL;

        public const string RequiredRimGateBiotechName = "RimGate - Jaffa, Kree! (Biotech)";

        // Verified RimGate Biotech Def identities.
        public const string RimGateJaffaXenotypeDefName = "JKB_Jaffa";
        public const string RimGateFirstPrimeXenotypeDefName = "JKB_JaffaFirstPrime";
        public const string RimGateApophisFactionDefName = "JKB_JaffaApophis";
        public const string RimGateAnubisFactionDefName = "JKB_JaffaAnubis";
        public const string RimGateRaFactionDefName = "JKB_JaffaRa";

        // Verified ONAC integration surface.
        public const string OnacArchitectCategoryDefName = "ONAC_Architect";
        public const string OnacNaquadahDefName = "ONAC_Naquadah";
        public const string OnacLiquidNaquadriaDefName = "ONAC_LiquidNaquadria";
        public const string OnacMakeLiquidNaquadriaRecipeDefName = "ONAC_MakeLiquidNaquadria";
        public const string OnacGoauldFoundryResearchDefName = "ONAC_GoauldFoundryResearch";
        public const string OnacNaquadahLiquefactionResearchDefName = "ONAC_NaquadahLiquefaction";
        public const string OnacGoauldResearchBenchDefName = "ONAC_GoauldResearchBench";

        public static bool StargatesActive => IsPackageActive(StargatesPackageId);
        public static bool OnacActive => IsPackageActive(OnacPackageId);
        public static bool RequiredRimGateBiotechActive => IsPackageActive(RimGateBiotechPackageId);
        public static bool RimGateJaffaActive => RequiredRimGateBiotechActive;
        public static bool GoauldJaffaIntegrationActive => OnacActive && RequiredRimGateBiotechActive;

        /// <summary>
        /// Resolve only the three verified System Lord faction Defs from the Biotech rewrite.
        /// No fuzzy name/description matching is used, so older HAR RimGate variants cannot be
        /// accidentally treated as ONAC's required dependency.
        /// </summary>
        public static IReadOnlyList<FactionDef> ResolveRimGateSystemLordFactionDefs()
        {
            if (!RequiredRimGateBiotechActive)
                return Array.Empty<FactionDef>();

            string[] names =
            {
                RimGateApophisFactionDefName,
                RimGateAnubisFactionDefName,
                RimGateRaFactionDefName
            };

            return names
                .Select(DefDatabase<FactionDef>.GetNamedSilentFail)
                .Where(def => def != null &&
                              def.modContentPack != null &&
                              string.Equals(def.modContentPack.PackageIdPlayerFacing,
                                  RimGateBiotechPackageId,
                                  StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();
        }

        public static bool IsVerifiedRimGateSystemLordFaction(Faction faction)
            => faction?.def != null && ResolveRimGateSystemLordFactionDefs().Contains(faction.def);

        public static DesignationCategoryDef ResolveOnacArchitectCategory()
            => OnacActive ? DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(OnacArchitectCategoryDefName) : null;

        public static ThingDef ResolveOnacLiquidNaquadria()
            => GoauldJaffaIntegrationActive ? DefDatabase<ThingDef>.GetNamedSilentFail(OnacLiquidNaquadriaDefName) : null;

        public static ResearchProjectDef ResolveOnacNaquadahLiquefactionResearch()
            => GoauldJaffaIntegrationActive ? DefDatabase<ResearchProjectDef>.GetNamedSilentFail(OnacNaquadahLiquefactionResearchDefName) : null;

        private static bool IsPackageActive(string packageId)
            => !string.IsNullOrEmpty(packageId) && ModsConfig.IsActive(packageId);
    }
}
