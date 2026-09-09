using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public static class ReplicatorCrisisUtility
    {
        public static int CountHostileBlocks(Map map)
        {
            if (map?.mapPawns == null)
                return 0;

            return map.mapPawns.AllPawnsSpawned.Count(p =>
                p != null && !p.Dead && p.Spawned && p.Faction != null && p.Faction != Faction.OfPlayer
                && p.Faction.HostileTo(Faction.OfPlayer)
                && ReplicatorQueenUtility.IsBlockReplicator(p));
        }
    }

    /// <summary>
    /// Save-wide unresolved Replicator pressure. The value is intentionally bounded: world-scale
    /// failures can make a later outbreak slightly harder, but can never stack into exponential
    /// incident seeding. Future world-site/rescue systems can call RecordEscapedCrisis explicitly.
    /// </summary>
    public sealed class GameComponent_ReplicatorCrisisPressure : GameComponent
    {
        private int unresolvedPressure;

        public GameComponent_ReplicatorCrisisPressure(Game game)
        {
        }

        public int HostileOutbreakBonus => Math.Min(2, Math.Max(0, unresolvedPressure / 2));

        public void RecordEscapedCrisis(int amount = 1)
        {
            unresolvedPressure = Math.Min(6, unresolvedPressure + Math.Max(0, amount));
        }

        public void ResolvePressure(int amount = 1)
        {
            unresolvedPressure = Math.Max(0, unresolvedPressure - Math.Max(0, amount));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref unresolvedPressure, "wngReplicatorUnresolvedPressure", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                unresolvedPressure = Math.Max(0, Math.Min(6, unresolvedPressure));
        }
    }
}
