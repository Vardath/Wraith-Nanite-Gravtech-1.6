using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit38ResourceConservationDiagnostics
    {
        private const string MatterDefName = "WNG_ReplicatorMatter";

        [DebugAction(
            "WNG",
            "Audit 38 - resource conservation",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.Playing)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 38] RESOURCE CONSERVATION / EXPLOIT");

            ThingDef matter = DefDatabase<ThingDef>.GetNamedSilentFail(MatterDefName);
            ThingDef drone = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (matter == null || drone == null)
            {
                failures.Add("Replicator Matter or Drone Def is missing.");
            }
            else
            {
                CompProperties_ReplicatorMatterReassembly reassembly =
                    matter.comps?.OfType<CompProperties_ReplicatorMatterReassembly>().FirstOrDefault();
                int consume = reassembly == null ? 0 : Math.Max(1, reassembly.consumePerDrone);
                int droneDrop = CountLeaving(drone, MatterDefName);
                sb.AppendLine("Loose Blocks per reassembled Drone: " + consume);
                sb.AppendLine("Drone Replicator Block killed-leavings: " + droneDrop);
                sb.AppendLine("Drone has corpse: " + (drone.race?.hasCorpse ?? false));
                if (drone.race?.hasCorpse == true)
                    failures.Add("Replicator Drone unexpectedly has a corpse; expected despawn/drop behavior.");
                if (droneDrop <= 0 || droneDrop >= consume)
                    failures.Add("Loose Block -> Drone -> death loop is not lossy.");
            }

            string[] hierarchyNames =
            {
                "WNG_ReplicatorDrone",
                "WNG_ReplicatorHunter",
                "WNG_ReplicatorBulwark",
                "WNG_ReplicatorTitan",
                "WNG_ReplicatorSiegeMass",
                "WNG_ReplicatorRepairer",
                "WNG_ReplicatorBurrower",
                "WNG_ReplicatorArtillery"
            };

            Dictionary<string, CompProperties_ReplicatorHierarchy> hierarchy =
                new Dictionary<string, CompProperties_ReplicatorHierarchy>();
            foreach (string name in hierarchyNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                CompProperties_ReplicatorHierarchy props =
                    def?.comps?.OfType<CompProperties_ReplicatorHierarchy>().FirstOrDefault();
                if (props != null)
                    hierarchy[name] = props;
            }

            sb.AppendLine();
            sb.AppendLine("Hierarchy contracts:");
            foreach (KeyValuePair<string, CompProperties_ReplicatorHierarchy> pair in hierarchy.OrderBy(p => p.Key))
            {
                CompProperties_ReplicatorHierarchy p = pair.Value;
                sb.AppendLine(
                    " - " + pair.Key +
                    ": upgrade=" + (p.upgradePawnKind ?? "<none>") +
                    ", unitsRequired=" + p.unitsRequired +
                    ", splitChild=" + (p.splitChildPawnKind ?? "<none>") +
                    ", splitCount=" + p.splitCount);
            }

            ThingDef controller = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorController");
            int controllerDrop = CountLeaving(controller, MatterDefName);
            sb.AppendLine("Controller Replicator Block killed-leavings: " + controllerDrop);
            if (controllerDrop > 6)
                failures.Add("Controller drops more than the 6-Block terminal yield of its minimum Hunter source.");

            RecipeDef toy = DefDatabase<RecipeDef>.GetNamedSilentFail("WNG_MakeChildsToy");
            RecipeDef reprocess = DefDatabase<RecipeDef>.GetNamedSilentFail("WNG_ReprocessReplicatorMatter");
            RecipeDef slurry = DefDatabase<RecipeDef>.GetNamedSilentFail("WNG_StabilizeAsuranNaniteSlurry");
            sb.AppendLine();
            AppendRecipe(sb, toy);
            AppendRecipe(sb, reprocess);
            AppendRecipe(sb, slurry);

            AbilityDef fabricate = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_AsuranFabricateReplicatorBlocks");
            CompProperties_AbilityAsuranReplicatorMatter fabProps =
                fabricate?.comps?.OfType<CompProperties_AbilityAsuranReplicatorMatter>().FirstOrDefault();
            int blockCount = fabProps?.blockCount ?? 0;
            float reserveCost = WNGSettingsUtility.AsuranBlockFabricationReserveCost;
            sb.AppendLine();
            sb.AppendLine("Asuran/player human-form explicit external source: " + blockCount +
                          " Blocks for " + (reserveCost * 100f).ToString("0") + "% Nanite Reserve.");
            if (blockCount != 25)
                failures.Add("Asuran fabrication output is not exactly 25 Blocks.");

            sb.AppendLine();
            sb.AppendLine("LIVE TRANSACTION CHECKS REQUIRED:");
            sb.AppendLine(" - 10 exposed loose Blocks must commit one hostile Drone only after placement; killing that Drone must create exactly 3 Blocks.");
            sb.AppendLine(" - Recombine 2 Drones -> Hunter, then kill Hunter: it must despawn and split into exactly 2 Drones, with no extra Replicator Block drop.");
            sb.AppendLine(" - Allow a Controller to form from a Hunter, then kill it: it must despawn and create exactly 6 Replicator Blocks, not 9.");
            sb.AppendLine(" - Child's Toy replication must consume the chosen target exactly once and create exactly 2 controlled Toys; a failed placement/control transaction must leave the target intact.");
            sb.AppendLine(" - Child's Toy feral conversion must consume the exact Toy once and create exactly one Drone.");
            sb.AppendLine(" - Asuran/player human-form fabrication must create exactly 25 Blocks and spend the configured Nanite Reserve once; failed placement/spend must create nothing.");
            sb.AppendLine(" - Reprocess 10 Blocks -> 6 Steel and stabilize 10 Blocks -> 20 slurry; neither path may return Blocks.");

            if (failures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 38 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("PASS: runtime conservation contracts are structurally consistent; destructive live transaction tests remain required.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 38 structural runtime PASS; live transaction checks still required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private static int CountLeaving(ThingDef def, string defName)
        {
            if (def?.killedLeavings == null)
                return 0;
            int count = 0;
            foreach (ThingDefCountClass leaving in def.killedLeavings)
                if (leaving?.thingDef?.defName == defName)
                    count += leaving.count;
            return count;
        }

        private static void AppendRecipe(StringBuilder sb, RecipeDef recipe)
        {
            if (recipe == null)
            {
                sb.AppendLine("Recipe: <missing>");
                return;
            }
            int blockInput = 0;
            if (recipe.ingredients != null)
            {
                foreach (IngredientCount ingredient in recipe.ingredients)
                {
                    if (ingredient?.filter?.AllowedThingDefs?.Any(d => d?.defName == MatterDefName) == true)
                        blockInput += (int)Math.Round(ingredient.GetBaseCount());
                }
            }
            string products = recipe.products == null
                ? "<none>"
                : string.Join(", ", recipe.products.Select(p => (p?.thingDef?.defName ?? "<null>") + " x" + (p?.count ?? 0)));
            sb.AppendLine("Recipe " + recipe.defName + ": Block input=" + blockInput + "; products=" + products);
        }
    }
}
