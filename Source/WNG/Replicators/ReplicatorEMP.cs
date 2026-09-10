using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public static class ReplicatorEMP
    {
        public static bool IsSuppressed(Pawn pawn) => pawn?.TryGetComp<CompReplicatorEMP>()?.Suppressed == true;
    }

    public sealed class CompProperties_ReplicatorEMP : CompProperties
    {
        public int suppressionTicks = 1800;
        public CompProperties_ReplicatorEMP() => compClass = typeof(CompReplicatorEMP);
    }

    public sealed class CompReplicatorEMP : ThingComp
    {
        private int suppressedUntil;
        private CompProperties_ReplicatorEMP Props => (CompProperties_ReplicatorEMP)props;
        public bool Suppressed => (Find.TickManager?.TicksGame ?? 0) < suppressedUntil;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (dinfo.Def != DamageDefOf.EMP) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            int duration = Math.Max(60, Props.suppressionTicks);
            long until = (long)now + duration;
            suppressedUntil = Math.Max(suppressedUntil, until >= int.MaxValue ? int.MaxValue : (int)until);
        }

        public override string CompInspectStringExtra()
        {
            if (!Suppressed) return null;
            int remaining = Math.Max(0, suppressedUntil - (Find.TickManager?.TicksGame ?? 0));
            return $"Replication systems disrupted by EMP: {remaining / 2500f:0.0} hour(s)";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref suppressedUntil, "wngReplicatorEmpUntil", 0);
        }
    }
}
