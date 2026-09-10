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
    /// Offensive mission layer for one real Puddle Jumper. Native CompShuttle/CompTransporter
    /// remain authoritative for boarding, loading, landing and launch. WNG only supplies the
    /// Stargate-style two-pass Ancient drone attack sequence and mission state.
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

            completedPasses = 0;
            dronesLaunched = 0;
            phase = WNGShuttleRaidPhase.AttackPasses;
            nextPhaseTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.passIntervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Destroyed != false || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextPhaseTick)
                return;

            if (phase == WNGShuttleRaidPhase.AttackPasses)
            {
                ExecuteDronePass();
                completedPasses++;
                if (completedPasses >= Math.Max(1, Props.attackPasses))
                {
                    phase = WNGShuttleRaidPhase.LandedRaid;
                    nextPhaseTick = now + Math.Max(60, Props.landedRaidDelayTicks);
                }
                else
                {
                    nextPhaseTick = now + Math.Max(60, Props.passIntervalTicks);
                }
            }
        }

        private void ExecuteDronePass()
        {
            if (parent?.Spawned != true || parent.Map == null || Props.droneProjectile == null)
                return;

            float radius = Math.Max(1f, Props.acquisitionRadius);
            float radiusSq = radius * radius;
            int limit = Math.Max(1, Props.dronesPerPass);

            List<Pawn> targets = parent.Map.mapPawns.AllPawnsSpawned
                .Where(IsValidCombatTarget)
                .Where(p => p.Position.DistanceToSquared(parent.Position) <= radiusSq)
                .OrderBy(p => p.Position.DistanceToSquared(parent.Position))
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

                GenSpawn.Spawn(projectile, parent.Position, parent.Map);
                projectile.Launch(parent, parent.DrawPos, target, target, ProjectileHitFlags.IntendedTarget);
                dronesLaunched++;
            }
        }

        private bool IsValidCombatTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map)
                return false;
            if (pawn.Faction == parent.Faction)
                return false;
            if (parent.Faction == null)
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
