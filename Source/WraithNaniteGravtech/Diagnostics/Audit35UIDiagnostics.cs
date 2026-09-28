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
    public static class Audit35UIDiagnostics
    {
        private static readonly string[] ArchitectCategories =
        {
            "WNG_WraithArchitect",
            "WNG_AsuranArchitect",
            "WNG_GoauldArchitect"
        };

        [DebugAction(
            "WNG",
            "Audit 35 - selected UI surface",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<object> selected = Find.Selector?.SelectedObjectsListForReading?.ToList() ??
                                    new List<object>();
            List<Thing> things = selected.OfType<Thing>()
                .Where(t => t?.def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true ||
                            t is Pawn p && p.kindDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true)
                .ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 35] SELECTED UI SURFACE");
            sb.AppendLine("Selected objects: " + selected.Count);
            sb.AppendLine("Selected WNG Things/Pawns: " + things.Count);

            int gizmoCount = 0;
            int missingLabels = 0;
            int missingDescriptions = 0;
            int disabledWithoutReason = 0;
            int missingIcons = 0;

            foreach (Thing thing in things)
            {
                sb.AppendLine();
                sb.AppendLine("OBJECT: " + thing.LabelCap + " [" + (thing.def?.defName ?? thing.kindDef?.defName ?? "?") + "]");
                IEnumerable<Gizmo> gizmos;
                try
                {
                    gizmos = thing.GetGizmos()?.ToList() ?? new List<Gizmo>();
                }
                catch (Exception ex)
                {
                    sb.AppendLine(" - GIZMO ENUMERATION THREW: " + ex.GetType().Name + ": " + ex.Message);
                    Log.Error(sb.ToString());
                    Messages.Message("WNG Audit 35 gizmo enumeration FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                foreach (Gizmo gizmo in gizmos)
                {
                    gizmoCount++;
                    if (gizmo is Command command)
                    {
                        string label = command.defaultLabel ?? string.Empty;
                        string desc = command.defaultDesc ?? string.Empty;
                        string disabledReason = command.disabledReason ?? string.Empty;

                        if (label.NullOrEmpty()) missingLabels++;
                        if (desc.NullOrEmpty()) missingDescriptions++;
                        if (command.disabled && disabledReason.NullOrEmpty()) disabledWithoutReason++;
                        if (command.icon == null) missingIcons++;

                        sb.AppendLine(
                            " - COMMAND: label='" + label + "'" +
                            ", disabled=" + command.disabled +
                            ", disabledReason='" + disabledReason + "'" +
                            ", descChars=" + desc.Length +
                            ", icon=" + (command.icon != null));
                    }
                    else
                    {
                        sb.AppendLine(" - GIZMO: " + gizmo.GetType().FullName);
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("ARCHITECT CATEGORIES:");
            foreach (string categoryName in ArchitectCategories)
            {
                DesignationCategoryDef category =
                    DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(categoryName);
                int designators = category?.AllResolvedDesignators?.Count() ?? 0;
                sb.AppendLine(" - " + categoryName + ": exists=" + (category != null) +
                              ", resolvedDesignators=" + designators);
            }

            int wngRecipes = DefDatabase<RecipeDef>.AllDefsListForReading.Count(r =>
                r?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true);
            int labeledRecipes = DefDatabase<RecipeDef>.AllDefsListForReading.Count(r =>
                r?.defName?.StartsWith("WNG_", StringComparison.Ordinal) == true &&
                !r.label.NullOrEmpty());
            sb.AppendLine();
            sb.AppendLine("WNG recipes: " + wngRecipes + "; labeled: " + labeledRecipes);
            sb.AppendLine("Gizmos inspected: " + gizmoCount);
            sb.AppendLine("Missing command labels: " + missingLabels);
            sb.AppendLine("Missing command descriptions: " + missingDescriptions);
            sb.AppendLine("Disabled commands without explanation: " + disabledWithoutReason);
            sb.AppendLine("Commands using fallback/no icon: " + missingIcons);

            bool fail =
                things.Count == 0 ||
                missingLabels > 0 ||
                missingDescriptions > 0 ||
                disabledWithoutReason > 0 ||
                labeledRecipes != wngRecipes ||
                ArchitectCategories.Any(name =>
                    DefDatabase<DesignationCategoryDef>.GetNamedSilentFail(name) == null);

            if (things.Count == 0)
                sb.AppendLine("INCOMPLETE: select one or more spawned WNG pawns/buildings/items before running this diagnostic.");

            sb.AppendLine();
            sb.AppendLine("MANUAL VISUAL CHECK REQUIRED:");
            sb.AppendLine(" - Open each reported gizmo and verify labels/descriptions fit without clipping or overlap.");
            sb.AppendLine(" - Verify disabled commands visibly explain why they are disabled.");
            sb.AppendLine(" - Open Wraith, Asuran and Goa'uld Architect tabs and confirm icons/buttons do not overlap or duplicate.");
            sb.AppendLine(" - Open WNG settings and scroll top-to-bottom; verify headings, sliders and tooltips fit.");
            sb.AppendLine(" - Open representative WNG bill menus and confirm recipe labels/buttons remain visible.");

            if (fail)
            {
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 35 FAILED/INCOMPLETE: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 35 selected UI metadata PASS; manual visual fit check still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
