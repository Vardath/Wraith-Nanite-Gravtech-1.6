using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class IncidentWorker_ReplicatorOutbreak : IncidentWorker
    {
        private const int InitialReplicators = 3;
        private const int SuppressIncidentAboveCount = 24;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!WNG_Config.ReplicatorOutbreaksEnabled) return false;
            if (!(parms.target is Map map)) return false;
            ThingDef raceDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (raceDef == null) return false;
            int current = ReplicatorCrisisUtility.CountHostileBlocks(map);
            return current < SuppressIncidentAboveCount && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!WNG_Config.ReplicatorOutbreaksEnabled || !(parms.target is Map map)) return false;
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            if (kind == null || factionDef == null) return false;
            Faction faction = Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null) return false;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entryCell, map, 0f))
                entryCell = CellFinder.RandomEdgeCell(map);
            GameComponent_ReplicatorQueenState queenState = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();
            GameComponent_ReplicatorCrisisPressure crisis = Current.Game?.GetComponent<GameComponent_ReplicatorCrisisPressure>();
            int count = InitialReplicators
                + (queenState?.HostileOutbreakBonus ?? 0)
                + (crisis?.HostileOutbreakBonus ?? 0);
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(entryCell, map, 8);
                GenSpawn.Spawn(pawn, cell, map);
            }
            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, new TargetInfo(entryCell, map));
            return true;
        }
    }
}
