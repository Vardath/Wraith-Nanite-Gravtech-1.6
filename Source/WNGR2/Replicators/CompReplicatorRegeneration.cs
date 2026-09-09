using System.Collections.Generic;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorRegeneration : CompProperties
    {
        public int intervalTicks = 600;
        public float healAmount = 0.20f;

        public CompProperties_ReplicatorRegeneration()
        {
            compClass = typeof(CompReplicatorRegeneration);
        }
    }

    public sealed class CompReplicatorRegeneration : ThingComp
    {
        private int nextHealTick;
        private CompProperties_ReplicatorRegeneration Props => (CompProperties_ReplicatorRegeneration)props;

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Downed || pawn.health == null)
                return;
            int now = Find.TickManager.TicksGame;
            if (now < nextHealTick)
                return;
            nextHealTick = now + Props.intervalTicks;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            Hediff_Injury best = null;
            float highestSeverity = 0f;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.Severity <= highestSeverity)
                    continue;
                best = injury;
                highestSeverity = injury.Severity;
            }
            best?.Heal(Props.healAmount);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextHealTick, "wngReplicatorNextHealTick", 0);
        }
    }
}
