using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit29RemovalSafetyDiagnostics
    {
        private static bool PackageActive(string packageId)
        {
            return ModsConfig.ActiveModsInLoadOrder.Any(m =>
                string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase));
        }

        [DebugAction(
            "WNG",
            "Audit 29 - optional mod removal safety",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 29] OPTIONAL MOD REMOVAL SAFETY");

            bool stargates = PackageActive("ccyt.stargatesmod");
            bool onac = PackageActive("idolord.onac");
            bool jaffa = PackageActive("cravemode.rimgatejaffakreebiotech");
            bool ce = PackageActive("CETeam.CombatExtended");

            sb.AppendLine("Stargates active: " + stargates);
            sb.AppendLine("ONAC active: " + onac);
            sb.AppendLine("RimGate Jaffa/Kree active: " + jaffa);
            sb.AppendLine("Combat Extended active: " + ce);

            try
            {
                ThingDef gate = DefDatabase<ThingDef>.GetNamedSilentFail("StargateMod_Stargate");
                ThingDef dhd = DefDatabase<ThingDef>.GetNamedSilentFail("StargateMod_DialHomeDevice");
                PawnsArrivalModeDef mode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail("StargateMod_StargateEnterMode");
                if (!stargates && (gate != null || dhd != null || mode != null))
                    failures.Add("CatCraft package is absent but Stargate-owned defs are still loaded.");
                sb.AppendLine("Stargate defs: gate=" + (gate != null) + ", DHD=" + (dhd != null) + ", arrivalMode=" + (mode != null));
            }
            catch (Exception ex)
            {
                failures.Add("Silent Stargate-def probe threw " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                List<Faction> systemLords = GoauldOptionalInterop.ActiveSystemLordFactions().ToList();
                List<Faction> ecosystem = GoauldOptionalInterop.ActiveOnacEcosystemFactions().ToList();
                if (!jaffa && systemLords.Count != 0)
                    failures.Add("RimGate Jaffa/Kree is absent but WNG still resolved System-Lord factions.");
                if ((!onac || !jaffa) && ecosystem.Count != 0)
                    failures.Add("Full ONAC/Jaffa ecosystem is incomplete but WNG returned ecosystem factions.");
                sb.AppendLine("Resolved System-Lord factions: " + systemLords.Count);
                sb.AppendLine("Resolved full ONAC/Jaffa ecosystem factions: " + ecosystem.Count);
            }
            catch (Exception ex)
            {
                failures.Add("Optional Goa'uld/Jaffa interop probe threw " + ex.GetType().Name + ": " + ex.Message);
            }

            try
            {
                sb.AppendLine("Optional Asgard providers registered: " +
                    WNGAsgardCompatibility.RegisteredProviderIds.Count);
            }
            catch (Exception ex)
            {
                failures.Add("Optional provider registry probe threw " + ex.GetType().Name + ": " + ex.Message);
            }

            sb.AppendLine("This action is read-only. Actual removal from an existing save must be tested by removing one optional mod at a time from a COPY of the save.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 29 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: currently absent/present optional integrations resolve safely.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 29 automated checks PASS; save-removal test still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
