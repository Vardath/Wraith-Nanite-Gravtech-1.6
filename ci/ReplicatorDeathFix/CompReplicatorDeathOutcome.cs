using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.DeathFix
{
    public sealed class CompProperties_ReplicatorDeathOutcomeBridge : CompProperties
    {
        public CompProperties_ReplicatorDeathOutcomeBridge()
        {
            compClass = typeof(CompReplicatorDeathOutcomeBridge);
        }
    }

    public sealed class CompReplicatorDeathOutcomeBridge : ThingComp
    {
        private const int DamageDeathGraceTicks = 30;
        private const string HierarchyTypeName = "WraithNaniteGravtech.CompReplicatorHierarchy";
        private const string SalvageTypeName = "WraithNaniteGravtech.CompReplicatorSalvage";

        private IntVec3 lastKnownPosition = IntVec3.Invalid;
        private int lastDamageTick = -999999;
        private bool splitResolved;
        private bool fallbackMatterEmitted;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            CachePosition();
        }

        public override void CompTick()
        {
            base.CompTick();
            CachePosition();
            Pawn pawn = parent as Pawn;
            if (pawn?.Dead == true)
                TryResolveSplit(pawn.MapHeld, pawn.PositionHeld);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);
            CachePosition();
            lastDamageTick = Find.TickManager?.TicksGame ?? 0;
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            CachePosition();
            Pawn pawn = parent as Pawn;
            if (pawn?.Dead == true)
                TryResolveSplit(pawn.MapHeld, pawn.PositionHeld);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            if (IsCombatDeathCleanup(mode, pawn))
            {
                TryResolveSplit(previousMap, lastKnownPosition);
                IntVec3 oldSalvageCell = parent.Position;
                bool oldSalvageCanHandle =
                    (mode == DestroyMode.KillFinalize || (mode == DestroyMode.Vanish && pawn?.Dead == true)) &&
                    previousMap != null && oldSalvageCell.IsValid && oldSalvageCell.InBounds(previousMap);
                if (!oldSalvageCanHandle)
                    TryEmitFallbackMatter(pawn, previousMap, lastKnownPosition);
            }
            base.PostDestroy(mode, previousMap);
        }

        private bool IsCombatDeathCleanup(DestroyMode mode, Pawn pawn)
        {
            if (mode == DestroyMode.KillFinalize || pawn?.Dead == true)
                return true;
            if (mode != DestroyMode.Vanish)
                return false;
            int now = Find.TickManager?.TicksGame ?? lastDamageTick;
            int delta = now - lastDamageTick;
            return delta >= 0 && delta <= DamageDeathGraceTicks;
        }

        private void TryResolveSplit(Map map, IntVec3 origin)
        {
            if (splitResolved)
                return;
            Pawn pawn = parent as Pawn;
            if (pawn == null)
                return;
            if (!TryRecoverContext(pawn, ref map, ref origin))
                return;
            ThingComp hierarchy = pawn.AllComps?.FirstOrDefault(c => c?.GetType().FullName == HierarchyTypeName);
            if (hierarchy == null)
            {
                splitResolved = true;
                return;
            }
            try
            {
                MethodInfo method = hierarchy.GetType().GetMethod("TryEmitDeathSplit", BindingFlags.Instance | BindingFlags.Public);
                method?.Invoke(hierarchy, new object[] { map, origin });
                splitResolved = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[WNG] Replicator child-form breakup bridge failed for {pawn.def?.defName}: {Unwrap(ex).Message}");
            }
        }

        private void TryEmitFallbackMatter(Pawn pawn, Map map, IntVec3 origin)
        {
            if (fallbackMatterEmitted || pawn == null)
                return;
            if (!TryRecoverContext(pawn, ref map, ref origin))
                return;
            ThingComp salvage = pawn.AllComps?.FirstOrDefault(c => c?.GetType().FullName == SalvageTypeName);
            if (salvage == null)
                return;
            int min = 2;
            int max = 4;
            try
            {
                PropertyInfo propsProperty = salvage.GetType().GetProperty("Props", BindingFlags.Instance | BindingFlags.Public);
                object props = propsProperty?.GetValue(salvage, null);
                if (props != null)
                {
                    FieldInfo minField = props.GetType().GetField("minMatter", BindingFlags.Instance | BindingFlags.Public);
                    FieldInfo maxField = props.GetType().GetField("maxMatter", BindingFlags.Instance | BindingFlags.Public);
                    if (minField?.GetValue(props) is int minValue) min = minValue;
                    if (maxField?.GetValue(props) is int maxValue) max = maxValue;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[WNG] Replicator salvage range lookup failed for {pawn.def?.defName}; using safe defaults: {Unwrap(ex).Message}");
            }
            if (max < min) max = min;
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorMatter");
            if (matterDef == null)
            {
                Log.Error("[WNG] WNG_ReplicatorMatter is missing; destroyed Replicator could not return matter.");
                return;
            }
            int count = Rand.RangeInclusive(min, max);
            if (count <= 0)
            {
                fallbackMatterEmitted = true;
                return;
            }
            Thing matter = ThingMaker.MakeThing(matterDef);
            matter.stackCount = count;
            if (GenPlace.TryPlaceThing(matter, origin, map, ThingPlaceMode.Near))
            {
                fallbackMatterEmitted = true;
                return;
            }
            if (!matter.Destroyed)
                matter.Destroy(DestroyMode.Vanish);
            Log.Warning($"[WNG] Replicator matter placement failed for {pawn.def?.defName} at {origin}.");
        }

        private bool TryRecoverContext(Pawn pawn, ref Map map, ref IntVec3 origin)
        {
            if (map == null)
                map = pawn.MapHeld;
            if ((!origin.IsValid || map == null || !origin.InBounds(map)) && map != null && lastKnownPosition.IsValid && lastKnownPosition.InBounds(map))
                origin = lastKnownPosition;
            if (map != null && origin.IsValid && origin.InBounds(map))
                return true;
            Log.Warning($"[WNG] Replicator death outcome could not recover map context for {pawn.def?.defName}; leaving fallback eligible.");
            return false;
        }

        private void CachePosition()
        {
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;
        }

        private static Exception Unwrap(Exception ex)
        {
            return ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastDamageTick, "lastDamageTick", -999999);
            Scribe_Values.Look(ref splitResolved, "splitResolved", false);
            Scribe_Values.Look(ref fallbackMatterEmitted, "fallbackMatterEmitted", false);
        }
    }
}
