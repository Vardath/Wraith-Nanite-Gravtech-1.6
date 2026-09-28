using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit15PawnGenerationDiagnostics
    {
        private sealed class Expected
        {
            public string Xenotype;
            public string Apparel;
            public Gender? Gender;
            public int? ExactAge;
        }

        private static readonly Dictionary<string, Expected> Contracts =
            new Dictionary<string, Expected>
            {
                { "WNG_WraithHunter", new Expected { Xenotype="WNG_Wraith", Apparel="WNG_WraithHunterCoat" } },
                { "WNG_WraithWarrior", new Expected { Xenotype="WNG_Wraith", Apparel="WNG_WraithWarriorCarapace" } },
                { "WNG_WraithCommander", new Expected { Xenotype="WNG_Wraith", Apparel="WNG_WraithCommanderCarapace" } },
                { "WNG_WraithKeeper", new Expected { Xenotype="WNG_Wraith", Apparel="WNG_WraithKeeperMantle" } },
                { "WNG_WraithQueen", new Expected { Xenotype="WNG_Wraith", Apparel="WNG_WraithQueenRaiment", Gender=Gender.Female } },
                { "WNG_PlayerWraithHunter", new Expected { Xenotype="WNG_Wraith", Apparel="WNG_WraithHunterCoat" } },
                { "WNG_PrecursorEngineer", new Expected { Xenotype="WNG_NanitePrecursor", Apparel="WNG_HumanFormUniform" } },
                { "WNG_PrecursorSoldier", new Expected { Xenotype="WNG_NanitePrecursor", Apparel="WNG_PrecursorFieldArmor" } },
                { "WNG_PrecursorCommander", new Expected { Xenotype="WNG_NanitePrecursor", Apparel="WNG_PrecursorCommandArmor" } },
                { "WNG_HumanFormReplicator", new Expected { Xenotype="WNG_HumanFormReplicator", Apparel="WNG_HumanFormCombatArmor" } },
                { "WNG_PlayerHumanFormReplicator", new Expected { Xenotype="WNG_HumanFormReplicator", Apparel="WNG_HumanFormUniform" } },
                { "WNG_HumanFormCopy", new Expected { Xenotype="WNG_NanitePrecursor", Apparel="WNG_HumanFormUniform" } },
                { "WNG_ReplicatorQueenChild", new Expected { Xenotype="WNG_HumanFormReplicator", Apparel="WNG_HumanFormUniform", Gender=Gender.Female, ExactAge=13 } },
            };

        [DebugAction(
            "WNG",
            "Audit 15 - pawn generation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            int generated = 0;
            const int SamplesPerKind = 3;

            foreach (var pair in Contracts)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(pair.Key);
                if (kind == null)
                {
                    failures.Add("Missing PawnKindDef " + pair.Key);
                    continue;
                }

                Faction faction = kind.defaultFactionDef == FactionDefOf.PlayerColony
                    ? Faction.OfPlayer
                    : null;

                for (int i = 0; i < SamplesPerKind; i++)
                {
                    Pawn pawn = null;
                    try
                    {
                        pawn = PawnGenerator.GeneratePawn(kind, faction);
                        generated++;
                        if (pawn == null)
                        {
                            failures.Add(pair.Key + " generation returned null.");
                            continue;
                        }

                        string xenotype = pawn.genes?.Xenotype?.defName;
                        if (xenotype != pair.Value.Xenotype)
                            failures.Add(pair.Key + " sample " + i + " xenotype=" + (xenotype ?? "<null>") +
                                         ", expected " + pair.Value.Xenotype);

                        ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Value.Apparel);
                        if (apparelDef != null && pawn.apparel?.WornApparel?.Any(a => a?.def == apparelDef) != true)
                            failures.Add(pair.Key + " sample " + i + " missing required apparel " + pair.Value.Apparel);

                        if (pair.Value.Gender.HasValue && pawn.gender != pair.Value.Gender.Value)
                            failures.Add(pair.Key + " sample " + i + " gender=" + pawn.gender +
                                         ", expected " + pair.Value.Gender.Value);

                        if (pair.Value.ExactAge.HasValue)
                        {
                            int age = pawn.ageTracker?.AgeBiologicalYears ?? -1;
                            if (age != pair.Value.ExactAge.Value)
                                failures.Add(pair.Key + " sample " + i + " age=" + age +
                                             ", expected " + pair.Value.ExactAge.Value);
                        }

                        if (pair.Key == "WNG_ReplicatorQueenChild" && pawn.equipment?.Primary != null)
                            failures.Add("Replicator Queen child generated armed with " + pawn.equipment.Primary.def.defName);

                        if (pair.Key.StartsWith("WNG_Precursor", StringComparison.Ordinal) ||
                            pair.Key == "WNG_HumanFormReplicator" ||
                            pair.Key == "WNG_PlayerHumanFormReplicator")
                        {
                            if (pawn.equipment?.Primary == null)
                                failures.Add(pair.Key + " sample " + i + " generated without a primary weapon.");
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(pair.Key + " sample " + i + " generation threw " +
                                     ex.GetType().Name + ": " + ex.Message);
                    }
                    finally
                    {
                        if (pawn != null && !pawn.Destroyed && !pawn.Spawned && pawn.ParentHolder == null)
                            pawn.Destroy(DestroyMode.Vanish);
                    }
                }
            }

            sb.AppendLine("[WNG AUDIT 15] PAWN GENERATION");
            sb.AppendLine("Generated samples: " + generated);
            sb.AppendLine("Kinds sampled: " + Contracts.Count);
            sb.AppendLine("Samples per kind: " + SamplesPerKind);

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 15 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: canonical WNG pawn kinds generated with expected xenotypes, apparel, weapons, age and gender.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 15 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }
}
