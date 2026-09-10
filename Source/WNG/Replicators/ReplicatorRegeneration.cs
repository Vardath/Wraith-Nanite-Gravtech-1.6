using System.Linq;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorRegeneration : CompProperties
    {
        public int intervalTicks = 600;
        public float healAmount = 0.2f;
        public CompProperties_ReplicatorRegeneration() => compClass = typeof(CompReplicatorRegeneration);
    }

    public sealed class CompReplicatorRegeneration : ThingComp
    {
        private int nextHealTick;
        private CompProperties_ReplicatorRegeneration Props => (CompProperties_ReplicatorRegeneration)props;

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || ReplicatorEMP.IsSuppressed(pawn)) return;
            int now = Find.TickManager.TicksGame;
            if (now < nextHealTick) return;
            nextHealTick = now + System.Math.Max(60, Props.intervalTicks);
            Hediff_Injury injury = pawn.health?.hediffSet?.hediffs?.OfType<Hediff_Injury>()
                .Where(x => x.Severity > 0f)
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();
            injury?.Heal(System.Math.Max(0f, Props.healAmount));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextHealTick, "wngReplicatorNextHeal", 0);
        }
    }
}
