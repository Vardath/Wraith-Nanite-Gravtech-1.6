using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    public sealed class CompProperties_ReplicatorSuppression : CompProperties
    {
        public int empSuppressionTicks = 2500;

        public CompProperties_ReplicatorSuppression()
        {
            compClass = typeof(CompReplicatorSuppression);
        }
    }

    /// <summary>
    /// Local EMP response for Replicator things and pawns. No global damage patch is required.
    /// EMP does not get absorbed here; it temporarily suppresses WNG autonomous lattice behavior.
    /// </summary>
    public sealed class CompReplicatorSuppression : ThingComp
    {
        private int suppressedUntilTick;

        public CompProperties_ReplicatorSuppression Props => (CompProperties_ReplicatorSuppression)props;
        public bool IsSuppressed => CurrentTick < suppressedUntilTick;
        public int SuppressedUntilTick => suppressedUntilTick;

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            if (dinfo.Def == DamageDefOf.EMP)
                SuppressForTicks(Props.empSuppressionTicks);
        }

        public void SuppressForTicks(int ticks)
        {
            int until = CurrentTick + System.Math.Max(0, ticks);
            if (until > suppressedUntilTick)
                suppressedUntilTick = until;

            if (parent is Pawn pawn)
                pawn.TryGetComp<CompReplicatorHierarchy>()?.BlockRecombinationForTicks(ticks);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref suppressedUntilTick, "suppressedUntilTick", 0);
        }
    }
}
