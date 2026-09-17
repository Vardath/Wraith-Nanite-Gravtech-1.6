using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AncientControlChair : CompProperties
    {
        public float droneRange = 82f;
        public float droneFuelCost = 1f;
        public int operatorCheckInterval = 60;
        public string droneProjectileDefName = "WNG_AncientDroneProjectile";
        public string operatorJobDefName = "WNG_OperateAncientControlChair";
        public CompProperties_AncientControlChair() { compClass = typeof(CompAncientControlChair); }
    }

    public sealed class CompAncientControlChair : ThingComp
    {
        private Pawn operatorPawn;
        private int nextOperatorValidationTick;
        public CompProperties_AncientControlChair Props => (CompProperties_AncientControlChair)props;
        public Pawn OperatorPawn => operatorPawn;

        public bool NetworkActive
        {
            get
            {
                if (!parent.Spawned || parent.Map == null || parent.Faction == null)
                    return false;
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                return (power == null || power.PowerOn) && IsOperatorActivelyControlling(operatorPawn);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref operatorPawn, "wngAncientChairOperator");
            Scribe_Values.Look(ref nextOperatorValidationTick, "wngAncientChairNextValidation", 0);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || Find.TickManager == null || Find.TickManager.TicksGame < nextOperatorValidationTick)
                return;
            nextOperatorValidationTick = Find.TickManager.TicksGame + Math.Max(30, Props.operatorCheckInterval);
            if (operatorPawn != null && (!operatorPawn.Spawned || operatorPawn.Map != parent.Map || operatorPawn.Dead))
                operatorPawn = null;
        }

        public override string CompInspectStringExtra()
        {
            string status = operatorPawn == null ? "Neural operator: none" : "Neural operator: " + operatorPawn.LabelShortCap + (NetworkActive ? " (linked)" : " (inactive)");
            CompRefuelable fuel = parent.TryGetComp<CompRefuelable>();
            if (fuel != null)
                status += $"\nAncient drones loaded: {Math.Floor(fuel.Fuel)} / {Math.Floor(fuel.Props.fuelCapacity)}";
            return status;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;
            if (parent.Faction != Faction.OfPlayer || !parent.Spawned)
                yield break;

            Command_Action link = new Command_Action
            {
                defaultLabel = operatorPawn == null ? "Link Ancient operator" : "Change Ancient operator",
                defaultDesc = "Assign an ATA-compatible pawn to physically operate this Ancient control chair.",
                icon = ContentFinder<Texture2D>.Get("UI/WNG/AncientAffinity", true),
                action = BeginSelectOperator
            };
            yield return link;

            Command_Action launch = new Command_Action
            {
                defaultLabel = "Launch Ancient drone",
                defaultDesc = "Launch one physical reconstructed Ancient drone at a hostile target. One loaded drone is consumed per successful launch.",
                icon = ContentFinder<Texture2D>.Get("UI/WNG/AncientDrone", true),
                action = BeginSelectDroneTarget
            };
            if (!NetworkActive)
                launch.Disable("A compatible neural operator must remain at the powered control chair.");
            else if (!HasDroneAmmo())
                launch.Disable("No Ancient drones are loaded.");
            yield return launch;
        }

        private void BeginSelectOperator()
        {
            TargetingParameters parms = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetLocations = false,
                canTargetSelf = false,
                validator = t => t.Thing is Pawn p && IsCompatibleOperator(p)
            };
            Find.Targeter.BeginTargeting(parms, t => AssignOperator(t.Thing as Pawn));
        }

        private void AssignOperator(Pawn pawn)
        {
            if (!IsCompatibleOperator(pawn) || pawn.Map != parent.Map || pawn.jobs == null)
                return;
            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(Props.operatorJobDefName);
            if (jobDef == null)
                return;
            Job job = JobMaker.MakeJob(jobDef, parent);
            job.playerForced = true;
            if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                operatorPawn = pawn;
        }

        private bool IsOperatorActivelyControlling(Pawn pawn)
        {
            if (!IsCompatibleOperator(pawn) || !pawn.Spawned || pawn.Map != parent.Map || pawn.Downed)
                return false;
            if (pawn.CurJobDef?.defName != Props.operatorJobDefName || pawn.CurJob?.targetA.Thing != parent)
                return false;
            return pawn.Position.DistanceToSquared(parent.InteractionCell) <= 2f;
        }

        private static bool IsCompatibleOperator(Pawn pawn)
        {
            return pawn != null && !pawn.Dead && !pawn.Downed && pawn.Faction == Faction.OfPlayer && AncientCompatibilityUtility.IsCompatible(pawn);
        }

        private bool HasDroneAmmo()
        {
            CompRefuelable fuel = parent.TryGetComp<CompRefuelable>();
            return fuel != null && fuel.Fuel + 0.0001f >= Math.Max(0.01f, Props.droneFuelCost);
        }

        private void BeginSelectDroneTarget()
        {
            if (!NetworkActive || !HasDroneAmmo())
                return;
            TargetingParameters parms = new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = true,
                canTargetItems = false,
                canTargetLocations = false,
                canTargetSelf = false,
                validator = t => ValidDroneTarget(t.Thing)
            };
            Find.Targeter.BeginTargeting(parms, LaunchDroneAt);
        }

        private bool ValidDroneTarget(Thing target)
        {
            return target != null && !target.Destroyed && target.Spawned && target.Map == parent.Map && target.Faction != null &&
                   parent.Faction != null && target.Faction.HostileTo(parent.Faction) &&
                   target.Position.DistanceToSquared(parent.Position) <= Props.droneRange * Props.droneRange;
        }

        private void LaunchDroneAt(LocalTargetInfo target)
        {
            if (!NetworkActive || !HasDroneAmmo() || !ValidDroneTarget(target.Thing))
                return;
            ThingDef projectileDef = DefDatabase<ThingDef>.GetNamedSilentFail(Props.droneProjectileDefName);
            Projectile projectile = projectileDef == null ? null : ThingMaker.MakeThing(projectileDef) as Projectile;
            CompRefuelable fuel = parent.TryGetComp<CompRefuelable>();
            float cost = Math.Max(0.01f, Props.droneFuelCost);
            if (projectile == null || fuel == null || fuel.Fuel + 0.0001f < cost)
            {
                if (projectile != null && !projectile.Destroyed)
                    projectile.Destroy(DestroyMode.Vanish);
                return;
            }

            float before = fuel.Fuel;
            try
            {
                fuel.ConsumeFuel(cost);
                GenSpawn.Spawn(projectile, parent.Position, parent.Map);
                projectile.Launch(parent, parent.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, false, null);
            }
            catch
            {
                if (!projectile.Destroyed)
                    projectile.Destroy(DestroyMode.Vanish);
                float consumed = Math.Max(0f, before - fuel.Fuel);
                if (consumed > 0.0001f)
                    fuel.Refuel(consumed / Math.Max(0.0001f, fuel.Props.FuelMultiplierCurrentDifficulty));
                return;
            }
            DefDatabase<SoundDef>.GetNamedSilentFail("WNG_AncientDroneLaunch")?.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
        }
    }

    public sealed class JobDriver_OperateAncientControlChair : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil operate = ToilMaker.MakeToil("OperateAncientControlChair");
            operate.defaultCompleteMode = ToilCompleteMode.Never;
            operate.tickAction = () => pawn.rotationTracker.FaceTarget(TargetA);
            yield return operate;
        }
    }
}
