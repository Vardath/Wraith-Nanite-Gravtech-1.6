using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Narrow implant identity helpers. The Sovereign Neural Lattice grants the targeted directive
    /// authority that explicitly checks for it; it does not silently turn its bearer into a Queen
    /// or broaden passive Replicator control.
    /// </summary>
    public static class WNGImplantUtility
    {
        public static bool HasSovereignNeuralLattice(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return false;

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_SovereignNeuralLattice");
            return def != null && pawn.health.hediffSet.HasHediff(def);
        }
    }
}
