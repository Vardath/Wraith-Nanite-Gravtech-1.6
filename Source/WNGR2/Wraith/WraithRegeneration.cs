using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Wraith tissue regeneration.  Recently fed Wraith heal substantially faster; an unfed Wraith
    /// still regenerates slowly while it has Life Force remaining.  The values are intentionally
    /// isolated here so live-test balance changes do not touch feeding or age-transfer logic.
    /// </summary>
    public sealed class Gene_WraithRegeneration : Gene
    {
        private int nextHealTick;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);

            if (!Active || pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextHealTick)
                return;
            nextHealTick = now + 250;

            Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(pawn);
            if (lifeForce == null || lifeForce.Value <= 0.001f)
                return;

            HediffDef fedDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_FedRecently");
            bool fedRecently = fedDef != null && pawn.health.hediffSet.HasHediff(fedDef);
            float healAmount = fedRecently ? 0.65f : 0.14f;

            Hediff_Injury injury = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.CanHealNaturally())
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();

            injury?.Heal(healAmount);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextHealTick, "wngWraithNextHealTick", 0);
        }
    }

    /// <summary>
    /// Gene-owned ability anchor.  RimWorld removes a gene's abilities when one copy is removed;
    /// if another active copy remains, this restores only the missing abilities.  This makes gene
    /// duplication/overwrite safe without a global Harmony patch.
    /// </summary>
    public sealed class Gene_WraithAbilityAnchor : Gene
    {
        private int nextReconcileTick;

        public override void PostAdd()
        {
            base.PostAdd();
            Reconcile();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn?.abilities == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextReconcileTick)
                return;
            nextReconcileTick = now + 300;
            Reconcile();
        }

        private void Reconcile()
        {
            if (!Active || pawn?.abilities == null || def?.abilities == null)
                return;

            foreach (AbilityDef abilityDef in def.abilities)
            {
                if (abilityDef != null && pawn.abilities.GetAbility(abilityDef) == null)
                    pawn.abilities.GainAbility(abilityDef);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextReconcileTick, "wngWraithAbilityReconcileTick", 0);
        }
    }
}
