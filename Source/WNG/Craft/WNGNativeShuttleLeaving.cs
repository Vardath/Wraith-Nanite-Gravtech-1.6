using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Native PassengerShuttleLeaving with one narrow WNG hook. RimWorld's CompLaunchable wraps
    /// shuttle contents in an ActiveTransporter, then stores the exact shuttle through
    /// ActiveTransporterInfo.SetShuttle. At the real LeaveMap boundary we recover that same craft
    /// and finalize WNG Dart captivity only when it is actually performing native fallback escape.
    /// Boarding, loading, fuel use, destination selection and world-flight behavior remain native.
    /// </summary>
    public sealed class WNGNativeShuttleLeaving : PassengerShuttleLeaving
    {
        protected override void LeaveMap()
        {
            ActiveTransporter active = innerContainer?.OfType<ActiveTransporter>().FirstOrDefault();
            Thing shuttle = active?.Contents?.GetShuttle();
            CompWraithDartRaidMission mission = shuttle?.TryGetComp<CompWraithDartRaidMission>();

            if (mission?.Phase == WNGShuttleRaidPhase.NativeEscapePending)
                mission.NotifyNativeEscapeCompleted();

            base.LeaveMap();
        }
    }
}
