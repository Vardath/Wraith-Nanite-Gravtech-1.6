using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class HediffCompProperties_ReplicatorDeathOutcomeCached : HediffCompProperties
    {
        public HediffCompProperties_ReplicatorDeathOutcomeCached()
        {
            compClass = typeof(HediffComp_ReplicatorDeathOutcomeCached);
        }
    }

    public sealed class HediffComp_ReplicatorDeathOutcomeCached : HediffComp
    {
        private Map lastKnownMap;
        private IntVec3 lastKnownPosition = IntVec3.Invalid;
        private bool deathOutcomeEmitted;

        private void SnapshotLiveLocation()
        {
            Pawn pawn = parent?.pawn;
            if (pawn?.Spawned == true && pawn.Map != null)
            {
                lastKnownMap = pawn.Map;
                lastKnownPosition = pawn.Position;
            }
        }

        public override void CompPostMake()
        {
            base.CompPostMake();
            SnapshotLiveLocation();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SnapshotLiveLocation();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            SnapshotLiveLocation();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastKnownPosition, "wngReplicatorDeathPosition", IntVec3.Invalid);
            Scribe_Values.Look(ref deathOutcomeEmitted, "wngReplicatorDeathOutcomeEmitted", false);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            Pawn pawn = parent?.pawn;
            if (pawn == null || deathOutcomeEmitted || ReplicatorDestructionIntent.IsIntentionalHierarchyConsumption(pawn))
                return;
            Map map = lastKnownMap;
            IntVec3 origin = lastKnownPosition;
            if (map == null || !origin.IsValid || !origin.InBounds(map))
            {
                Log.Error($"[WNG] Replicator death outcome had no cached live map position for {pawn.def?.defName}; no split or matter could be emitted.");
                return;
            }
            pawn.TryGetComp<CompReplicatorHierarchy>()?.TryEmitDeathSplit(map, origin);
            pawn.TryGetComp<CompReplicatorSalvage>()?.TryEmitMatter(map, origin);
            deathOutcomeEmitted = true;
        }
    }
}
