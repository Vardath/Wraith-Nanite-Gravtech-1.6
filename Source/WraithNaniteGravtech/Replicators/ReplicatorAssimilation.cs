using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Data-driven tuning shared by autonomous block Replicator forms.
    /// Player-control behavior is intentionally not defined here.
    /// </summary>
    public sealed class ReplicatorBlockExtension : DefModExtension
    {
        public int assimilationTicks = 300;
        public int assimilationOffspringCount = 2;
        public int hostilePopulationCap = 120;
        public float assimilationSearchRadius = 60f;
    }

    public static class ReplicatorAssimilationUtility
    {
        private const string DroneKindDefName = "WNG_ReplicatorDrone";
        private const string ReplicatorMatterDefName = "WNG_ReplicatorMatter";
        private const string ReplicatorCoreFragmentDefName = "WNG_ReplicatorCoreFragment";

        public static ReplicatorBlockExtension ExtensionFor(Pawn pawn)
        {
            return pawn?.def?.GetModExtension<ReplicatorBlockExtension>();
        }

        public static bool IsBlockReplicator(Pawn pawn)
        {
            return ExtensionFor(pawn) != null;
        }

        public static bool CanAutonomouslyAssimilate(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Map == null || pawn.Faction == null)
                return false;

            // Player Replicators are a separate future control contract. They must never inherit
            // autonomous colony-eating simply because they share a mechanical body definition.
            if (pawn.Faction == Faction.OfPlayer)
                return false;

            if (ReplicatorInterferenceUtility.IsEmpDisrupted(pawn) ||
                TemporaryAsuranIntrusionUtility.IsCommandSuppressed(pawn))
                return false;

            // Mature-swarm Controllers are coordination support bodies, not matter harvesters.
            if (ReplicatorCoordinationUtility.IsController(pawn))
                return false;

            if (pawn.Spawned && ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
                return false;

            return ExtensionFor(pawn) != null;
        }

        public static bool IsAssimilationTarget(Thing target, Pawn pawn)
        {
            if (!CanAutonomouslyAssimilate(pawn) || target == null || target.Destroyed || !target.Spawned || target == pawn)
                return false;

            if (target is Pawn)
                return false;

            ThingDef def = target.def;
            if (def == null || !def.destroyable)
                return false;

            string defName = def.defName;
            if (defName == ReplicatorMatterDefName || defName == ReplicatorCoreFragmentDefName)
                return false;

            // Never consume a structure belonging to the same Replicator faction.
            if (target.Faction != null && target.Faction == pawn.Faction)
                return false;

            // A live containment field protects both a Replicator inside it and material/technology
            // stored inside it. The utility rechecks live power state, so power loss immediately
            // removes this suppression without a separate cached flag.
            if (ReplicatorContainmentUtility.BlocksAssimilation(pawn, target))
                return false;

            // Phase 1 deliberately limits ordinary assimilation to physical items and artificial
            // buildings. Plants/natural rock can be added later only after their yield semantics are
            // explicitly decided, rather than inheriting old behavior by accident.
            if (def.category == ThingCategory.Item)
                return target.stackCount > 0;

            return def.category == ThingCategory.Building && def.building != null && !def.building.isNaturalRock;
        }

        /// <summary>
        /// Canonical reachable environmental assimilation target used by both ordinary matter
        /// consumption and the starvation-biological fallback. Keeping this search in one place
        /// guarantees living prey can never outrank a target the current environmental ecology
        /// would actually consume.
        /// </summary>
        public static Thing FindClosestAssimilationTarget(Pawn pawn)
        {
            if (!CanAutonomouslyAssimilate(pawn))
                return null;

            ReplicatorBlockExtension ext = ExtensionFor(pawn);
            if (ext == null)
                return null;

            Predicate<Thing> validator = delegate(Thing thing)
            {
                return IsAssimilationTarget(thing, pawn) && pawn.CanReserve(thing);
            };

            float radius = Math.Max(5f, ext.assimilationSearchRadius);
            TraverseParms traverse = TraverseParms.For(pawn);

            CompReplicatorAdaptation adaptation =
                pawn.TryGetComp<CompReplicatorAdaptation>();
            ReplicatorAdaptationFlags unresolvedHistory =
                adaptation == null
                    ? ReplicatorAdaptationFlags.None
                    : adaptation.HistoricalInterests & ~adaptation.Learned;

            if (unresolvedHistory != ReplicatorAdaptationFlags.None)
            {
                Predicate<Thing> historicalValidator = delegate(Thing thing)
                {
                    return validator(thing) &&
                           adaptation.HistoricalInterestMatches(
                               ReplicatorAdaptationUtility.EvidenceFrom(thing));
                };

                Thing historicalItem =
                    GenClosest.ClosestThing_Global_Reachable(
                        pawn.Position,
                        pawn.Map,
                        pawn.Map.listerThings.ThingsInGroup(
                            ThingRequestGroup.HaulableEver),
                        PathEndMode.Touch,
                        traverse,
                        radius,
                        historicalValidator);

                Thing historicalBuilding =
                    GenClosest.ClosestThing_Global_Reachable(
                        pawn.Position,
                        pawn.Map,
                        pawn.Map.listerThings.ThingsInGroup(
                            ThingRequestGroup.BuildingArtificial),
                        PathEndMode.Touch,
                        traverse,
                        radius,
                        historicalValidator);

                if (historicalItem != null ||
                    historicalBuilding != null)
                {
                    if (historicalItem == null)
                        return historicalBuilding;
                    if (historicalBuilding == null)
                        return historicalItem;

                    return pawn.Position.DistanceToSquared(
                               historicalItem.Position) <=
                           pawn.Position.DistanceToSquared(
                               historicalBuilding.Position)
                        ? historicalItem
                        : historicalBuilding;
                }
            }

            Thing item = GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver),
                PathEndMode.Touch,
                traverse,
                radius,
                validator);

            Thing building = GenClosest.ClosestThing_Global_Reachable(
                pawn.Position,
                pawn.Map,
                pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial),
                PathEndMode.Touch,
                traverse,
                radius,
                validator);

            if (item == null)
                return building;
            if (building == null)
                return item;

            return pawn.Position.DistanceToSquared(item.Position) <=
                   pawn.Position.DistanceToSquared(building.Position)
                ? item
                : building;
        }

        public static int CountHostileBlocks(Map map)
        {
            if (map == null)
                return 0;

            int count = 0;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!IsBlockReplicator(pawn) || pawn.Faction == null || Faction.OfPlayer == null)
                    continue;

                if (pawn.Faction.HostileTo(Faction.OfPlayer))
                    count++;
            }
            return count;
        }

        public static int HostilePopulationCap(ReplicatorBlockExtension ext)
        {
            // The Def value remains a safe load-time fallback; once ModSettings exist, the player's
            // current global cap is authoritative for every hostile block-Replicator creation path.
            return WNGMod.Settings != null
                ? WNGSettingsUtility.HostileReplicatorCap
                : Math.Max(1, ext?.hostilePopulationCap ?? 120);
        }

        public static bool HasPopulationRoom(Pawn parent, int offspringCount)
        {
            ReplicatorBlockExtension ext = ExtensionFor(parent);
            if (parent?.Map == null || ext == null || offspringCount <= 0)
                return false;

            if (Faction.OfPlayer != null && parent.Faction != null && parent.Faction.HostileTo(Faction.OfPlayer))
                return CountHostileBlocks(parent.Map) + offspringCount <= HostilePopulationCap(ext);

            return true;
        }

        public static bool TryCommitAssimilation(Pawn parent, Thing target)
        {
            if (!IsAssimilationTarget(target, parent))
                return false;

            ReplicatorBlockExtension ext = ExtensionFor(parent);
            if (ext == null)
                return false;

            int offspringCount = Math.Max(1, ext.assimilationOffspringCount);
            if (!HasPopulationRoom(parent, offspringCount))
                return false;

            PawnKindDef droneKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(DroneKindDefName);
            if (droneKind == null || parent.Map == null || parent.Faction == null)
                return false;

            Map map = parent.Map;
            IntVec3 origin = target.Position;
            ReplicatorAdaptationEvidence adaptationEvidence = ReplicatorAdaptationUtility.EvidenceFrom(target);
            List<Pawn> staged = new List<Pawn>(offspringCount);

            try
            {
                // Recheck the cap immediately before each placement. RimWorld is single-threaded, but
                // multiple Replicator jobs can complete on adjacent ticks and must not leapfrog the cap.
                for (int i = 0; i < offspringCount; i++)
                {
                    if (!HasPopulationRoom(parent, offspringCount - staged.Count))
                    {
                        Rollback(staged);
                        return false;
                    }

                    Pawn child = PawnGenerator.GeneratePawn(droneKind, parent.Faction);
                    ReplicatorDomainUtility.CopyDomain(parent, child);
                    TemporaryAsuranIntrusionUtility.CopyState(parent, child);
                    ReplicatorSovereignControlUtility.CopyState(parent, child);
                    child.TryGetComp<CompReplicatorAdaptation>()?.InheritFrom(parent.TryGetComp<CompReplicatorAdaptation>());
                    if (!GenPlace.TryPlaceThing(child, origin, map, ThingPlaceMode.Near))
                    {
                        if (!child.Destroyed)
                            child.Destroy(DestroyMode.Vanish);
                        Rollback(staged);
                        return false;
                    }
                    staged.Add(child);
                }

                // The real environmental target is the commit point. If it changed while the job was
                // running, do not exchange it for offspring.
                if (!IsAssimilationTarget(target, parent))
                {
                    Rollback(staged);
                    return false;
                }

                try
                {
                    target.Destroy(DestroyMode.Vanish);
                }
                catch (Exception ex)
                {
                    // If RimWorld actually completed destruction before throwing, conservation says the
                    // staged offspring are now the committed result. Otherwise roll them back.
                    if (!target.Destroyed)
                    {
                        Rollback(staged);
                        Log.Error("[WNG] Replicator assimilation target destruction failed before commit: " + ex);
                        return false;
                    }
                    Log.Error("[WNG] Replicator assimilation target reported an exception after destruction; keeping committed offspring: " + ex);
                }

                if (!target.Destroyed)
                {
                    Rollback(staged);
                    return false;
                }

                // Adaptation is post-commit knowledge. A learning/share failure must never roll back
                // already-committed offspring after the real environmental target is gone.
                try
                {
                    ReplicatorAdaptationUtility.ShareEvidence(parent, adaptationEvidence);
                }
                catch (Exception ex)
                {
                    Log.Error("[WNG] Replicator assimilation committed but adaptation sharing failed: " + ex);
                }

                return true;
            }
            catch (Exception ex)
            {
                Rollback(staged);
                Log.Error("[WNG] Replicator assimilation transaction failed and staged offspring were rolled back: " + ex);
                return false;
            }
        }

        private static void Rollback(List<Pawn> pawns)
        {
            if (pawns == null)
                return;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && !pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);
            }
            pawns.Clear();
        }
    }

    public sealed class JobDriver_ReplicatorAssimilate : JobDriver
    {
        private Thing Target => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Target != null && pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Target == null || Target.Destroyed || !Target.Spawned ||
                              !ReplicatorAssimilationUtility.IsAssimilationTarget(Target, pawn));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            ReplicatorBlockExtension ext = ReplicatorAssimilationUtility.ExtensionFor(pawn);
            int baseDuration = Math.Max(90, ext?.assimilationTicks ?? 300);
            float adaptationFactor = pawn.TryGetComp<CompReplicatorAdaptationEffects>()?.AssimilationTimeFactor ?? 1f;
            float coordinationFactor = pawn.Map?.GetComponent<MapComponent_ReplicatorCoordination>()?.AssimilationFactorFor(pawn) ?? 1f;
            float specialistFactor = ReplicatorBurrowerUtility.AssimilationTimeFactor(pawn, Target);
            int duration = Math.Max(60, (int)Math.Round(baseDuration * adaptationFactor * coordinationFactor * specialistFactor));

            Toil work = ToilMaker.MakeToil("WNG_ReplicatorAssimilate");
            work.defaultCompleteMode = ToilCompleteMode.Delay;
            work.defaultDuration = duration;
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return work;

            Toil finish = ToilMaker.MakeToil("WNG_ReplicatorAssimilateFinish");
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            finish.initAction = delegate
            {
                Thing target = Target;
                if (target != null)
                    ReplicatorAssimilationUtility.TryCommitAssimilation(pawn, target);
            };
            yield return finish;
        }
    }

    public sealed class JobGiver_ReplicatorAssimilate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorAssimilationUtility.CanAutonomouslyAssimilate(pawn))
                return null;

            ReplicatorBlockExtension ext = ReplicatorAssimilationUtility.ExtensionFor(pawn);
            if (ext == null ||
                !ReplicatorAssimilationUtility.HasPopulationRoom(
                    pawn,
                    Math.Max(1, ext.assimilationOffspringCount)))
            {
                return null;
            }

            Thing target = ReplicatorAssimilationUtility.FindClosestAssimilationTarget(pawn);
            if (target == null)
                return null;

            JobDef jobDef =
                DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorAssimilate");
            return jobDef == null
                ? null
                : JobMaker.MakeJob(jobDef, target);
        }
    }
}
