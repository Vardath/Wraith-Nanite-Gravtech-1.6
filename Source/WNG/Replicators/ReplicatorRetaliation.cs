using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorRetaliation : CompProperties
    {
        public float signalRadius = 24f;
        public int maxNearbyResponders = 5;
        public int retaliationTicks = 6000;
        public CompProperties_ReplicatorRetaliation() => compClass = typeof(CompReplicatorRetaliation);
    }

    public sealed class CompReplicatorRetaliation : ThingComp
    {
        private int retaliationUntil;
        private Pawn retaliationTarget;
        private CompProperties_ReplicatorRetaliation Props => (CompProperties_ReplicatorRetaliation)props;
        private Pawn Pawn => parent as Pawn;

        public bool IsRetaliating
        {
            get
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                return retaliationUntil > now;
            }
        }

        public Pawn CurrentTarget => retaliationTarget != null && !retaliationTarget.Dead && retaliationTarget.Spawned ? retaliationTarget : null;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled ||
                pawn.TryGetComp<CompReplicatorSovereignty>()?.InterferenceBlocked == true)
                return;

            Pawn instigator = dinfo.Instigator as Pawn;
            if (instigator == null || instigator == pawn || instigator.Dead)
                return;
            if (instigator.Faction != null && instigator.Faction == pawn.Faction)
                return;

            Provoke(instigator, signalOthers: true);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
                return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (retaliationUntil <= now)
                retaliationTarget = null;
        }

        public void Provoke(Pawn target, bool signalOthers)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || target == null || target.Dead ||
                pawn.TryGetComp<CompReplicatorSovereignty>()?.InterferenceBlocked == true)
                return;
            int now = Find.TickManager?.TicksGame ?? 0;
            retaliationUntil = Math.Max(retaliationUntil, now + Math.Max(60, Props.retaliationTicks));
            retaliationTarget = target;

            if (!signalOthers)
                return;

            float radius = Math.Max(1f, Props.signalRadius);
            float radiusSq = radius * radius;
            int max = Math.Max(0, Props.maxNearbyResponders);
            foreach (Pawn responder in pawn.Map.mapPawns.AllPawnsSpawned
                         .Where(p => p != null && p != pawn && !p.Dead && p.Spawned
                             && p.TryGetComp<CompReplicatorState>() != null
                             && ReplicatorSovereigntyUtility.SameDomain(pawn, p)
                             && p.Position.DistanceToSquared(pawn.Position) <= radiusSq
                             && !ReplicatorEMP.IsSuppressed(p)
                             && p.TryGetComp<CompReplicatorSovereignty>()?.InterferenceBlocked != true)
                         .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                         .ThenBy(p => p.thingIDNumber)
                         .Take(max))
            {
                responder.TryGetComp<CompReplicatorRetaliation>()?.Provoke(target, signalOthers: false);
            }
        }

        public override string CompInspectStringExtra()
            => IsRetaliating && !ReplicatorTerminalUtility.IsTerminal(Pawn) ? "Replicator state: local retaliation" : null;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref retaliationUntil, "wngReplicatorRetaliationUntil", 0);
            Scribe_References.Look(ref retaliationTarget, "wngReplicatorRetaliationTarget");
        }
    }

    public sealed class JobGiver_ReplicatorRetaliate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (ReplicatorTerminalUtility.IsTerminal(pawn)) return null;
            CompReplicatorRetaliation state = pawn?.TryGetComp<CompReplicatorRetaliation>();
            if (state?.IsRetaliating != true || ReplicatorEMP.IsSuppressed(pawn) ||
                pawn.TryGetComp<CompReplicatorSovereignty>()?.InterferenceBlocked == true)
                return null;

            Pawn target = state.CurrentTarget;
            if (target == null || target.Map != pawn.Map)
                return null;

            return JobMaker.MakeJob(JobDefOf.AttackMelee, target);
        }
    }

    internal static class ReplicatorCombatPermission
    {
        public static bool CanAttack(Pawn pawn)
            => pawn?.Faction == Faction.OfPlayer || pawn?.IsColonyMechPlayerControlled == true
                || ReplicatorSovereigntyUtility.IsOperationallyControlled(pawn)
                || ReplicatorTerminalUtility.IsTerminal(pawn)
                || pawn?.TryGetComp<CompReplicatorRetaliation>()?.IsRetaliating == true;
    }
}
