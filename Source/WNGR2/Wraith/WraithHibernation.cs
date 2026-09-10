using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityWraithHibernate : CompProperties_AbilityEffect
    {
        public HediffDef hibernatingHediff;

        public CompProperties_AbilityWraithHibernate()
        {
            compClass = typeof(CompAbilityEffect_WraithHibernate);
        }
    }

    /// <summary>
    /// Deliberate self-hibernation. The three-day duration and biological penalties live on the
    /// Hediff Def; Life Force itself reads that same Hediff to reduce drain and regeneration.
    /// </summary>
    public sealed class CompAbilityEffect_WraithHibernate : CompAbilityEffect
    {
        private CompProperties_AbilityWraithHibernate HibernateProps => (CompProperties_AbilityWraithHibernate)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            bool valid = caster != null
                && !caster.Dead
                && WraithLifeForceUtility.Get(caster) != null
                && HibernateProps.hibernatingHediff != null
                && caster.health?.hediffSet != null
                && !caster.health.hediffSet.HasHediff(HibernateProps.hibernatingHediff);

            if (!valid && throwMessages && caster != null)
            {
                Messages.Message(
                    "Only an active Wraith that is not already hibernating can enter hibernation.",
                    caster,
                    MessageTypeDefOf.RejectInput,
                    historical: false);
            }

            return valid && base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || caster.Dead || WraithLifeForceUtility.Get(caster) == null)
                return;
            if (caster.health?.hediffSet == null || HibernateProps.hibernatingHediff == null)
                return;
            if (caster.health.hediffSet.HasHediff(HibernateProps.hibernatingHediff))
                return;

            caster.health.AddHediff(HibernateProps.hibernatingHediff);
        }
    }
}
