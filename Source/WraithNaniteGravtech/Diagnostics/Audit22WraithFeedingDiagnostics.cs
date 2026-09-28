using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit22WraithFeedingDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 22 - Wraith feeding",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 22] WRAITH FEEDING");

            GeneDef lifeDef = DefDatabase<GeneDef>.GetNamedSilentFail("WNG_LifeForceMetabolism");
            AbilityDef full = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_LifeDrain");
            AbilityDef partial = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_PartialFeed");
            HediffDef resistance = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_FeedingResistance");
            HediffDef humanized = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithRetroviralHumanization");
            HediffDef independent = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_FeedingIndependent");

            if (lifeDef == null) failures.Add("Missing WNG_LifeForceMetabolism.");
            if (full == null) failures.Add("Missing WNG_LifeDrain.");
            if (partial == null) failures.Add("Missing WNG_PartialFeed.");
            if (resistance == null) failures.Add("Missing WNG_FeedingResistance.");
            if (humanized == null) failures.Add("Missing WNG_WraithRetroviralHumanization.");
            if (independent == null) failures.Add("Missing WNG_FeedingIndependent.");

            WraithStrategicHungerRegistry strategic = Current.Game?.GetComponent<WraithStrategicHungerRegistry>();
            if (strategic == null)
                failures.Add("WraithStrategicHungerRegistry is unavailable in the loaded game.");

            int wraiths = 0;
            int childrenUnder13 = 0;
            int activeLifeForce = 0;
            int foodNeedPresent = 0;
            int resistant = 0;
            int humanizedCount = 0;
            int independentCount = 0;

            Map map = Find.CurrentMap;
            if (map?.mapPawns?.AllPawnsSpawned != null)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!WraithHiveEcologyUtility.IsWraith(pawn))
                        continue;
                    wraiths++;

                    int age = pawn.ageTracker?.AgeBiologicalYears ?? -1;
                    Gene life = lifeDef == null ? null : pawn.genes?.GetGene(lifeDef);
                    bool active = life?.Active == true;
                    bool hasFood = pawn.needs?.food != null;

                    if (age >= 0 && age < 13)
                    {
                        childrenUnder13++;
                        if (active)
                            failures.Add(pawn.LabelShortCap + " is under 13 but Life Force metabolism is active.");
                        if (!hasFood)
                            failures.Add(pawn.LabelShortCap + " is under 13 but has no ordinary Food need.");
                    }

                    if (active) activeLifeForce++;
                    if (hasFood) foodNeedPresent++;
                    if (resistance != null && pawn.health?.hediffSet?.HasHediff(resistance) == true) resistant++;
                    if (humanized != null && pawn.health?.hediffSet?.HasHediff(humanized) == true) humanizedCount++;
                    if (independent != null && pawn.health?.hediffSet?.HasHediff(independent) == true)
                    {
                        independentCount++;
                        if (life != null)
                            failures.Add(pawn.LabelShortCap + " is feeding-independent but still has WNG_LifeForceMetabolism.");
                        if (!hasFood)
                            failures.Add(pawn.LabelShortCap + " is feeding-independent but ordinary Food need is absent.");
                    }
                }
            }

            sb.AppendLine("Spawned Wraith: " + wraiths);
            sb.AppendLine("Wraith under age 13: " + childrenUnder13);
            sb.AppendLine("Active Life Force metabolism: " + activeLifeForce);
            sb.AppendLine("Wraith with ordinary Food need: " + foodNeedPresent);
            sb.AppendLine("Feeding Resistance active: " + resistant);
            sb.AppendLine("Retrovirally humanized: " + humanizedCount);
            sb.AppendLine("Feeding-independent: " + independentCount);
            sb.AppendLine("Strategic hunger registry: " + (strategic == null ? "MISSING" : "loaded"));
            sb.AppendLine("Read-only probe. Full/partial feed transactions, strategic request/refusal UI and treatment outcomes require the recorded live behavior checks.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 22 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded Wraith feeding-state contracts resolve.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 22 automated checks PASS; live feeding behavior checks pending.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
