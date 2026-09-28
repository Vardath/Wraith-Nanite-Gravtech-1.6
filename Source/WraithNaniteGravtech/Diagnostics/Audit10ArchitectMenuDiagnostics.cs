using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit10ArchitectMenuDiagnostics
    {
        private static string ExpectedFamily(BuildableDef def)
        {
            string name = (def?.defName ?? string.Empty).ToLowerInvariant();
            string art = string.Empty;
            if (def is ThingDef thing)
                art = (thing.graphicData?.texPath ?? thing.uiIconPath ?? string.Empty).ToLowerInvariant();
            else if (def is TerrainDef terrain)
                art = (terrain.texturePath ?? string.Empty).ToLowerInvariant();

            string blob = name + " " + art;
            if (blob.Contains("goauld") || blob.Contains("alkesh") || blob.Contains("/goauld/") || blob.Contains("terrain/goauld/"))
                return "WNG_GoauldArchitect";
            if (blob.Contains("wraith") || blob.Contains("hive") || blob.Contains("living") || blob.Contains("bioelectric") ||
                blob.Contains("/wraith/") || blob.Contains("terrain/wraith/"))
                return "WNG_WraithArchitect";
            if (blob.Contains("asuran") || blob.Contains("precursor") || blob.Contains("humanform") ||
                blob.Contains("/precursor/") || blob.Contains("terrain/precursor/"))
                return "WNG_AsuranArchitect";
            return null;
        }

        [DebugAction(
            "WNG",
            "Audit 10 - Architect menu",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            List<string> notes = new List<string>();
            string[] familyNames =
            {
                "WNG_WraithArchitect",
                "WNG_AsuranArchitect",
                "WNG_GoauldArchitect"
            };

            Dictionary<string, DesignationCategoryDef> families = new Dictionary<string, DesignationCategoryDef>();
            foreach (string name in familyNames)
            {
                DesignationCategoryDef cat = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(name);
                if (cat == null)
                    failures.Add("Missing runtime family Architect category " + name);
                else
                    families[name] = cat;
            }

            List<BuildableDef> buildables = new List<BuildableDef>();
            buildables.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                .Where(d => d?.defName != null && d.defName.StartsWith("WNG_", StringComparison.Ordinal) && d.designationCategory != null));
            buildables.AddRange(DefDatabase<TerrainDef>.AllDefsListForReading
                .Where(d => d?.defName != null && d.defName.StartsWith("WNG_", StringComparison.Ordinal) && d.designationCategory != null));

            foreach (BuildableDef def in buildables)
            {
                if (def.designationCategory?.defName != null &&
                    familyNames.Contains(def.designationCategory.defName))
                {
                    failures.Add(def.defName + " is destructively assigned to family category " + def.designationCategory.defName);
                }

                string expected = ExpectedFamily(def);
                if (expected == null || !families.TryGetValue(expected, out DesignationCategoryDef family))
                    continue;

                List<Designator_Build> entries = family.ResolvedAllowedDesignators
                    .OfType<Designator_Build>()
                    .Where(d => d.PlacingDef == def)
                    .ToList();

                if (entries.Count == 0)
                    failures.Add(def.defName + " missing from expected family tab " + expected);
                if (entries.Count > 1)
                    failures.Add(def.defName + " appears " + entries.Count + " times in family tab " + expected);
            }

            foreach (DesignationCategoryDef category in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                if (category?.ResolvedAllowedDesignators == null)
                    continue;

                var dupes = category.ResolvedAllowedDesignators
                    .OfType<Designator_Build>()
                    .Where(d => d.PlacingDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                    .GroupBy(d => d.PlacingDef)
                    .Where(g => g.Count() > 1);

                foreach (var dup in dupes)
                    failures.Add(category.defName + " contains duplicate WNG designators for " + dup.Key.defName);
            }

            bool originalGod = DebugSettings.godMode;
            try
            {
                DebugSettings.godMode = false;
                int normalVisible = 0;
                int normalTotal = 0;
                foreach (DesignationCategoryDef category in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
                {
                    if (category?.ResolvedAllowedDesignators == null) continue;
                    foreach (Designator_Build_WNGGodMode d in category.ResolvedAllowedDesignators.OfType<Designator_Build_WNGGodMode>())
                    {
                        normalTotal++;
                        if (d.Visible) normalVisible++;
                    }
                }

                DebugSettings.godMode = true;
                int godVisible = 0;
                int godTotal = 0;
                foreach (DesignationCategoryDef category in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
                {
                    if (category?.ResolvedAllowedDesignators == null) continue;
                    foreach (Designator_Build_WNGGodMode d in category.ResolvedAllowedDesignators.OfType<Designator_Build_WNGGodMode>())
                    {
                        godTotal++;
                        if (d.Visible) godVisible++;
                    }
                }

                if (godVisible != godTotal)
                    failures.Add("God Mode does not expose every WNG build designator (" + godVisible + "/" + godTotal + ").");

                notes.Add("Normal mode visible WNG designators: " + normalVisible + "/" + normalTotal);
                notes.Add("God Mode visible WNG designators: " + godVisible + "/" + godTotal);
            }
            finally
            {
                DebugSettings.godMode = originalGod;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 10] ARCHITECT MENU");
            sb.AppendLine("WNG buildables checked: " + buildables.Count);
            foreach (string note in notes) sb.AppendLine(" - " + note);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 10 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: native categories, family tabs, duplicate suppression and God Mode visibility are intact.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 10 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
