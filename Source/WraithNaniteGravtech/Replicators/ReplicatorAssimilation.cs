using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    internal static class ReplicatorAssimilationUtility
    {
        public const int PopulationCap = 120;
        public const int OffspringMatterCost = 10;
        public const int TargetRefreshTicks = 300;
        public const int IdleRefreshTicks = 1200;

        public static bool IsHarvester(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Faction == Faction.OfPlayer || pawn.TryGetComp<CompReplicatorState>() == null)
                return false;
            return pawn.TryGetComp<CompReplicatorSpecialist>()?.Role != ReplicatorSpecialistRole.Controller;
        }

        public static bool IsAssimilationTarget(Thing thing, Pawn pawn)
        {
            if (thing == null || pawn == null || thing.Destroyed || !thing.Spawned || thing == pawn || thing is Pawn || thing is Corpse)
                return false;
            if (pawn.Faction == Faction.OfPlayer || pawn.TryGetComp<CompReplicatorState>()?.EMPSuppressed == true)
                return false;
            if (thing.def?.defName == "WNG_ReplicatorMatter" || thing.def?.defName == "WNG_ReplicatorCoreFragment")
                return false;

            if (thing.def.category == ThingCategory.Item)
                return thing.stackCount > 0;

            if (thing.def.category != ThingCategory.Building || thing.def.building == null)
                return false;

            if (thing.def.building.isNaturalRock)
                return thing.def.building.mineableThing != null;

            return thing.def.destroyable && thing.Faction != pawn.Faction;
        }

        public static Thing FindTarget(Pawn pawn)
        {
            if (!IsHarvester(pawn) || pawn.Map == null)
                return null;

            MapComponent_ReplicatorAssimilationTargets cache = pawn.Map.GetComponent<MapComponent_ReplicatorAssimilationTargets>();
            if (cache == null)
                return null;

            Pawn controller = ReplicatorSpecialistUtility.FindController(pawn, 30f);
            IntVec3 anchor = controller?.Position ?? pawn.Position;
            ReplicatorSpecialistRole? role = pawn.TryGetComp<CompReplicatorSpecialist>()?.Role;
            Thing best = null;
            float bestScore = float.MaxValue;

            foreach (Thing candidate in cache.Candidates)
            {
                if (!IsAssimilationTarget(candidate, pawn) || !pawn.CanReach(candidate, PathEndMode.Touch, Danger.Deadly))
                    continue;

                float score = anchor.DistanceToSquared(candidate.Position) + pawn.Position.DistanceToSquared(candidate.Position) * 0.20f;
                if (candidate.def.category == ThingCategory.Item)
                    score -= 20f;
                if (candidate.TryGetComp<CompPowerTrader>() != null)
                    score -= 35f;
                if (role == ReplicatorSpecialistRole.Burrower)
                    score += candidate.def.category == ThingCategory.Building ? -45f : 20f;
                if (role == ReplicatorSpecialistRole.Repairer)
                    score += 10f;
                if (role == ReplicatorSpecialistRole.Artillery)
                    score += 15f;

                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best;
        }

        public static int MatterYield(Thing target)
        {
            if (target == null)
                return 0;

            float marketValue = 0f;
            try { marketValue = target.GetStatValue(StatDefOf.MarketValue); }
            catch { marketValue = 0f; }

            if (target.def.category == ThingCategory.Item)
            {
                float value = Math.Max(1f, marketValue) * Math.Max(1, target.stackCount);
                return Math.Max(1, (int)Math.Ceiling(value * 0.02f));
            }

            float structure = Math.Max(1, target.MaxHitPoints) * 0.025f;
            float technology = Math.Max(0f, marketValue) * 0.01f;
            return Math.Max(1, (int)Math.Ceiling(structure + technology));
        }

        public static ReplicatorAdaptationFlags AdaptationsFrom(Thing target)
        {
            if (target?.def == null)
                return ReplicatorAdaptationFlags.None;

            ReplicatorAdaptationFlags learned = ReplicatorAdaptationFlags.Material;
            string name = target.def.defName ?? string.Empty;

            if (target.def.category == ThingCategory.Building)
                learned |= ReplicatorAdaptationFlags.Armor;
            if (target.TryGetComp<CompPowerTrader>() != null || target.TryGetComp<CompPowerBattery>() != null ||
                name.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("generator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("battery", StringComparison.OrdinalIgnoreCase) >= 0)
                learned |= ReplicatorAdaptationFlags.Power;
            if (target.def.building?.turretGunDef != null ||
                name.IndexOf("turret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("gun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("cannon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0)
                learned |= ReplicatorAdaptationFlags.Ranged;
            if (name.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                target.def.comps?.Any(c => c is CompProperties_ProjectileInterceptor) == true)
                learned |= ReplicatorAdaptationFlags.Shield;
            if (name.IndexOf("grav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("thruster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("shuttle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("puddlejumper", StringComparison.OrdinalIgnoreCase) >= 0)
                learned |= ReplicatorAdaptationFlags.Grav;

            return learned;
        }

        public static int CountBlockReplicators(Map map, Faction faction)
        {
            if (map?.mapPawns == null || faction == null)
                return 0;
            return map.mapPawns.AllPawnsSpawned.Count(p => p != null && !p.Dead && p.Spawned && p.Faction == faction && p.TryGetComp<CompReplicatorState>() != null);
        }

        public static int SpendMatterOnOffspring(Pawn parent, IntVec3 nearCell, int requested = 2)
        {
            if (parent?.Map == null || parent.Faction == null || parent.Faction == Faction.OfPlayer)
                return 0;

            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            PawnKindDef droneKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (state == null || droneKind == null)
                return 0;

            int spawned = 0;
            int maximum = Math.Max(0, Math.Min(2, requested));
            for (int i = 0; i < maximum; i++)
            {
                if (state.StoredMatter < OffspringMatterCost || CountBlockReplicators(parent.Map, parent.Faction) >= PopulationCap)
                    break;

                Pawn child = null;
                try
                {
                    child = PawnGenerator.GeneratePawn(droneKind, parent.Faction);
                    if (child == null)
                        break;

                    child.TryGetComp<CompReplicatorState>()?.InheritFrom(state, 0);
                    if (!GenPlace.TryPlaceThing(child, nearCell, parent.Map, ThingPlaceMode.Near))
                    {
                        if (!child.Destroyed)
                            child.Destroy(DestroyMode.Vanish);
                        break;
                    }

                    state.AddMatter(-OffspringMatterCost);
                    spawned++;
                }
                catch (Exception ex)
                {
                    if (child != null && !child.Destroyed && !child.Spawned)
                        child.Destroy(DestroyMode.Vanish);
                    Log.Error($"[WNG] Replicator offspring assembly failed: {ex}");
                    break;
                }
            }
            return spawned;
        }
    }

    public sealed class MapComponent_ReplicatorAssimilationTargets : MapComponent
    {
        private readonly List<Thing> candidates = new List<Thing>();
        private int nextRefreshTick;
        public IReadOnlyList<Thing> Candidates => candidates;

        public MapComponent_ReplicatorAssimilationTargets(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now < nextRefreshTick)
                return;

            bool active = map.mapPawns?.AllPawnsSpawned?.Any(ReplicatorAssimilationUtility.IsHarvester) == true;
            nextRefreshTick = now + (active ? ReplicatorAssimilationUtility.TargetRefreshTicks : ReplicatorAssimilationUtility.IdleRefreshTicks);
            if (!active)
            {
                candidates.Clear();
                return;
            }

            candidates.Clear();
            foreach (Thing thing in map.listerThings.AllThings)
            {
                if (thing == null || thing.Destroyed || !thing.Spawned || thing is Pawn || thing is Corpse)
                    continue;
                if (thing.def?.defName == "WNG_ReplicatorMatter" || thing.def?.defName == "WNG_ReplicatorCoreFragment")
                    continue;
                if (thing.def.category == ThingCategory.Item)
                {
                    candidates.Add(thing);
                    continue;
                }
                if (thing.def.category != ThingCategory.Building || thing.def.building == null)
                    continue;
                if (thing.def.building.isNaturalRock && thing.def.building.mineableThing == null)
                    continue;
                if (!thing.def.destroyable && !thing.def.building.isNaturalRock)
                    continue;
                candidates.Add(thing);
            }
        }
    }

    public sealed class JobGiver_ReplicatorAssimilate : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ReplicatorAssimilationUtility.IsHarvester(pawn) || pawn.Map == null || pawn.TryGetComp<CompReplicatorState>()?.EMPSuppressed == true)
                return null;

            Thing target = ReplicatorAssimilationUtility.FindTarget(pawn);
            if (target == null || !pawn.CanReserve(target, 1, -1, null, false))
                return null;

            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorAssimilate");
            return def == null ? null : JobMaker.MakeJob(def, target);
        }
    }

    public sealed class JobDriver_ReplicatorAssimilate : JobDriver
    {
        private Thing Target => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Target == null || Target.Destroyed || !Target.Spawned || !ReplicatorAssimilationUtility.IsAssimilationTarget(Target, pawn));
            this.FailOn(() => pawn.TryGetComp<CompReplicatorState>()?.EMPSuppressed == true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            int workTicks = 300;
            CompReplicatorSpecialist specialist = pawn.TryGetComp<CompReplicatorSpecialist>();
            if (specialist?.Role == ReplicatorSpecialistRole.Burrower && Target?.def?.category == ThingCategory.Building)
                workTicks = 180;
            if ((pawn.TryGetComp<CompReplicatorState>()?.Adaptations & ReplicatorAdaptationFlags.Material) != 0)
                workTicks = Math.Max(120, (int)Math.Round(workTicks * 0.85f));

            Toil assimilate = ToilMaker.MakeToil("WNG_ReplicatorAssimilate");
            assimilate.defaultCompleteMode = ToilCompleteMode.Delay;
            assimilate.defaultDuration = workTicks;
            assimilate.WithProgressBarToilDelay(TargetIndex.A);
            assimilate.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return assimilate;

            Toil finish = ToilMaker.MakeToil("WNG_ReplicatorAssimilateFinish");
            finish.initAction = () =>
            {
                Thing target = Target;
                if (!ReplicatorAssimilationUtility.IsAssimilationTarget(target, pawn) || pawn.Map == null)
                    return;

                CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
                if (state == null || state.EMPSuppressed)
                    return;

                int matter = ReplicatorAssimilationUtility.MatterYield(target);
                ReplicatorAdaptationFlags learned = ReplicatorAssimilationUtility.AdaptationsFrom(target);
                if ((learned & ReplicatorAdaptationFlags.Shield) != 0 &&
                    (state.Adaptations & ReplicatorAdaptationFlags.Shield) != 0)
                    learned |= ReplicatorAdaptationFlags.AntiShield;

                IntVec3 cell = target.Position;
                state.AddMatter(matter);
                state.Learn(learned);
                target.Destroy(DestroyMode.Vanish);
                ReplicatorAssimilationUtility.SpendMatterOnOffspring(pawn, cell, 2);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}
