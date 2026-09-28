using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit27WorldgenDiagnostics
    {
        private static bool ModActive(string packageId)
        {
            return LoadedModManager.RunningModsListForReading.Any(
                m => string.Equals(m.PackageIdPlayerFacing, packageId, StringComparison.OrdinalIgnoreCase));
        }

        [DebugAction(
            "WNG",
            "Audit 27 - worldgen",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 27] WORLDGEN");

            string[] scenarios =
            {
                "WNG_WraithLandfall",
                "WNG_WraithOrbit",
                "WNG_HumanFormReplicatorLandfall",
                "WNG_HumanFormReplicatorOrbit"
            };
            foreach (string name in scenarios)
                if (DefDatabase<ScenarioDef>.GetNamedSilentFail(name) == null)
                    failures.Add("Missing ScenarioDef " + name);

            List<SitePartDef> siteParts = DefDatabase<SitePartDef>.AllDefsListForReading
                .Where(d => d?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                .ToList();
            foreach (SitePartDef part in siteParts)
                if (part.Worker == null)
                    failures.Add(part.defName + " has no runtime SitePart worker.");

            Map map = Find.CurrentMap;
            if (map == null)
            {
                failures.Add("No current map.");
            }
            else
            {
                sb.AppendLine("Current map tile: " + map.Tile);
                sb.AppendLine("Current biome: " + (map.Biome?.defName ?? "<none>"));
                sb.AppendLine("Current map parent: " + (map.Parent?.def?.defName ?? "<none>"));
                sb.AppendLine("Spawned WNG grav engines: " +
                    map.listerThings.AllThings.Count(t =>
                        t?.def?.defName == "WNG_WraithGravEngine" ||
                        t?.def?.defName == "WNG_AsuranGravEngine" ||
                        t?.def?.defName == "WNG_GoauldGravEngine"));
            }

            int wngSites = Find.WorldObjects?.AllWorldObjects
                ?.OfType<Site>()
                .Count(s => s?.parts != null && s.parts.Any(p =>
                    p?.def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)) ?? 0;
            sb.AppendLine("Loaded WNG SitePartDefs: " + siteParts.Count);
            sb.AppendLine("Current WNG world sites: " + wngSites);

            bool gravTide = ModActive("gravtide.mod");
            bool landforms = ModActive("m00nl1ght.GeologicalLandforms");
            bool vehicles = ModActive("smashphil.vehicleframework");
            bool onac = ModActive("idolord.onac");
            sb.AppendLine("Known compatibility mods active: GravTide=" + gravTide +
                          ", GeologicalLandforms=" + landforms +
                          ", VehicleFramework=" + vehicles +
                          ", ONAC=" + onac);

            bool zAdaptive = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, "ZAdaptiveRuntime", StringComparison.OrdinalIgnoreCase));
            sb.AppendLine("Z Adaptive runtime loaded: " + zAdaptive);
            if ((gravTide && landforms || gravTide && vehicles) && !zAdaptive)
                sb.AppendLine("WARNING: this stack contains a known worldgen compatibility combination but Z Adaptive Runtime is not loaded.");

            sb.AppendLine("Read-only probe: actual new-world/new-map generation remains a user-side test.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 27 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded WNG scenario/site/worldgen entry points resolve.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 27 automated checks PASS; new-world generation test pending.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
