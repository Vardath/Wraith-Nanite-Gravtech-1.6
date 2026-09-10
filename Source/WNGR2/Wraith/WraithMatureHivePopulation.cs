using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_MatureWraithHivePopulation : CompProperties
    {
        public int replacementRetryTicks = 30000;

        public CompProperties_MatureWraithHivePopulation()
        {
            compClass = typeof(CompMatureWraithHivePopulation);
        }
    }

    /// <summary>
    /// Demographic controller for a generated mature NPC Hive only. It never discovers or adopts
    /// pawns from the map. The generator must explicitly provide the exact founding Wraith and the
    /// exact Growth Chamber. That living founder count becomes a fixed population ceiling.
    /// Sealed Dormancy Vault reserve is deliberately outside this demographic list.
    /// </summary>
    public sealed class CompMatureWraithHivePopulation : ThingComp
    {
        private bool initializedByMatureHiveGenerator;
        private int foundingPopulationCap;
        private int nextReplacementAttemptTick = -1;
        private bool hunterNext = true;
        private List<Pawn> demographicMembers = new List<Pawn>();
        private Building generatedGrowthChamber;

        private CompProperties_MatureWraithHivePopulation PopulationProps =>
            (CompProperties_MatureWraithHivePopulation)props;

        public bool InitializedByMatureHiveGenerator => initializedByMatureHiveGenerator;
        public int FoundingPopulationCap => foundingPopulationCap;

        public bool InitializeGeneratedHive(IEnumerable<Pawn> foundingMembers, Building exactGeneratedGrowthChamber)
        {
            if (initializedByMatureHiveGenerator || parent == null || parent.Faction == null || parent.Faction == Faction.OfPlayer)
                return false;
            if (foundingMembers == null || exactGeneratedGrowthChamber == null || exactGeneratedGrowthChamber.Destroyed)
                return false;
            if (exactGeneratedGrowthChamber.Faction != parent.Faction)
                return false;
            if (exactGeneratedGrowthChamber.GetComp<CompWraithGrowthChamber>() == null)
                return false;

            List<Pawn> exactFounders = new List<Pawn>();
            foreach (Pawn pawn in foundingMembers)
            {
                if (pawn == null || pawn.Dead || pawn.Faction != parent.Faction)
                    continue;
                if (WraithLifeForceUtility.Get(pawn) == null)
                    continue;
                if (!exactFounders.Contains(pawn))
                    exactFounders.Add(pawn);
            }

            if (exactFounders.Count == 0)
                return false;

            demographicMembers.Clear();
            demographicMembers.AddRange(exactFounders);
            foundingPopulationCap = exactFounders.Count;
            generatedGrowthChamber = exactGeneratedGrowthChamber;
            initializedByMatureHiveGenerator = true;
            nextReplacementAttemptTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(1, PopulationProps.replacementRetryTicks);
            return true;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!initializedByMatureHiveGenerator || parent == null || !parent.Spawned || parent.Faction == null || parent.Faction == Faction.OfPlayer)
                return;
            if (!parent.IsHashIntervalTick(600))
                return;

            CompWraithGrowthChamber chamber = ValidGeneratedGrowthChamber();
            if (chamber == null)
                return;

            Pawn completed = chamber.TakeCompletedPopulationClone();
            if (completed != null)
                RegisterExactCompletedReplacement(completed);

            int living = LivingDemographicCount();
            if (living >= foundingPopulationCap || chamber.HasActiveGestation)
                return;

            int now = Find.TickManager.TicksGame;
            if (nextReplacementAttemptTick < 0)
                nextReplacementAttemptTick = now;
            if (now < nextReplacementAttemptTick)
                return;

            nextReplacementAttemptTick = now + Math.Max(1, PopulationProps.replacementRetryTicks);
            string replacementKind = ChooseReplacementKind();
            if (replacementKind.NullOrEmpty())
                return;

            if (chamber.TryStartPopulationReplacement(replacementKind)
                && (replacementKind == "WNG_WraithHunter" || replacementKind == "WNG_WraithWarrior"))
            {
                hunterNext = !hunterNext;
            }
        }

        private CompWraithGrowthChamber ValidGeneratedGrowthChamber()
        {
            if (generatedGrowthChamber == null || generatedGrowthChamber.Destroyed || !generatedGrowthChamber.Spawned)
                return null;
            if (generatedGrowthChamber.Faction != parent.Faction || generatedGrowthChamber.Map != parent.Map)
                return null;
            return generatedGrowthChamber.GetComp<CompWraithGrowthChamber>();
        }

        private void RegisterExactCompletedReplacement(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Faction != parent.Faction)
                return;
            if (WraithLifeForceUtility.Get(pawn) == null)
                return;

            string kind = pawn.kindDef?.defName ?? string.Empty;
            if (kind == "WNG_WraithQueen")
                return;
            if (kind != "WNG_WraithHunter" && kind != "WNG_WraithWarrior" && kind != "WNG_WraithKeeper")
                return;

            // Only this exact chamber's completed handoff may add a replacement. Dead demographic
            // references are retired here; no other map pawn is searched for or adopted.
            for (int i = demographicMembers.Count - 1; i >= 0; i--)
            {
                Pawn member = demographicMembers[i];
                if (member == null || member.Dead)
                    demographicMembers.RemoveAt(i);
            }

            if (LivingDemographicCount() >= foundingPopulationCap || demographicMembers.Contains(pawn))
                return;

            demographicMembers.Add(pawn);
        }

        private int LivingDemographicCount()
        {
            int count = 0;
            for (int i = 0; i < demographicMembers.Count; i++)
            {
                Pawn pawn = demographicMembers[i];
                if (pawn != null && !pawn.Dead)
                    count++;
            }
            return count;
        }

        private bool HasLivingKind(string pawnKindDefName)
        {
            for (int i = 0; i < demographicMembers.Count; i++)
            {
                Pawn pawn = demographicMembers[i];
                if (pawn != null && !pawn.Dead && pawn.kindDef?.defName == pawnKindDefName)
                    return true;
            }
            return false;
        }

        private string ChooseReplacementKind()
        {
            bool queenAlive = HasLivingKind("WNG_WraithQueen");
            bool keeperAlive = HasLivingKind("WNG_WraithKeeper");
            if (queenAlive && !keeperAlive)
                return "WNG_WraithKeeper";

            return hunterNext ? "WNG_WraithHunter" : "WNG_WraithWarrior";
        }

        public override string CompInspectStringExtra()
        {
            if (!initializedByMatureHiveGenerator)
                return null;
            return "Mature Hive population: " + LivingDemographicCount() + " / " + foundingPopulationCap;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initializedByMatureHiveGenerator, "wngMatureHivePopulationInitialized", false);
            Scribe_Values.Look(ref foundingPopulationCap, "wngMatureHiveFoundingPopulationCap", 0);
            Scribe_Values.Look(ref nextReplacementAttemptTick, "wngMatureHiveReplacementRetryTick", -1);
            Scribe_Values.Look(ref hunterNext, "wngMatureHiveHunterNext", true);
            Scribe_Collections.Look(ref demographicMembers, "wngMatureHiveDemographicMembers", LookMode.Reference);
            Scribe_References.Look(ref generatedGrowthChamber, "wngMatureHiveGeneratedGrowthChamber");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && demographicMembers == null)
                demographicMembers = new List<Pawn>();
        }
    }
}
