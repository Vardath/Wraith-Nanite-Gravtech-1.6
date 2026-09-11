using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityTemporaryAsuranIntrusion : CompProperties_AbilityEffect
    {
        public int durationTicks = 2500;
        public int maxIntrudedBlocks = 2;

        public CompProperties_AbilityTemporaryAsuranIntrusion()
        {
            compClass = typeof(CompAbilityEffect_TemporaryAsuranIntrusion);
        }
    }

    /// <summary>
    /// Bounded Asuran field intrusion into the existing Replicator sovereignty network. This never
    /// grants Queen identity: the exact target block temporarily enters a TemporaryAsuran domain,
    /// while CompReplicatorSovereignty preserves the exact pre-intrusion domain for restoration.
    /// </summary>
    public sealed class CompAbilityEffect_TemporaryAsuranIntrusion : CompAbilityEffect
    {
        private CompProperties_AbilityTemporaryAsuranIntrusion IntrusionProps =>
            (CompProperties_AbilityTemporaryAsuranIntrusion)props;

        private float EffectiveRange
        {
            get
            {
                if (parent?.verb != null)
                    return Math.Max(1f, parent.verb.EffectiveRange);
                return Math.Max(1f, parent?.def?.verbProperties?.range ?? 20f);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Pawn block = target.Pawn;
            if (!ReplicatorSovereigntyUtility.CanAcquireForTemporaryAsuran(
                    caster,
                    block,
                    EffectiveRange,
                    Math.Max(1, IntrusionProps.maxIntrudedBlocks),
                    out string rejection))
                return Reject(rejection, throwMessages);

            return base.Valid(target, throwMessages);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return ReplicatorSovereigntyUtility.CanAcquireForTemporaryAsuran(
                parent?.pawn,
                target.Pawn,
                EffectiveRange,
                Math.Max(1, IntrusionProps.maxIntrudedBlocks),
                out _);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = parent?.pawn;
            Pawn block = target.Pawn;
            if (caster == null || block == null)
                return;

            CompReplicatorSovereignty before = block.TryGetComp<CompReplicatorSovereignty>();
            bool playerAffected = block.Faction == Faction.OfPlayer || before?.Controller?.Faction == Faction.OfPlayer;

            if (!ReplicatorSovereigntyUtility.TryAcquireForTemporaryAsuran(
                    caster,
                    block,
                    EffectiveRange,
                    Math.Max(1, IntrusionProps.maxIntrudedBlocks),
                    Math.Max(60, IntrusionProps.durationTicks),
                    out string rejection))
            {
                if (caster.Faction == Faction.OfPlayer && !rejection.NullOrEmpty())
                    Messages.Message(rejection, caster, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (caster.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{caster.LabelShort} temporarily intruded {block.LabelShort}'s Replicator control lattice.",
                    block,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
            else if (playerAffected)
            {
                Messages.Message(
                    $"{caster.LabelShort} temporarily hijacked {block.LabelShort}'s Replicator control lattice!",
                    block,
                    MessageTypeDefOf.ThreatSmall,
                    historical: true);
            }
        }

        private bool Reject(string reason, bool throwMessages)
        {
            if (throwMessages && parent?.pawn != null && !reason.NullOrEmpty())
                Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
    }
}
