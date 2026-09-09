using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    /// <summary>
    /// Central safety rule used by autonomous Replicator consumption jobs.
    /// Player-owned Replicators may be directed by the player, but their autonomous hunger logic
    /// is never allowed to consume the player's home colony behind the player's back.
    /// </summary>
    public static class ReplicatorConsumptionUtility
    {
        public static bool CanAutonomouslyConsume(Pawn replicator, Thing target)
        {
            if (replicator == null || target == null || target.Destroyed || target == replicator)
                return false;

            if (replicator.Faction == Faction.OfPlayer && target.Map?.IsPlayerHome == true)
                return false;

            if (target.Faction != null && target.Faction == replicator.Faction)
                return false;

            return true;
        }

        public static bool BiologicalFallbackAllowed(Pawn replicator, Pawn biologicalTarget, bool reachableMaterialExists)
        {
            if (reachableMaterialExists)
                return false;

            if (biologicalTarget?.RaceProps?.IsFlesh != true)
                return false;

            return CanAutonomouslyConsume(replicator, biologicalTarget);
        }
    }
}
