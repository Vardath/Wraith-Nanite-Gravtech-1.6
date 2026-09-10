using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public sealed class ReplicatorSpecialistExtension : DefModExtension
    {
        public float controllerRadius = 30f;
        public float repairRange = 35f;
        public int repairWorkTicks = 180;
        public float repairAmount = 4f;
        public float breachRange = 45f;
        public int breachWorkTicks = 150;
        public float breachDamage = 38f;
        public float artilleryMinRange = 8f;
        public float artilleryRange = 42f;
        public int artilleryWarmupTicks = 180;
        public float artilleryDamage = 18f;
        public float artilleryArmorPenetration = 0.25f;
    }

    internal static class ReplicatorSpecialistUtility
    {
        public static bool IsAutonomous(Pawn pawn)
            => pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map != null
                && pawn.Faction != null && pawn.Faction != Faction.OfPlayer
                && !pawn.IsColonyMechPlayerControlled && !ReplicatorEMP.IsSuppressed(pawn);

        public static bool IsReplicator(Pawn pawn)
            => pawn?.TryGetComp<CompReplicatorState>() != null;

        public static bool SameDomain(Pawn a, Pawn b)
            => a != null && b != null && a.Faction == b.Faction;

        public static ReplicatorSpecialistExtension Extension(Pawn pawn)
            => pawn?.def?.GetModExtension<ReplicatorSpecialistExtension>();
    }

    internal static class ReplicatorCoordinationUtility
    {
        public static Pawn FindControllerFocus(Pawn pawn)
        {
            if (!ReplicatorSpecialistUtility.IsAutonomous(pawn)) return null;

            Pawn controller = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p.def?.defName == "WNG_ReplicatorController"
                    && ReplicatorSpecialistUtility.IsAutonomous(p)
                    && ReplicatorSpecialistUtility.SameDomain(pawn, p))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .FirstOrDefault(p =>
                {
                    float radius = Math.Max(1f, ReplicatorSpecialistUtility.Extension(p)?.controllerRadius ?? 30f);
                    return p.Position.DistanceToSquared(pawn.Position) <= radius * radius;
                });

            if (controller == null) return null;
            float focusRadius = Math.Max(1f, ReplicatorSpecialistUtility.Extension(controller)?.controllerRadius ?? 30f) * 1.5f;
            float focusRadiusSq = focusRadius * focusRadius;

            return pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned
                    && pawn.HostileTo(p)
                    && p.Position.DistanceToSquared(controller.Position) <= focusRadiusSq
                    && pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(p => p.Position.DistanceToSquared(controller.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
        }
    }

    public sealed class JobGiver_ReplicatorControllerFocus : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorSpecialistUtility.IsAutonomous(pawn)) return null;
            if (pawn.def?.defName == "WNG_ReplicatorRepairer" || pawn.def?.defName == "WNG_ReplicatorArtillery") return null;
            Pawn target = ReplicatorCoordinationUtility.FindControllerFocus(pawn);
            return target == null ? null : JobMaker.MakeJob(JobDefOf.AttackMelee, target);
        }
    }

    public sealed class JobGiver_ReplicatorRepair : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.def?.defName != "WNG_ReplicatorRepairer" || !ReplicatorSpecialistUtility.IsAutonomous(pawn)) return null;
            ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
            float range = Math.Max(1f, ext?.repairRange ?? 35f);
            float rangeSq = range * range;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned
                    && ReplicatorSpecialistUtility.IsReplicator(p)
                    && ReplicatorSpecialistUtility.SameDomain(pawn, p)
                    && p.health?.hediffSet?.hediffs?.OfType<Hediff_Injury>().Any(i => i.Severity > 0f) == true
                    && p.Position.DistanceToSquared(pawn.Position) <= rangeSq
                    && pawn.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .OrderByDescending(p => p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(i => i.Severity))
                .ThenBy(p => p.Position.DistanceToSquared(pawn.Position))
                .FirstOrDefault();

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorRepair");
            return target == null || jobDef == null ? null : JobMaker.MakeJob(jobDef, target);
        }
    }

    public sealed class JobDriver_ReplicatorRepair : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMP.IsSuppressed(pawn));
            this.FailOn(() => job.targetA.Pawn?.Faction != pawn.Faction);
            yield return Toils_Goto.GotoThing(Target, PathEndMode.Touch);

            ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
            Toil work = Toils_General.Wait(Math.Max(30, ext?.repairWorkTicks ?? 180));
            work.WithProgressBarToilDelay(Target);
            yield return work;

            Toil finish = ToilMaker.MakeToil("WNGReplicatorRepairFinish");
            finish.initAction = () =>
            {
                Pawn target = job.targetA.Pawn;
                if (target == null || target.Dead || target.Faction != pawn.Faction) return;
                float remaining = Math.Max(0f, ReplicatorSpecialistUtility.Extension(pawn)?.repairAmount ?? 4f);
                foreach (Hediff_Injury injury in target.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                    .Where(i => i.Severity > 0f).OrderByDescending(i => i.Severity).ToList())
                {
                    if (remaining <= 0f) break;
                    float amount = Math.Min(remaining, injury.Severity);
                    injury.Heal(amount);
                    remaining -= amount;
                }
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    public sealed class JobGiver_ReplicatorBreach : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.def?.defName != "WNG_ReplicatorBurrower" || !ReplicatorSpecialistUtility.IsAutonomous(pawn)) return null;
            ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
            float range = Math.Max(1f, ext?.breachRange ?? 45f);
            float rangeSq = range * range;

            Thing target = pawn.Map.listerThings.AllThings
                .Where(t => t != null && !t.Destroyed && t.Spawned && t.def?.category == ThingCategory.Building
                    && t.def.destroyable && t.Faction != null && pawn.HostileTo(t)
                    && t.Position.DistanceToSquared(pawn.Position) <= rangeSq
                    && pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(t => t.Position.DistanceToSquared(pawn.Position))
                .ThenBy(t => t.HitPoints)
                .FirstOrDefault();

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorBreach");
            return target == null || jobDef == null ? null : JobMaker.MakeJob(jobDef, target);
        }
    }

    public sealed class JobDriver_ReplicatorBreach : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
            => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMP.IsSuppressed(pawn));
            yield return Toils_Goto.GotoThing(Target, PathEndMode.Touch);

            ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
            Toil work = Toils_General.Wait(Math.Max(30, ext?.breachWorkTicks ?? 150));
            work.WithProgressBarToilDelay(Target);
            yield return work;

            Toil strike = ToilMaker.MakeToil("WNGReplicatorBreachStrike");
            strike.initAction = () =>
            {
                Thing target = job.targetA.Thing;
                if (target == null || target.Destroyed || !pawn.HostileTo(target)) return;
                float damage = Math.Max(1f, ReplicatorSpecialistUtility.Extension(pawn)?.breachDamage ?? 38f);
                target.TakeDamage(new DamageInfo(DamageDefOf.Crush, damage, instigator: pawn));
            };
            strike.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return strike;
        }
    }

    public sealed class JobGiver_ReplicatorArtillery : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.def?.defName != "WNG_ReplicatorArtillery" || !ReplicatorSpecialistUtility.IsAutonomous(pawn)) return null;
            ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
            float minRange = Math.Max(0f, ext?.artilleryMinRange ?? 8f);
            float maxRange = Math.Max(minRange + 1f, ext?.artilleryRange ?? 42f);
            float minSq = minRange * minRange;
            float maxSq = maxRange * maxRange;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && pawn.HostileTo(p))
                .Where(p =>
                {
                    float d = p.Position.DistanceToSquared(pawn.Position);
                    return d >= minSq && d <= maxSq && GenSight.LineOfSight(pawn.Position, p.Position, pawn.Map);
                })
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorArtilleryFire");
            return target == null || jobDef == null ? null : JobMaker.MakeJob(jobDef, target);
        }
    }

    public sealed class JobDriver_ReplicatorArtilleryFire : JobDriver
    {
        private const TargetIndex Target = TargetIndex.A;
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(Target);
            this.FailOn(() => ReplicatorEMP.IsSuppressed(pawn));
            this.FailOn(() => job.targetA.Pawn == null || !pawn.HostileTo(job.targetA.Pawn));

            ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
            Toil warmup = Toils_General.Wait(Math.Max(30, ext?.artilleryWarmupTicks ?? 180));
            warmup.WithProgressBarToilDelay(Target);
            yield return warmup;

            Toil fire = ToilMaker.MakeToil("WNGReplicatorArtilleryFire");
            fire.initAction = () =>
            {
                Pawn target = job.targetA.Pawn;
                if (target == null || target.Dead || !target.Spawned || target.Map != pawn.Map || !pawn.HostileTo(target)) return;
                if (!GenSight.LineOfSight(pawn.Position, target.Position, pawn.Map)) return;
                ReplicatorSpecialistExtension props = ReplicatorSpecialistUtility.Extension(pawn);
                float damage = Math.Max(1f, props?.artilleryDamage ?? 18f);
                float penetration = Math.Max(0f, props?.artilleryArmorPenetration ?? 0.25f);
                target.TakeDamage(new DamageInfo(DamageDefOf.Bullet, damage, penetration, instigator: pawn));
            };
            fire.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return fire;
        }
    }
}
