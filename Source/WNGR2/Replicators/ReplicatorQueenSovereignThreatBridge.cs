using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Gives the hostile Lattice Collective a bounded, visible gameplay consequence after it has
    /// actually escaped with the unique Replicator Queen.  This does not create another raid timer.
    /// Instead, an ordinary future Lattice assault on a player home map can receive a small block-
    /// Replicator escort under the same Lord/faction.  Each assault Lord is augmented at most once.
    /// </summary>
    public sealed class MapComponent_ReplicatorQueenSovereignThreatBridge : MapComponent
    {
        private const int ScanIntervalTicks = 120;
        private const int MaximumEscortsPerAssault = 3;
        private const int HumanFormsPerEscort = 4;
        private const int MaximumRememberedLordIds = 64;

        private int nextScanTick;
        private List<string> processedLordIds = new List<string>();

        public MapComponent_ReplicatorQueenSovereignThreatBridge(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (map == null || !map.IsPlayerHome || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextScanTick)
                return;
            nextScanTick = now + ScanIntervalTicks;

            GameComponent_ReplicatorQueenState queenState = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();
            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            if (queenState == null || lattice == null || !queenState.FactionHasCapturedQueenAuthority(lattice))
                return;

            List<Pawn> latticeHumans = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null
                    && !pawn.Dead
                    && pawn.Faction == lattice
                    && pawn.RaceProps.Humanlike
                    && ReplicatorQueenUtility.IsHumanFormReplicator(pawn))
                .ToList();
            if (latticeHumans.Count == 0)
                return;

            foreach (IGrouping<Lord, Pawn> group in latticeHumans
                         .Select(pawn => new { Pawn = pawn, Lord = pawn.GetLord() })
                         .Where(entry => entry.Lord != null && entry.Lord.LordJob is LordJob_AssaultColony)
                         .GroupBy(entry => entry.Lord, entry => entry.Pawn)
                         .ToList())
            {
                Lord lord = group.Key;
                string lordId = lord.GetUniqueLoadID();
                if (lordId.NullOrEmpty() || processedLordIds.Contains(lordId))
                    continue;

                List<Pawn> humanAssault = group.Where(pawn => pawn.Spawned && pawn.Map == map).ToList();
                if (humanAssault.Count == 0)
                    continue;

                int escortCount = Math.Min(
                    MaximumEscortsPerAssault,
                    Math.Max(1, (humanAssault.Count + HumanFormsPerEscort - 1) / HumanFormsPerEscort));
                int spawned = SpawnSovereignEscorts(lattice, lord, humanAssault, escortCount);

                // A Lord is processed once whether its bounded support successfully spawned or a
                // particular map position/Def prevented it.  This prevents a failed placement from
                // becoming an accidental infinite reinforcement loop.
                RememberProcessedLord(lordId);

                if (spawned > 0)
                {
                    Messages.Message(
                        $"The Lattice assault is using the captured Replicator Queen's sovereign link: {spawned} block Replicator escort{(spawned == 1 ? string.Empty : "s")} entered under direct Lattice control.",
                        MessageTypeDefOf.ThreatSmall,
                        historical: true);
                }
            }
        }

        private int SpawnSovereignEscorts(Faction lattice, Lord lord, List<Pawn> humanAssault, int escortCount)
        {
            if (lattice == null || lord == null || humanAssault == null || humanAssault.Count == 0 || escortCount <= 0)
                return 0;

            Pawn anchor = humanAssault.OrderBy(pawn => pawn.thingIDNumber).First();
            int spawned = 0;
            for (int i = 0; i < escortCount; i++)
            {
                PawnKindDef kind = ResolveEscortKind(humanAssault.Count, i);
                if (kind == null)
                    continue;

                Pawn escort = null;
                try
                {
                    escort = PawnGenerator.GeneratePawn(kind, lattice, map.Tile);
                    if (escort == null)
                        continue;

                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(anchor.Position, map, 6);
                    GenSpawn.Spawn(escort, cell, map);
                    if (!escort.Spawned || escort.Map != map)
                        throw new InvalidOperationException("sovereign block escort did not spawn on the Lattice assault map");

                    lord.AddPawn(escort);
                    spawned++;
                }
                catch (Exception ex)
                {
                    if (escort != null && !escort.Destroyed)
                    {
                        try { escort.Destroy(DestroyMode.Vanish); } catch { }
                    }
                    Log.Warning("[WNG] Captured-Queen sovereign escort could not be added to a Lattice assault: " + ex.Message);
                }
            }

            return spawned;
        }

        private static PawnKindDef ResolveEscortKind(int humanCount, int escortIndex)
        {
            string defName;
            if (escortIndex == 0 && humanCount >= 12)
                defName = "WNG_ReplicatorBulwark";
            else if (escortIndex == 0 && humanCount >= 6)
                defName = "WNG_ReplicatorHunter";
            else
                defName = "WNG_ReplicatorDrone";
            return DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
        }

        private void RememberProcessedLord(string lordId)
        {
            if (lordId.NullOrEmpty() || processedLordIds.Contains(lordId))
                return;
            processedLordIds.Add(lordId);
            while (processedLordIds.Count > MaximumRememberedLordIds)
                processedLordIds.RemoveAt(0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextScanTick, "wngQueenSovereignThreatNextScanTick", 0);
            Scribe_Collections.Look(ref processedLordIds, "wngQueenSovereignProcessedLords", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && processedLordIds == null)
                processedLordIds = new List<string>();
        }
    }
}
