using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Tunable hostile-Lattice use of the Asuran subspace link. This is a temporary controller
    /// hijack, not innate Queen sovereignty and not a permanent faction conversion.
    /// </summary>
    public sealed class AsuranLatticeLinkExtension : DefModExtension
    {
        public float intrusionRange = 18f;
        public int intrusionDurationTicks = 2500;
        public int maxIntrudedBlocks = 3;
        public int aiCheckIntervalTicks = 600;
        public float aiAttemptChance = 0.65f;
    }

    public sealed class Gene_AsuranLatticeLink : Gene
    {
        private int nextIntrusionTick;

        private AsuranLatticeLinkExtension Extension => def?.GetModExtension<AsuranLatticeLinkExtension>();

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || pawn.Downed)
                return;

            // Only the hostile operational Asuran Lattice uses this automatically. A recruited or
            // future player-aligned nanite humanoid keeps the gene without silently stealing blocks.
            if (pawn.Faction?.def?.defName != "WNG_AsuranLattice")
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextIntrusionTick)
                return;
            nextIntrusionTick = now + Math.Max(60, Extension?.aiCheckIntervalTicks ?? 600);

            if (ReplicatorSovereigntyUtility.IsAsuranSignalDisrupted(pawn))
                return;
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
                return;

            float chance = Mathf.Clamp01(Extension?.aiAttemptChance ?? 0.65f);
            if (chance < 1f && !Rand.Chance(chance))
                return;

            float range = Math.Max(1f, Extension?.intrusionRange ?? 18f);
            float rangeSq = range * range;
            int duration = Math.Max(60, Extension?.intrusionDurationTicks ?? 2500);
            int cap = Math.Max(1, Extension?.maxIntrudedBlocks ?? 3);

            foreach (Pawn target in pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned)
                .Where(ReplicatorSovereigntyUtility.IsBlockReplicator)
                .Where(p => p.Position.DistanceToSquared(pawn.Position) <= rangeSq)
                .OrderByDescending(IntrusionPriority)
                .ThenBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber))
            {
                bool wasPlayerAligned = target.Faction == Faction.OfPlayer;
                if (!ReplicatorSovereigntyUtility.TryAcquireForTemporaryAsuran(
                        pawn,
                        target,
                        range,
                        cap,
                        duration,
                        out _))
                    continue;

                if (wasPlayerAligned)
                {
                    Messages.Message(
                        $"{pawn.LabelShort} temporarily breached {target.LabelShort}'s Replicator control lattice.",
                        target,
                        MessageTypeDefOf.ThreatSmall,
                        historical: false);
                }
                break;
            }
        }

        private static int IntrusionPriority(Pawn target)
        {
            if (target?.Faction == Faction.OfPlayer)
                return 3;
            CompReplicatorSovereignty sovereignty = target?.TryGetComp<CompReplicatorSovereignty>();
            if (sovereignty?.HasAuthority == true && sovereignty.Authority != ReplicatorControlAuthority.TemporaryAsuran)
                return 2;
            return 1;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextIntrusionTick, "wngAsuranLatticeNextIntrusionTick", 0);
        }
    }
}
