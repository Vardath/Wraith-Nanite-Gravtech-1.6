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
        public float starvationMatterThreshold = 0.1f;
        public int starvationCheckTicks = 600;
        public float feedstockSearchRadius = 45f;
        public CompProperties_ReplicatorRetaliation() => compClass = typeof(CompReplicatorRetaliation);
    }

    public sealed class CompReplicatorRetaliation : ThingComp
    {
        private int retaliationUntil;
        private int nextStarvationCheck;
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
            if (pawn == null || pawn.Dead || pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled)
                return;
            Pawn instigator = dinfo.Instigator as Pawn;
            if (instigator != null && instigator != pawn && pawn.HostileTo(instigator))
                Provoke(instigator, signalOthers: true);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled)
                return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (retaliationUntil <= now)
                retaliationTarget = null;
            if (now < nextStarvationCheck || IsRetaliating)
                return;
            nextStarvationCheck = now + Math.Max(60, Props.starvationCheckTicks);

            CompReplicatorAssimilation assimilation = pawn.TryGetComp<CompReplicatorAssimilation>();
            if (assimilation == null || assimilation.StoredMatter > Math.Max(0f, Props.starvationMatterThreshold))
                return;
            if (ReplicatorFeedstockUtility.HasAccessibleFeedstock(pawn, Math.Max(5f, Props.feedstockSearchRadius)))
                return;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && pawn.HostileTo(p))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
            if (target != null)
                Provoke(target, signalOthers: false);
        }

        public void Provoke(Pawn target, bool signalOthers)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || target == null || target.Dead)
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
                         .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && p.Faction == pawn.Faction
                             && p.TryGetComp<CompReplicatorState>() != null
                             && p.Position.DistanceToSquared(pawn.Position) <= radiusSq
                             && !ReplicatorEMP.IsSuppressed(p))
                         .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                         .ThenBy(p => p.thingIDNumber)
                         .Take(max))
            {
                responder.TryGetComp<CompReplicatorRetaliation>()?.Provoke(target, signalOthers: false);
            }
        }

        public override string CompInspectStringExtra()
            => IsRetaliating ? "Replicator state: local retaliation" : null;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref retaliationUntil, "wngReplicatorRetaliationUntil", 0);
            Scribe_Values.Look(ref nextStarvationCheck, "wngReplicatorStarvationCheck", 0);
            Scribe_References.Look(ref retaliationTarget, "wngReplicatorRetaliationTarget");
        }
    }

    internal static class ReplicatorFeedstockUtility
    {
        public static bool HasAccessibleFeedstock(Pawn pawn, float radius)
        {
            if (pawn?.Spawned != true || pawn.Map == null)
                return false;
            float radiusSq = radius * radius;
            if (pawn.Map.listerThings.AllThings.Any(t => IsConsumableThing(pawn, t, radiusSq)))
                return true;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
            {
                if (!cell.InBounds(pawn.Map) || cell.DistanceToSquared(pawn.Position) > radiusSq)
                    continue;
                TerrainDef terrain = cell.GetTerrain(pawn.Map);
                RoofDef roof = cell.GetRoof(pawn.Map);
                if ((terrain != null && terrain != TerrainDefOf.Soil) || roof != null)
                    return true;
            }
            return false;
        }

        private static bool IsConsumableThing(Pawn pawn, Thing thing, float radiusSq)
        {
            if (thing == null || thing == pawn || thing.Destroyed || !thing.Spawned || thing.Map != pawn.Map || thing.Position.DistanceToSquared(pawn.Position) > radiusSq)
                return false;
            if (thing is Pawn || thing is Corpse || thing.Faction == pawn.Faction)
                return false;
            if (thing is Plant)
                return pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly);
            if (thing.def == null || !thing.def.destroyable)
                return false;
            bool item = thing.def.category == ThingCategory.Item && thing.def.EverHaulable;
            bool building = thing.def.category == ThingCategory.Building && thing.def.useHitPoints;
            return (item || building) && pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly);
        }
    }

    public sealed class JobGiver_ReplicatorRetaliate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompReplicatorRetaliation state = pawn?.TryGetComp<CompReplicatorRetaliation>();
            if (state?.IsRetaliating != true || ReplicatorEMP.IsSuppressed(pawn))
                return null;
            Pawn target = state.CurrentTarget;
            if (target == null || target.Map != pawn.Map || !pawn.HostileTo(target))
            {
                target = pawn.Map?.mapPawns?.AllPawnsSpawned
                    .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && pawn.HostileTo(p))
                    .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                    .FirstOrDefault();
            }
            return target == null ? null : JobMaker.MakeJob(JobDefOf.AttackMelee, target);
        }
    }

    internal static class ReplicatorCombatPermission
    {
        public static bool CanAttack(Pawn pawn)
            => pawn?.Faction == Faction.OfPlayer || pawn?.IsColonyMechPlayerControlled == true
                || pawn?.TryGetComp<CompReplicatorRetaliation>()?.IsRetaliating == true;
    }
}
