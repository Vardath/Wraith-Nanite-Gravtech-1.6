using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Standalone Wraith Dart culling run. This incident does not require CatCraft Stargates.
    /// Stargate-enabled arrival/retreat is an optional route layered elsewhere.
    /// </summary>
    public sealed class IncidentWorker_WraithDartCulling : IncidentWorker
    {
        public override float ChanceFactorNow(IIncidentTarget target)
        {
            float factor = base.ChanceFactorNow(target);
            WraithFactionHunger hunger = Current.Game?.GetComponent<WraithFactionHunger>();
            if (hunger == null || Find.FactionManager?.AllFactionsListForReading == null)
                return factor;

            float highest = 0f;
            foreach (Faction faction in HostileWraithFactions())
                highest = Math.Max(highest, hunger.GetStrategicHunger(faction));

            // Opportunistic culling remains possible even when a Hive is relatively fed;
            // strategic hunger only increases pressure, it does not create a second feeding system.
            return factor * (0.35f + 1.65f * highest);
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map) || Faction.OfPlayer == null)
                return false;

            ThingDef dartDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithDart");
            if (dartDef == null)
                return false;

            if (map.listerThings.ThingsOfDef(dartDef).Any(t => t != null && !t.Destroyed))
                return false;

            if (!map.mapPawns.AllPawnsSpawned.Any(p => p != null && p.Spawned && WraithCaptivityRegistry.IsValidBiologicalCaptive(p)))
                return false;

            return HostileWraithFactions().Any() && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map))
                return false;

            ThingDef dartDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithDart");
            if (dartDef == null || map.listerThings.ThingsOfDef(dartDef).Any(t => t != null && !t.Destroyed))
                return false;

            List<Faction> candidates = HostileWraithFactions().ToList();
            if (candidates.Count == 0)
                return false;

            if (!CellFinder.TryFindRandomEdgeCellWith(
                    cell => !cell.Fogged(map) && GenSpawn.CanSpawnAt(dartDef, cell, map),
                    map,
                    CellFinder.EdgeRoadChance_Hostile,
                    out IntVec3 entryCell))
                return false;

            Faction faction = SelectDispatchingFaction(candidates);
            if (faction == null)
                return false;

            Thing dart = ThingMaker.MakeThing(dartDef);
            if (dart == null)
                return false;

            dart.SetFaction(faction);
            GenSpawn.Spawn(dart, entryCell, map);

            CompWraithDartRaidMission mission = dart.TryGetComp<CompWraithDartRaidMission>();
            mission?.BeginTwoPassRaid();

            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, new TargetInfo(entryCell, map));
            return true;
        }

        private static IEnumerable<Faction> HostileWraithFactions()
        {
            if (Find.FactionManager?.AllFactionsListForReading == null || Faction.OfPlayer == null)
                return Enumerable.Empty<Faction>();

            return Find.FactionManager.AllFactionsListForReading.Where(f =>
                f != null &&
                !f.defeated &&
                WraithCaptivityRegistry.IsWraithFaction(f) &&
                f.HostileTo(Faction.OfPlayer));
        }

        private static Faction SelectDispatchingFaction(List<Faction> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            WraithFactionHunger hunger = Current.Game?.GetComponent<WraithFactionHunger>();
            if (hunger == null)
                return candidates.RandomElement();

            float total = 0f;
            foreach (Faction faction in candidates)
                total += 0.20f + 1.80f * hunger.GetStrategicHunger(faction);

            if (total <= 0f)
                return candidates.RandomElement();

            float roll = Rand.Value * total;
            foreach (Faction faction in candidates)
            {
                roll -= 0.20f + 1.80f * hunger.GetStrategicHunger(faction);
                if (roll <= 0f)
                    return faction;
            }

            return candidates[candidates.Count - 1];
        }
    }
}
