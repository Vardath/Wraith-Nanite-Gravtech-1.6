using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public static class Audit44HistoricalBugRegressionDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 44 - historical regression smoke test",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 44] HISTORICAL REGRESSION SMOKE TEST");

            ThingDef alkesh = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AlkeshTransport");
            ThingDef siege = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorSiegeMass");
            ThingDef controller = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorController");
            ThingDef wraithTurret = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithLivingTurret");
            ThingDef asuranTurret = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AsuranAutoturret");
            GeneDef life = DefDatabase<GeneDef>.GetNamedSilentFail("WNG_LifeForceMetabolism");
            AbilityDef blocks = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_AsuranFabricateReplicatorBlocks");

            if (alkesh == null) failures.Add("Al'kesh Def missing.");
            if (siege == null) failures.Add("Replicator Siege Mass Def missing.");
            if (controller == null) failures.Add("Replicator Controller Def missing.");
            if (wraithTurret == null) failures.Add("Wraith defensive turret missing.");
            if (asuranTurret == null) failures.Add("Asuran defensive turret missing.");
            if (life == null) failures.Add("Wraith Life Force gene missing.");
            if (blocks == null) failures.Add("Asuran/Human-form Replicator Block fabrication ability missing.");

            if (wraithTurret?.GetCompProperties<CompProperties_Power>() != null)
                failures.Add("Wraith defensive turret unexpectedly requires power.");
            if (asuranTurret?.GetCompProperties<CompProperties_Power>() != null)
                failures.Add("Asuran defensive turret unexpectedly requires power.");

            int controllerBlocks = 0;
            if (controller?.killedLeavings != null)
                controllerBlocks = controller.killedLeavings
                    .Where(x => x?.thingDef?.defName == "WNG_ReplicatorMatter")
                    .Sum(x => x.count);
            if (controller != null && controllerBlocks != 6)
                failures.Add("Replicator Controller killed-leavings=" + controllerBlocks + ", expected 6 Blocks.");

            int positiveCostBuildables = 0;
            int wngBuildables = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true ||
                    def.designationCategory == null)
                    continue;
                wngBuildables++;
                if (def.costList != null && def.costList.Any(c => c != null && c.count > 0) &&
                    def.GetStatValueAbstract(StatDefOf.WorkToBuild) > 0f)
                    positiveCostBuildables++;
            }
            if (wngBuildables == 0 || positiveCostBuildables != wngBuildables)
                failures.Add("One or more WNG Architect ThingDefs lost normal construction cost/work.");

            sb.AppendLine("WNG Architect ThingDef buildables: " + wngBuildables);
            sb.AppendLine("Buildables with positive cost/work: " + positiveCostBuildables);
            sb.AppendLine("Al'kesh resolved: " + (alkesh != null));
            sb.AppendLine("Siege Mass resolved: " + (siege != null));
            sb.AppendLine("Controller Block death output: " + controllerBlocks);
            sb.AppendLine("Wraith turret resolved/no-power: " + (wraithTurret != null && wraithTurret.GetCompProperties<CompProperties_Power>() == null));
            sb.AppendLine("Asuran turret resolved/no-power: " + (asuranTurret != null && asuranTurret.GetCompProperties<CompProperties_Power>() == null));
            sb.AppendLine("Wraith Life Force gene resolved: " + (life != null));
            sb.AppendLine("Player Human-form/Asuran Block-fabrication ability resolved: " + (blocks != null));
            sb.AppendLine();
            sb.AppendLine("This smoke test complements, but does not replace, the individual live actions for bills/recipes, gravship connectivity, pawn gear, weapon rendering, Replicator death/split, Wraith feeding, save/load and Dev/God Mode parity.");

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures)
                    sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 44 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: core historical runtime smoke contracts remain intact.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 44 historical smoke PASS; individual historical live checks remain required.", MessageTypeDefOf.NeutralEvent, false);
            }
        }
    }
}
