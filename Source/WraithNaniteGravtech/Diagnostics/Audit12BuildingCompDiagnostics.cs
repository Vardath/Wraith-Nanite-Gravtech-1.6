using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit12BuildingCompDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 12 - building comps",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            int buildings = 0;
            int instantiated = 0;
            int customComps = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                if (def.category != ThingCategory.Building)
                    continue;

                buildings++;

                if (def.comps != null)
                {
                    var duplicates = def.comps
                        .Where(c => c != null)
                        .GroupBy(c => c.compClass ?? c.GetType())
                        .Where(g => g.Count() > 1 &&
                                    !g.Any(c => c is CompProperties_AffectedByFacilities) &&
                                    !g.Any(c => c is CompProperties_WNGGravshipFacility));
                    foreach (var dup in duplicates)
                        failures.Add(def.defName + " has duplicate effective comp class " + dup.Key.FullName + " x" + dup.Count());

                    foreach (CompProperties props in def.comps)
                    {
                        if (props == null) continue;
                        if (props.GetType().Namespace?.StartsWith("WraithNaniteGravtech", StringComparison.Ordinal) == true)
                        {
                            customComps++;
                            if (props.compClass == null)
                                failures.Add(def.defName + " custom comp properties " + props.GetType().FullName + " has null compClass.");
                            else if (!typeof(ThingComp).IsAssignableFrom(props.compClass))
                                failures.Add(def.defName + " custom comp properties " + props.GetType().FullName +
                                             " resolves to non-ThingComp " + props.compClass.FullName);
                        }

                        if (props is CompProperties_Refuelable fuel && fuel.fuelCapacity <= 0f)
                            failures.Add(def.defName + " has refuelable comp with non-positive capacity.");
                    }
                }

                try
                {
                    ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                    Thing thing = ThingMaker.MakeThing(def, stuff);
                    instantiated++;
                    if (thing is ThingWithComps twc && def.comps != null)
                    {
                        foreach (CompProperties props in def.comps.Where(p => p != null && p.compClass != null))
                        {
                            if (twc.AllComps == null || !twc.AllComps.Any(c => props.compClass.IsAssignableFrom(c.GetType())))
                                failures.Add(def.defName + " failed to instantiate comp " + props.compClass.FullName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(def.defName + " ThingMaker.MakeThing threw " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 12] BUILDING COMPS");
            sb.AppendLine("WNG building defs checked: " + buildings);
            sb.AppendLine("Buildings instantiated: " + instantiated);
            sb.AppendLine("Custom comp-properties instances checked: " + customComps);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 12 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: building comp stacks instantiate cleanly.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 12 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
