using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit13GravshipConnectivityDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 13 - gravship connectivity",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();

            string[] engines =
            {
                "WNG_WraithGravEngine",
                "WNG_AsuranGravEngine",
                "WNG_GoauldGravEngine"
            };

            foreach (string engineName in engines)
            {
                ThingDef engineDef = DefDatabase<ThingDef>.GetNamedSilentFail(engineName);
                if (engineDef == null)
                {
                    failures.Add("Missing runtime engine def " + engineName);
                    continue;
                }

                CompProperties_AffectedByFacilities affected =
                    engineDef.GetCompProperties<CompProperties_AffectedByFacilities>();

                if (affected == null)
                {
                    failures.Add(engineName + " has no CompProperties_AffectedByFacilities at runtime.");
                    continue;
                }

                if (affected.linkableFacilities == null || affected.linkableFacilities.Count == 0)
                    failures.Add(engineName + " has empty linkableFacilities at runtime.");

                sb.AppendLine(engineName + ": " +
                    (affected.linkableFacilities?.Count ?? 0) + " linkable facility defs.");

                if (affected.linkableFacilities != null)
                {
                    foreach (ThingDef facility in affected.linkableFacilities)
                    {
                        if (facility == null)
                        {
                            failures.Add(engineName + " contains a null linkable facility reference.");
                            continue;
                        }

                        bool hasFacilityComp =
                            facility.comps != null &&
                            facility.comps.Any(c =>
                                c is CompProperties_GravshipFacility ||
                                c is CompProperties_WNGGravshipFacility ||
                                c is CompProperties_WNGPilotConsole ||
                                c is CompProperties_WNGFuelTankFacility ||
                                c is CompProperties_WNGThruster);

                        if (!hasFacilityComp)
                            failures.Add(engineName + " links " + facility.defName +
                                         " but that def has no gravship-facility comp.");
                    }
                }
            }

            if (Find.CurrentMap != null)
            {
                foreach (string engineName in engines)
                {
                    ThingDef engineDef = DefDatabase<ThingDef>.GetNamedSilentFail(engineName);
                    if (engineDef == null) continue;

                    foreach (Building engine in Find.CurrentMap.listerBuildings.AllBuildingsColonistOfDef(engineDef))
                    {
                        if (engine is not ThingWithComps twc)
                            continue;

                        CompAffectedByFacilities comp = twc.GetComp<CompAffectedByFacilities>();
                        if (comp == null)
                        {
                            failures.Add(engineName + " spawned without CompAffectedByFacilities.");
                            continue;
                        }

                        try
                        {
                            var linked = comp.LinkedFacilitiesListForReading;
                            sb.AppendLine(engineName + " spawned instance linked facilities: " + linked.Count);
                        }
                        catch (Exception ex)
                        {
                            failures.Add(engineName + " linked-facility enumeration threw " +
                                         ex.GetType().Name + ": " + ex.Message);
                        }
                    }
                }
            }

            sb.Insert(0, "[WNG AUDIT 13] GRAVSHIP CONNECTIVITY\n");
            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 13 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: gravship engine/facility connectivity resolves at runtime.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 13 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
