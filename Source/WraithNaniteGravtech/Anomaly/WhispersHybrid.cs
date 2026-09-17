using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech.Anomaly
{
    /// <summary>
    /// Early Michael hybrid from SGA "Whispers": blind, sound-hunting and continuously
    /// exuding obscuring mist from neck gills. Native BlindSmoke is used as the mechanical
    /// fog because it creates the intended close-range predator advantage without inventing
    /// a toxic effect the episode never established.
    /// </summary>
    public sealed class Gene_WhispersPredator : Gene
    {
        private const string SensoryHediffDefName = "WNG_WhispersBlindHunter";
        private const int FogIntervalTicks = 120;

        public override void PostAdd()
        {
            base.PostAdd();
            EnsureSensoryState();
        }

        public override void PostRemove()
        {
            RemoveSensoryState();
            base.PostRemove();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead)
                return;

            if (pawn.IsHashIntervalTick(600, delta))
                EnsureSensoryState();

            if (pawn.Spawned && pawn.Map != null && pawn.IsHashIntervalTick(FogIntervalTicks, delta))
                EmitPredatoryFog();
        }

        private void EnsureSensoryState()
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead)
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(SensoryHediffDefName);
            if (def != null && !pawn.health.hediffSet.HasHediff(def))
                pawn.health.AddHediff(def);
        }

        private void RemoveSensoryState()
        {
            if (pawn?.health?.hediffSet == null)
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(SensoryHediffDefName);
            Hediff hediff = def == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff != null)
                pawn.health.RemoveHediff(hediff);
        }

        private void EmitPredatoryFog()
        {
            Map map = pawn.Map;
            if (map?.gasGrid == null)
                return;

            IntVec3 origin = pawn.Position;
            IntVec3[] cells = GenAdj.AdjacentCellsAndInside;
            for (int i = 0; i < cells.Length; i++)
            {
                IntVec3 cell = origin + cells[i];
                if (!cell.InBounds(map))
                    continue;
                map.gasGrid.AddGas(cell, GasType.BlindSmoke, i == cells.Length - 1 ? 30 : 16);
            }
        }
    }

    public sealed class IncidentWorker_WhispersHybridEscape : IncidentWorker
    {
        private const string FactionDefName = "WNG_MichaelsExperiments";
        private const string PawnKindDefName = "WNG_WhispersHybrid";

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(PawnKindDefName);
            return map != null && map.IsPlayerHome && factionDef != null && kind != null &&
                   Find.FactionManager.FirstFactionOfDef(factionDef) != null &&
                   map.mapPawns.FreeColonistsSpawned.Count > 0 && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            if (map == null)
                return false;

            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(PawnKindDefName);
            Faction faction = factionDef == null ? null : Find.FactionManager.FirstFactionOfDef(factionDef);
            if (faction == null || kind == null)
                return false;

            IntVec3 entry;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out entry, map, CellFinder.EdgeRoadChance_Hostile))
                return false;

            int count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(300f, parms.points) / 160f), 2, 8);
            List<Pawn> pawns = new List<Pawn>(count);
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                if (pawn == null)
                    continue;
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(entry, map, 8);
                GenSpawn.Spawn(pawn, cell, map, Rot4.FromAngleFlat((map.Center - cell).AngleFlat));
                pawns.Add(pawn);
            }

            if (pawns.Count == 0)
                return false;

            parms.faction = faction;
            LordMaker.MakeNewLord(
                faction,
                new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: false, canSteal: false),
                map,
                pawns);

            SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, pawns[0]);
            Find.TickManager.slower.SignalForceNormalSpeedShort();
            return true;
        }
    }
}
