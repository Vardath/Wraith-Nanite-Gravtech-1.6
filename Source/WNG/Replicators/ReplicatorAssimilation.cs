using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorAssimilation : CompProperties
    {
        public float searchRadius = 45f;
        public int searchIntervalTicks = 600;
        public int workTicks = 300;
        public int itemUnitsPerBite = 10;
        public float matterPerMarketValue = 0.02f;
        public float matterPerBuildingHitPoint = 0.025f;
        public float matterPerPlantHitPoint = 0.018f;
        public float minimumMatterYield = 1f;
        public float offspringMatterCost = 10f;
        public int maxOffspringPerAssimilation = 2;
        public int maxHostileReplicatorsPerMap = 120;
        public string offspringPawnKind = "WNG_ReplicatorDrone";
        public CompProperties_ReplicatorAssimilation() => compClass = typeof(CompReplicatorAssimilation);
    }

    public sealed class CompReplicatorAssimilation : ThingComp
    {
        private int nextSearchTick;
        private Thing cachedTarget;
        private float storedMatter;
        private CompProperties_ReplicatorAssimilation Props => (CompProperties_ReplicatorAssimilation)props;
        public int WorkTicks => Math.Max(60, Props.workTicks);
        public float StoredMatter => Math.Max(0f, storedMatter);

        public void SetStoredMatter(float value) => storedMatter = Math.Max(0f, value);
        public void AddStoredMatter(float value) { if (value > 0f) storedMatter += value; }

        public bool CanAct
        {
            get
            {
                Pawn pawn = parent as Pawn;
                return pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map != null
                    && pawn.Faction != Faction.OfPlayer && !pawn.IsColonyMechPlayerControlled
                    && !ReplicatorEMP.IsSuppressed(pawn)
                    && !ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position);
            }
        }

        public Thing GetTarget()
        {
            Pawn pawn = parent as Pawn;
            if (!CanAct || pawn == null) return null;
            int now = Find.TickManager.TicksGame;
            if (cachedTarget != null && IsTargetValid(pawn, cachedTarget)) return cachedTarget;
            if (now < nextSearchTick) return null;
            nextSearchTick = now + Math.Max(120, Props.searchIntervalTicks);
            float radiusSq = Props.searchRadius * Props.searchRadius;
            cachedTarget = pawn.Map.listerThings.AllThings
                .Where(t => IsTargetValid(pawn, t) && t.Position.DistanceToSquared(pawn.Position) <= radiusSq)
                .OrderByDescending(TargetPriority)
                .ThenBy(t => t.Position.DistanceToSquared(pawn.Position))
                .ThenBy(t => t.thingIDNumber)
                .FirstOrDefault();
            return cachedTarget;
        }

        private static float TargetPriority(Thing target)
        {
            if (target?.def == null) return 0f;
            string identity = ((target.def.defName ?? string.Empty) + " " + (target.def.label ?? string.Empty)).ToLowerInvariant();
            float score = 0f;
            if (identity.Contains("shield") || identity.Contains("barrier")) score += 1200f;
            if (identity.Contains("grav") || identity.Contains("gravity")) score += 1100f;
            if (target.def.IsWeapon && target.def.IsRangedWeapon) score += 950f;
            if (target.TryGetComp<CompPowerTrader>() != null || target.TryGetComp<CompPowerBattery>() != null) score += 850f;
            if (target.def.apparel != null) score += 700f;
            if (target.def.useHitPoints && target.def.BaseMaxHitPoints >= 500) score += 600f;
            if (target.def.category == ThingCategory.Building) score += 250f;
            if (target is Plant) score += 80f;
            score += Math.Min(250f, Math.Max(0f, target.MarketValue) * 0.05f);
            return score;
        }

        private static bool IsTargetValid(Pawn pawn, Thing thing)
        {
            if (pawn == null || thing == null || thing == pawn || thing.Destroyed || !thing.Spawned || thing.Map != pawn.Map) return false;
            if (thing is Pawn || thing is Corpse) return false;
            if (thing.Faction != null && thing.Faction == pawn.Faction) return false;
            if (thing.def == null || !thing.def.destroyable) return false;
            if (ReplicatorContainmentUtility.BlocksAssimilation(pawn, thing)) return false;
            bool item = thing.def.category == ThingCategory.Item && thing.def.EverHaulable;
            bool building = thing.def.category == ThingCategory.Building && thing.def.useHitPoints;
            bool plant = thing is Plant;
            if (!item && !building && !plant) return false;
            return pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly);
        }

        public void Finish(Thing target)
        {
            Pawn pawn = parent as Pawn;
            if (!CanAct || pawn == null || !IsTargetValid(pawn, target)) return;
            pawn.TryGetComp<CompReplicatorState>()?.RecordAssimilation(target);
            float yield = CalculateYield(target);
            Consume(target);
            cachedTarget = null;
            storedMatter += yield;
            SpawnAffordableOffspring(pawn);
        }

        private float CalculateYield(Thing target)
        {
            if (target.def.category == ThingCategory.Item)
            {
                int units = Math.Min(Math.Max(1, Props.itemUnitsPerBite), Math.Max(1, target.stackCount));
                return Math.Max(Props.minimumMatterYield, target.MarketValue * units * Math.Max(0f, Props.matterPerMarketValue));
            }
            if (target is Plant)
                return Math.Max(Props.minimumMatterYield, target.HitPoints * Math.Max(0f, Props.matterPerPlantHitPoint));
            return Math.Max(Props.minimumMatterYield, target.HitPoints * Math.Max(0f, Props.matterPerBuildingHitPoint));
        }

        private void Consume(Thing target)
        {
            if (target.def.category == ThingCategory.Item)
            {
                int units = Math.Min(Math.Max(1, Props.itemUnitsPerBite), target.stackCount);
                if (units < target.stackCount) target.stackCount -= units;
                else target.Destroy(DestroyMode.Vanish);
                return;
            }
            target.Destroy(DestroyMode.Vanish);
        }

        private void SpawnAffordableOffspring(Pawn parentPawn)
        {
            float cost = Math.Max(0.1f, Props.offspringMatterCost);
            int possibleByMatter = Math.Min(Math.Max(0, Props.maxOffspringPerAssimilation), (int)(storedMatter / cost));
            if (possibleByMatter <= 0) return;
            int cap = Math.Max(1, Props.maxHostileReplicatorsPerMap);
            int current = parentPawn.Map.mapPawns.AllPawnsSpawned.Count(p => p != null && !p.Dead && p.Faction == parentPawn.Faction && p.TryGetComp<CompReplicatorState>() != null);
            int possible = Math.Min(possibleByMatter, Math.Max(0, cap - current));
            if (possible <= 0) return;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.offspringPawnKind);
            if (kind == null) return;
            for (int i = 0; i < possible; i++)
            {
                Pawn child = PawnGenerator.GeneratePawn(kind, parentPawn.Faction);
                child.TryGetComp<CompReplicatorState>()?.CopyFrom(parentPawn.TryGetComp<CompReplicatorState>());
                child.TryGetComp<CompReplicatorSovereignty>()?.CopyAuthorityFrom(parentPawn.TryGetComp<CompReplicatorSovereignty>());
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(parentPawn.Position, parentPawn.Map, 2);
                GenSpawn.Spawn(child, cell, parentPawn.Map);
                storedMatter -= cost;
            }
        }

        public override string CompInspectStringExtra() => storedMatter > 0.01f ? $"Stored replication matter: {storedMatter:0.0}" : null;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextSearchTick, "wngReplicatorNextSearch", 0);
            Scribe_Values.Look(ref storedMatter, "wngReplicatorStoredMatter", 0f);
            Scribe_References.Look(ref cachedTarget, "wngReplicatorCachedTarget");
        }
    }

    public sealed class JobGiver_ReplicatorAssimilate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompReplicatorAssimilation comp = pawn?.TryGetComp<CompReplicatorAssimilation>();
            Thing target = comp?.GetTarget();
            if (target == null) return null;
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorAssimilate");
            return jobDef == null ? null : JobMaker.MakeJob(jobDef, target);
        }
    }

    public sealed class JobDriver_ReplicatorAssimilate : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMP.IsSuppressed(pawn) || ReplicatorContainmentUtility.BlocksAssimilation(pawn, job.targetA.Thing));
            yield return Toils_Goto.GotoThing(Target, PathEndMode.Touch);
            CompReplicatorAssimilation comp = pawn.TryGetComp<CompReplicatorAssimilation>();
            Toil work = Toils_General.Wait(comp?.WorkTicks ?? 300);
            work.WithProgressBarToilDelay(Target);
            yield return work;
            Toil finish = ToilMaker.MakeToil("WNGReplicatorAssimilationFinish");
            finish.initAction = () => pawn.TryGetComp<CompReplicatorAssimilation>()?.Finish(job.targetA.Thing);
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
