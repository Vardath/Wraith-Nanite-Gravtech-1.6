using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Stargate traversal policy shared by WNG incidents/craft and an optional CatCraft adapter.
    /// CatCraft remains authoritative for actual address/network/dial/iris/shield/receive-buffer
    /// implementation. WNG only decides whether a proposed traversal is valid for its mission logic.
    /// </summary>
    public enum WNGStargateConnectionDirection
    {
        Inactive,
        OutboundFromLocalGate,
        InboundToLocalGate,
        UnknownActive
    }

    public enum WNGStargateArrivalBarrier
    {
        Open,
        IrisClosed,
        ShieldClosed,
        UnknownClosedBarrier
    }

    public enum WNGStargateTransitDecision
    {
        Allowed,
        MustRedialOutbound,
        IncomingMatterDestroyedBeforeRematerialization,
        Unusable
    }

    public static class WNGStargateTransitPolicy
    {
        /// <summary>
        /// A craft/pawn attempting to leave through the local gate may only enter an outbound
        /// wormhole initiated by that local gate. An active inbound wormhole is one-way and must
        /// shut down before a new outbound dial can be attempted.
        /// </summary>
        public static WNGStargateTransitDecision EvaluateLocalDeparture(WNGStargateConnectionDirection direction)
        {
            switch (direction)
            {
                case WNGStargateConnectionDirection.OutboundFromLocalGate:
                    return WNGStargateTransitDecision.Allowed;
                case WNGStargateConnectionDirection.InboundToLocalGate:
                case WNGStargateConnectionDirection.UnknownActive:
                    return WNGStargateTransitDecision.MustRedialOutbound;
                case WNGStargateConnectionDirection.Inactive:
                default:
                    return WNGStargateTransitDecision.MustRedialOutbound;
            }
        }

        /// <summary>
        /// Incoming matter reaching a receiving Stargate with its iris/shield closed is destroyed
        /// before successful rematerialization. The adapter should resolve the exact incoming pawns,
        /// shuttle and/or buffered contents through the normal gate-destruction outcome rather than
        /// spawning them on the destination map.
        /// </summary>
        public static WNGStargateTransitDecision EvaluateIncomingArrival(WNGStargateArrivalBarrier barrier)
        {
            switch (barrier)
            {
                case WNGStargateArrivalBarrier.Open:
                    return WNGStargateTransitDecision.Allowed;
                case WNGStargateArrivalBarrier.IrisClosed:
                case WNGStargateArrivalBarrier.ShieldClosed:
                case WNGStargateArrivalBarrier.UnknownClosedBarrier:
                    return WNGStargateTransitDecision.IncomingMatterDestroyedBeforeRematerialization;
                default:
                    return WNGStargateTransitDecision.Unusable;
            }
        }
    }

    /// <summary>
    /// Optional integration surface implemented by a CatCraft compatibility layer once the actual
    /// CatCraft API is present. This prevents WNG mission code from inventing or owning gate APIs.
    /// </summary>
    public interface IWNGStargateCraftBridge
    {
        WNGStargateConnectionDirection GetLocalConnectionDirection(Map map, IntVec3 gateCell);
        WNGStargateArrivalBarrier GetArrivalBarrier(Map map, IntVec3 gateCell);
        bool TryBeginOutboundDial(Thing craft, Map map, IntVec3 gateCell);
        bool CanCraftReachGate(Thing craft, Map map, IntVec3 gateCell);
    }
}
