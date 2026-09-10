using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Native PassengerShuttleLeaving with one narrow WNG hook: if the exact inner craft is a
    /// hostile Wraith Dart already committed to native fallback escape, finalize its exact captive
    /// transfer at the same boundary where RimWorld removes the leaving skyfaller from the map.
    /// All boarding, loading, launch targeting, fuel use and world-flight behavior remain vanilla.
    /// </summary>
    public sealed class WNGNativeShuttleLeaving : PassengerShuttleLeaving
    {
        protected override void LeaveMap()
        {
            Thing dart = innerContainer?.FirstOrDefault(t => t?.TryGetComp<CompWraithDartRaidMission>() != null);
            CompWraithDartRaidMission mission = dart?.TryGetComp<CompWraithDartRaidMission>();

            if (mission?.Phase == WNGShuttleRaidPhase.NativeEscapePending)
                mission.NotifyNativeEscapeCompleted();

            base.LeaveMap();
        }
    }
}
