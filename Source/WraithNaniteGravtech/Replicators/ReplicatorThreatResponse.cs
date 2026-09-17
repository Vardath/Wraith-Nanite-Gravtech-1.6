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

    /// <summary>
    /// Personal attack memory for an autonomous block Replicator. This does not turn the pawn into a
    /// generic enemy-seeking raider; it only remembers a real pawn aggressor for a bounded period and
    /// publishes a bounded same-domain retaliation alert to the map component.
    /// </summary>
    public sealed class CompReplicatorThreatResponse : ThingComp
    {
        private Pawn lastAggressor;
        private int selfDefenseUntilTick;

        private CompProperties_ReplicatorThreatResponse Props =>
            (CompProperties_ReplicatorThreatResponse)props;

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);

            Pawn victim = parent as Pawn;
            Pawn attacker = dinfo.Instigator as Pawn;
            if (victim == null || attacker == null || victim.Dead || !victim.Spawned || victim.Map == null || attacker == victim)
                return;
            if (victim.Faction == null || victim.Faction == Faction.OfPlayer || attacker.Faction == victim.Faction)
                return;
            if (ReplicatorInterferenceUtility.IsEmpDisrupted(victim))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            lastAggressor = attacker;
            selfDefenseUntilTick = SafeFutureTick(now, Math.Max(60, Props.selfDefenseTicks));

            victim.Map.GetComponent<MapComponent_ReplicatorThreatResponse>()?.RegisterAttack(
                victim,
                attacker,
                Math.Max(60, Props.swarmAlertTicks),
                Math.Max(0f, Math.Min(1f, Props.swarmResponseChance)),
                Math.Max(1f, Props.swarmResponseRadius));
        }

        public bool TryGetSelfDefenseTarget(Pawn responder, out Pawn target)
        {
            target = null;
            if (responder == null || responder.Dead || !responder.Spawned || responder.Faction == Faction.OfPlayer)
                return false;
            if (ReplicatorInterferenceUtility.IsEmpDisrupted(responder))
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

    public sealed class ReplicatorThreatAlert : IExposable
    {
        public string domainId;
        public Pawn aggressor;
        public IntVec3 attackCell = IntVec3.Invalid;
        public int untilTick;
        public int serial;
        public float responseChance;
        public float responseRadiusSq;

        public void ExposeData()
        {
            Scribe_Values.Look(ref domainId, "domainId");
            Scribe_References.Look(ref aggressor, "aggressor");
            Scribe_Values.Look(ref attackCell, "attackCell", IntVec3.Invalid);
            Scribe_Values.Look(ref untilTick, "untilTick", 0);
            Scribe_Values.Look(ref serial, "serial", 0);
            Scribe_Values.Look(ref responseChance, "responseChance", 0f);
            Scribe_Values.Look(ref responseRadiusSq, "responseRadiusSq", 0f);
        }
    }

    /// <summary>
    /// Save-persistent bounded retaliation memory. Alerts are controller-domain scoped: different
    /// domains do not coordinate merely because they currently share a faction.
    /// </summary>
    public sealed class MapComponent_ReplicatorThreatResponse : MapComponent
    {
        private List<ReplicatorThreatAlert> alerts = new List<ReplicatorThreatAlert>();
        private int nextSerial;

        public MapComponent_ReplicatorThreatResponse(Map map) : base(map)
        {
        }

        public void RegisterAttack(Pawn victim, Pawn attacker, int durationTicks, float responseChance, float responseRadius)
        {
            if (victim == null || attacker == null || victim.Map != map || attacker.Map != map || victim.Faction == null)
                return;

            string domainId = ReplicatorDomainUtility.DomainId(victim);
            if (string.IsNullOrEmpty(domainId))
                return;

            ReplicatorThreatAlert alert = null;
            for (int i = 0; i < alerts.Count; i++)
            {
                ReplicatorThreatAlert candidate = alerts[i];
                if (candidate != null && candidate.domainId == domainId)
                {
                    alert = candidate;
                    break;
                }
            }

            if (alert == null)
            {
                alert = new ReplicatorThreatAlert { domainId = domainId };
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
            if (ReplicatorInterferenceUtility.IsEmpDisrupted(responder))
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            string responderDomainId = ReplicatorDomainUtility.DomainId(responder);
            if (string.IsNullOrEmpty(responderDomainId))
                return false;
            for (int i = alerts.Count - 1; i >= 0; i--)
            {
                ReplicatorThreatAlert alert = alerts[i];
                if (alert == null || now > alert.untilTick || alert.aggressor == null || alert.aggressor.Dead || !alert.aggressor.Spawned || alert.aggressor.Map != map)
                {
                    alerts.RemoveAt(i);
                    continue;
                }
                if (alert.domainId != responderDomainId || alert.aggressor.Faction == responder.Faction)
                    continue;
                if (responder.Position.DistanceToSquared(alert.attackCell) > alert.responseRadiusSq)
                    continue;

                int hash = responder.thingIDNumber;
                unchecked { hash = (hash * 397) ^ (alert.serial * 7919); }
                int threshold = (int)Math.Round(Math.Max(0f, Math.Min(1f, alert.responseChance)) * 10000f);
                if ((hash & 0x7fffffff) % 10000 >= threshold)
                    return false;
                if (!responder.CanReach(alert.aggressor, PathEndMode.Touch, Danger.Deadly))
                    return false;

                target = alert.aggressor;
                return true;
            }
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref alerts, "wngReplicatorThreatAlerts", LookMode.Deep);
            Scribe_Values.Look(ref nextSerial, "wngReplicatorThreatNextSerial", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && alerts == null)
                alerts = new List<ReplicatorThreatAlert>();
        }
    }

    public sealed class JobGiver_ReplicatorDefense : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Faction == Faction.OfPlayer)
                return null;
            if (ReplicatorInterferenceUtility.IsEmpDisrupted(pawn))
                return null;

            Pawn aggressor;
            CompReplicatorThreatResponse personal = pawn.TryGetComp<CompReplicatorThreatResponse>();
            if (personal?.TryGetSelfDefenseTarget(pawn, out aggressor) == true)
                return MakeDefenseJob(pawn, aggressor, 900);

            MapComponent_ReplicatorThreatResponse swarm = pawn.Map.GetComponent<MapComponent_ReplicatorThreatResponse>();
            if (swarm?.TryGetRetaliationTarget(pawn, out aggressor) == true)
                return MakeDefenseJob(pawn, aggressor, 720);

            return null;
        }

        private static Job MakeDefenseJob(Pawn pawn, Pawn target, int expiryTicks)
        {
            // A real confrontation with an active native personal barrier is AntiShield evidence.
            // The adaptation utility throttles/shared-records the sample, so repeated ThinkTree
            // evaluation cannot inflate the lineage history.
            if (ReplicatorAdaptationUtility.HasActiveNativePersonalShield(target))
                ReplicatorAdaptationUtility.ShareShieldEngagement(pawn);

            if (pawn?.equipment?.Primary != null && pawn.equipment.Primary.def.IsRangedWeapon)
            {
                Job ranged = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                ranged.expiryInterval = expiryTicks;
                ranged.maxNumStaticAttacks = 3;
                ranged.endIfCantShootTargetFromCurPos = true;
                return ranged;
            }

            Job melee = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            melee.expiryInterval = expiryTicks;
            return melee;
        }
    }
}
