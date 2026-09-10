using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithGrowthChamber : CompProperties
    {
        public string requiredResearchDefName = "WNG_CloningInfrastructure";
        public float minimumDonorLifeForce = 0.55f;
        public float donorLifeForceCost = 0.25f;
        public float newbornLifeForce = 0.35f;

        public CompProperties_WraithGrowthChamber()
        {
            compClass = typeof(CompWraithGrowthChamber);
        }
    }

    /// <summary>
    /// Fresh Wraith cloning organ. It grows only the ordinary Hunter, Warrior and Keeper castes.
    /// Queen reproduction is intentionally outside this mechanism. Each cycle consumes real
    /// cultured biomass and Life Force from a same-faction Wraith donor before gestation begins.
    /// </summary>
    public sealed class CompWraithGrowthChamber : ThingComp
    {
        private string activePawnKindDefName;
        private int finishTick = -1;
        private int investedBiomass;
        private bool activePopulationReplacement;
        private Pawn completedPopulationClone;

        private CompProperties_WraithGrowthChamber ChamberProps => (CompProperties_WraithGrowthChamber)props;
        public bool HasActiveGestation => !activePawnKindDefName.NullOrEmpty() && finishTick >= 0;

        public override void CompTick()
        {
            base.CompTick();
            if (!HasActiveGestation || !parent.Spawned || !parent.IsHashIntervalTick(250))
                return;

            if (Find.TickManager.TicksGame >= finishTick)
                TryCompleteGestation();
        }

        /// <summary>
        /// Mature NPC Hives use this entry point so demographic replacement pays through the same
        /// biomass/Life Force path as ordinary gestation. Only the three bounded replacement castes
        /// are accepted. The exact resulting Pawn is retained for TakeCompletedPopulationClone().
        /// </summary>
        public bool TryStartPopulationReplacement(string pawnKindDefName)
        {
            if (parent.Faction == null || parent.Faction == Faction.OfPlayer || completedPopulationClone != null)
                return false;

            switch (pawnKindDefName)
            {
                case "WNG_WraithHunter":
                    return TryStartGestation(pawnKindDefName, 160, 120000, true);
                case "WNG_WraithWarrior":
                    return TryStartGestation(pawnKindDefName, 220, 180000, true);
                case "WNG_WraithKeeper":
                    return TryStartGestation(pawnKindDefName, 280, 240000, true);
                default:
                    return false;
            }
        }

        public Pawn TakeCompletedPopulationClone()
        {
            Pawn result = completedPopulationClone;
            completedPopulationClone = null;
            return result;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent.Faction != Faction.OfPlayer)
                yield break;

            if (HasActiveGestation)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Cancel Wraith gestation",
                    defaultDesc = "Abort the active clone cycle. Half of the invested cultured biomass is recovered. Donated Life Force is not recoverable.",
                    action = CancelGestation
                };
                yield break;
            }

            yield return BuildGestationCommand("WNG_WraithHunter", "Gestate Wraith hunter", 160, 120000);
            yield return BuildGestationCommand("WNG_WraithWarrior", "Gestate Wraith warrior", 220, 180000);
            yield return BuildGestationCommand("WNG_WraithKeeper", "Gestate Wraith keeper", 280, 240000);
        }

        private Command_Action BuildGestationCommand(string pawnKindDefName, string label, int biomassCost, int durationTicks)
        {
            Command_Action command = new Command_Action
            {
                defaultLabel = label,
                defaultDesc = "Begin a supervised Wraith clone cycle. Requires " + biomassCost
                    + " cultured biomass, a Keeper or Queen, and a same-faction Wraith donor with at least "
                    + ChamberProps.minimumDonorLifeForce.ToString("0.00") + " Life Force. Gestation: "
                    + (durationTicks / 60000f).ToString("0.0") + " days.",
                action = () => TryStartGestation(pawnKindDefName, biomassCost, durationTicks, false)
            };

            string reason = DisabledReason(biomassCost);
            if (!reason.NullOrEmpty())
                command.Disable(reason);
            return command;
        }

        private string DisabledReason(int biomassCost)
        {
            if (!parent.Spawned || parent.Map == null || parent.Faction == null)
                return "Growth Chamber is not on an active faction map.";
            if (!ResearchFinished(ChamberProps.requiredResearchDefName))
                return "Requires Wraith cloning infrastructure research.";
            if (!SupervisorPresent(parent.Map, parent.Faction))
                return "Requires a Wraith Keeper or Queen on this map.";
            if (CountBiomass(parent.Map) < biomassCost)
                return "Not enough cultured biomass.";
            if (FindChargedDonor(parent.Map, parent.Faction, ChamberProps.minimumDonorLifeForce) == null)
                return "No allied Wraith has enough Life Force to seed the clone.";
            return null;
        }

        private bool TryStartGestation(string pawnKindDefName, int biomassCost, int durationTicks, bool populationReplacement)
        {
            if (HasActiveGestation || !DisabledReason(biomassCost).NullOrEmpty())
                return false;

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindDefName);
            Gene_Resource_LifeForce donor = FindChargedDonor(parent.Map, parent.Faction, ChamberProps.minimumDonorLifeForce);
            if (kind == null || donor == null)
                return false;
            if (pawnKindDefName == "WNG_WraithQueen")
                return false;
            if (!TryConsumeBiomass(parent.Map, biomassCost))
                return false;

            donor.Value = Math.Max(0f, donor.Value - Math.Max(0f, ChamberProps.donorLifeForceCost));
            activePawnKindDefName = pawnKindDefName;
            investedBiomass = biomassCost;
            activePopulationReplacement = populationReplacement;
            finishTick = Find.TickManager.TicksGame + Math.Max(1, durationTicks);
            Messages.Message("Wraith gestation begun in " + parent.LabelShort + ".", parent, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        private void TryCompleteGestation()
        {
            Map map = parent.Map;
            Faction faction = parent.Faction;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(activePawnKindDefName);
            if (map == null || faction == null || kind == null || activePawnKindDefName == "WNG_WraithQueen")
                return;

            Pawn clone = null;
            try
            {
                clone = PawnGenerator.GeneratePawn(kind, faction);
                Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(clone);
                if (lifeForce != null)
                    lifeForce.Value = Math.Min(lifeForce.Max, Math.Max(0f, ChamberProps.newbornLifeForce));

                IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(parent.InteractionCell, map, 4);
                GenSpawn.Spawn(clone, spawnCell, map);
                if (!clone.Spawned || clone.Map != map)
                    throw new InvalidOperationException("Paid Wraith gestation did not produce a spawned clone.");

                bool populationReplacement = activePopulationReplacement;
                ClearGestation();
                if (populationReplacement)
                    completedPopulationClone = clone;

                Find.LetterStack.ReceiveLetter(
                    "Wraith clone matured",
                    clone.LabelShortCap + " has emerged from the Growth Chamber with a limited Life Force reserve.",
                    LetterDefOf.PositiveEvent,
                    clone);
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Wraith gestation completion failed; the paid cycle remains pending for retry: " + ex);
            }
        }

        private void CancelGestation()
        {
            if (!HasActiveGestation)
                return;

            Map map = parent.Map;
            IntVec3 cell = parent.InteractionCell;
            int refund = investedBiomass / 2;
            ClearGestation();
            SpawnBiomass(map, cell, refund);
        }

        private void ClearGestation()
        {
            activePawnKindDefName = null;
            finishTick = -1;
            investedBiomass = 0;
            activePopulationReplacement = false;
        }

        private static bool ResearchFinished(string defName)
        {
            ResearchProjectDef research = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return research != null && research.IsFinished;
        }

        private static bool SupervisorPresent(Map map, Faction faction)
        {
            if (map == null || faction == null)
                return false;

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed || pawn.Faction != faction)
                    continue;
                string kind = pawn.kindDef?.defName ?? string.Empty;
                if (kind == "WNG_WraithKeeper" || kind == "WNG_WraithQueen")
                    return true;
            }
            return false;
        }

        private static Gene_Resource_LifeForce FindChargedDonor(Map map, Faction faction, float minimum)
        {
            if (map == null || faction == null)
                return null;

            Gene_Resource_LifeForce best = null;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || pawn.Downed || pawn.Faction != faction)
                    continue;
                Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(pawn);
                if (lifeForce == null || lifeForce.Value < minimum)
                    continue;
                if (best == null || lifeForce.Value > best.Value)
                    best = lifeForce;
            }
            return best;
        }

        private static int CountBiomass(Map map)
        {
            ThingDef biomass = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            if (map == null || biomass == null)
                return 0;

            int total = 0;
            List<Thing> stacks = map.listerThings.ThingsOfDef(biomass);
            for (int i = 0; i < stacks.Count; i++)
            {
                Thing stack = stacks[i];
                if (stack != null && !stack.Destroyed)
                    total += stack.stackCount;
            }
            return total;
        }

        private static bool TryConsumeBiomass(Map map, int count)
        {
            if (count <= 0)
                return true;
            ThingDef biomass = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            if (map == null || biomass == null || CountBiomass(map) < count)
                return false;

            int remaining = count;
            List<Thing> stacks = map.listerThings.ThingsOfDef(biomass);
            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing stack = stacks[i];
                if (stack == null || stack.Destroyed)
                    continue;
                int take = Math.Min(remaining, stack.stackCount);
                if (take >= stack.stackCount)
                {
                    remaining -= stack.stackCount;
                    stack.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    Thing split = stack.SplitOff(take);
                    remaining -= take;
                    split.Destroy(DestroyMode.Vanish);
                }
            }
            return remaining <= 0;
        }

        private static void SpawnBiomass(Map map, IntVec3 near, int count)
        {
            if (map == null || count <= 0)
                return;
            ThingDef biomass = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            if (biomass == null)
                return;

            int remaining = count;
            int stackLimit = Math.Max(1, biomass.stackLimit);
            while (remaining > 0)
            {
                Thing stack = ThingMaker.MakeThing(biomass);
                stack.stackCount = Math.Min(remaining, stackLimit);
                remaining -= stack.stackCount;
                GenPlace.TryPlaceThing(stack, near, map, ThingPlaceMode.Near);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!HasActiveGestation)
                return "Cloning organ: dormant";

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(activePawnKindDefName);
            int ticksRemaining = Math.Max(0, finishTick - Find.TickManager.TicksGame);
            return "Gestating " + (kind?.label ?? activePawnKindDefName) + ": "
                + (ticksRemaining / 60000f).ToString("0.0") + " days remaining";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref activePawnKindDefName, "wngGrowthChamberCloneKind");
            Scribe_Values.Look(ref finishTick, "wngGrowthChamberFinishTick", -1);
            Scribe_Values.Look(ref investedBiomass, "wngGrowthChamberInvestedBiomass", 0);
            Scribe_Values.Look(ref activePopulationReplacement, "wngGrowthChamberPopulationReplacement", false);
            Scribe_References.Look(ref completedPopulationClone, "wngGrowthChamberCompletedPopulationClone");
        }
    }
}
