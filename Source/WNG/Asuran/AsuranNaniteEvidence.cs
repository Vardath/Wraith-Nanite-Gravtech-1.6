using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Technical identity gene shared by public and concealed human-form Asurans so the exact pawn
    /// can leave recoverable fabrication evidence on a physical map death. The gene itself does not
    /// decide allegiance: only the hostile Asuran Lattice, including a covert pawn whose persisted
    /// true faction is that Lattice, is an evidence source.
    /// </summary>
    public sealed class Gene_AsuranEvidenceResidue : Gene
    {
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            if (!IsHostileLatticeSource(pawn))
                return;

            Corpse corpse = pawn?.Corpse;
            if (corpse == null || !corpse.Spawned || corpse.Map == null)
                return;

            ThingDef residueDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AsuranNaniteResidue");
            if (residueDef == null)
                return;

            Thing residue = ThingMaker.MakeThing(residueDef);
            if (residue == null)
                return;

            if (!GenPlace.TryPlaceThing(residue, corpse.Position, corpse.Map, ThingPlaceMode.Near) && !residue.Destroyed)
                residue.Destroy(DestroyMode.Vanish);
        }

        private static bool IsHostileLatticeSource(Pawn source)
        {
            if (source == null)
                return false;

            if (source.Faction?.def?.defName == "WNG_AsuranLattice")
                return true;

            Hediff_AsuranInfiltration infiltration = AsuranInfiltrationUtility.State(source);
            return infiltration?.TrueFaction?.def?.defName == "WNG_AsuranLattice";
        }
    }
}
