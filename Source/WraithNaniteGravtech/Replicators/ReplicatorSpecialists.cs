using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public enum ReplicatorSpecialistRole
    {
        Controller,
        Repairer,
        Burrower,
        Artillery
    }

    public sealed class CompProperties_ReplicatorSpecialist : CompProperties
    {
        public ReplicatorSpecialistRole role = ReplicatorSpecialistRole.Controller;
        public float effectRadius = 10f;
        public int actionIntervalTicks = 600;
        public float repairAmount = 0.40f;
        public float artilleryRange = 28f;
        public int artilleryCooldownTicks = 420;
        public float artilleryDamage = 12f;
        public int retaliationTicks = 2500;

        public CompProperties_ReplicatorSpecialist()
        {
            compClass = typeof(CompReplicatorSpecialist);
        }
    }

    public sealed class CompReplicatorSpecialist : ThingComp
    {
        private int nextActionTick;
        private int retaliationUntilTick;
        private CompProperties_ReplicatorSpecialist Props => (CompProperties_ReplicatorSpecialist)props;

        public ReplicatorSpecialistRole Role => Props.role;
        public bool ControllerActive => Props.role == ReplicatorSpecialistRole.Controller && !Suppressed;
        public bool Suppressed => parent.TryGetComp<CompReplicatorState>()?.EMPSuppressed == true;

        private bool HasAdaptation(ReplicatorAdaptationFlags flag)
        {
            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            return state != null && (state.Adaptations & flag) != ReplicatorAdaptationFlags.None;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextActionTick <= 0)
                nextActionTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.actionIntervalTicks);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (Props.role != ReplicatorSpecialistRole.Artillery || dinfo.Instigator == null)
                return;

            Pawn pawn = parent as Pawn;
            Pawn attacker = dinfo.Instigator as Pawn;
            if (pawn?.Faction == null || attacker?.Faction == null || !pawn.Faction.HostileTo(attacker.Faction))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            retaliationUntilTick = Math.Max(retaliationUntilTick, now + Math.Max(60, Props.retaliationTicks));
            nextActionTick = Math.Min(nextActionTick, now + 60);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || Suppressed)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextActionTick)
                return;

            switch (Props.role)
            {
                case ReplicatorSpecialistRole.Repairer:
                    nextActionTick = now + Math.Max(60, Props.actionIntervalTicks);
                    TryRepairAlly(pawn);
                    break;
                case ReplicatorSpecialistRole.Artillery:
                    int cooldown = Math.Max(60, Props.artilleryCooldownTicks);
                    if (HasAdaptation(ReplicatorAdaptationFlags.Power))
                        cooldown = Math.Max(60, (int)Math.Round(cooldown * 0.75f));
                    nextActionTick = now + cooldown;
                    if (now <= retaliationUntilTick)
                        TryRetaliate(pawn);
                    break;
                default:
                    nextActionTick = now + Math.Max(60, Props.actionIntervalTicks);
                    break;
            }
        }

        private void TryRepairAlly(Pawn pawn)
        {
            float radiusSq = Props.effectRadius * Props.effectRadius;
            CompReplicatorState ownState = pawn.TryGetComp<CompReplicatorState>();
            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned && p.Faction == pawn.Faction && p.TryGetComp<CompReplicatorState>() != null)
                .Where(p => p.Position.DistanceToSquared(pawn.Position) <= radiusSq)
                .Where(p => ownState != null && ownState.SameControlDomain(p.TryGetComp<CompReplicatorState>()))
                .Where(p => p.TryGetComp<CompReplicatorState>()?.EMPSuppressed != true)
                .Where(p => p.health?.hediffSet?.hediffs?.OfType<Hediff_Injury>().Any(h => h != null && !h.IsPermanent() && h.Severity > 0f) == true)
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();

            Hediff_Injury injury = target?.health?.hediffSet?.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => h != null && !h.IsPermanent() && h.Severity > 0f)
                .OrderByDescending(h => h.Severity)
                .FirstOrDefault();
            if (injury == null)
                return;

            float repair = Math.Max(0.01f, Props.repairAmount);
            if (HasAdaptation(ReplicatorAdaptationFlags.Power))
                repair *= 1.35f;
            injury.Heal(repair);
            FleckMaker.Static(target.TrueCenter(), target.Map, FleckDefOf.MicroSparks, 0.45f);
        }

        private void TryRetaliate(Pawn pawn)
        {
            float range = Math.Max(1f, Props.artilleryRange);
            if (HasAdaptation(ReplicatorAdaptationFlags.Ranged))
                range *= 1.20f;
            float radiusSq = range * range;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned && p.Faction != null && pawn.Faction != null && pawn.Faction.HostileTo(p.Faction))
                .Where(p => p.Position.DistanceToSquared(pawn.Position) <= radiusSq)
                .Where(p => GenSight.LineOfSight(pawn.Position, p.Position, pawn.Map))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
            if (target == null)
                return;

            float damage = Math.Max(1f, Props.artilleryDamage);
            if (HasAdaptation(ReplicatorAdaptationFlags.Ranged))
                damage *= 1.25f;
            target.TakeDamage(new DamageInfo(DamageDefOf.Bullet, damage, 0.25f, -1f, pawn));

            if (!target.Destroyed && HasAdaptation(ReplicatorAdaptationFlags.AntiShield))
                target.TakeDamage(new DamageInfo(DamageDefOf.EMP, 8f, 0f, -1f, pawn));

            if (!target.Destroyed && target.Spawned)
                FleckMaker.Static(target.TrueCenter(), target.Map, FleckDefOf.ExplosionFlash, 0.55f);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextActionTick, "wngReplicatorSpecialistNextAction", 0);
            Scribe_Values.Look(ref retaliationUntilTick, "wngReplicatorArtilleryRetaliationUntil", 0);
        }
    }

    public sealed class CompProperties_ReplicatorSpecialistGrowth : CompProperties
    {
        public int checkIntervalTicks = 1200;
        public int lightMatterCost = 10;
        public int heavyMatterCost = 20;
        public int controllerMinDomainPopulation = 8;

        public CompProperties_ReplicatorSpecialistGrowth()
        {
            compClass = typeof(CompReplicatorSpecialistGrowth);
        }
    }

    public sealed class CompReplicatorSpecialistGrowth : ThingComp
    {
        private static bool transformInProgress;
        private int nextCheckTick;
        private CompProperties_ReplicatorSpecialistGrowth Props => (CompProperties_ReplicatorSpecialistGrowth)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextCheckTick <= 0)
                nextCheckTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, Math.Max(250, Props.checkIntervalTicks));
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Faction == null || pawn.Faction == Faction.OfPlayer ||
                transformInProgress || pawn.TryGetComp<CompReplicatorSpecialist>() != null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextCheckTick)
                return;
            nextCheckTick = SafeFutureTick(now, Math.Max(250, Props.checkIntervalTicks));

            CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
            if (state == null || state.EMPSuppressed || string.IsNullOrEmpty(state.ControlDomainId))
                return;
            if (!TryChoose(pawn, state, out PawnKindDef targetKind, out int matterCost) || state.StoredMatter < matterCost)
                return;

            Transform(pawn, state, targetKind, matterCost);
        }

        private bool TryChoose(Pawn pawn, CompReplicatorState state, out PawnKindDef kind, out int cost)
        {
            kind = null;
            cost = 0;
            bool drone = pawn.def?.defName == "WNG_ReplicatorDrone";
            bool hunter = pawn.def?.defName == "WNG_ReplicatorHunter";
            if (!drone && !hunter)
                return false;

            int population = CountDomain(pawn, state);
            ReplicatorAdaptationFlags learned = state.Adaptations;

            if (hunter && population >= Math.Max(2, Props.controllerMinDomainPopulation) && Has(learned, ReplicatorAdaptationFlags.Power) &&
                Has(learned, ReplicatorAdaptationFlags.Material) && CountRole(pawn, state, ReplicatorSpecialistRole.Controller) == 0)
                return Resolve("WNG_ReplicatorController", Props.heavyMatterCost, out kind, out cost);

            if (hunter && Has(learned, ReplicatorAdaptationFlags.Ranged) &&
                CountRole(pawn, state, ReplicatorSpecialistRole.Artillery) < Math.Max(1, population / 10))
                return Resolve("WNG_ReplicatorArtillery", Props.heavyMatterCost, out kind, out cost);

            if (drone && Has(learned, ReplicatorAdaptationFlags.Power) &&
                CountRole(pawn, state, ReplicatorSpecialistRole.Repairer) < Math.Max(1, population / 8))
                return Resolve("WNG_ReplicatorRepairer", Props.lightMatterCost, out kind, out cost);

            if (drone && Has(learned, ReplicatorAdaptationFlags.Material) && Has(learned, ReplicatorAdaptationFlags.Armor) &&
                CountRole(pawn, state, ReplicatorSpecialistRole.Burrower) < Math.Max(1, population / 8))
                return Resolve("WNG_ReplicatorBurrower", Props.lightMatterCost, out kind, out cost);

            return false;
        }

        private static bool Has(ReplicatorAdaptationFlags flags, ReplicatorAdaptationFlags flag) => (flags & flag) != ReplicatorAdaptationFlags.None;

        private static bool Resolve(string defName, int requestedCost, out PawnKindDef kind, out int cost)
        {
            kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
            cost = Math.Max(1, requestedCost);
            return kind?.race != null;
        }

        private static int CountDomain(Pawn pawn, CompReplicatorState state)
        {
            return pawn.Map.mapPawns.AllPawnsSpawned.Count(other =>
                other != null && !other.Dead && other.Spawned && other.Faction == pawn.Faction &&
                state.SameControlDomain(other.TryGetComp<CompReplicatorState>()));
        }

        private static int CountRole(Pawn pawn, CompReplicatorState state, ReplicatorSpecialistRole role)
        {
            return pawn.Map.mapPawns.AllPawnsSpawned.Count(other =>
                other != null && !other.Dead && other.Spawned && other.Faction == pawn.Faction &&
                other.TryGetComp<CompReplicatorSpecialist>()?.Role == role &&
                state.SameControlDomain(other.TryGetComp<CompReplicatorState>()));
        }

        private static void Transform(Pawn source, CompReplicatorState sourceState, PawnKindDef targetKind, int matterCost)
        {
            Pawn replacement = null;
            try
            {
                transformInProgress = true;
                replacement = PawnGenerator.GeneratePawn(new PawnGenerationRequest(targetKind, source.Faction, PawnGenerationContext.NonPlayer, source.Map.Tile));
                if (replacement == null)
                    return;

                replacement.TryGetComp<CompReplicatorState>()?.InheritFrom(sourceState, Math.Max(0, sourceState.StoredMatter - matterCost));
                GenSpawn.Spawn(replacement, source.Position, source.Map);
                if (!replacement.Spawned)
                {
                    if (!replacement.Destroyed)
                        replacement.Destroy(DestroyMode.Vanish);
                    return;
                }

                ReplicatorHierarchyTransaction.Begin(source);
                try { source.Destroy(DestroyMode.Vanish); }
                finally { ReplicatorHierarchyTransaction.End(source); }

                if (!source.Destroyed && !replacement.Destroyed)
                {
                    replacement.Destroy(DestroyMode.Vanish);
                    Log.Error("[WNG] Replicator specialist transform failed closed because the source body remained.");
                }
            }
            catch (Exception ex)
            {
                if (replacement != null && !replacement.Destroyed)
                    replacement.Destroy(DestroyMode.Vanish);
                Log.Error($"[WNG] Replicator specialist transform failed: {ex}");
            }
            finally
            {
                transformInProgress = false;
            }
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(0, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextCheckTick, "wngReplicatorSpecialistGrowthNextTick", 0);
        }
    }

    internal static class ReplicatorSpecialistUtility
    {
        public static CompReplicatorSpecialist Specialist(Pawn pawn)
        {
            return pawn?.TryGetComp<CompReplicatorSpecialist>();
        }

        public static Pawn FindController(Pawn pawn, float radius = 30f)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return null;

            CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
            float radiusSq = radius * radius;
            return pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned && p.Faction == pawn.Faction)
                .Where(p => p.Position.DistanceToSquared(pawn.Position) <= radiusSq)
                .Where(p => p.TryGetComp<CompReplicatorSpecialist>()?.ControllerActive == true)
                .Where(p => state != null && state.SameControlDomain(p.TryGetComp<CompReplicatorState>()))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
        }
    }
}
