using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Native PassengerShuttleLeaving with narrow WNG hooks. RimWorld's CompLaunchable and
    /// ShipJob_FlyAway wrap shuttle contents in an ActiveTransporter, then store the exact shuttle
    /// through ActiveTransporterInfo.SetShuttle. At the real LeaveMap boundary WNG may finalize
    /// exact-pawn state that is only valid once that same physical shuttle is genuinely departing.
    /// </summary>
    public sealed class WNGNativeShuttleLeaving : PassengerShuttleLeaving
    {
        protected override void LeaveMap()
        {
            ActiveTransporter active = innerContainer?.OfType<ActiveTransporter>().FirstOrDefault();
            Thing shuttle = active?.Contents?.GetShuttle();

            CompAsuranQueenRecoveryMission asuranRecovery =
                shuttle?.TryGetComp<CompAsuranQueenRecoveryMission>();
            if (asuranRecovery?.Phase == AsuranQueenRecoveryPhase.NativeEscapePending)
            {
                // Queen capture is not committed by stun, carry or loading. The exact Queen must
                // still be present in this exact transit container at RimWorld's real map-leave
                // boundary. If that commit cannot be proven, do not consume the departure.
                if (!asuranRecovery.NotifyNativeEscapeCompleted(active?.Contents?.innerContainer))
                {
                    Log.Error("[WNG] Asuran recovery Jumper reached LeaveMap without a provable exact-Queen capture; departure was halted rather than faking/losing the Queen.");
                    return;
                }
            }

            CompWraithDartRaidMission wraithMission = shuttle?.TryGetComp<CompWraithDartRaidMission>();
            if (wraithMission?.Phase == WNGShuttleRaidPhase.NativeEscapePending)
                wraithMission.NotifyNativeEscapeCompleted(active?.Contents?.innerContainer);

            base.LeaveMap();
        }
    }
}
