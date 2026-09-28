using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit24HealthGeneDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 24 - health / genes",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            int genes = 0;
            int hediffs = 0;
            int customGeneClasses = 0;
            int customHediffClasses = 0;
            int customHediffComps = 0;

            foreach (GeneDef def in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                genes++;

                Type geneClass = def.geneClass ?? typeof(Gene);
                if (!typeof(Gene).IsAssignableFrom(geneClass))
                    failures.Add(def.defName + " geneClass is not a Gene: " + geneClass.FullName);
                if (geneClass.Namespace != null &&
                    geneClass.Namespace.StartsWith("WraithNaniteGravtech", StringComparison.Ordinal))
                    customGeneClasses++;
            }

            foreach (HediffDef def in DefDatabase<HediffDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;
                hediffs++;

                Type hediffClass = def.hediffClass ?? typeof(Hediff);
                if (!typeof(Hediff).IsAssignableFrom(hediffClass))
                    failures.Add(def.defName + " hediffClass is not a Hediff: " + hediffClass.FullName);
                if (hediffClass.Namespace != null &&
                    hediffClass.Namespace.StartsWith("WraithNaniteGravtech", StringComparison.Ordinal))
                    customHediffClasses++;

                if (def.comps == null)
                    continue;
                foreach (HediffCompProperties props in def.comps)
                {
                    if (props == null)
                    {
                        failures.Add(def.defName + " contains a null HediffCompProperties entry.");
                        continue;
                    }

                    Type compClass = props.compClass;
                    if (compClass == null)
                    {
                        failures.Add(def.defName + " has HediffCompProperties " +
                                     props.GetType().FullName + " with null compClass.");
                        continue;
                    }
                    if (!typeof(HediffComp).IsAssignableFrom(compClass))
                        failures.Add(def.defName + " compClass is not a HediffComp: " + compClass.FullName);

                    if ((props.GetType().Namespace?.StartsWith("WraithNaniteGravtech", StringComparison.Ordinal) == true) ||
                        (compClass.Namespace?.StartsWith("WraithNaniteGravtech", StringComparison.Ordinal) == true))
                        customHediffComps++;
                }
            }

            sb.AppendLine("[WNG AUDIT 24] HEALTH / GENES");
            sb.AppendLine("Loaded WNG GeneDefs: " + genes);
            sb.AppendLine("Loaded WNG HediffDefs: " + hediffs);
            sb.AppendLine("Custom WNG gene classes: " + customGeneClasses);
            sb.AppendLine("Custom WNG hediff classes: " + customHediffClasses);
            sb.AppendLine("Custom WNG hediff comp entries: " + customHediffComps);
            sb.AppendLine("Read-only probe: no genes or health conditions are added/removed by this action.");
            sb.AppendLine("Add/remove, death/resurrection, xenotype change and save/reload remain throwaway-save live checks.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 24 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded WNG gene/Hediff classes and HediffComp links resolve.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 24 automated checks PASS; lifecycle live test pending.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
