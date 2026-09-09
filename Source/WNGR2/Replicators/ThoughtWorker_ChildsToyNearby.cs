using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class ThoughtWorker_ChildsToyNearby : ThoughtWorker
    {
        private const float RadiusSquared = 100f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || !p.Spawned || p.Map == null || p.Faction != Faction.OfPlayer || !p.RaceProps.Humanlike || p.needs?.mood == null)
                return ThoughtState.Inactive;

            foreach (Pawn other in p.Map.mapPawns.AllPawnsSpawned)
            {
                if (other == null || other.Dead || other.Faction != Faction.OfPlayer)
                    continue;
                if (other.def?.defName != "WNG_ChildsToy")
                    continue;
                if (!other.IsColonyMechPlayerControlled)
                    continue;
                if (p.Position.DistanceToSquared(other.Position) <= RadiusSquared)
                    return ThoughtState.ActiveAtStage(0);
            }

            return ThoughtState.Inactive;
        }
    }
}
