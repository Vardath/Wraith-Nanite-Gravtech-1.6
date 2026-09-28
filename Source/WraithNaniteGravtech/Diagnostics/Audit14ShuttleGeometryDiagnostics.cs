using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit14ShuttleGeometryDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 14 - vehicle / shuttle geometry",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            int craftCount = 0;
            int transportDefs = 0;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                if (def.thingClass != typeof(Building_PassengerShuttle))
                    continue;

                craftCount++;
                IntVec2 size = def.size;
                if (size.x <= 0 || size.z <= 0)
                    failures.Add(def.defName + " has invalid footprint " + size);

                if (def.graphicData == null)
                    failures.Add(def.defName + " has null graphicData.");

                if (def.GetCompProperties<CompProperties_Shuttle>() == null)
                    failures.Add(def.defName + " lacks CompProperties_Shuttle.");
                if (def.GetCompProperties<CompProperties_Transporter>() == null)
                    failures.Add(def.defName + " lacks CompProperties_Transporter.");

                bool launchable = def.comps?.Any(c => c is CompProperties_Launchable) == true;
                if (!launchable)
                    failures.Add(def.defName + " lacks a launchable comp.");

                try
                {
                    Thing thing = ThingMaker.MakeThing(def);
                    if (thing is not Building_PassengerShuttle)
                        failures.Add(def.defName + " did not instantiate as Building_PassengerShuttle.");
                }
                catch (Exception ex)
                {
                    failures.Add(def.defName + " instantiation threw " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            foreach (TransportShipDef ship in DefDatabase<TransportShipDef>.AllDefsListForReading)
            {
                if (ship?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                transportDefs++;

                if (ship.shipThing == null)
                    failures.Add(ship.defName + " has null shipThing.");
                if (ship.arrivingSkyfaller == null)
                    failures.Add(ship.defName + " has null arrivingSkyfaller.");
                if (ship.leavingSkyfaller == null)
                    failures.Add(ship.defName + " has null leavingSkyfaller.");
                if (ship.worldObject == null)
                    failures.Add(ship.defName + " has null worldObject.");
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 14] VEHICLE / SHUTTLE GEOMETRY");
            sb.AppendLine("Passenger shuttles checked: " + craftCount);
            sb.AppendLine("TransportShipDefs checked: " + transportDefs);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 14 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: shuttle geometry and native transport links resolve at runtime.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 14 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
