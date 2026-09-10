using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithHiveHeart : CompProperties
    {
        public int pulseTicks = 600;
        public float repairRadius = 24f;
        public int maxRepairTargets = 4;
        public int repairHitPoints = 7;
        public int biomassCostPerPulse = 1;

        public CompProperties_WraithHiveHeart()
        {
            compClass = typeof(CompWraithHiveHeart);
        }
    }

    /// <summary>
    /// Bounded local maintenance organ for an existing Wraith Hive. The Heart never creates,
    /// resurrects or reconstructs anything: it can only repair already-existing damaged WNG
    /// organic structures of its own faction, paying cultured biomass for each productive pulse.
    /// </summary>
    public sealed class CompWraithHiveHeart : ThingComp
    {
        private static readonly string[] OrganicStructureDefNames =
        {
            "WNG_HiveHeart",
            "WNG_LivingForge",
            "WNG_FeedingNiche",
            "WNG_HibernationPod",
            "WNG_GrowthChamber",
            "WNG_BioelectricOrgan",
            "WNG_DormancyVault"
        };

        private CompProperties_WraithHiveHeart HeartProps => (CompProperties_WraithHiveHeart)props;

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || parent.Map == null || parent.Faction == null)
                return;
            if (!parent.IsHashIntervalTick(Math.Max(1, HeartProps.pulseTicks)))
                return;

            List<Thing> damaged = FindDamagedOrganicStructures();
            if (damaged.Count == 0)
                return;

            int biomassCost = Math.Max(0, HeartProps.biomassCostPerPulse);
            if (biomassCost > 0 && !TryConsumeBiomass(parent.Map, biomassCost))
                return;

            damaged.Sort((a, b) =>
                a.Position.DistanceToSquared(parent.Position).CompareTo(b.Position.DistanceToSquared(parent.Position)));

            int repaired = 0;
            int limit = Math.Max(0, HeartProps.maxRepairTargets);
            int heal = Math.Max(0, HeartProps.repairHitPoints);
            for (int i = 0; i < damaged.Count && repaired < limit; i++)
            {
                Thing structure = damaged[i];
                if (structure == null || structure.Destroyed || structure.HitPoints >= structure.MaxHitPoints)
                    continue;

                structure.HitPoints = Math.Min(structure.MaxHitPoints, structure.HitPoints + heal);
                repaired++;
            }
        }

        private List<Thing> FindDamagedOrganicStructures()
        {
            List<Thing> result = new List<Thing>();
            Map map = parent.Map;
            float radiusSquared = HeartProps.repairRadius * HeartProps.repairRadius;

            for (int d = 0; d < OrganicStructureDefNames.Length; d++)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(OrganicStructureDefNames[d]);
                if (def == null)
                    continue;

                List<Thing> things = map.listerThings.ThingsOfDef(def);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (thing == null || thing.Destroyed || !thing.Spawned || thing.Faction != parent.Faction)
                        continue;
                    if (thing.HitPoints >= thing.MaxHitPoints)
                        continue;
                    if (thing.Position.DistanceToSquared(parent.Position) > radiusSquared)
                        continue;
                    result.Add(thing);
                }
            }

            return result;
        }

        private static bool TryConsumeBiomass(Map map, int count)
        {
            if (count <= 0)
                return true;
            if (map == null)
                return false;

            ThingDef biomass = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            if (biomass == null)
                return false;

            List<Thing> stacks = map.listerThings.ThingsOfDef(biomass);
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

        public override string CompInspectStringExtra()
        {
            if (!parent.Spawned || parent.Map == null)
                return null;

            int nearbyDamaged = FindDamagedOrganicStructures().Count;
            return nearbyDamaged > 0
                ? "Hive repair lattice: " + nearbyDamaged + " damaged organic structure(s) in range"
                : "Hive repair lattice: stable";
        }
    }
}
