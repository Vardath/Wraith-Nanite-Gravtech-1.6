using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit18TextureGraphicDiagnostics
    {
        private static bool IsWngPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith("UI/WNG/", StringComparison.Ordinal) ||
                    path.IndexOf("WNG_", StringComparison.Ordinal) >= 0 ||
                    path.StartsWith("WraithNaniteGravtech/", StringComparison.Ordinal));
        }

        [DebugAction(
            "WNG",
            "Audit 18 - texture / graphic families",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            int defs = 0;
            int graphics = 0;
            int icons = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;

                defs++;

                if (def.graphicData != null && IsWngPath(def.graphicData.texPath))
                {
                    try
                    {
                        Graphic graphic = def.graphicData.Graphic;
                        graphics++;
                        if (graphic == null || graphic.MatSingle == null)
                            failures.Add(def.defName + " resolved a null graphic/material for " + def.graphicData.texPath);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(def.defName + " graphic load threw " + ex.GetType().Name + ": " + ex.Message);
                    }
                }

                if (IsWngPath(def.uiIconPath))
                {
                    try
                    {
                        Texture2D icon = ContentFinder<Texture2D>.Get(def.uiIconPath, false);
                        icons++;
                        if (icon == null)
                            failures.Add(def.defName + " could not load uiIconPath " + def.uiIconPath);
                    }
                    catch (Exception ex)
                    {
                        failures.Add(def.defName + " UI icon load threw " + ex.GetType().Name + ": " + ex.Message);
                    }
                }
            }

            sb.AppendLine("[WNG AUDIT 18] TEXTURE / GRAPHIC FAMILIES");
            sb.AppendLine("WNG ThingDefs visited: " + defs);
            sb.AppendLine("Local graphics resolved: " + graphics);
            sb.AppendLine("Explicit UI icons resolved: " + icons);
            sb.AppendLine("Static D149 separately verifies directional files, worn graphics, masks, linked atlases and literal C# icon paths.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 18 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded WNG graphics and explicit UI icons resolve through RimWorld.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 18 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
