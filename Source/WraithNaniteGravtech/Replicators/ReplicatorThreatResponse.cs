using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorThreatResponse : CompProperties
    {
        public int selfDefenseTicks = 900;
        public int swarmAlertTicks = 1800;
        public float swarmResponseChance = 0.24f;
        public float swarmResponseRadius = 40f;

        public CompProperties_ReplicatorThreatResponse()
        {
            compClass = typeof(CompReplicatorThreatResponse);
        }
    }

    public sealed class CompReplicatorThreatResponse : ThingComp
    {
        private Pawn lastAggressor;
        private int selfDefenseUntilTick;
        private CompProperties_ReplicatorThreatResponse Props => (CompProperties_ReplicatorThreatResponse)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn victim = parent as Pawn;
            Pawn attacker = dinfo.Instigator as Pawn;
            if (victim == null || attacker == null || victim.Dead || !victim.Spawned || victim.Map == null || attacker == victim)
                return;
            if (victim.Faction == Faction.OfPlayer || attacker.Faction == victim.Faction)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            lastAggressor = attacker;
            selfDefenseUntilTick = SafeFutureTick(now, Math.Max(60, Props.selfDefenseTicks));

            CompReplicatorState state = victim.TryGetComp<CompReplicatorState>();
            if (state?.EMPSuppressed != true)
            {
                victim.Map.GetComponent<MapComponent_ReplicatorThreatResponse>()?.RegisterAttack(
                    victim,
                    attacker,
                    state,
                    Math.Max(60, Props.swarmAlertTicks),
                    Math.Max(0f, Math.Min(1f, Props.swarmResponseChance)),
                    Math.Max(1f, Props.swarmResponseRadius));
            }
        }

        public bool TryGetSelfDefenseTarget(Pawn responder, out Pawn target)
        {
            target = null;
            if (responder == null || responder.Dead || !responder.Spawned || responder.Faction == Faction.OfPlayer)
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now > selfDefenseUntilTick || lastAggressor == null || lastAggressor.Dead || !lastAggressor.Spawned || lastAggressor.Map != responder.Map)
                return false;
            if (lastAggressor.Faction == responder.Faction || !responder.CanReach(lastAggressor, PathEndMode.Touch, Danger.Deadly))
                return false;

            target = lastAggressor;
            return true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref lastAggressor, "wngReplicatorLastAggressor");
            Scribe_Values.Look(ref selfDefenseUntilTick, "wngReplicatorSelfDefenseUntil", 0);
        }

        internal static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(0, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }

    public sealed class MapComponent_ReplicatorThreatResponse : MapComponent
    {
        private sealed class Alert
        {
            public Faction faction;
            public ReplicatorControlKind controlKind;
            public string domainId;
            public Pawn aggressor;
            public IntVec3 attackCell;
            public int untilTick;
            public int serial;
            public float responseChance;
            public float responseRadiusSq;
        }

        private readonly List<Alert> alerts = new List<Alert>();
        private int nextSerial;

        public MapComponent_ReplicatorThreatResponse(Map map) : base(map) { }

        public void RegisterAttack(Pawn victim, Pawn attacker, CompReplicatorState state, int durationTicks, float responseChance, float responseRadius)
        {
            if (victim == null || attacker == null || state == null || victim.Map != map || attacker.Map != map || victim.Faction == null)
                return;

            Alert alert = null;
            for (int i = 0; i < alerts.Count; i++)
            {
                Alert candidate = alerts[i];
                if (candidate.faction == victim.Faction && candidate.controlKind == state.ControlKind &&
                    string.Equals(candidate.domainId ?? string.Empty, state.ControlDomainId, StringComparison.Ordinal))
                {
                    alert = candidate;
                    break;
                }
            }

            if (alert == null)
            {
                alert = new Alert
                {
                    faction = victim.Faction,
                    controlKind = state.ControlKind,
                    domainId = state.ControlDomainId
                };
                alerts.Add(alert);
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            alert.aggressor = attacker;
            alert.attackCell = victim.Position;
            alert.untilTick = CompReplicatorThreatResponse.SafeFutureTick(now, durationTicks);
            alert.responseChance = responseChance;
            alert.responseRadiusSq = responseRadius * responseRadius;
            unchecked { alert.serial = ++nextSerial; }
        }

        public bool TryGetRetaliationTarget(Pawn responder, out Pawn target)
        {
            target = null;
            if (responder == null || responder.Dead || !responder.Spawned || responder.Map != map || responder.Faction == null || responder.Faction == Faction.OfPlayer)
                return false;

            CompReplicatorState state = responder.TryGetComp<CompReplicatorState>();
            if (state == null || state.EMPSuppressed)
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            for (int i = alerts.Count - 1; i >= 0; i--)
            {
                Alert alert = alerts[i];
                if (now > alert.untilTick || alert.aggressor == null || alert.aggressor.Dead || !alert.aggressor.Spawned || alert.aggressor.Map != map)
                {
                    alerts.RemoveAt(i);
                    continue;
                }
                if (alert.faction != responder.Faction || alert.controlKind != state.ControlKind ||
                    !string.Equals(alert.domainId ?? string.Empty, state.ControlDomainId, StringComparison.Ordinal))
                    continue;
                if (alert.aggressor.Faction == responder.Faction || responder.Position.DistanceToSquared(alert.attackCell) > alert.responseRadiusSq)
                    continue;

                int hash = responder.thingIDNumber;
                unchecked { hash = (hash * 397) ^ (alert.serial * 7919); }
                int threshold = (int)Math.Round(alert.responseChance * 10000f);
                if ((hash & 0x7fffffff) % 10000 >= threshold)
                    return false;
                if (!responder.CanReach(alert.aggressor, PathEndMode.Touch, Danger.Deadly))
                    return false;

                target = alert.aggressor;
                return true;
            }
            return false;
        }
    }

    public sealed class JobGiver_ReplicatorDefense : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Faction == Faction.OfPlayer)
                return null;

            Pawn aggressor;
            CompReplicatorThreatResponse personal = pawn.TryGetComp<CompReplicatorThreatResponse>();
            if (personal?.TryGetSelfDefenseTarget(pawn, out aggressor) == true)
                return MakeMeleeDefense(aggressor, 900);

            MapComponent_ReplicatorThreatResponse swarm = pawn.Map.GetComponent<MapComponent_ReplicatorThreatResponse>();
            if (swarm?.TryGetRetaliationTarget(pawn, out aggressor) == true)
                return MakeMeleeDefense(aggressor, 720);

            return null;
        }

        private static Job MakeMeleeDefense(Pawn target, int expiryTicks)
        {
            Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.expiryInterval = expiryTicks;
            return job;
        }
    }
}
