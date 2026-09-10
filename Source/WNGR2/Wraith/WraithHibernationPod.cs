using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithHibernationPod : CompProperties
    {
        public int hibernationTicks = 180000;
        public int maintenanceIntervalTicks = 60000;
        public int biomassMaintenanceCost = 1;
        public float maintenanceLifeForceGain = 0.02f;

        public CompProperties_WraithHibernationPod()
        {
            compClass = typeof(CompWraithHibernationPod);
        }
    }

    /// <summary>
    /// Dedicated Wraith hibernation bed. Occupancy itself maintains the same hibernation Hediff
    /// used by the deliberate self-cast ability, so no Keeper is required merely to hibernate.
    /// A Keeper or Queen of the pod's faction may optionally spend cultured biomass once per day
    /// to give the sleeper a small Life Force maintenance pulse.
    /// </summary>
    public sealed class CompWraithHibernationPod : ThingComp
    {
        private int nextMaintenanceTick = -1;
        private bool keeperMaintenanceEnabled = true;

        private CompProperties_WraithHibernationPod PodProps => (CompProperties_WraithHibernationPod)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextMaintenanceTick < 0)
                nextMaintenanceTick = Find.TickManager.TicksGame + Math.Max(1, PodProps.maintenanceIntervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || !parent.IsHashIntervalTick(250))
                return;

            Building_Bed pod = parent as Building_Bed;
            Pawn sleeper = FindWraithSleeper(pod);
            if (sleeper == null)
                return;

            MaintainHibernationState(sleeper);

            int now = Find.TickManager.TicksGame;
            if (nextMaintenanceTick < 0)
                nextMaintenanceTick = now + Math.Max(1, PodProps.maintenanceIntervalTicks);
            if (now < nextMaintenanceTick)
                return;

            nextMaintenanceTick = now + Math.Max(1, PodProps.maintenanceIntervalTicks);
            if (!keeperMaintenanceEnabled)
                return;

            Faction owner = parent.Faction;
            if (owner == null || sleeper.Faction != owner || !SupervisorPresent(parent.Map, owner))
                return;

            Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(sleeper);
            if (lifeForce == null || lifeForce.Value >= lifeForce.Max - 0.001f)
                return;

            int biomassCost = Math.Max(0, PodProps.biomassMaintenanceCost);
            if (biomassCost > 0 && !TryConsumeBiomass(parent.Map, biomassCost))
                return;

            WraithLifeForceUtility.Offset(sleeper, Math.Max(0f, PodProps.maintenanceLifeForceGain));
        }

        private static Pawn FindWraithSleeper(Building_Bed pod)
        {
            if (pod == null)
                return null;

            foreach (Pawn pawn in pod.CurOccupants)
            {
                if (pawn == null || pawn.Dead)
                    continue;
                if (WraithLifeForceUtility.Get(pawn) != null)
                    return pawn;
            }
            return null;
        }

        private void MaintainHibernationState(Pawn sleeper)
        {
            HediffDef hibernatingDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
            if (hibernatingDef == null || sleeper?.health == null)
                return;

            Hediff hibernating = sleeper.health.hediffSet.GetFirstHediffOfDef(hibernatingDef);
            if (hibernating == null)
            {
                hibernating = sleeper.health.AddHediff(hibernatingDef);
            }

            HediffComp_Disappears disappears = hibernating?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
                disappears.ticksToDisappear = Math.Max(disappears.ticksToDisappear, Math.Max(1, PodProps.hibernationTicks));
        }

        private static bool SupervisorPresent(Map map, Faction owner)
        {
            if (map == null || owner == null)
                return false;

            IReadOnlyList<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn pawn = spawned[i];
                if (pawn == null || pawn.Dead || pawn.Faction != owner)
                    continue;

                string kind = pawn.kindDef?.defName ?? string.Empty;
                if (kind == "WNG_WraithKeeper" || kind == "WNG_WraithQueen")
                    return true;
            }
            return false;
        }

        private static bool TryConsumeBiomass(Map map, int count)
        {
            if (count <= 0)
                return true;
            if (map == null)
                return false;

            ThingDef biomassDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            if (biomassDef == null)
                return false;

            List<Thing> stacks = map.listerThings.ThingsOfDef(biomassDef);
            int total = 0;
            for (int i = 0; i < stacks.Count; i++)
            {
                Thing stack = stacks[i];
                if (stack != null && !stack.Destroyed)
                    total += stack.stackCount;
            }
            if (total < count)
                return false;

            int remaining = count;
            for (int i = stacks.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Thing stack = stacks[i];
                if (stack == null || stack.Destroyed)
                    continue;

                int take = Math.Min(remaining, stack.stackCount);
                if (take >= stack.stackCount)
                {
                    remaining -= stack.stackCount;
                    stack.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    Thing split = stack.SplitOff(take);
                    remaining -= take;
                    split.Destroy(DestroyMode.Vanish);
                }
            }

            return remaining <= 0;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent.Faction != Faction.OfPlayer)
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "Keeper biomass maintenance",
                defaultDesc = "Allow a Keeper or Queen to spend one cultured biomass per day to restore a small amount of Life Force to an allied Wraith sleeping in this pod. Hibernation itself does not require a Keeper.",
                isActive = () => keeperMaintenanceEnabled,
                toggleAction = () => keeperMaintenanceEnabled = !keeperMaintenanceEnabled
            };
        }

        public override string CompInspectStringExtra()
        {
            Building_Bed pod = parent as Building_Bed;
            Pawn sleeper = FindWraithSleeper(pod);
            if (sleeper == null)
                return "Hibernation pod: empty";

            if (!keeperMaintenanceEnabled)
                return "Hibernation: maintained\nKeeper biomass maintenance: disabled";

            Faction owner = parent.Faction;
            bool supervised = owner != null && sleeper.Faction == owner && SupervisorPresent(parent.Map, owner);
            return supervised
                ? "Hibernation: maintained\nKeeper biomass maintenance: available"
                : "Hibernation: maintained\nKeeper biomass maintenance: requires allied Keeper or Queen";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextMaintenanceTick, "wngHibernationPodNextMaintenance", -1);
            Scribe_Values.Look(ref keeperMaintenanceEnabled, "wngHibernationPodKeeperMaintenance", true);
        }
    }
}
