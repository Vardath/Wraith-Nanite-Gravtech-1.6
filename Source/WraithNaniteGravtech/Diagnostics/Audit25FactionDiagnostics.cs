using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit25FactionDiagnostics
    {
        private static readonly string[] FactionNames =
        {
            "WNG_WraithSableBrood",
            "WNG_WraithCinderCourt",
            "WNG_WraithVeiledHive",
            "WNG_WraithPaleCovenant",
            "WNG_PrecursorCollective",
            "WNG_HumanFormEnclave",
            "WNG_ReplicatorSwarm",
            "WNG_MichaelsExperiments"
        };

        [DebugAction(
            "WNG",
            "Audit 25 - factions",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 25] FACTIONS");

            Faction player = Faction.OfPlayer;
            int liveCount = 0;
            int generatedSamples = 0;
            int absentLiveInstances = 0;

            foreach (string name in FactionNames)
            {
                FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(name);
                if (def == null)
                {
                    failures.Add("Missing FactionDef " + name);
                    continue;
                }

                PawnKindDef basic = def.basicMemberKind;
                if (basic == null)
                {
                    failures.Add(name + " has no loaded basicMemberKind.");
                    continue;
                }

                Faction faction = Find.FactionManager?.FirstFactionOfDef(def);
                if (faction == null)
                {
                    absentLiveInstances++;
                    sb.AppendLine(" - " + name + ": no live instance in this save (allowed for scenarios/saves that do not instantiate every WNG faction).");
                    continue;
                }

                liveCount++;
                string relation = player == null
                    ? "<no player faction>"
                    : (faction.HostileTo(player) ? "hostile" : "non-hostile") +
                      ", goodwill=" + faction.BaseGoodwillWith(player);

                int settlements = 0;
                if (Find.WorldObjects?.Settlements != null)
                    settlements = Find.WorldObjects.Settlements.Count(s => s?.Faction == faction);

                sb.AppendLine(
                    " - " + name +
                    ": " + relation +
                    ", permanentEnemy=" + def.permanentEnemy +
                    ", settlements=" + settlements +
                    ", caravanTraders=" + (def.caravanTraderKinds?.Count ?? 0) +
                    ", visitorTraders=" + (def.visitorTraderKinds?.Count ?? 0) +
                    ", baseTraders=" + (def.baseTraderKinds?.Count ?? 0));

                if (def.permanentEnemy && player != null && !faction.HostileTo(player))
                    failures.Add(name + " is permanentEnemy but is not hostile to the player faction.");

                Pawn pawn = null;
                try
                {
                    pawn = PawnGenerator.GeneratePawn(basic, faction);
                    generatedSamples++;
                    if (pawn == null)
                    {
                        failures.Add(name + " basic member generation returned null.");
                        continue;
                    }
                    if (pawn.Faction != faction)
                        failures.Add(name + " generated basic member leaked to faction " +
                                     (pawn.Faction?.def?.defName ?? "<none>") + ".");

                    if (basic.apparelRequired != null)
                    {
                        foreach (ThingDef required in basic.apparelRequired.Where(x => x != null))
                            if (pawn.apparel?.WornApparel?.Any(a => a?.def == required) != true)
                                failures.Add(name + "/" + basic.defName +
                                             " generated without required apparel " + required.defName + ".");
                    }

                    if (basic.weaponTags != null && basic.weaponTags.Count > 0)
                    {
                        ThingWithComps primary = pawn.equipment?.Primary;
                        if (primary == null)
                            failures.Add(name + "/" + basic.defName + " generated without a primary weapon.");
                        else if (primary.def.weaponTags == null ||
                                 !primary.def.weaponTags.Any(t => basic.weaponTags.Contains(t)))
                            failures.Add(name + "/" + basic.defName +
                                         " generated weapon does not match its faction PawnKind tags.");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add(name + " basic member generation threw " +
                                 ex.GetType().Name + ": " + ex.Message);
                }
                finally
                {
                    if (pawn != null && !pawn.Destroyed && !pawn.Spawned && pawn.ParentHolder == null)
                        pawn.Destroy(DestroyMode.Vanish);
                }
            }

            TraderKindDef asuranTrader =
                DefDatabase<TraderKindDef>.GetNamedSilentFail("WNG_AsuranArtifactExchange");
            if (asuranTrader == null)
                failures.Add("Missing WNG_AsuranArtifactExchange TraderKindDef.");

            sb.AppendLine("Live WNG faction instances: " + liveCount + "/" + FactionNames.Length);
            sb.AppendLine("Generated unspawned basic-member samples: " + generatedSamples);
            sb.AppendLine("Faction defs without a live instance in this save: " + absentLiveInstances);
            sb.AppendLine("This probe does not create settlements, raids, visitors, caravans or quests.");
            sb.AppendLine("Those world/map interactions remain the recorded user-side live test.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct())
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 25 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: loaded faction defs and any live faction instances/basic-member samples are coherent.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 25 automated checks PASS; world interaction checks pending.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
