using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Independent Wraith Dart culling incident. This is deliberately not tied to Stargate
    /// availability: a hostile Wraith faction may dispatch a Dart directly when biological prey
    /// is present. Only one active Dart is allowed per map from this incident at a time.
    /// </summary>
    public sealed class IncidentWorker_WraithDartCulling : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map))
                return false;

            ThingDef dartDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithDart");
            if (dartDef == null)
                return false;

            if (map.listerThings.ThingsOfDef(dartDef).Any(t => t != null && !t.Destroyed))
                return false;

            if (!map.mapPawns.AllPawnsSpawned.Any(WraithCaptureUtility.IsValidAbductionTarget))
                return false;

            if (!HostileWraithFactions().Any())
                return false;

            return base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map))
                return false;

            ThingDef dartDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_WraithDart");
            if (dartDef == null)
                return false;

            if (map.listerThings.ThingsOfDef(dartDef).Any(t => t != null && !t.Destroyed))
                return false;

            if (!map.mapPawns.AllPawnsSpawned.Any(WraithCaptureUtility.IsValidAbductionTarget))
                return false;

            List<Faction> candidates = HostileWraithFactions().ToList();
            if (candidates.Count == 0)
                return false;

            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => !c.Fogged(map) && GenSpawn.CanSpawnAt(dartDef, c, map),
                    map,
                    CellFinder.EdgeRoadChance_Hostile,
                    out IntVec3 entryCell))
                return false;

            Faction faction = candidates.RandomElement();
            Thing dart = ThingMaker.MakeThing(dartDef);
            if (dart == null)
                return false;

            dart.SetFaction(faction);
            GenSpawn.Spawn(dart, entryCell, map);
            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, new TargetInfo(entryCell, map));
            return true;
        }

        private static IEnumerable<Faction> HostileWraithFactions()
        {
            if (Find.FactionManager?.AllFactionsListForReading == null)
                return Enumerable.Empty<Faction>();

            return Find.FactionManager.AllFactionsListForReading.Where(f =>
                f != null
                && !f.defeated
                && WraithCaptureUtility.IsWraithCaptor(f)
                && f.HostileTo(Faction.OfPlayer));
        }
    }
}
