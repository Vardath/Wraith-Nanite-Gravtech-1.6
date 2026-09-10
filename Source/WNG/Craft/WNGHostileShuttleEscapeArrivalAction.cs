using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Terminal world-arrival action for hostile shuttle withdrawals. The actual map departure and
    /// world flight are still RimWorld TransportShip/FlyShipLeaving behavior. WNG only consumes the
    /// off-map hostile craft/crew at the far end so a retreating raid shuttle does not create an
    /// unrelated player-visible destination encounter.
    ///
    /// Exact Dart culling captives are removed from ActiveTransporterInfo at the real leaving-map
    /// boundary and passed to WNG's persistent captivity registry before this action is reached.
    /// </summary>
    public sealed class WNGHostileShuttleEscapeArrivalAction : TransportersArrivalAction
    {
        public override bool GeneratesMap => false;

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            if (transporters == null)
                return;

            foreach (ActiveTransporterInfo info in transporters.Where(x => x != null))
            {
                Thing shuttle = info.GetShuttle();
                if (shuttle != null)
                {
                    info.RemoveShuttle();
                    if (!shuttle.Destroyed)
                        shuttle.Destroy(DestroyMode.Vanish);
                }

                if (info.innerContainer == null)
                    continue;

                foreach (Pawn pawn in info.innerContainer.OfType<Pawn>().ToList())
                {
                    info.innerContainer.Remove(pawn);
                    if (!pawn.Dead && !pawn.IsWorldPawn())
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                }

                info.innerContainer.ClearAndDestroyContents();
            }
        }
    }
}
