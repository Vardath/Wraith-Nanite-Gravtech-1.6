using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorEMPReceiver : CompProperties
    {
        public int suppressionTicks = 1800;

        public CompProperties_ReplicatorEMPReceiver()
        {
            compClass = typeof(CompReplicatorEMPReceiver);
        }
    }

    public sealed class CompReplicatorEMPReceiver : ThingComp
    {
        private CompProperties_ReplicatorEMPReceiver Props => (CompProperties_ReplicatorEMPReceiver)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (dinfo.Def == DamageDefOf.EMP)
                parent.TryGetComp<CompReplicatorState>()?.SuppressByEMP(Math.Max(60, Props.suppressionTicks));
        }
    }

    public sealed class CompProperties_ReplicatorRegeneration : CompProperties
    {
        public int intervalTicks = 600;
        public float healAmount = 0.20f;

        public CompProperties_ReplicatorRegeneration()
        {
            compClass = typeof(CompReplicatorRegeneration);
        }
    }

    public sealed class CompReplicatorRegeneration : ThingComp
    {
        private int nextHealTick;
        private CompProperties_ReplicatorRegeneration Props => (CompProperties_ReplicatorRegeneration)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextHealTick <= 0)
                nextHealTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(60, Props.intervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextHealTick)
                return;

            CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
            bool powerAdapted = state != null && (state.Adaptations & ReplicatorAdaptationFlags.Power) != 0;
            int interval = Math.Max(60, powerAdapted ? (int)Math.Round(Props.intervalTicks * 0.65f) : Props.intervalTicks);
            nextHealTick = now + interval;

            if (state?.EMPSuppressed == true)
                return;

            Hediff_Injury injury = pawn.health?.hediffSet?.hediffs
                .OfType<Hediff_Injury>()
                .Where(h => h != null && !h.IsPermanent() && h.Severity > 0f)
                .OrderByDescending(h => h.Severity)
                .FirstOrDefault();
            if (injury == null)
                return;

            float amount = Math.Max(0.01f, Props.healAmount) * (powerAdapted ? 1.35f : 1f);
            injury.Heal(amount);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextHealTick, "wngReplicatorNextHealTick", 0);
        }
    }

    public sealed class CompProperties_ReplicatorMatterReassembly : CompProperties
    {
        public int minimumStack = 10;
        public int consumePerReplicator = 10;
        public int dormantTicks = 60000;
        public float baseChancePerRareTick = 0.0007f;
        public int hostilePopulationCap = 120;

        public CompProperties_ReplicatorMatterReassembly()
        {
            compClass = typeof(CompReplicatorMatterReassembly);
        }
    }

    public sealed class CompReplicatorMatterReassembly : ThingComp
    {
        private int exposedTicks;
        private CompProperties_ReplicatorMatterReassembly Props => (CompProperties_ReplicatorMatterReassembly)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref exposedTicks, "wngReplicatorMatterExposedTicks", 0);
        }

        public override void PostSplitOff(Thing piece)
        {
            base.PostSplitOff(piece);
            CompReplicatorMatterReassembly split = piece?.TryGetComp<CompReplicatorMatterReassembly>();
            if (split != null)
                split.exposedTicks = exposedTicks;
        }

        public override void PreAbsorbStack(Thing otherStack, int count)
        {
            base.PreAbsorbStack(otherStack, count);
            CompReplicatorMatterReassembly other = otherStack?.TryGetComp<CompReplicatorMatterReassembly>();
            if (other != null)
                exposedTicks = Math.Max(exposedTicks, other.exposedTicks);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent == null || !parent.Spawned || parent.Map == null)
                return;

            if (ReplicatorContainmentUtility.IsContained(parent))
            {
                exposedTicks = 0;
                return;
            }

            exposedTicks = Math.Min(int.MaxValue - 250, exposedTicks) + 250;
            if (exposedTicks < Props.dormantTicks || parent.stackCount < Props.minimumStack)
                return;

            Faction swarm = ResolveSwarmFaction();
            if (swarm == null || CountActiveNonPlayerBlocks(parent.Map) >= Math.Max(1, Props.hostilePopulationCap))
                return;

            float stackScale = Math.Max(0.5f, parent.stackCount / 20f);
            float chance = Math.Min(0.01f, Math.Max(0f, Props.baseChancePerRareTick) * stackScale);
            if (!Rand.Chance(chance))
                return;

            TryReassemble(parent.Map, parent.Position, swarm);
        }

        public override string CompInspectStringExtra()
        {
            if (parent == null || parent.stackCount < Props.minimumStack)
                return null;
            if (parent.Spawned && ReplicatorContainmentUtility.IsContained(parent))
                return "Replicator substrate contained: powered containment has reset the self-assembly timer.";
            if (exposedTicks < Props.dormantTicks)
            {
                float days = Math.Max(0, Props.dormantTicks - exposedTicks) / 60000f;
                return $"Dormant Replicator substrate: {days:0.0} day(s) until self-assembly risk.";
            }
            return "Unstable Replicator substrate: may self-assemble into a hostile Replicator.";
        }

        private static Faction ResolveSwarmFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            return def == null ? null : Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.def == def);
        }

        private static int CountActiveNonPlayerBlocks(Map map)
        {
            if (map?.mapPawns == null)
                return 0;
            return map.mapPawns.AllPawnsSpawned.Count(p =>
                p != null && !p.Dead && p.Spawned && p.Faction != null && p.Faction != Faction.OfPlayer &&
                p.TryGetComp<CompReplicatorState>() != null);
        }

        private void TryReassemble(Map map, IntVec3 position, Faction swarm)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (kind == null || CountActiveNonPlayerBlocks(map) >= Math.Max(1, Props.hostilePopulationCap))
                return;

            Pawn pawn = null;
            try
            {
                pawn = PawnGenerator.GeneratePawn(kind, swarm);
                if (pawn == null || !GenPlace.TryPlaceThing(pawn, position, map, ThingPlaceMode.Near))
                {
                    if (pawn != null && !pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                    return;
                }

                int consume = Math.Min(Math.Max(1, Props.consumePerReplicator), parent.stackCount);
                if (consume >= parent.stackCount)
                    parent.Destroy(DestroyMode.Vanish);
                else
                    parent.stackCount -= consume;

                Messages.Message("Dormant Replicator Matter has self-assembled into a hostile Replicator.", pawn, MessageTypeDefOf.ThreatSmall, true);
                exposedTicks = 0;
            }
            catch (Exception ex)
            {
                if (pawn != null && !pawn.Destroyed && !pawn.Spawned)
                    pawn.Destroy(DestroyMode.Vanish);
                Log.Error($"[WNG] Replicator Matter reassembly failed: {ex}");
            }
        }
    }

    internal static class ReplicatorContainmentUtility
    {
        public static bool IsContained(Thing matter)
        {
            if (matter == null || !matter.Spawned || matter.Map == null)
                return false;

            return matter.Position.GetThingList(matter.Map).Any(t =>
                t != null &&
                t.def?.defName == "WNG_ReplicatorContainmentField" &&
                t.TryGetComp<CompPowerTrader>()?.PowerOn == true);
        }
    }
}
