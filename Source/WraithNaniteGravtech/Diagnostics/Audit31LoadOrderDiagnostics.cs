using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit31LoadOrderDiagnostics
    {
        private const string WngPackageId = "vardath.wraithnanitegravtech";
        private const string ZAdaptivePackageId = "vardath.adaptiveerrorpatch";

        private static int IndexOf(string packageId)
        {
            List<ModMetaData> mods = ModsConfig.ActiveModsInLoadOrder;
            for (int i = 0; i < mods.Count; i++)
            {
                ModMetaData mod = mods[i];
                if (string.Equals(mod?.PackageId, packageId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mod?.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string At(int index)
        {
            if (index < 0 || index >= ModsConfig.ActiveModsInLoadOrder.Count)
                return "<none>";
            ModMetaData mod = ModsConfig.ActiveModsInLoadOrder[index];
            return (mod?.Name ?? "<unnamed>") + " [" + (mod?.PackageIdPlayerFacing ?? mod?.PackageId ?? "?") + "]";
        }

        [DebugAction(
            "WNG",
            "Audit 31 - load order",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            List<string> warnings = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 31] LOAD ORDER");

            int wng = IndexOf(WngPackageId);
            if (wng < 0)
                failures.Add("WNG package could not be located in the active load order.");
            else
            {
                sb.AppendLine("WNG index: " + wng + " / " + (ModsConfig.ActiveModsInLoadOrder.Count - 1));
                sb.AppendLine("Immediately before WNG: " + At(wng - 1));
                sb.AppendLine("Immediately after WNG: " + At(wng + 1));
            }

            string[] directProviders =
            {
                "ccyt.stargatesmod",
                "idolord.onac",
                "cravemode.rimgatejaffakreebiotech",
                "CETeam.CombatExtended"
            };
            foreach (string pkg in directProviders)
            {
                int index = IndexOf(pkg);
                if (index < 0)
                {
                    sb.AppendLine(pkg + ": inactive");
                    continue;
                }

                sb.AppendLine(pkg + ": index " + index);
                if (wng >= 0 && index > wng)
                    warnings.Add(pkg + " is loaded after WNG even though WNG declares loadAfter for it. Test its integration carefully; user load-order override may be in effect.");
            }

            int zAdaptive = IndexOf(ZAdaptivePackageId);
            sb.AppendLine("Z Adaptive index: " + (zAdaptive < 0 ? "inactive" : zAdaptive.ToString()));

            string[] zProviders =
            {
                "vanillaexpanded.gravship",
                "smashphil.vehicleframework",
                "gravtide.mod",
                "m00nl1ght.GeologicalLandforms",
                "cruesoe.autonamebabies"
            };
            foreach (string pkg in zProviders)
            {
                int index = IndexOf(pkg);
                if (index < 0)
                    continue;
                sb.AppendLine("Z target " + pkg + ": index " + index);
                if (zAdaptive >= 0 && index > zAdaptive)
                    warnings.Add(pkg + " is loaded after Z Adaptive despite Z Adaptive declaring loadAfter for it; its Harmony arbitration may not represent the intended stack order.");
            }

            bool ce = IndexOf("CETeam.CombatExtended") >= 0;
            ThingDef ceProbe = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_CE_Ammo_WraithStunStaffCharge");
            if (ce != (ceProbe != null))
                failures.Add("CE load-folder result does not match the active CE package state.");

            bool stargates = IndexOf("ccyt.stargatesmod") >= 0;
            ThingDef gate = DefDatabase<ThingDef>.GetNamedSilentFail("StargateMod_Stargate");
            if (!stargates && gate != null)
                failures.Add("Stargates package is inactive but a CatCraft gate def is still loaded.");

            try
            {
                bool onac = IndexOf("idolord.onac") >= 0;
                bool jaffa = IndexOf("cravemode.rimgatejaffakreebiotech") >= 0;
                if (GoauldOptionalInterop.OnacLoaded() != onac)
                    failures.Add("ONAC runtime package detection disagrees with active load order.");
                if (GoauldOptionalInterop.RimGateJaffaLoaded() != jaffa)
                    failures.Add("RimGate/Jaffa runtime package detection disagrees with active load order.");
            }
            catch (Exception ex)
            {
                failures.Add("Optional integration load-order probe threw " + ex.GetType().Name + ": " + ex.Message);
            }

            sb.AppendLine("Read-only probe: actual order permutations require a full RimWorld restart because XML patches and static constructors are established during startup.");

            if (warnings.Count > 0)
            {
                sb.AppendLine("LOAD-ORDER WARNINGS:");
                foreach (string warning in warnings.Distinct())
                    sb.AppendLine(" - " + warning);
            }

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 31 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: current loaded order has no WNG-owned package/Def-state inconsistency.");
                Log.Message(sb.ToString());
                Messages.Message(
                    warnings.Count == 0
                        ? "WNG Audit 31 automated checks PASS; restart order permutations still pending."
                        : "WNG Audit 31 WNG checks PASS with load-order warnings; see Player.log.",
                    MessageTypeDefOf.NeutralEvent,
                    false);
            }
        }
    }
}
