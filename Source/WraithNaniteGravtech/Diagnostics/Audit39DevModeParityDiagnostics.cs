using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit39DevModeParityDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 39 - dev mode parity",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            int buildables = 0;
            int positiveCosts = 0;
            int positiveWork = 0;
            int researchGated = 0;
            int debugSpawnablePhysical = 0;
            int physicalWngDefs = 0;
            int godDesignators = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;

                if (def.category == ThingCategory.Item || def.category == ThingCategory.Building)
                {
                    physicalWngDefs++;
                    if (def.forceDebugSpawnable)
                        debugSpawnablePhysical++;
                    else
                        failures.Add(def.defName + " is physical WNG content but is not direct-debug-spawnable.");
                }

                if (def.designationCategory == null)
                    continue;

                buildables++;
                if (def.costList != null && def.costList.Any(c => c != null && c.count > 0))
                    positiveCosts++;
                else
                    failures.Add(def.defName + " has no positive runtime construction cost.");

                float work = def.GetStatValueAbstract(StatDefOf.WorkToBuild);
                if (work > 0f)
                    positiveWork++;
                else
                    failures.Add(def.defName + " has non-positive WorkToBuild=" + work);

                if (def.researchPrerequisites != null && def.researchPrerequisites.Count > 0)
                    researchGated++;
                else
                    failures.Add(def.defName + " has no runtime researchPrerequisites.");
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
                        if (god.PlacingDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                            failures.Add("A WNG God Mode designator targets non-WNG content.");
                    }
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 39] DEV-MODE PARITY");
            sb.AppendLine("Current God Mode: " + DebugSettings.godMode);
            sb.AppendLine("WNG physical ThingDefs: " + physicalWngDefs);
            sb.AppendLine("Direct-debug-spawnable physical WNG ThingDefs: " + debugSpawnablePhysical);
            sb.AppendLine("WNG Architect ThingDef buildables: " + buildables);
            sb.AppendLine("Buildables retaining positive costList: " + positiveCosts);
            sb.AppendLine("Buildables retaining positive WorkToBuild: " + positiveWork);
            sb.AppendLine("Buildables retaining research prerequisites: " + researchGated);
            sb.AppendLine("WNG God-Mode-aware Designator_Build instances: " + godDesignators);
            sb.AppendLine();
            sb.AppendLine("MODE CONTRACT:");
            sb.AppendLine(" - Normal play: Architect placement must create ordinary blueprint/frame construction, consume resources/work and respect research.");
            sb.AppendLine(" - Dev Mode with God Mode OFF: physical WNG defs may be directly spawned from developer spawn tools, but Architect placement remains ordinary construction.");
            sb.AppendLine(" - God Mode ON: WNG Architect designators may directly finish placement with no resource/work cost, exactly as intentional developer convenience.");
            sb.AppendLine(" - Turning God Mode back OFF must immediately restore ordinary construction without mutating any Def cost/work/research data.");

            if (physicalWngDefs != debugSpawnablePhysical)
                failures.Add("Not all physical WNG ThingDefs are available to direct developer spawn.");
            if (godDesignators == 0)
                failures.Add("No WNG God-Mode-aware build designators resolved.");

            sb.AppendLine();
            sb.AppendLine("LIVE THREE-MODE CHECK REQUIRED:");
            sb.AppendLine(" 1. God Mode OFF: place a representative Wraith, Asuran, Goa'uld and gravship buildable through Architect; each must create a blueprint/frame and require its displayed materials/work/research.");
            sb.AppendLine(" 2. Dev Mode ON but God Mode OFF: use direct developer spawn to spawn representative WNG items/buildings; then place the same Architect buildables normally and confirm they still create costed blueprints/frames.");
            sb.AppendLine(" 3. God Mode ON: place the same representatives through Architect and confirm immediate finished placement without material/work cost.");
            sb.AppendLine(" 4. Turn God Mode OFF again: repeat one placement and confirm the blueprint/frame/cost path returns immediately.");

            if (failures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 39 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("PASS: runtime Defs retain normal economy/research while direct debug spawning and explicit God Mode remain separately available.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 39 structural runtime PASS; three-mode placement test still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
