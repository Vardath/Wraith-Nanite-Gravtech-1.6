using System.Collections.Generic;
using Verse;

namespace WraithNaniteGravtech
{
    public enum WNGStargateRoofEmergenceOutcome
    {
        Clear,
        PunchThroughLightRoof,
        CatastrophicThickRoofObstruction
    }

    public readonly struct WNGStargateRoofEmergenceResult
    {
        public readonly WNGStargateRoofEmergenceOutcome outcome;
        public readonly int roofedCells;
        public readonly int thickRoofCells;

        public WNGStargateRoofEmergenceResult(WNGStargateRoofEmergenceOutcome outcome, int roofedCells, int thickRoofCells)
        {
            this.outcome = outcome;
            this.roofedCells = roofedCells;
            this.thickRoofCells = thickRoofCells;
        }
    }

    /// <summary>
    /// Evaluates roof obstruction over the exact cells through which an optional Stargate adapter
    /// says a craft is attempting to rematerialize/emerge. It does not guess gate geometry and it
    /// does not own CatCraft's receive buffer.
    /// </summary>
    public static class WNGStargateCraftEmergence
    {
        public static WNGStargateRoofEmergenceResult Evaluate(Map map, IEnumerable<IntVec3> emergenceCells)
        {
            if (map == null || emergenceCells == null)
                return new WNGStargateRoofEmergenceResult(WNGStargateRoofEmergenceOutcome.Clear, 0, 0);

            int roofed = 0;
            int thick = 0;

            foreach (IntVec3 cell in emergenceCells)
            {
                if (!cell.InBounds(map))
                    continue;

                RoofDef roof = map.roofGrid.RoofAt(cell);
                if (roof == null)
                    continue;

                roofed++;
                if (roof.isThickRoof)
                    thick++;
            }

            if (thick > 0)
                return new WNGStargateRoofEmergenceResult(WNGStargateRoofEmergenceOutcome.CatastrophicThickRoofObstruction, roofed, thick);
            if (roofed > 0)
                return new WNGStargateRoofEmergenceResult(WNGStargateRoofEmergenceOutcome.PunchThroughLightRoof, roofed, 0);
            return new WNGStargateRoofEmergenceResult(WNGStargateRoofEmergenceOutcome.Clear, 0, 0);
        }
    }
}
