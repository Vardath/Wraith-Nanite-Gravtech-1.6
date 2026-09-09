using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public static class ReplicatorEMPSuppressionUtility
    {
        public static bool IsSuppressed(Pawn pawn) => pawn?.TryGetComp<CompReplicatorEMPSuppression>()?.Suppressed == true;
    }

    public sealed class CompProperties_ReplicatorEMPSuppression : CompProperties
    {
        public int suppressionTicks = 1800;
        public CompProperties_ReplicatorEMPSuppression() { compClass = typeof(CompReplicatorEMPSuppression); }
    }

    public sealed class CompReplicatorEMPSuppression : ThingComp
    {
        private int suppressedUntilTick;
        private CompProperties_ReplicatorEMPSuppression Props => (CompProperties_ReplicatorEMPSuppression)props;
        public bool Suppressed => (Find.TickManager?.TicksGame ?? 0) < suppressedUntilTick;

        private static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(0, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        public void ApplySuppression(int ticks = -1)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            int duration = ticks > 0 ? ticks : Props.suppressionTicks;
            suppressedUntilTick = Math.Max(suppressedUntilTick, SafeFutureTick(now, Math.Max(60, duration)));
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (dinfo.Def == DamageDefOf.EMP) ApplySuppression();
        }

        public override string CompInspectStringExtra()
        {
            if (!Suppressed) return null;
            int now = Find.TickManager?.TicksGame ?? 0;
            float hours = Math.Max(0f, suppressedUntilTick - now) / 2500f;
            return $"EMP replication suppression: {hours:0.0} hour(s) remaining. Assimilation, recombination and controller coordination are interrupted.";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref suppressedUntilTick, "wngReplicatorEMPSuppressedUntil", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && suppressedUntilTick < 0) suppressedUntilTick = 0;
        }
    }

    public sealed class CompProperties_ReplicatorSuppressionEmitter : CompProperties
    {
        public float radius = 13f;
        public int pulseIntervalTicks = 1500;
        public int maxTargets = 6;
        public int suppressionTicks = 2100;
        public float empDamage = 12f;
        public CompProperties_ReplicatorSuppressionEmitter() { compClass = typeof(CompReplicatorSuppressionEmitter); }
    }

    public sealed class CompReplicatorSuppressionEmitter : ThingComp
    {
        private int nextPulseTick;
        private CompProperties_ReplicatorSuppressionEmitter Props => (CompProperties_ReplicatorSuppressionEmitter)props;
        private bool Active
        {
            get
            {
                if (parent == null || !parent.Spawned) return false;
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                return power != null && power.PowerOn;
            }
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(0, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!Active || parent.Map == null) return;
            int now = Find.TickManager.TicksGame;
            if (now < nextPulseTick) return;
            nextPulseTick = SafeFutureTick(now, Math.Max(250, Props.pulseIntervalTicks));
            Pulse(parent.Map);
        }

        private void Pulse(Map map)
        {
            float radiusSq = Props.radius * Props.radius;
            List<Pawn> targets = map.mapPawns.AllPawnsSpawned
                .Where(p => IsEligibleHostileReplicator(p) && p.Position.DistanceToSquared(parent.Position) <= radiusSq)
                .OrderByDescending(p => ReplicatorCoordinationUtility.IsController(p))
                .ThenByDescending(p => p.TryGetComp<CompReplicatorAdaptation>()?.Specialization == ReplicatorAdaptationType.Shield)
                .ThenBy(p => p.Position.DistanceToSquared(parent.Position))
                .ThenBy(p => p.thingIDNumber)
                .Take(Math.Max(1, Props.maxTargets))
                .ToList();
            for (int i = 0; i < targets.Count; i++)
            {
                Pawn target = targets[i];
                target.TryGetComp<CompReplicatorEMPSuppression>()?.ApplySuppression(Props.suppressionTicks);
                target.TakeDamage(new DamageInfo(DamageDefOf.EMP, Props.empDamage, instigator: parent));
            }
        }

        private static bool IsEligibleHostileReplicator(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Faction == null || pawn.Faction == Faction.OfPlayer) return false;
            if (!pawn.Faction.HostileTo(Faction.OfPlayer)) return false;
            return ReplicatorQueenUtility.IsReplicator(pawn) || ReplicatorCoordinationUtility.IsController(pawn);
        }

        public override string CompInspectStringExtra()
        {
            string state = Active ? "active" : "offline";
            return $"Anti-Replicator EMP suppression: {state}. Radius {Props.radius:0.0}; up to {Props.maxTargets} hostile WNG Replicators per pulse. Pulses interrupt block replication systems and trigger normal human-form nanite EMP disruption.";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextPulseTick, "wngReplicatorSuppressionNextPulse", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && nextPulseTick < 0) nextPulseTick = 0;
        }
    }
}
