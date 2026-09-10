using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Native PassengerShuttleLeaving with one narrow WNG hook. RimWorld's CompLaunchable and
    /// ShipJob_FlyAway wrap shuttle contents in an ActiveTransporter, then store the exact shuttle
    /// through ActiveTransporterInfo.SetShuttle. At the real LeaveMap boundary we recover that same
    /// craft and finalize WNG Dart captivity from the actual transit container.
    /// </summary>
    public sealed class WNGNativeShuttleLeaving : PassengerShuttleLeaving
    {
        protected override void LeaveMap()
        {
            ActiveTransporter active = innerContainer?.OfType<ActiveTransporter>().FirstOrDefault();
            Thing shuttle = active?.Contents?.GetShuttle();
            CompWraithDartRaidMission mission = shuttle?.TryGetComp<CompWraithDartRaidMission>();

            if (mission?.Phase == WNGShuttleRaidPhase.NativeEscapePending)
                mission.NotifyNativeEscapeCompleted(active?.Contents?.innerContainer);

            base.LeaveMap();
        }
    }
}
