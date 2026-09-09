using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public static class ReplicatorUtility
    {
        public const int TargetRefreshTicks = 300;
        public const int IdleTargetCheckTicks = 1200;

        public static bool IsSmallReplicator(Pawn pawn)
        {
            string defName = pawn?.def?.defName ?? pawn?.kindDef?.defName;
            return defName == "WNG_ReplicatorDrone"
                || defName == "WNG_ReplicatorHunter"
                || defName == "WNG_ReplicatorBulwark"
                || defName == "WNG_ReplicatorTitan"
                || defName == "WNG_ReplicatorRepairer"
                || defName == "WNG_ReplicatorBurrower"
                || defName == "WNG_ReplicatorArtillery"
                || defName == "WNG_ReplicatorSiegeMass";
        }

        public static int CountBlockReplicatorsForFaction(Map map, Faction faction)
        {
            if (map?.mapPawns == null || faction == null)
                return 0;
            return map.mapPawns.AllPawnsSpawned.Count(p =>
                p != null && !p.Dead && p.Spawned && p.Faction == faction &&
                ReplicatorQueenUtility.IsBlockReplicator(p));
        }

        public static bool IsAssimilationTarget(Thing thing, Pawn replicator)
        {
            if (thing == null || replicator == null || thing.Destroyed || !thing.Spawned) return false;
            if (ReplicatorEMPSuppressionUtility.IsSuppressed(replicator)) return false;
            if (thing == replicator || thing is Pawn) return false;
            if (thing.def.defName == "WNG_ReplicatorMatter") return false;
            if (thing.def.category == ThingCategory.Item)
                return thing.stackCount > 0;
            if (thing.def.category != ThingCategory.Building || thing.def.building == null)
                return false;
            if (thing.def.building.isNaturalRock)
                return thing.def.building.mineableThing != null;
            return thing.Faction != replicator.Faction && thing.def.destroyable;
        }

        public static ThingDef ResolveAssimilatedMaterial(Thing target)
        {
            if (target == null) return null;
            if (target.Stuff != null) return target.Stuff;
            if (target.def?.stuffProps != null) return target.def;
            if (target.def?.building?.mineableThing?.stuffProps != null)
                return target.def.building.mineableThing;
            return target.def;
        }

        public static Thing FindClosestAssimilationTarget(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Faction == Faction.OfPlayer || ReplicatorEMPSuppressionUtility.IsSuppressed(pawn)) return null;
            Map map = pawn.Map;
            MapComponent_ReplicatorTargets cache = map.GetComponent<MapComponent_ReplicatorTargets>();
            if (cache == null) return null;
            MapComponent_ReplicatorContainment containment = map.GetComponent<MapComponent_ReplicatorContainment>();
            if (containment?.IsContained(pawn.Position) == true)
                return null;
            MapComponent_ReplicatorSwarmBehavior behavior = map.GetComponent<MapComponent_ReplicatorSwarmBehavior>();
            CompReplicatorSpecialistBody specialist = pawn.TryGetComp<CompReplicatorSpecialistBody>();
            Thing best = null;
            float bestScore = float.MaxValue;
            foreach (Thing candidate in cache.Candidates)
            {
                if (!IsAssimilationTarget(candidate, pawn)) continue;
                if (containment?.IsContained(candidate.Position) == true) continue;
                float dist = pawn.Position.DistanceToSquared(candidate.Position);
                float score = dist + (behavior?.TargetScoreOffset(pawn, candidate) ??
                                      (candidate.def.category == ThingCategory.Item ? -25f : 0f));
                score += specialist?.TargetScoreOffset(candidate) ?? 0f;
                if (score >= bestScore || !pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly)) continue;
                best = candidate;
                bestScore = score;
            }
            return best;
        }

        public static int SpawnOffspring(Pawn parent, IntVec3 nearCell, ThingDef inheritedMaterial, int requestedChildren = 2)
        {
            if (parent?.Map == null || parent.Faction == null) return 0;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (kind == null)
            {
                Log.ErrorOnce("[Wraith & Nanite Gravtech] Missing WNG_ReplicatorDrone PawnKindDef.", 48173011);
                return 0;
            }
            requestedChildren = Math.Max(1, Math.Min(2, requestedChildren));
            Map map = parent.Map;
            int current = CountBlockReplicatorsForFaction(map, parent.Faction);
            int available = WNG_Config.MaxHostileReplicatorsPerMap - current;
            int toSpawn = Math.Min(requestedChildren, Math.Max(0, available));
            if (toSpawn <= 0)
            {
                ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorMatter");
                if (matterDef != null)
                {
                    Thing overflow = ThingMaker.MakeThing(matterDef);
                    overflow.stackCount = 10 * requestedChildren;
                    GenPlace.TryPlaceThing(overflow, nearCell, map, ThingPlaceMode.Near);
                }
                return 0;
            }
            int spawned = 0;
            for (int i = 0; i < toSpawn; i++)
            {
                Pawn child = PawnGenerator.GeneratePawn(kind, parent.Faction);
                if (child == null) continue;
                child.TryGetComp<CompReplicatorMaterial>()?.ApplyMaterialSignature(inheritedMaterial);
                child.TryGetComp<CompReplicatorAdaptation>()?.InheritFrom(parent, map);
                if (GenPlace.TryPlaceThing(child, nearCell, map, ThingPlaceMode.Near))
                    spawned++;
                else if (!child.Destroyed)
                    child.Destroy(DestroyMode.Vanish);
            }
            if (spawned > 0)
            {
                SoundDef sound = DefDatabase<SoundDef>.GetNamedSilentFail("WNG_ReplicatorAssembly");
                if (sound != null)
                    sound.PlayOneShot(new TargetInfo(nearCell, map));
            }
            return spawned;
        }
    }

    public sealed class MapComponent_ReplicatorTargets : MapComponent
    {
        private readonly List<Thing> candidates = new List<Thing>();
        private int nextRefreshTick;
        public IReadOnlyList<Thing> Candidates => candidates;

        public MapComponent_ReplicatorTargets(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now < nextRefreshTick) return;
            bool hasAutonomousHarvester = map.mapPawns?.AllPawnsSpawned?.Any(p =>
                p != null && !p.Dead && p.Spawned && p.Faction != Faction.OfPlayer &&
                ReplicatorUtility.IsSmallReplicator(p)) == true;
            if (!hasAutonomousHarvester)
            {
                if (candidates.Count > 0)
                    candidates.Clear();
                nextRefreshTick = now + ReplicatorUtility.IdleTargetCheckTicks;
                return;
            }
            nextRefreshTick = now + ReplicatorUtility.TargetRefreshTicks;
            Refresh();
        }

        private void Refresh()
        {
            candidates.Clear();
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing == null || thing.Destroyed || !thing.Spawned || thing is Pawn) continue;
                if (thing.def.category == ThingCategory.Item)
                {
                    if (thing.def.defName != "WNG_ReplicatorMatter") candidates.Add(thing);
                    continue;
                }
                if (thing.def.category != ThingCategory.Building || thing.def.building == null) continue;
                if (thing.def.building.isNaturalRock && thing.def.building.mineableThing == null) continue;
                if (!thing.def.destroyable && !thing.def.building.isNaturalRock) continue;
                candidates.Add(thing);
            }
        }
    }
}
