using System;
using System.Collections.Generic;
using System.Linq;
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
        public float lifeForceCutoff = 0.85f;
        public bool requireKeeperOrQueen = true;

        public CompProperties_WraithFeedingNiche()
        {
            compClass = typeof(CompWraithFeedingNiche);
        }
    }

    /// <summary>
    /// Local Mature-Hive feeding-stock ecology. A Feeding Niche is a prisoner bed, not a proxy-pawn
    /// container: the exact captive pawn remains the bed occupant and is registered in the global
    /// exact-pawn captivity ledger. This system never opens or modifies strategic faction-hunger UI.
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
                nextCycleTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, NicheProps.intervalTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!rationFeedingEnabled || !parent.Spawned || !parent.IsHashIntervalTick(250))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (nextCycleTick < 0)
                nextCycleTick = SafeFutureTick(now, NicheProps.intervalTicks);
            if (now < nextCycleTick)
                return;

            Building_Bed niche = parent as Building_Bed;
            Map map = parent.Map;
            Faction owner = parent.Faction;
            if (niche == null || map == null || owner == null || !WraithCaptivityRegistry.IsWraithFaction(owner))
                return;
            if (NicheProps.requireKeeperOrQueen && !SupervisorPresent(map, owner))
                return;

            Pawn prisoner = FindHostedPrisoner(niche, owner);
            if (prisoner == null)
                return;

            List<Pawn> recipients = FindHungryWraith(map, owner, NicheProps.lifeForceCutoff);
            if (recipients.Count == 0)
                return;

            WraithCaptivityRegistry.Current?.RegisterCapturedPawn(prisoner, owner, feedingStock: true);
            AddBiologicalYears(prisoner, Math.Max(0, NicheProps.prisonerAgeYears));
            RefreshLifeDrained(prisoner);

            int fed = Math.Min(Math.Max(1, NicheProps.recipientLimit), recipients.Count);
            for (int i = 0; i < fed; i++)
                WraithLifeForceUtility.Offset(recipients[i], Math.Max(0f, NicheProps.lifeForcePerRecipient));

            nextCycleTick = SafeFutureTick(now, NicheProps.intervalTicks);
        }

        private static Pawn FindHostedPrisoner(Building_Bed niche, Faction owner)
        {
            foreach (Pawn pawn in niche.CurOccupants)
            {
                if (!WraithCaptivityRegistry.IsValidBiologicalCaptive(pawn))
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
            float threshold = Math.Max(0f, Math.Min(1f, cutoff));
            return map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Faction == owner)
                .Select(p => new { Pawn = p, LifeForce = WraithLifeForceUtility.Get(p) })
                .Where(x => x.LifeForce != null && x.LifeForce.Value < threshold)
                .OrderBy(x => x.LifeForce.Value)
                .Select(x => x.Pawn)
                .ToList();
        }

        private static bool SupervisorPresent(Map map, Faction owner)
        {
            return map.mapPawns.AllPawnsSpawned.Any(p =>
            {
                if (p == null || p.Dead || p.Downed || p.Faction != owner)
                    return false;
                string kind = p.kindDef?.defName ?? string.Empty;
                return kind == "WNG_WraithKeeper" || kind == "WNG_WraithQueen";
            });
        }

        private static void AddBiologicalYears(Pawn pawn, int years)
        {
            if (pawn?.ageTracker == null || years <= 0)
                return;
            long delta = (long)years * TicksPerBiologicalYear;
            long current = pawn.ageTracker.AgeBiologicalTicks;
            pawn.ageTracker.AgeBiologicalTicks = current > long.MaxValue - delta ? long.MaxValue : current + delta;
        }

        private static void RefreshLifeDrained(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeDrained");
            if (def == null || pawn?.health?.hediffSet == null)
                return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
                pawn.health.RemoveHediff(existing);
            pawn.health.AddHediff(def);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
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
                defaultDesc = "Allow this Feeding Niche to provide nonfatal local ration feeding to hungry Wraith. This does not interact with strategic faction hunger.",
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
            if (NicheProps.requireKeeperOrQueen && !SupervisorPresent(parent.Map, parent.Faction))
                return "Ration feeding: requires Keeper or Queen";
            return "Ration feeding: local Hive ecology";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextCycleTick, "wngFeedingNicheNextCycle", -1);
            Scribe_Values.Look(ref rationFeedingEnabled, "wngFeedingNicheEnabled", true);
        }
    }
}
