using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithFeedingNiche : CompProperties
    {
        public int intervalTicks = 120000;
        public int prisonerAgeYears = 2;
        public float lifeForcePerRecipient = 0.12f;
        public int recipientLimit = 3;
        public int biomassYield = 4;
        public float lifeForceCutoff = 0.85f;

        public CompProperties_WraithFeedingNiche()
        {
            compClass = typeof(CompWraithFeedingNiche);
        }
    }

    /// <summary>
    /// Keeper-supervised ration feeding for a prisoner held in a Feeding Niche.
    /// This is local hive husbandry: it neither raises faction hunger requests nor uses
    /// the fatal repeat-feed rule from the full Drain Life ability.
    /// </summary>
    public sealed class CompWraithFeedingNiche : ThingComp
    {
        private const long TicksPerBiologicalYear = 3600000L;
        private int nextCycleTick = -1;
        private bool rationFeedingEnabled = true;

        private CompProperties_WraithFeedingNiche NicheProps => (CompProperties_WraithFeedingNiche)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && nextCycleTick < 0)
                nextCycleTick = Find.TickManager.TicksGame + Math.Max(1, NicheProps.intervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!rationFeedingEnabled || !parent.Spawned || !parent.IsHashIntervalTick(250))
                return;

            int now = Find.TickManager.TicksGame;
            if (nextCycleTick < 0)
                nextCycleTick = now + Math.Max(1, NicheProps.intervalTicks);
            if (now < nextCycleTick)
                return;

            Building_Bed niche = parent as Building_Bed;
            Map map = parent.Map;
            Faction owner = parent.Faction;
            if (niche == null || map == null || owner == null || !SupervisorPresent(map, owner))
                return;

            Pawn prisoner = FindHostedPrisoner(niche, owner);
            if (prisoner == null)
                return;

            List<Pawn> recipients = FindHungryWraith(map, owner, NicheProps.lifeForceCutoff);
            if (recipients.Count == 0)
                return;

            AddBiologicalYears(prisoner, Math.Max(0, NicheProps.prisonerAgeYears));
            RefreshLifeDrained(prisoner);

            int limit = Math.Max(1, NicheProps.recipientLimit);
            int fed = Math.Min(limit, recipients.Count);
            for (int i = 0; i < fed; i++)
                WraithLifeForceUtility.Offset(recipients[i], Math.Max(0f, NicheProps.lifeForcePerRecipient));

            SpawnBiomass(map, parent.Position, Math.Max(0, NicheProps.biomassYield));
            nextCycleTick = now + Math.Max(1, NicheProps.intervalTicks);
        }

        private static Pawn FindHostedPrisoner(Building_Bed niche, Faction owner)
        {
            List<Pawn> occupants = niche.CurOccupants;
            for (int i = 0; i < occupants.Count; i++)
            {
                Pawn pawn = occupants[i];
                if (!WraithCaptureUtility.IsValidCaptiveIdentity(pawn))
                    continue;
                if (pawn.guest?.IsPrisoner != true)
                    continue;
                if (pawn.guest.HostFaction != owner)
                    continue;
                return pawn;
            }
            return null;
        }

        private static List<Pawn> FindHungryWraith(Map map, Faction owner, float cutoff)
        {
            List<Pawn> result = new List<Pawn>();
            List<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
            float threshold = Math.Max(0f, Math.Min(1f, cutoff));

            for (int i = 0; i < spawned.Count; i++)
            {
                Pawn pawn = spawned[i];
                if (pawn == null || pawn.Dead || pawn.Faction != owner)
                    continue;

                Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(pawn);
                if (lifeForce == null || lifeForce.Value >= threshold)
                    continue;
                result.Add(pawn);
            }

            result.Sort((a, b) =>
            {
                Gene_Resource_LifeForce left = WraithLifeForceUtility.Get(a);
                Gene_Resource_LifeForce right = WraithLifeForceUtility.Get(b);
                return (left?.Value ?? 1f).CompareTo(right?.Value ?? 1f);
            });
            return result;
        }

        private static bool SupervisorPresent(Map map, Faction owner)
        {
            List<Pawn> spawned = map.mapPawns.AllPawnsSpawned;
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

        private static void AddBiologicalYears(Pawn pawn, int years)
        {
            if (pawn?.ageTracker == null || years <= 0)
                return;

            long added;
            try
            {
                checked { added = (long)years * TicksPerBiologicalYear; }
            }
            catch (OverflowException)
            {
                added = long.MaxValue;
            }

            try
            {
                checked { pawn.ageTracker.AgeBiologicalTicks += added; }
            }
            catch (OverflowException)
            {
                pawn.ageTracker.AgeBiologicalTicks = long.MaxValue;
            }
        }

        private static void RefreshLifeDrained(Pawn prisoner)
        {
            HediffDef drained = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeDrained");
            if (drained == null || prisoner?.health == null)
                return;

            Hediff existing = prisoner.health.hediffSet.GetFirstHediffOfDef(drained);
            if (existing != null)
                prisoner.health.RemoveHediff(existing);
            prisoner.health.AddHediff(drained);
        }

        private static void SpawnBiomass(Map map, IntVec3 near, int count)
        {
            if (map == null || count <= 0)
                return;

            ThingDef biomassDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Biomass");
            if (biomassDef == null)
                return;

            int remaining = count;
            int stackLimit = Math.Max(1, biomassDef.stackLimit);
            while (remaining > 0)
            {
                Thing biomass = ThingMaker.MakeThing(biomassDef);
                biomass.stackCount = Math.Min(remaining, stackLimit);
                remaining -= biomass.stackCount;
                GenPlace.TryPlaceThing(biomass, near, map, ThingPlaceMode.Near);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent.Faction != Faction.OfPlayer)
                yield break;

            yield return new Command_Toggle
            {
                defaultLabel = "Keeper ration feeding",
                defaultDesc = "Permit Keeper- or Queen-supervised nonfatal ration feeding from a prisoner held in this niche when allied Wraith are below the configured Life Force threshold.",
                isActive = () => rationFeedingEnabled,
                toggleAction = () => rationFeedingEnabled = !rationFeedingEnabled
            };
        }

        public override string CompInspectStringExtra()
        {
            if (!rationFeedingEnabled)
                return "Ration feeding: disabled";
            if (!parent.Spawned)
                return null;
            return SupervisorPresent(parent.Map, parent.Faction)
                ? "Ration feeding: Keeper supervised"
                : "Ration feeding: requires Keeper or Queen";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextCycleTick, "wngFeedingNicheNextCycle", -1);
            Scribe_Values.Look(ref rationFeedingEnabled, "wngFeedingNicheEnabled", true);
        }
    }
}
