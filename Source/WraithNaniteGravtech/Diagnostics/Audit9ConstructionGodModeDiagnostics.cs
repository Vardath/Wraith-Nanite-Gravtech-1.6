using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit9ConstructionGodModeDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 9 - construction / God Mode",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            int buildables = 0;
            int godDesignators = 0;
            int debugSpawnables = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName == null || !def.defName.StartsWith("WNG_", StringComparison.Ordinal))
                    continue;

                if (def.forceDebugSpawnable)
                    debugSpawnables++;

                if (def.designationCategory == null)
                    continue;

                buildables++;

                if (def.costList == null || def.costList.Count == 0 || !def.costList.Any(c => c.count > 0))
                    failures.Add(def.defName + " has no positive construction cost at runtime.");

                float work = def.GetStatValueAbstract(StatDefOf.WorkToBuild);
                if (work <= 0f)
                    failures.Add(def.defName + " has non-positive runtime WorkToBuild=" + work);

                if (def.researchPrerequisites == null || def.researchPrerequisites.Count == 0)
                    failures.Add(def.defName + " has no runtime research prerequisite.");
            }

            foreach (DesignationCategoryDef category in DefDatabase<DesignationCategoryDef>.AllDefsListForReading)
            {
                if (category?.ResolvedAllowedDesignators == null)
                    continue;

                foreach (Designator designator in category.ResolvedAllowedDesignators)
                {
                    if (designator is Designator_Build_WNGGodMode god)
                    {
                        godDesignators++;
                        BuildableDef placing = god.PlacingDef;
                        if (placing == null || placing.defName == null || !placing.defName.StartsWith("WNG_", StringComparison.Ordinal))
                            failures.Add("WNG God Mode designator is attached to a non-WNG buildable.");
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 9] CONSTRUCTION / GOD MODE");
            sb.AppendLine("WNG ThingDef buildables checked: " + buildables);
            sb.AppendLine("WNG God Mode designators found: " + godDesignators);
            sb.AppendLine("WNG debug-spawnable physical defs: " + debugSpawnables);
            sb.AppendLine("Current DebugSettings.godMode: " + DebugSettings.godMode);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 9 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: runtime construction economy and God Mode routing are intact.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 9 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
