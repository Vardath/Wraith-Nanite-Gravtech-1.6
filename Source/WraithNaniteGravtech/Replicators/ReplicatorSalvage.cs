using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorSalvage : CompProperties
    {
        public int irreducibleMatterYield = 4;
        public float coreFragmentChance = 0.12f;

        public CompProperties_ReplicatorSalvage()
        {
            compClass = typeof(CompReplicatorSalvage);
        }
    }

    /// <summary>
    /// Converts only the final irreducible Drone body into recoverable matter. Larger block
    /// bodies physically split down the hierarchy, so producing salvage from those same deaths
    /// would duplicate their constituent mass. Vanish-destruction used by upward recombination
    /// never produces salvage either.
    /// </summary>
    public sealed class CompReplicatorSalvage : ThingComp
    {
        private IntVec3 lastKnownPosition = IntVec3.Invalid;
        private CompProperties_ReplicatorSalvage Props => (CompProperties_ReplicatorSalvage)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (parent?.Spawned == true)
                lastKnownPosition = parent.Position;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Spawned == true)
                lastKnownPosition = parent.Position;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeath = mode == DestroyMode.KillFinalize || (mode == DestroyMode.Vanish && pawn?.Dead == true);
            if (genuineDeath && pawn?.def?.defName == "WNG_ReplicatorDrone" && previousMap != null)
                EmitFinalSalvage(pawn, previousMap);

            base.PostDestroy(mode, previousMap);
        }

        private void EmitFinalSalvage(Pawn pawn, Map map)
        {
            IntVec3 origin = lastKnownPosition;
            if (!origin.IsValid || !origin.InBounds(map))
                return;

            CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
            int matterCount = Math.Max(0, Props.irreducibleMatterYield) + Math.Max(0, state?.StoredMatter ?? 0);
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorMatter");
            if (matterDef != null && matterCount > 0)
                PlaceStacks(matterDef, matterCount, origin, map);

            ThingDef coreDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorCoreFragment");
            if (coreDef != null && Props.coreFragmentChance > 0f && Rand.Chance(Math.Min(1f, Props.coreFragmentChance)))
                PlaceStacks(coreDef, 1, origin, map);
        }

        private static void PlaceStacks(ThingDef def, int totalCount, IntVec3 origin, Map map)
        {
            int remaining = Math.Max(0, totalCount);
            int stackLimit = Math.Max(1, def.stackLimit);
            while (remaining > 0)
            {
                int count = Math.Min(stackLimit, remaining);
                Thing stack = ThingMaker.MakeThing(def);
                stack.stackCount = count;
                if (!GenPlace.TryPlaceThing(stack, origin, map, ThingPlaceMode.Near) && !stack.Destroyed)
                    stack.Destroy(DestroyMode.Vanish);
                remaining -= count;
            }
        }
    }
}
