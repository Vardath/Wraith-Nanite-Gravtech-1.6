using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    internal static class ReplicatorJobUtility
    {
        private const int DefaultCombatExpiryTicks = 450;

        public static bool CanActAutonomously(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
                return false;
            if (pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled)
                return false;
            if (ReplicatorEMPSuppressionUtility.IsSuppressed(pawn))
                return false;
            return !ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(pawn);
        }

        public static Job MakeCombatJob(Pawn pawn, Thing target, int expiryTicks = DefaultCombatExpiryTicks)
        {
            if (pawn == null || target == null || target.Destroyed || !target.Spawned || pawn.Map != target.Map)
                return null;

            bool hasRangedWeapon = pawn.equipment?.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon;
            JobDef def = hasRangedWeapon ? JobDefOf.AttackStatic : JobDefOf.AttackMelee;
            Job job = JobMaker.MakeJob(def, target);
            job.expiryInterval = Math.Max(120, expiryTicks);
            job.checkOverrideOnExpire = true;
            return job;
        }

        public static bool IsRepairableReplicator(Pawn candidate, Pawn repairer)
        {
            if (candidate == null || repairer == null || candidate == repairer || candidate.Dead || !candidate.Spawned)
                return false;
            if (candidate.Map != repairer.Map || candidate.Faction == null || candidate.Faction != repairer.Faction)
                return false;
            if (!ReplicatorQueenUtility.IsBlockReplicator(candidate) && !ReplicatorCoordinationUtility.IsController(candidate))
                return false;
            return candidate.health?.hediffSet?.hediffs?.Any(h => h is Hediff_Injury injury && injury.Severity > 0.01f) == true;
        }

        public static Thing FindBiologicalAssimilationTarget(Pawn pawn)
        {
            if (!CanActAutonomously(pawn) || pawn.Map == null)
                return null;

            // Biological matter is a starvation fallback. If ordinary matter can still be reached,
            // the swarm keeps harvesting the environment instead of becoming an ordinary manhunter raid.
            if (ReplicatorUtility.FindClosestAssimilationTarget(pawn) != null)
                return null;

            Thing best = null;
            float bestDistance = float.MaxValue;

            foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (candidate == null || candidate == pawn || candidate.Dead || !candidate.Downed || !candidate.Spawned)
                    continue;
                if (candidate.RaceProps == null || candidate.RaceProps.IsMechanoid)
                    continue;
                if (candidate.Faction != null && candidate.Faction == pawn.Faction)
                    continue;
                if (!pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float distance = pawn.Position.DistanceToSquared(candidate.Position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            foreach (Corpse corpse in pawn.Map.listerThings.AllThings.OfType<Corpse>())
            {
                Pawn inner = corpse?.InnerPawn;
                if (corpse == null || corpse.Destroyed || !corpse.Spawned || inner?.RaceProps == null || inner.RaceProps.IsMechanoid)
                    continue;
                if (inner.Faction != null && inner.Faction == pawn.Faction)
                    continue;
                if (!pawn.CanReach(corpse, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float distance = pawn.Position.DistanceToSquared(corpse.Position);
                if (distance < bestDistance)
                {
                    best = corpse;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static Pawn FindBiologicalHuntTarget(Pawn pawn)
        {
            if (!CanActAutonomously(pawn) || pawn.Map == null)
                return null;
            if (ReplicatorUtility.FindClosestAssimilationTarget(pawn) != null)
                return null;
            if (FindBiologicalAssimilationTarget(pawn) != null)
                return null;

            Pawn best = null;
            float bestDistance = float.MaxValue;
            foreach (Pawn candidate in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (candidate == null || candidate == pawn || candidate.Dead || candidate.Downed || !candidate.Spawned)
                    continue;
                if (candidate.RaceProps == null || candidate.RaceProps.IsMechanoid)
                    continue;
                if (candidate.Faction == pawn.Faction)
                    continue;
                if (candidate.Faction != null && pawn.Faction != null && !pawn.Faction.HostileTo(candidate.Faction))
                    continue;
                if (!pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float distance = pawn.Position.DistanceToSquared(candidate.Position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static Thing FindShieldObjective(Pawn pawn)
        {
            if (!CanActAutonomously(pawn) || pawn.Map == null)
                return null;

            CompReplicatorAdaptation adaptation = pawn.TryGetComp<CompReplicatorAdaptation>();
            if (adaptation?.Specialization != ReplicatorAdaptationType.Shield || !adaptation.HasShieldCountermeasureWeapon)
                return null;
            if (pawn.Map.GetComponent<MapComponent_ReplicatorAdaptation>()?.ShieldCountermeasureLearned != true)
                return null;

            Thing best = null;
            float bestDistance = float.MaxValue;
            foreach (Thing candidate in pawn.Map.listerThings.AllThings)
            {
                if (candidate == null || candidate.Destroyed || !candidate.Spawned || candidate.def?.category != ThingCategory.Building)
                    continue;
                if (candidate.Faction == null || pawn.Faction == null || !pawn.Faction.HostileTo(candidate.Faction))
                    continue;
                if (!candidate.def.destroyable || ReplicatorAdaptationUtility.ClassifyTechnology(candidate) != ReplicatorAdaptationType.Shield)
                    continue;
                if (!pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float distance = pawn.Position.DistanceToSquared(candidate.Position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }
    }

    public sealed class JobGiver_ReplicatorSelfDefense : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorJobUtility.CanActAutonomously(pawn))
                return null;
            if (pawn.TryGetComp<CompReplicatorControl>()?.TryGetSelfDefenseTarget(out Pawn attacker) != true)
                return null;
            return ReplicatorJobUtility.MakeCombatJob(pawn, attacker, 300);
        }
    }

    public sealed class JobGiver_ReplicatorRetaliate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorJobUtility.CanActAutonomously(pawn) || pawn.Map == null)
                return null;

            // Only a bounded subset answers the shared threat. A controller raises coordination,
            // but never causes the entire swarm to abandon matter harvesting.
            bool controllerPresent = pawn.Map.GetComponent<MapComponent_ReplicatorCoordination>()?.HasActiveControllerFor(pawn) == true;
            int divisor = controllerPresent ? 3 : 5;
            if (Math.Abs(pawn.thingIDNumber) % divisor != 0)
                return null;

            if (pawn.Map.GetComponent<MapComponent_ReplicatorThreatResponse>()?.TryGetRetaliationTarget(pawn, out Pawn target) != true)
                return null;
            return ReplicatorJobUtility.MakeCombatJob(pawn, target, 360);
        }
    }

    public sealed class JobGiver_ReplicatorShieldBreaker : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Thing target = ReplicatorJobUtility.FindShieldObjective(pawn);
            return target == null ? null : ReplicatorJobUtility.MakeCombatJob(pawn, target, 600);
        }
    }

    public sealed class JobGiver_ReplicatorRepair : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorJobUtility.CanActAutonomously(pawn) || pawn.Map == null)
                return null;
            if (pawn.TryGetComp<CompReplicatorSpecialistBody>()?.Role != ReplicatorSpecialistRole.Repair)
                return null;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => ReplicatorJobUtility.IsRepairableReplicator(p, pawn))
                .Where(p => pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(p => pawn.Position.DistanceToSquared(p.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
            if (target == null)
                return null;

            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorRepairAlly");
            return def == null ? null : JobMaker.MakeJob(def, target);
        }
    }

    public sealed class JobGiver_ReplicatorAssimilate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorJobUtility.CanActAutonomously(pawn))
                return null;
            Thing target = ReplicatorUtility.FindClosestAssimilationTarget(pawn);
            if (target == null)
                return null;

            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorAssimilate");
            return def == null ? null : JobMaker.MakeJob(def, target);
        }
    }

    public sealed class JobGiver_ReplicatorAssimilateBiological : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Thing target = ReplicatorJobUtility.FindBiologicalAssimilationTarget(pawn);
            if (target == null)
                return null;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorAssimilateBiological");
            return def == null ? null : JobMaker.MakeJob(def, target);
        }
    }

    public sealed class JobGiver_ReplicatorBiologicalHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            Pawn target = ReplicatorJobUtility.FindBiologicalHuntTarget(pawn);
            return target == null ? null : ReplicatorJobUtility.MakeCombatJob(pawn, target, 420);
        }
    }

    public sealed class JobDriver_ReplicatorAssimilate : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMPSuppressionUtility.IsSuppressed(pawn));

            yield return Toils_Goto.GotoThing(Target, PathEndMode.Touch);

            Thing initialTarget = job.targetA.Thing;
            int baseTicks = pawn.TryGetComp<CompReplicatorAdaptation>()?.AssimilationTicks ?? 300;
            float specialistFactor = pawn.TryGetComp<CompReplicatorSpecialistBody>()?.AssimilationFactorFor(initialTarget) ?? 1f;
            int workTicks = Math.Max(60, (int)Math.Round(baseTicks * specialistFactor));
            Toil work = Toils_General.Wait(workTicks);
            work.WithProgressBarToilDelay(Target);
            yield return work;

            Toil finish = ToilMaker.MakeToil("ReplicatorAssimilateFinish");
            finish.initAction = () => FinishAssimilation(job.targetA.Thing);
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private void FinishAssimilation(Thing target)
        {
            if (target == null || target.Destroyed || !target.Spawned || pawn.Map == null)
                return;
            if (!ReplicatorUtility.IsAssimilationTarget(target, pawn))
                return;
            if (ReplicatorContainmentUtility.BlocksAssimilation(pawn, target) || ReplicatorEMPSuppressionUtility.IsSuppressed(pawn))
                return;

            Map map = pawn.Map;
            IntVec3 origin = target.Position;
            ThingDef inheritedMaterial = ReplicatorUtility.ResolveAssimilatedMaterial(target)
                                         ?? pawn.TryGetComp<CompReplicatorMaterial>()?.SourceDef;
            int childCount = EstimateOffspring(target);

            map.GetComponent<MapComponent_ReplicatorAdaptation>()?.RecordSuccessfulAssimilation(pawn, target);
            ConsumeTarget(target);
            ReplicatorUtility.SpawnOffspring(pawn, origin, inheritedMaterial, childCount);
        }

        private static int EstimateOffspring(Thing target)
        {
            if (target?.def?.category == ThingCategory.Item)
                return Math.Min(target.stackCount, 10) >= 8 ? 2 : 1;
            if (target?.def?.category == ThingCategory.Building)
            {
                int area = Math.Max(1, target.def.size.x * target.def.size.z);
                return area >= 4 || target.MaxHitPoints >= 350 ? 2 : 1;
            }
            return 1;
        }

        private static void ConsumeTarget(Thing target)
        {
            if (target == null || target.Destroyed)
                return;
            if (target.def.category == ThingCategory.Item && target.stackCount > 10)
            {
                target.stackCount -= 10;
                return;
            }
            target.Destroy(DestroyMode.Vanish);
        }
    }

    public sealed class JobDriver_ReplicatorRepairAlly : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;
        private const float RepairBudget = 12f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMPSuppressionUtility.IsSuppressed(pawn));
            this.FailOn(() => !(job.targetA.Thing is Pawn ally) || !ReplicatorJobUtility.IsRepairableReplicator(ally, pawn));

            yield return Toils_Goto.GotoThing(Target, PathEndMode.Touch);
            Toil work = Toils_General.Wait(180);
            work.WithProgressBarToilDelay(Target);
            yield return work;

            Toil finish = ToilMaker.MakeToil("ReplicatorRepairFinish");
            finish.initAction = RepairTarget;
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private void RepairTarget()
        {
            Pawn ally = job.targetA.Thing as Pawn;
            if (!ReplicatorJobUtility.IsRepairableReplicator(ally, pawn))
                return;

            float remaining = RepairBudget;
            List<Hediff_Injury> injuries = ally.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(i => i.Severity > 0.01f)
                .OrderByDescending(i => i.Severity)
                .ToList();

            for (int i = 0; i < injuries.Count && remaining > 0.01f; i++)
            {
                Hediff_Injury injury = injuries[i];
                float amount = Math.Min(remaining, injury.Severity);
                injury.Heal(amount);
                remaining -= amount;
            }
        }
    }

    public sealed class JobDriver_ReplicatorAssimilateBiological : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMPSuppressionUtility.IsSuppressed(pawn));
            yield return Toils_Goto.GotoThing(Target, PathEndMode.Touch);

            Toil work = Toils_General.Wait(420);
            work.WithProgressBarToilDelay(Target);
            yield return work;

            Toil finish = ToilMaker.MakeToil("ReplicatorBiologicalAssimilationFinish");
            finish.initAction = FinishBiologicalAssimilation;
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private void FinishBiologicalAssimilation()
        {
            Thing target = job.targetA.Thing;
            if (target == null || target.Destroyed || !target.Spawned || pawn.Map == null)
                return;
            if (ReplicatorContainmentUtility.BlocksAssimilation(pawn, target) || ReplicatorEMPSuppressionUtility.IsSuppressed(pawn))
                return;

            Pawn victim = target as Pawn;
            Corpse corpse = target as Corpse;
            Pawn sourcePawn = victim ?? corpse?.InnerPawn;
            if (sourcePawn?.RaceProps == null || sourcePawn.RaceProps.IsMechanoid)
                return;
            if (sourcePawn.Faction != null && sourcePawn.Faction == pawn.Faction)
                return;
            if (victim != null && !victim.Downed)
                return;

            IntVec3 origin = target.Position;
            int childCount = sourcePawn.BodySize >= 1.15f ? 2 : 1;
            ThingDef inheritedMaterial = pawn.TryGetComp<CompReplicatorMaterial>()?.SourceDef ?? ThingDefOf.Steel;

            if (victim != null)
            {
                victim.Kill(null);
                Corpse generatedCorpse = victim.Corpse;
                if (generatedCorpse != null && !generatedCorpse.Destroyed)
                    generatedCorpse.Destroy(DestroyMode.Vanish);
            }
            else if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }

            ReplicatorUtility.SpawnOffspring(pawn, origin, inheritedMaterial, childCount);
        }
    }
}
