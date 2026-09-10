using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithHibernationPod : CompProperties
    {
        public int hibernationRefreshTicks = 180000;

        public CompProperties_WraithHibernationPod()
        {
            compClass = typeof(CompWraithHibernationPod);
        }
    }

    /// <summary>
    /// Dedicated Wraith hibernation bed. Occupancy maintains the same hibernation Hediff used by
    /// voluntary self-hibernation. It does not create Wraith, refill faction hunger or require a
    /// strategic feeding request.
    /// </summary>
    public sealed class CompWraithHibernationPod : ThingComp
    {
        private CompProperties_WraithHibernationPod PodProps => (CompProperties_WraithHibernationPod)props;

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || !parent.IsHashIntervalTick(250))
                return;

            Building_Bed pod = parent as Building_Bed;
            if (pod == null)
                return;

            foreach (Pawn pawn in pod.CurOccupants)
            {
                if (pawn == null || pawn.Dead || !WraithLifeForceUtility.IsWraith(pawn))
                    continue;
                MaintainHibernation(pawn);
            }
        }

        private void MaintainHibernation(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
            if (def == null || pawn?.health?.hediffSet == null)
                return;

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
                hediff = pawn.health.AddHediff(def);

            HediffComp_Disappears disappears = hediff?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
                disappears.ticksToDisappear = Math.Max(disappears.ticksToDisappear, Math.Max(1, PodProps.hibernationRefreshTicks));
        }

        public override string CompInspectStringExtra()
        {
            Building_Bed pod = parent as Building_Bed;
            bool occupied = pod != null && pod.CurOccupants.Any(p => p != null && !p.Dead && WraithLifeForceUtility.IsWraith(p));
            return occupied ? "Wraith hibernation: maintained" : "Wraith hibernation pod: empty";
        }
    }

    public sealed class CompProperties_WraithDormancyVault : CompProperties
    {
        public int dormantCount = 4;
        public float triggerRadius = 18f;
        public int awakenIntervalTicks = 180;
        public int awakenPerWave = 2;

        public CompProperties_WraithDormancyVault()
        {
            compClass = typeof(CompWraithDormancyVault);
        }
    }

    /// <summary>
    /// Finite sealed combat reserve. The saved reserve count only moves downward and is never part
    /// of the mature Hive demographic cap or Growth Chamber replacement pool.
    /// </summary>
    public sealed class CompWraithDormancyVault : ThingComp
    {
        private int remainingDormants = -1;
        private int nextWakeTick = -1;
        private bool activated;
        private bool warned;

        private CompProperties_WraithDormancyVault VaultProps => (CompProperties_WraithDormancyVault)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && remainingDormants < 0)
                remainingDormants = Math.Max(0, VaultProps.dormantCount);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || parent.Map == null || parent.Faction == null || remainingDormants <= 0)
                return;
            if (!parent.IsHashIntervalTick(60) || !parent.Faction.HostileTo(Faction.OfPlayer))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (!activated && Triggered())
            {
                activated = true;
                nextWakeTick = now;
            }
            if (!activated || now < nextWakeTick)
                return;

            ReleaseWave();
            nextWakeTick = SafeFutureTick(now, VaultProps.awakenIntervalTicks);
        }

        private bool Triggered()
        {
            if (parent.HitPoints < parent.MaxHitPoints)
                return true;

            float radiusSq = Math.Max(1f, VaultProps.triggerRadius) * Math.Max(1f, VaultProps.triggerRadius);
            return parent.Map.mapPawns.FreeColonistsSpawned.Any(p =>
                p != null && !p.Dead && p.Position.DistanceToSquared(parent.Position) <= radiusSq);
        }

        private void ReleaseWave()
        {
            PawnKindDef hunter = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithHunter");
            PawnKindDef warrior = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithWarrior");
            if (hunter == null || warrior == null)
                return;

            int wave = Math.Min(Math.Max(1, VaultProps.awakenPerWave), remainingDormants);
            List<Pawn> released = new List<Pawn>();
            for (int i = 0; i < wave; i++)
            {
                PawnKindDef kind = ((remainingDormants + i) & 1) == 0 ? warrior : hunter;
                Pawn pawn = PawnGenerator.GeneratePawn(kind, parent.Faction);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(parent.Position, parent.Map, 5);
                GenSpawn.Spawn(pawn, cell, parent.Map);
                if (!pawn.Spawned || pawn.Map != parent.Map)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                    continue;
                }
                remainingDormants--;
                released.Add(pawn);
            }

            if (released.Count == 0)
                return;

            Lord lord = LordMaker.MakeNewLord(parent.Faction, new LordJob_DefendBase(parent.Faction, parent.Position, 60000), parent.Map);
            foreach (Pawn pawn in released)
                lord.AddPawn(pawn);

            if (!warned)
            {
                warned = true;
                Find.LetterStack.ReceiveLetter(
                    "Dormant Wraith awakening",
                    "A sealed Wraith combat reserve has begun waking in response to the intrusion.",
                    LetterDefOf.ThreatBig,
                    parent);
            }
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override string CompInspectStringExtra()
        {
            if (remainingDormants < 0)
                return null;
            string state = remainingDormants <= 0 ? "empty" : activated ? "awakening" : "sealed";
            return "Dormant Wraith reserve: " + remainingDormants + " (" + state + ")";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref remainingDormants, "wngDormancyVaultRemaining", -1);
            Scribe_Values.Look(ref nextWakeTick, "wngDormancyVaultNextWake", -1);
            Scribe_Values.Look(ref activated, "wngDormancyVaultActivated", false);
            Scribe_Values.Look(ref warned, "wngDormancyVaultWarned", false);
        }
    }

    public sealed class CompProperties_MatureWraithHivePopulation : CompProperties
    {
        public float dormantWakeRadius = 18f;
        public int activeLossesBeforeDormantWake = 2;

        public CompProperties_MatureWraithHivePopulation()
        {
            compClass = typeof(CompMatureWraithHivePopulation);
        }
    }

    /// <summary>
    /// Exact mature-Hive population anchor. The site generator supplies the actual active founders,
    /// ordinary sleeping founders, their exact pods and finite exact feeding-stock prisoners.
    /// This component does not spawn demographic replacements. Growth Chamber integration is added
    /// later and must respect the founding Wraith cap recorded here.
    /// </summary>
    public sealed class CompMatureWraithHivePopulation : ThingComp
    {
        private bool initialized;
        private int foundingPopulationCap;
        private int initialActiveFounderCount;
        private bool dormantReleased;
        private List<Pawn> demographicMembers = new List<Pawn>();
        private List<Pawn> dormantMembers = new List<Pawn>();
        private List<Building_Bed> dormantPods = new List<Building_Bed>();
        private List<Pawn> feedingStock = new List<Pawn>();
        private List<Building_Bed> feedingNiches = new List<Building_Bed>();

        private CompProperties_MatureWraithHivePopulation PopulationProps => (CompProperties_MatureWraithHivePopulation)props;

        public bool Initialized => initialized;
        public int FoundingPopulationCap => foundingPopulationCap;
        public int LivingDemographicCount => demographicMembers.Count(p => p != null && !p.Dead);

        public bool InitializeGeneratedHive(
            IEnumerable<Pawn> activeFounders,
            IEnumerable<Pawn> sleepingFounders,
            IEnumerable<Building_Bed> exactPods,
            IEnumerable<Pawn> finiteFeedingStock,
            IEnumerable<Building_Bed> exactFeedingNiches)
        {
            if (initialized || parent?.Faction == null || parent.Faction == Faction.OfPlayer || parent.Map == null)
                return false;

            List<Pawn> active = ExactWraiths(activeFounders, null);
            List<Pawn> sleepers = ExactWraiths(sleepingFounders, active);
            List<Building_Bed> pods = ExactBeds(exactPods, typeof(CompWraithHibernationPod));
            List<Pawn> stock = ExactFeedingStock(finiteFeedingStock);
            List<Building_Bed> niches = ExactBeds(exactFeedingNiches, typeof(CompWraithFeedingNiche));

            if (active.Count == 0 || sleepers.Count != pods.Count || stock.Count != niches.Count)
                return false;

            demographicMembers = active.Concat(sleepers).ToList();
            dormantMembers = sleepers;
            dormantPods = pods;
            feedingStock = stock;
            feedingNiches = niches;
            foundingPopulationCap = demographicMembers.Count;
            initialActiveFounderCount = active.Count;
            dormantReleased = sleepers.Count == 0;
            initialized = true;

            for (int i = 0; i < stock.Count; i++)
                WraithCaptivityRegistry.Current?.RegisterCapturedPawn(stock[i], parent.Faction, feedingStock: true);
            return true;
        }

        private List<Pawn> ExactWraiths(IEnumerable<Pawn> source, List<Pawn> exclude)
        {
            List<Pawn> result = new List<Pawn>();
            if (source == null)
                return result;
            foreach (Pawn pawn in source)
            {
                if (pawn == null || pawn.Dead || pawn.Faction != parent.Faction || !WraithLifeForceUtility.IsWraith(pawn))
                    continue;
                if (result.Contains(pawn) || (exclude != null && exclude.Contains(pawn)))
                    continue;
                result.Add(pawn);
            }
            return result;
        }

        private List<Building_Bed> ExactBeds(IEnumerable<Building_Bed> source, Type requiredCompType)
        {
            List<Building_Bed> result = new List<Building_Bed>();
            if (source == null)
                return result;
            foreach (Building_Bed bed in source)
            {
                if (bed == null || bed.Destroyed || bed.Map != parent.Map || bed.Faction != parent.Faction || result.Contains(bed))
                    continue;
                bool hasRequiredComp = requiredCompType == typeof(CompWraithHibernationPod)
                    ? bed.GetComp<CompWraithHibernationPod>() != null
                    : bed.GetComp<CompWraithFeedingNiche>() != null;
                if (hasRequiredComp)
                    result.Add(bed);
            }
            return result;
        }

        private List<Pawn> ExactFeedingStock(IEnumerable<Pawn> source)
        {
            List<Pawn> result = new List<Pawn>();
            if (source == null)
                return result;
            foreach (Pawn pawn in source)
            {
                if (!WraithCaptivityRegistry.IsValidBiologicalCaptive(pawn) || pawn.guest?.IsPrisoner != true || pawn.guest.HostFaction != parent.Faction)
                    continue;
                if (!result.Contains(pawn))
                    result.Add(pawn);
            }
            return result;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!initialized || !parent.Spawned || parent.Map == null || parent.Faction == null || dormantReleased)
                return;
            if (!parent.IsHashIntervalTick(600))
                return;

            if (ShouldWakeDormant())
                WakeDormant();
        }

        private bool ShouldWakeDormant()
        {
            int livingDormant = dormantMembers.Count(p => p != null && !p.Dead);
            int activeLiving = Math.Max(0, LivingDemographicCount - livingDormant);
            int lossTrigger = Math.Max(0, initialActiveFounderCount - Math.Max(1, PopulationProps.activeLossesBeforeDormantWake));
            if (activeLiving <= lossTrigger)
                return true;

            float radius = Math.Max(1f, PopulationProps.dormantWakeRadius);
            float radiusSq = radius * radius;
            foreach (Pawn colonist in parent.Map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist == null || colonist.Dead)
                    continue;
                if (colonist.Position.DistanceToSquared(parent.Position) <= radiusSq)
                    return true;
                if (dormantPods.Any(p => p != null && p.Spawned && colonist.Position.DistanceToSquared(p.Position) <= radiusSq))
                    return true;
            }
            return false;
        }

        private void WakeDormant()
        {
            dormantReleased = true;
            HediffDef hibernating = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
            List<Pawn> released = new List<Pawn>();
            foreach (Pawn pawn in dormantMembers)
            {
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map || pawn.Faction != parent.Faction)
                    continue;
                if (hibernating != null && pawn.health?.hediffSet != null)
                {
                    Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hibernating);
                    if (existing != null)
                        pawn.health.RemoveHediff(existing);
                }
                pawn.jobs?.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
                released.Add(pawn);
            }

            if (released.Count == 0)
                return;
            Lord lord = LordMaker.MakeNewLord(parent.Faction, new LordJob_DefendBase(parent.Faction, parent.Position, 60000), parent.Map);
            foreach (Pawn pawn in released)
                lord.AddPawn(pawn);
        }

        public override string CompInspectStringExtra()
        {
            if (!initialized)
                return null;
            int livingDormant = dormantReleased ? 0 : dormantMembers.Count(p => p != null && !p.Dead);
            int active = Math.Max(0, LivingDemographicCount - livingDormant);
            int stock = feedingStock.Count(p => p != null && !p.Dead);
            return "Mature Hive population: " + active + " active / " + livingDormant + " ordinary dormant / " + stock + " finite feeding stock / " + foundingPopulationCap + " Wraith cap";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref initialized, "wngMatureHivePopulationInitialized", false);
            Scribe_Values.Look(ref foundingPopulationCap, "wngMatureHiveFoundingPopulationCap", 0);
            Scribe_Values.Look(ref initialActiveFounderCount, "wngMatureHiveInitialActiveFounderCount", 0);
            Scribe_Values.Look(ref dormantReleased, "wngMatureHiveDormantReleased", false);
            Scribe_Collections.Look(ref demographicMembers, "wngMatureHiveDemographicMembers", LookMode.Reference);
            Scribe_Collections.Look(ref dormantMembers, "wngMatureHiveDormantMembers", LookMode.Reference);
            Scribe_Collections.Look(ref dormantPods, "wngMatureHiveDormantPods", LookMode.Reference);
            Scribe_Collections.Look(ref feedingStock, "wngMatureHiveFeedingStock", LookMode.Reference);
            Scribe_Collections.Look(ref feedingNiches, "wngMatureHiveFeedingNiches", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                demographicMembers = demographicMembers ?? new List<Pawn>();
                dormantMembers = dormantMembers ?? new List<Pawn>();
                dormantPods = dormantPods ?? new List<Building_Bed>();
                feedingStock = feedingStock ?? new List<Pawn>();
                feedingNiches = feedingNiches ?? new List<Building_Bed>();
            }
        }
    }
}
