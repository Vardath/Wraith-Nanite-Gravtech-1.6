using System.Linq;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Provisional reconstruction-microforge tuning for D108. The implant repairs only ordinary
    /// non-permanent injuries. It does not remove scars, restore missing parts, resurrect pawns,
    /// or replace the stronger native human-form Replicator reconstruction system.
    /// </summary>
    public sealed class Hediff_ReconstructionMicroforge : HediffWithComps
    {
        private const int RepairIntervalTicks = 2500;
        private const float HealPerPulse = 0.75f;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null || delta <= 0)
                return;
            if (!pawn.IsHashIntervalTick(RepairIntervalTicks, delta))
                return;

            Hediff_Injury injury = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x != null && !x.IsPermanent() && x.Severity > 0f)
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();

            injury?.Heal(HealPerPulse);
        }
    }
}
