using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_MatureWraithHivePopulation : CompProperties
    {
        public int replacementRetryTicks = 30000;
        public float dormantWakeRadius = 18f;
        public int activeLossesBeforeDormantWake = 2;

        public CompProperties_MatureWraithHivePopulation()
        {
            compClass = typeof(CompMatureWraithHivePopulation);
        }
    }

    /// <summary>
    /// Controller for a generated mature NPC Hive only. The generator explicitly supplies exact
    /// Wraith founders, exact ordinary hibernators, exact finite biological feeding stock, exact beds,
    /// and the exact Growth Chamber. Wraith founders alone define the fixed demographic ceiling.
    /// Feeding stock never enters that ceiling and has no replenishment path. Sealed Dormancy Vault
    /// reserve remains completely outside this component.
    /// </summary>
    public sealed class CompMatureWraithHivePopulation : ThingComp
    {
        private bool initializedByMatureHiveGenerator;
        private int foundingPopulationCap;
        private int initialActiveFounderCount;
        private int nextReplacementAttemptTick = -1;
        private bool hunterNext = true;
        private bool dormantCohortReleased;
        private List<Pawn> demographicMembers = new List<Pawn>();
        private List<Pawn> dormantMembers = new List<Pawn>();
        private List<Building_Bed> dormantBeds = new List<Building_Bed>();
        private List<Pawn> feedingStock = new List<Pawn>();
        private List<Building_Bed> feedingBeds = new List<Building_Bed>();
        private Building generatedGrowthChamber;

        private CompProperties_MatureWraithHivePopulation PopulationProps =>
            (CompProperties_MatureWraithHivePopulation)props;

        public bool InitializedByMatureHiveGenerator => initializedByMatureHiveGenerator;
        public int FoundingPopulationCap => foundingPopulationCap;

        public bool InitializeGeneratedHive(IEnumerable<Pawn> foundingMembers, Building exactGeneratedGrowthChamber)
        {
            return InitializeGeneratedHive(foundingMembers, null, null, null, null, exactGeneratedGrowthChamber);
        }

        public bool InitializeGeneratedHive(
            IEnumerable<Pawn> activeFoundingMembers,
            IEnumerable<Pawn> hibernatingFounders,
            IEnumerable<Building_Bed> exactHibernationPods,
            Building exactGeneratedGrowthChamber)
        {
            return InitializeGeneratedHive(
                activeFoundingMembers,
                hibernatingFounders,
                exactHibernationPods,
                null,
                null,
                exactGeneratedGrowthChamber);
        }

        public bool InitializeGeneratedHive(
            IEnumerable<Pawn> activeFoundingMembers,
            IEnumerable<Pawn> hibernatingFounders,
            IEnumerable<Building_Bed> exactHibernationPods,
            IEnumerable<Pawn> finiteFeedingStock,
            IEnumerable<Building_Bed> exactFeedingNiches,
            Building exactGeneratedGrowthChamber)
        {
            if (initializedByMatureHiveGenerator || parent == null || parent.Faction == null || parent.Faction == Faction.OfPlayer)
                return false;
            if (activeFoundingMembers == null || exactGeneratedGrowthChamber == null || exactGeneratedGrowthChamber.Destroyed)
                return false;
            if (exactGeneratedGrowthChamber.Faction != parent.Faction)
                return false;
            if (exactGeneratedGrowthChamber.GetComp<CompWraithGrowthChamber>() == null)
                return false;

            List<Pawn> activeFounders = CollectExactWraiths(activeFoundingMembers, null);
            List<Pawn> sleepingFounders = CollectExactWraiths(hibernatingFounders, activeFounders);
            List<Building_Bed> sleepingPods = CollectExactPods(exactHibernationPods);
            List<Pawn> exactFeedingStock = CollectExactFeedingStock(finiteFeedingStock);
            List<Building_Bed> exactNiches = CollectExactFeedingNiches(exactFeedingNiches);

            if (activeFounders.Count == 0)
                return false;
            if (sleepingFounders.Count != sleepingPods.Count)
                return false;
            if (exactFeedingStock.Count != exactNiches.Count)
                return false;

            List<Pawn> exactFounders = new List<Pawn>(activeFounders.Count + sleepingFounders.Count);
            exactFounders.AddRange(activeFounders);
            exactFounders.AddRange(sleepingFounders);

            demographicMembers.Clear();
            demographicMembers.AddRange(exactFounders);
            dormantMembers.Clear();
            dormantMembers.AddRange(sleepingFounders);
            dormantBeds.Clear();
            dormantBeds.AddRange(sleepingPods);
            feedingStock.Clear();
            feedingStock.AddRange(exactFeedingStock);
            feedingBeds.Clear();
            feedingBeds.AddRange(exactNiches);

            foundingPopulationCap = exactFounders.Count;
            initialActiveFounderCount = activeFounders.Count;
            dormantCohortReleased = dormantMembers.Count == 0;
            generatedGrowthChamber = exactGeneratedGrowthChamber;
            initializedByMatureHiveGenerator = true;
            nextReplacementAttemptTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(1, PopulationProps.replacementRetryTicks);

            if (!dormantCohortReleased)
                MaintainDormantCohort();
            MaintainFeedingStock();
            return true;
        }

        private List<Pawn> CollectExactWraiths(IEnumerable<Pawn> pawns, List<Pawn> exclude)
        {
            List<Pawn> result = new List<Pawn>();
            if (pawns == null)
                return result;

            foreach (Pawn pawn in pawns)
            {
                if (pawn == null || pawn.Dead || pawn.Faction != parent.Faction)
                    continue;
                if (WraithLifeForceUtility.Get(pawn) == null)
                    continue;
                if ((exclude != null && exclude.Contains(pawn)) || result.Contains(pawn))
                    continue;
                result.Add(pawn);
            }
            return result;
        }

        private List<Building_Bed> CollectExactPods(IEnumerable<Building_Bed> pods)
        {
            List<Building_Bed> result = new List<Building_Bed>();
            if (pods == null)
                return result;

            foreach (Building_Bed bed in pods)
            {
                if (bed == null || bed.Destroyed || !bed.Spawned || bed.Map != parent.Map || bed.Faction != parent.Faction)
                    continue;
                if (bed.GetComp<CompWraithHibernationPod>() == null || result.Contains(bed))
                    continue;
                result.Add(bed);
            }
            return result;
        }

        private List<Pawn> CollectExactFeedingStock(IEnumerable<Pawn> pawns)
        {
            List<Pawn> result = new List<Pawn>();
            if (pawns == null)
                return result;

            foreach (Pawn pawn in pawns)
            {
                if (!IsValidFeedingStock(pawn) || result.Contains(pawn))
                    continue;
                result.Add(pawn);
            }
            return result;
        }

        private List<Building_Bed> CollectExactFeedingNiches(IEnumerable<Building_Bed> beds)
        {
            List<Building_Bed> result = new List<Building_Bed>();
            if (beds == null)
                return result;

            foreach (Building_Bed bed in beds)
            {
                if (bed == null || bed.Destroyed || !bed.Spawned || bed.Map != parent.Map || bed.Faction != parent.Faction)
                    continue;
                if (bed.GetComp<CompWraithFeedingNiche>() == null || result.Contains(bed))
                    continue;
                result.Add(bed);
            }
            return result;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!initializedByMatureHiveGenerator || parent == null || !parent.Spawned || parent.Faction == null || parent.Faction == Faction.OfPlayer)
                return;
            if (!parent.IsHashIntervalTick(600))
                return;

            if (!dormantCohortReleased)
            {
                MaintainDormantCohort();
                if (ShouldWakeDormantCohort())
                    WakeDormantCohort();
            }

            MaintainFeedingStock();

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

        private void MaintainDormantCohort()
        {
            HediffDef hibernatingDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
            int pairs = Math.Min(dormantMembers.Count, dormantBeds.Count);
            for (int i = 0; i < pairs; i++)
            {
                Pawn pawn = dormantMembers[i];
                Building_Bed bed = dormantBeds[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map || pawn.Faction != parent.Faction)
                    continue;
                if (bed == null || bed.Destroyed || !bed.Spawned || bed.Map != parent.Map || bed.Faction != parent.Faction)
                    continue;

                if (hibernatingDef != null && pawn.health?.hediffSet?.GetFirstHediffOfDef(hibernatingDef) == null)
                    pawn.health.AddHediff(hibernatingDef);

                EnsureLayDownJob(pawn, bed);
            }
        }

        private void MaintainFeedingStock()
        {
            int pairs = Math.Min(feedingStock.Count, feedingBeds.Count);
            for (int i = 0; i < pairs; i++)
            {
                Pawn pawn = feedingStock[i];
                Building_Bed bed = feedingBeds[i];
                if (!IsValidFeedingStock(pawn))
                    continue;
                if (bed == null || bed.Destroyed || !bed.Spawned || bed.Map != parent.Map || bed.Faction != parent.Faction)
                    continue;
                if (bed.GetComp<CompWraithFeedingNiche>() == null)
                    continue;

                bed.ForOwnerType = BedOwnerType.Prisoner;
                EnsureLayDownJob(pawn, bed);
            }
        }

        private static void EnsureLayDownJob(Pawn pawn, Building_Bed bed)
        {
            if (pawn?.jobs == null || bed == null)
                return;
            if (pawn.jobs.curJob != null && pawn.jobs.curJob.def == JobDefOf.LayDown && pawn.jobs.curJob.targetA.Thing == bed)
                return;

            Job job = JobMaker.MakeJob(JobDefOf.LayDown, bed);
            job.expiryInterval = 60000;
            job.checkOverrideOnExpire = false;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc);
        }

        private bool IsValidFeedingStock(Pawn pawn)
        {
            return pawn != null
                && !pawn.Dead
                && pawn.Spawned
                && pawn.Map == parent.Map
                && WraithCaptureUtility.IsValidCaptiveIdentity(pawn)
                && pawn.guest?.IsPrisoner == true
                && pawn.guest.HostFaction == parent.Faction;
        }

        private bool ShouldWakeDormantCohort()
        {
            if (dormantCohortReleased || dormantMembers.Count == 0 || parent.Map == null)
                return false;

            int activeLiving = LivingActiveDemographicCount();
            int lossTrigger = Math.Max(1, initialActiveFounderCount - Math.Max(1, PopulationProps.activeLossesBeforeDormantWake));
            if (activeLiving <= lossTrigger)
                return true;

            float radius = Math.Max(1f, PopulationProps.dormantWakeRadius);
            float radiusSquared = radius * radius;
            foreach (Pawn pawn in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.Downed)
                    continue;
                if (pawn.Position.DistanceToSquared(parent.Position) <= radiusSquared)
                    return true;

                for (int i = 0; i < dormantBeds.Count; i++)
                {
                    Building_Bed bed = dormantBeds[i];
                    if (bed != null && bed.Spawned && pawn.Position.DistanceToSquared(bed.Position) <= radiusSquared)
                        return true;
                }
            }
            return false;
        }

        private void WakeDormantCohort()
        {
            dormantCohortReleased = true;
            HediffDef hibernatingDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
            List<Pawn> released = new List<Pawn>();

            for (int i = 0; i < dormantMembers.Count; i++)
            {
                Pawn pawn = dormantMembers[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map || pawn.Faction != parent.Faction)
                    continue;

                if (hibernatingDef != null && pawn.health?.hediffSet != null)
                {
                    Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hibernatingDef);
                    if (existing != null)
                        pawn.health.RemoveHediff(existing);
                }
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                released.Add(pawn);
            }

            if (released.Count == 0)
                return;

            Lord lord = LordMaker.MakeNewLord(parent.Faction, new LordJob_DefendBase(parent.Faction, parent.Position, 60000), parent.Map);
            for (int i = 0; i < released.Count; i++)
                lord.AddPawn(released[i]);
            Messages.Message("Ordinary hibernating Wraith are waking to defend the mature Hive.", parent, MessageTypeDefOf.ThreatSmall, historical: false);
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

        private int LivingDormantCount()
        {
            if (dormantCohortReleased)
                return 0;

            int count = 0;
            for (int i = 0; i < dormantMembers.Count; i++)
            {
                Pawn pawn = dormantMembers[i];
                if (pawn != null && !pawn.Dead)
                    count++;
            }
            return count;
        }

        private int LivingActiveDemographicCount()
        {
            return Math.Max(0, LivingDemographicCount() - LivingDormantCount());
        }

        private int LivingFeedingStockCount()
        {
            int count = 0;
            for (int i = 0; i < feedingStock.Count; i++)
            {
                if (IsValidFeedingStock(feedingStock[i]))
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
            return "Mature Hive population: " + LivingActiveDemographicCount() + " active / "
                + LivingDormantCount() + " ordinary dormant / " + LivingFeedingStockCount()
                + " finite feeding stock / " + foundingPopulationCap + " Wraith cap";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initializedByMatureHiveGenerator, "wngMatureHivePopulationInitialized", false);
            Scribe_Values.Look(ref foundingPopulationCap, "wngMatureHiveFoundingPopulationCap", 0);
            Scribe_Values.Look(ref initialActiveFounderCount, "wngMatureHiveInitialActiveFounderCount", 0);
            Scribe_Values.Look(ref nextReplacementAttemptTick, "wngMatureHiveReplacementRetryTick", -1);
            Scribe_Values.Look(ref hunterNext, "wngMatureHiveHunterNext", true);
            Scribe_Values.Look(ref dormantCohortReleased, "wngMatureHiveDormantCohortReleased", false);
            Scribe_Collections.Look(ref demographicMembers, "wngMatureHiveDemographicMembers", LookMode.Reference);
            Scribe_Collections.Look(ref dormantMembers, "wngMatureHiveDormantMembers", LookMode.Reference);
            Scribe_Collections.Look(ref dormantBeds, "wngMatureHiveDormantBeds", LookMode.Reference);
            Scribe_Collections.Look(ref feedingStock, "wngMatureHiveFeedingStock", LookMode.Reference);
            Scribe_Collections.Look(ref feedingBeds, "wngMatureHiveFeedingBeds", LookMode.Reference);
            Scribe_References.Look(ref generatedGrowthChamber, "wngMatureHiveGeneratedGrowthChamber");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                demographicMembers = demographicMembers ?? new List<Pawn>();
                dormantMembers = dormantMembers ?? new List<Pawn>();
                dormantBeds = dormantBeds ?? new List<Building_Bed>();
                feedingStock = feedingStock ?? new List<Pawn>();
                feedingBeds = feedingBeds ?? new List<Building_Bed>();
            }
        }
    }
}
