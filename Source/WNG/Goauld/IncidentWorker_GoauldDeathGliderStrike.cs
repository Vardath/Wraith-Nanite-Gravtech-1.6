using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Optional ONAC/RimGate Biotech Death Glider strike. It binds only to the three exact verified
    /// System Lord factions and exact verified Jaffa warrior PawnKinds; no duplicate Goa'uld/Jaffa
    /// factions and no fuzzy HAR-era RimGate matching are introduced.
    /// </summary>
    public sealed class IncidentWorker_GoauldDeathGliderStrike : IncidentWorker
    {
        private static readonly string[] CrewKindDefNames =
        {
            "JKB_JaffaWarrior01",
            "JKB_JaffaWarrior02",
            "JKB_JaffaWarrior03"
        };

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map) || Faction.OfPlayer == null || !WNGOptionalIntegrations.GoauldJaffaIntegrationActive)
                return false;

            ThingDef gliderDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldDeathGlider");
            if (gliderDef == null)
                return false;

            if (map.listerThings.ThingsOfDef(gliderDef).Any(t =>
                    t != null && !t.Destroyed && t.Faction != null &&
                    WNGOptionalIntegrations.IsVerifiedRimGateSystemLordFaction(t.Faction)))
                return false;

            if (!map.mapPawns.FreeColonistsSpawned.Any(p => p != null && !p.Dead))
                return false;

            if (VerifiedCrewKinds().Count == 0)
                return false;

            return HostileSystemLordFactions(parms.faction).Any() && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map) || !WNGOptionalIntegrations.GoauldJaffaIntegrationActive)
                return false;

            ThingDef gliderDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldDeathGlider");
            if (gliderDef == null)
                return false;

            List<Faction> factions = HostileSystemLordFactions(parms.faction).ToList();
            List<PawnKindDef> crewKinds = VerifiedCrewKinds();
            if (factions.Count == 0 || crewKinds.Count == 0)
                return false;

            if (!CellFinder.TryFindRandomEdgeCellWith(
                    cell => !cell.Fogged(map) && GenSpawn.CanSpawnAt(gliderDef, cell, map),
                    map,
                    CellFinder.EdgeRoadChance_Hostile,
                    out IntVec3 entryCell))
                return false;

            Faction faction = parms.faction != null && factions.Contains(parms.faction)
                ? parms.faction
                : factions.RandomElement();
            parms.faction = faction;

            Thing glider = ThingMaker.MakeThing(gliderDef);
            if (glider == null)
                return false;

            glider.SetFaction(faction);
            GenSpawn.Spawn(glider, entryCell, map);

            CompTransporter transporter = glider.TryGetComp<CompTransporter>();
            CompGoauldDeathGliderMission mission = glider.TryGetComp<CompGoauldDeathGliderMission>();
            if (transporter == null || mission == null)
            {
                Log.Error("[WNG] Death Glider strike spawned a craft without its native transporter/mission comp.");
                if (!glider.Destroyed)
                    glider.Destroy(DestroyMode.Vanish);
                return false;
            }

            const int requiredCrew = 2;
            for (int i = 0; i < requiredCrew; i++)
            {
                PawnKindDef kind = crewKinds.RandomElement();
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                if (pawn == null || !transporter.innerContainer.TryAdd(pawn))
                {
                    if (pawn != null && !pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                    transporter.innerContainer.ClearAndDestroyContents();
                    if (!glider.Destroyed)
                        glider.Destroy(DestroyMode.Vanish);
                    Log.Error("[WNG] Could not load the exact Jaffa crew for a Death Glider strike.");
                    return false;
                }

                transporter.Notify_ThingAdded(pawn);
            }

            bool launched = mission.TryBeginCombatSortie(departWhenComplete: true, showFailureMessage: false);
            if (!launched)
                Log.Warning("[WNG] A hostile Death Glider could not begin its physical strike and remains landed at the map edge with its exact crew.");

            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, new TargetInfo(entryCell, map));
            return true;
        }

        private static IEnumerable<Faction> HostileSystemLordFactions(Faction requested)
        {
            if (Find.FactionManager?.AllFactionsListForReading == null || Faction.OfPlayer == null)
                return Enumerable.Empty<Faction>();

            HashSet<FactionDef> verifiedDefs = new HashSet<FactionDef>(WNGOptionalIntegrations.ResolveRimGateSystemLordFactionDefs());
            IEnumerable<Faction> factions = Find.FactionManager.AllFactionsListForReading.Where(f =>
                f != null &&
                !f.defeated &&
                verifiedDefs.Contains(f.def) &&
                f.HostileTo(Faction.OfPlayer));

            if (requested != null)
            {
                if (!factions.Contains(requested))
                    return Enumerable.Empty<Faction>();
                return new[] { requested };
            }

            return factions;
        }

        private static List<PawnKindDef> VerifiedCrewKinds()
        {
            if (!WNGOptionalIntegrations.RequiredRimGateBiotechActive)
                return new List<PawnKindDef>();

            List<PawnKindDef> result = new List<PawnKindDef>();
            foreach (string defName in CrewKindDefNames)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
                if (kind?.modContentPack == null)
                    continue;

                if (!string.Equals(
                        kind.modContentPack.PackageIdPlayerFacing,
                        WNGOptionalIntegrations.RimGateBiotechPackageId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                result.Add(kind);
            }

            return result;
        }
    }
}
