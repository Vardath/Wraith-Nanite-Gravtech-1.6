using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorSalvage : CompProperties
    {
        public int minMatter = 2;
        public int maxMatter = 4;

        public CompProperties_ReplicatorSalvage()
        {
            compClass = typeof(CompReplicatorSalvage);
        }
    }

    public sealed class CompReplicatorSalvage : ThingComp
    {
        private bool matterEmitted;
        private IntVec3 lastKnownPosition = IntVec3.Invalid;
        public CompProperties_ReplicatorSalvage Props => (CompProperties_ReplicatorSalvage)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;
            if (pawn?.health != null)
            {
                HediffDef marker = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_ReplicatorHierarchyMarker");
                if (marker != null && !pawn.health.hediffSet.HasHediff(marker))
                    pawn.health.AddHediff(marker);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Spawned == true)
                lastKnownPosition = parent.Position;
        }

        public void TryEmitMatter(Map map, IntVec3 origin)
        {
            if (matterEmitted || map == null)
                return;
            Pawn pawn = parent as Pawn;
            if (ReplicatorDestructionIntent.IsIntentionalHierarchyConsumption(pawn))
                return;
            if (!origin.IsValid || !origin.InBounds(map))
                origin = lastKnownPosition;
            if (!origin.IsValid || !origin.InBounds(map))
            {
                Log.Warning($"[WNG] Replicator matter recovery could not recover a valid map position for {parent?.def?.defName}.");
                return;
            }
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorMatter");
            if (matterDef == null)
            {
                Log.Error("[WNG] WNG_ReplicatorMatter is missing; destroyed Replicator could not return matter.");
                return;
            }
            int min = Props?.minMatter ?? 2;
            int max = Props?.maxMatter ?? 4;
            if (max < min) max = min;
            int count = Rand.RangeInclusive(min, max);
            if (count <= 0)
            {
                matterEmitted = true;
                return;
            }
            Thing matter = ThingMaker.MakeThing(matterDef);
            matter.stackCount = count;
            if (GenPlace.TryPlaceThing(matter, origin, map, ThingPlaceMode.Near))
            {
                matterEmitted = true;
                return;
            }
            if (!matter.Destroyed)
                matter.Destroy(DestroyMode.Vanish);
            Log.Warning($"[WNG] Replicator matter could not be placed for {parent?.def?.defName}; emission remains eligible for fallback.");
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            if (previousMap != null && !ReplicatorDestructionIntent.IsIntentionalHierarchyConsumption(pawn))
            {
                pawn?.TryGetComp<CompReplicatorHierarchy>()?.TryEmitDeathSplit(previousMap, lastKnownPosition);
                TryEmitMatter(previousMap, lastKnownPosition);
            }
            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref matterEmitted, "matterEmitted", false);
        }
    }
}
