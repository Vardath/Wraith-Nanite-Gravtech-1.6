using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit30OptionalModMatrixDiagnostics
    {
        private static bool PackageActive(string packageId)
        {
            return ModsConfig.ActiveModsInLoadOrder.Any(m =>
                string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase));
        }

        private static void AppendState(StringBuilder sb, string label, bool value)
        {
            sb.Append(label).Append('=').Append(value ? "ON" : "off").Append(' ');
        }

        [DebugAction(
            "WNG",
            "Audit 30 - optional mod matrix",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> wngFailures = new List<string>();
            List<string> externalWarnings = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 30] OPTIONAL MOD MATRIX");

            bool stargates = PackageActive("ccyt.stargatesmod");
            bool onac = PackageActive("idolord.onac");
            bool jaffa = PackageActive("cravemode.rimgatejaffakreebiotech");
            bool ce = PackageActive("CETeam.CombatExtended");
            bool gravTide = PackageActive("gravtide.mod");
            bool landforms = PackageActive("m00nl1ght.GeologicalLandforms");
            bool vehicles = PackageActive("smashphil.vehicleframework");
            bool vge = PackageActive("vanillaexpanded.gravship");
            bool autoNameBabies = PackageActive("cruesoe.autonamebabies");

            AppendState(sb, "Stargates", stargates);
            AppendState(sb, "ONAC", onac);
            AppendState(sb, "Jaffa/Kree", jaffa);
            AppendState(sb, "CE", ce);
            sb.AppendLine();
            AppendState(sb, "GravTide", gravTide);
            AppendState(sb, "Landforms", landforms);
            AppendState(sb, "VehicleFramework", vehicles);
            AppendState(sb, "VGE", vge);
            AppendState(sb, "AutoNameBabies", autoNameBabies);
            sb.AppendLine();

            ThingDef ceAmmo = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_CE_Ammo_WraithStunStaffCharge");
            if (ce && ceAmmo == null)
                wngFailures.Add("WNG OWNED: Combat Extended is active but WNG CE-only ammunition defs did not load.");
            if (!ce && ceAmmo != null)
                wngFailures.Add("WNG OWNED: Combat Extended is inactive but a WNG CE-only def leaked into the active DefDatabase.");
            sb.AppendLine("WNG CE compatibility defs loaded: " + (ceAmmo != null));

            ThingDef gate = DefDatabase<ThingDef>.GetNamedSilentFail("StargateMod_Stargate");
            ThingDef dhd = DefDatabase<ThingDef>.GetNamedSilentFail("StargateMod_DialHomeDevice");
            PawnsArrivalModeDef gateMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("StargateMod_StargateEnterMode");
            if (stargates && (gate == null || dhd == null || gateMode?.Worker == null))
                externalWarnings.Add("EXTERNAL CONTRACT: Stargates is active but one or more CatCraft defs/workers expected by WNG are unavailable. This may be an upstream/version mismatch; WNG gate features should fail closed.");
            if (!stargates && (gate != null || dhd != null || gateMode != null))
                wngFailures.Add("WNG OWNED: CatCraft package is absent but Stargate-owned defs are still loaded.");
            sb.AppendLine("Stargate contract: gate=" + (gate != null) + ", DHD=" + (dhd != null) + ", arrivalWorker=" + (gateMode?.Worker != null));

            try
            {
                int systemLords = GoauldOptionalInterop.ActiveSystemLordFactions().Count();
                int fullEcosystem = GoauldOptionalInterop.ActiveOnacEcosystemFactions().Count();
                if (!jaffa && systemLords != 0)
                    wngFailures.Add("WNG OWNED: Jaffa/Kree is inactive but WNG resolved System-Lord factions.");
                if ((!onac || !jaffa) && fullEcosystem != 0)
                    wngFailures.Add("WNG OWNED: full ONAC/Jaffa ecosystem resolved without both optional packages.");
                sb.AppendLine("Resolved System-Lord factions: " + systemLords);
                sb.AppendLine("Resolved full ONAC/Jaffa ecosystem factions: " + fullEcosystem);
            }
            catch (Exception ex)
            {
                wngFailures.Add("WNG OWNED: Goa'uld/Jaffa matrix probe threw " + ex.GetType().Name + ": " + ex.Message);
            }

            ThingDef onacFuel = DefDatabase<ThingDef>.GetNamedSilentFail("ONAC_LiquidNaquadria");
            ThingDef alkesh = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AlkeshTransport");
            if (onac)
            {
                if (onacFuel == null)
                {
                    externalWarnings.Add("EXTERNAL CONTRACT: ONAC is active but ONAC_LiquidNaquadria is unavailable; WNG cannot prove the current ONAC version matches its optional patch contract.");
                }
                else if (alkesh != null)
                {
                    CompProperties_Refuelable refuelable = alkesh.GetCompProperties<CompProperties_Refuelable>();
                    bool allowsOnacFuel = refuelable?.fuelFilter?.AllowedThingDefs?.Any(d => d == onacFuel) == true;
                    if (!allowsOnacFuel)
                        wngFailures.Add("WNG OWNED: ONAC and its expected fuel def are loaded, but the WNG Al'kesh did not receive the ONAC fuel patch.");
                }
            }
            else if (alkesh != null)
            {
                CompProperties_Refuelable refuelable = alkesh.GetCompProperties<CompProperties_Refuelable>();
                bool leakedOnacFuel = refuelable?.fuelFilter?.AllowedThingDefs?.Any(d =>
                    d?.defName?.StartsWith("ONAC_", StringComparison.Ordinal) == true) == true;
                if (leakedOnacFuel)
                    wngFailures.Add("WNG OWNED: ONAC is inactive but WNG Al'kesh still has an ONAC fuel reference.");
            }

            bool zAdaptive = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, "ZAdaptiveRuntime", StringComparison.OrdinalIgnoreCase));
            sb.AppendLine("Z Adaptive runtime loaded: " + zAdaptive);

            bool vehicleConflictPair = gravTide && vehicles;
            bool landformConflictPair = gravTide && landforms;
            if ((vehicleConflictPair || landformConflictPair) && !zAdaptive)
                externalWarnings.Add("STACK COMPATIBILITY: a historically conflicting GravTide combination is active without Z Adaptive Runtime. This is not classified as a WNG core failure.");
            if ((vge || autoNameBabies) && !zAdaptive)
                externalWarnings.Add("STACK COMPATIBILITY: a previously patched optional mod is active without Z Adaptive Runtime; only report a WNG fault if the resulting exception originates from WraithNaniteGravtech.");

            sb.AppendLine("Expected Z Adaptive pair guards: VehicleFramework+GravTide=" + vehicleConflictPair +
                          ", Landforms+GravTide=" + landformConflictPair);
            sb.AppendLine("Ownership rule: WNG-owned Def/package-gating failures are errors; missing/changing foreign defs/types and external cross-mod conflicts are reported separately.");

            if (externalWarnings.Count > 0)
            {
                sb.AppendLine("EXTERNAL / STACK WARNINGS:");
                foreach (string warning in externalWarnings.Distinct())
                    sb.AppendLine(" - " + warning);
            }

            if (wngFailures.Count > 0)
            {
                sb.AppendLine("WNG FAILURES:");
                foreach (string failure in wngFailures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 30 FAILED: WNG-owned matrix fault; see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: no WNG-owned optional-mod matrix fault detected in the currently loaded stack.");
                Log.Message(sb.ToString());
                Messages.Message(
                    externalWarnings.Count == 0
                        ? "WNG Audit 30 automated matrix checks PASS; combination live tests still required."
                        : "WNG Audit 30 WNG checks PASS with external/stack warnings; see Player.log.",
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }
        }
    }
}
