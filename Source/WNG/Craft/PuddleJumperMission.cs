using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_PuddleJumperRaidMission : CompProperties
    {
        public int attackPasses = 2;
        public int passIntervalTicks = 900;
        public int dronesPerPass = 3;
        public float acquisitionRadius = 45f;
        public int landedRaidDelayTicks = 1800;
        public ThingDef droneProjectile;

        public CompProperties_PuddleJumperRaidMission()
        {
            compClass = typeof(CompPuddleJumperRaidMission);
        }
    }

    /// <summary>
    /// Ancient/Lantean attack mission using the same physical Puddle Jumper through both skyfaller
    /// passes and final landing. Native shuttle transport/boarding remains untouched.
    /// </summary>
    public sealed class CompPuddleJumperRaidMission : ThingComp
    {
        private WNGShuttleRaidPhase phase = WNGShuttleRaidPhase.Idle;
        private int completedPasses;
        private int nextPhaseTick;
        private int dronesLaunched;

        private CompProperties_PuddleJumperRaidMission Props => (CompProperties_PuddleJumperRaidMission)props;

        public WNGShuttleRaidPhase Phase => phase;
        public int CompletedPasses => completedPasses;

        public void BeginTwoPassRaid()
        {
            if (parent?.Spawned != true || parent.Map == null || parent.Faction == null)
                return;

            Map map = parent.Map;
            IntVec3 firstPassCell = WNGShuttleFlightUtility.FindAttackPassCell(parent, map, parent.Position, oppositeSide: false);
            completedPasses = 0;
            dronesLaunched = 0;
            phase = WNGShuttleRaidPhase.AttackPasses;
            nextPhaseTick = int.MaxValue;

            if (!WNGShuttleFlightUtility.TryBeginPhysicalPasses(parent, map, firstPassCell))
            {
                phase = WNGShuttleRaidPhase.Stranded;
                nextPhaseTick = int.MaxValue;
            }
        }

        public bool ExecutePhysicalPass(Map map, IntVec3 passCell)
        {
            if (phase != WNGShuttleRaidPhase.AttackPasses || map == null || parent == null || parent.Destroyed)
                return false;

            ExecuteDronePass(map, passCell);
            completedPasses++;
            return completedPasses < Math.Max(1, Props.attackPasses);
        }

        public void NotifyPhysicallyLanded()
        {
            if (phase != WNGShuttleRaidPhase.AttackPasses || parent?.Spawned != true)
                return;
            phase = WNGShuttleRaidPhase.LandedRaid;
            nextPhaseTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.landedRaidDelayTicks);
        }

        private void ExecuteDronePass(Map map, IntVec3 passCell)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null || Props.droneProjectile == null)
                return;

            float radius = Math.Max(1f, Props.acquisitionRadius);
            float radiusSq = radius * radius;
            int limit = Math.Max(1, Props.dronesPerPass);

            List<Pawn> targets = map.mapPawns.AllPawnsSpawned
                .Where(p => IsValidCombatTarget(p, map))
                .Where(p => p.Position.DistanceToSquared(passCell) <= radiusSq)
                .OrderBy(p => p.Position.DistanceToSquared(passCell))
                .ThenBy(p => p.thingIDNumber)
                .Take(limit)
                .ToList();

            foreach (Pawn target in targets)
            {
                Thing thing = ThingMaker.MakeThing(Props.droneProjectile);
                if (!(thing is Projectile projectile))
                {
                    thing?.Destroy();
                    continue;
                }

                GenSpawn.Spawn(projectile, passCell, map);
                projectile.Launch(parent, passCell.ToVector3Shifted(), target, target, ProjectileHitFlags.IntendedTarget);
                dronesLaunched++;
            }
        }

        private bool IsValidCombatTarget(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map || parent?.Faction == null)
                return false;
            if (pawn.Faction == parent.Faction)
                return false;
            return pawn.HostileTo(parent.Faction) || parent.Faction.HostileTo(pawn.Faction);
        }

        public override string CompInspectStringExtra()
        {
            if (phase == WNGShuttleRaidPhase.Idle)
                return null;
            return "Puddle Jumper mission: " + phase +
                   "\nDrone passes: " + completedPasses + "/" + Math.Max(1, Props.attackPasses) +
                   "\nDrones launched: " + dronesLaunched;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref phase, "wngJumperRaidPhase", WNGShuttleRaidPhase.Idle);
            Scribe_Values.Look(ref completedPasses, "wngJumperCompletedPasses", 0);
            Scribe_Values.Look(ref nextPhaseTick, "wngJumperNextPhaseTick", 0);
            Scribe_Values.Look(ref dronesLaunched, "wngJumperDronesLaunched", 0);
        }
    }
}
