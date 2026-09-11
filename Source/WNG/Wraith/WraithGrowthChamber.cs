using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithGrowthChamber : CompProperties
    {
        public int cycleTicks = 60000;
        public float biomassCostPerCycle = 60f;
        public float queenLifeForceCost = 0.35f;
        public int spawnRadius = 5;
        public float hunterWeight = 0.45f;
        public int checkIntervalTicks = 60;

        public CompProperties_WraithGrowthChamber()
        {
            compClass = typeof(CompWraithGrowthChamber);
        }
    }

    /// <summary>
    /// RimWorld-scale abstraction of a Wraith cloning pod/facility: it replaces lost mature-Hive
    /// Hunters/Warriors only, never expands the recorded founding demographic cap, and requires
    /// same-Hive Queen genetics, bio-sludge biomass and electrical power.
    /// </summary>
    public sealed class CompWraithGrowthChamber : ThingComp
    {
        private int progressTicks;
        private string lastBlockReason;

        private CompProperties_WraithGrowthChamber ChamberProps => (CompProperties_WraithGrowthChamber)props;
        private CompPowerTrader Power => parent.GetComp<CompPowerTrader>();
        private CompRefuelable Biomass => parent.GetComp<CompRefuelable>();

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || parent.Map == null || parent.Faction == null)
                return;

            int interval = Math.Max(30, ChamberProps.checkIntervalTicks);
            if (!parent.IsHashIntervalTick(interval))
                return;

            CompMatureWraithHivePopulation population;
            Pawn queen;
            if (!CanProgress(out population, out queen, out string reason))
            {
                lastBlockReason = reason;
                if (population != null && population.LivingDemographicCount >= population.FoundingPopulationCap)
                    progressTicks = 0;
                return;
            }

            lastBlockReason = null;
            progressTicks = Math.Min(Math.Max(1, ChamberProps.cycleTicks), progressTicks + interval);
            if (progressTicks >= Math.Max(1, ChamberProps.cycleTicks))
                TryCompleteCycle(population, queen);
        }

        private bool CanProgress(out CompMatureWraithHivePopulation population, out Pawn queen, out string reason)
        {
            population = FindPopulationAnchor();
            queen = null;
            reason = null;

            if (population == null || !population.Initialized)
            {
                reason = "No initialized same-faction Mature Hive Heart is linked to this chamber.";
                return false;
            }
            if (population.LivingDemographicCount >= population.FoundingPopulationCap)
            {
                reason = "The Mature Hive is already at its recorded demographic ceiling.";
                return false;
            }
            if (Power == null || !Power.PowerOn)
            {
                reason = "The chamber lacks the enormous electrical input needed for cloning.";
                return false;
            }
            float biomassCost = Math.Max(0f, ChamberProps.biomassCostPerCycle);
            if (Biomass == null || Biomass.Fuel + 0.0001f < biomassCost)
            {
                reason = "Insufficient Wraith bio sludge for the next growth cycle.";
                return false;
            }

            queen = FindQueen();
            if (queen == null)
            {
                reason = "No living, operational same-faction Wraith Queen is available as the genetic source.";
                return false;
            }
            Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(queen);
            float queenCost = Math.Max(0f, ChamberProps.queenLifeForceCost);
            if (lifeForce == null || lifeForce.Value + 0.0001f < queenCost)
            {
                reason = "The local Queen lacks enough Life Force to sustain another cloning cycle.";
                return false;
            }
            return true;
        }

        private CompMatureWraithHivePopulation FindPopulationAnchor()
        {
            if (parent.Map == null || parent.Faction == null)
                return null;
            ThingDef heartDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithHiveHeart");
            if (heartDef == null)
                return null;

            return parent.Map.listerThings.ThingsOfDef(heartDef)
                .Where(t => t != null && !t.Destroyed && t.Faction == parent.Faction)
                .Select(t => t.TryGetComp<CompMatureWraithHivePopulation>())
                .Where(c => c != null && c.Initialized)
                .OrderBy(c => c.parent.Position.DistanceToSquared(parent.Position))
                .FirstOrDefault();
        }

        private Pawn FindQueen()
        {
            if (parent.Map == null || parent.Faction == null)
                return null;
            return parent.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && !p.Downed && p.Faction == parent.Faction &&
                            p.kindDef?.defName == "WNG_WraithQueen" && WraithLifeForceUtility.IsWraith(p))
                .OrderBy(p => p.Position.DistanceToSquared(parent.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
        }

        private void TryCompleteCycle(CompMatureWraithHivePopulation population, Pawn queen)
        {
            if (!CanProgress(out CompMatureWraithHivePopulation currentPopulation, out Pawn currentQueen, out string reason) ||
                currentPopulation != population || currentQueen != queen)
            {
                lastBlockReason = reason;
                return;
            }

            PawnKindDef hunter = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithHunter");
            PawnKindDef warrior = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithWarrior");
            if (hunter == null || warrior == null)
            {
                lastBlockReason = "Required Wraith Hunter/Warrior PawnKinds are unavailable.";
                return;
            }

            IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                parent.Position,
                parent.Map,
                Math.Max(1, ChamberProps.spawnRadius),
                c => c.Standable(parent.Map) && !c.Fogged(parent.Map));
            if (!cell.IsValid)
            {
                lastBlockReason = "No valid growth-release cell is available beside the chamber.";
                return;
            }

            float hunterWeight = Math.Max(0f, Math.Min(1f, ChamberProps.hunterWeight));
            PawnKindDef kind = Rand.Chance(hunterWeight) ? hunter : warrior;
            Pawn pawn = PawnGenerator.GeneratePawn(kind, parent.Faction, parent.Map.Tile);
            if (pawn == null)
            {
                lastBlockReason = "Wraith replacement generation failed before resources were committed.";
                return;
            }

            try
            {
                GenSpawn.Spawn(pawn, cell, parent.Map);
                if (!pawn.Spawned || pawn.Map != parent.Map || pawn.Faction != parent.Faction || !WraithLifeForceUtility.IsWraith(pawn))
                {
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                    lastBlockReason = "Generated replacement failed exact-pawn validation; no resources were committed.";
                    return;
                }

                if (!population.TryRegisterGrowthReplacement(pawn))
                {
                    if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
                    lastBlockReason = "Hive Heart rejected the replacement; no resources were committed.";
                    return;
                }

                Biomass.ConsumeFuel(Math.Max(0f, ChamberProps.biomassCostPerCycle));
                WraithLifeForceUtility.Offset(queen, -Math.Max(0f, ChamberProps.queenLifeForceCost));
                progressTicks = 0;
                lastBlockReason = null;

                Lord queenLord = queen.GetLord();
                queenLord?.AddPawn(pawn);
            }
            catch (Exception ex)
            {
                if (pawn != null && !pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);
                lastBlockReason = "Growth transaction failed safely before completion: " + ex.Message;
            }
        }

        public override string CompInspectStringExtra()
        {
            int cycle = Math.Max(1, ChamberProps.cycleTicks);
            float pct = Math.Max(0f, Math.Min(1f, progressTicks / (float)cycle));
            string status = lastBlockReason.NullOrEmpty() ? "growth cycle active" : lastBlockReason;
            return "Wraith growth chamber: " + pct.ToStringPercent() + " — " + status;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref progressTicks, "wngWraithGrowthChamberProgress", 0);
            Scribe_Values.Look(ref lastBlockReason, "wngWraithGrowthChamberBlockReason");
        }
    }
}
