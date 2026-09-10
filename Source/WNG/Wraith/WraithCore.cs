using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class Gene_Resource_LifeForce : Gene_Resource, IGeneResourceDrain
    {
        public Gene_Resource Resource => this;
        public Pawn Pawn => pawn;
        public bool CanOffset => Active && pawn != null && !pawn.Dead && !pawn.Suspended;
        public string DisplayLabel => Label + " (gene)";
        public float ResourceLossPerDay => def.resourceLossPerDay * (IsHibernating ? HibernationDrainFactor : 1f);

        public override float InitialResourceMax => 1f;
        public override float MinLevelForAlert => StarvedThreshold;
        public override float MaxLevelOffset => 0.05f;
        protected override Color BarColor => new Color(0.13f, 0.52f, 0.43f);
        protected override Color BarHighlightColor => new Color(0.28f, 0.72f, 0.60f);

        private float StarvedThreshold => Math.Max(0f, DefModExtension?.starvedThreshold ?? 0.15f);
        private float TorporThreshold => Math.Max(0f, DefModExtension?.torporThreshold ?? 0.001f);
        private float RecoverFromTorporThreshold => Math.Max(TorporThreshold, DefModExtension?.recoverFromTorporThreshold ?? 0.08f);
        private float HibernationDrainFactor => Math.Max(0f, DefModExtension?.hibernationDrainFactor ?? 0.02f);
        private WraithLifeForceExtension DefModExtension => def?.GetModExtension<WraithLifeForceExtension>();

        public bool IsHibernating
        {
            get
            {
                HediffDef hibernatingDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
                return hibernatingDef != null && pawn?.health?.hediffSet?.HasHediff(hibernatingDef) == true;
            }
        }

        public float RegenerationFactor
        {
            get
            {
                WraithLifeForceExtension ext = DefModExtension;
                if (IsHibernating) return Math.Max(0f, ext?.hibernatingRegenerationFactor ?? 0.12f);
                if (Value <= TorporThreshold) return 0f;
                if (Value < StarvedThreshold) return Math.Max(0f, ext?.starvedRegenerationFactor ?? 0.25f);
                if (Value < 0.40f) return Math.Max(0f, ext?.lowRegenerationFactor ?? 0.60f);
                if (Value < 0.70f) return Math.Max(0f, ext?.normalRegenerationFactor ?? 1f);
                return Math.Max(0f, ext?.highRegenerationFactor ?? 1.60f);
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            GeneResourceDrainUtility.TickResourceDrainInterval(this, delta);
            if (pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(60, delta)) return;
            UpdateStarvationState();
        }

        public override void SetTargetValuePct(float val)
        {
            targetValue = Math.Max(0f, Math.Min(Max - MaxLevelOffset, val * Max));
        }

        private void UpdateStarvationState()
        {
            if (pawn?.health?.hediffSet == null) return;
            HediffDef starvedDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeForceStarved");
            HediffDef torporDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeForceTorpor");
            if (starvedDef == null || torporDef == null) return;

            Hediff starved = pawn.health.hediffSet.GetFirstHediffOfDef(starvedDef);
            Hediff torpor = pawn.health.hediffSet.GetFirstHediffOfDef(torporDef);

            if (IsHibernating)
            {
                if (starved != null) pawn.health.RemoveHediff(starved);
                if (torpor != null) pawn.health.RemoveHediff(torpor);
                return;
            }

            if (Value <= TorporThreshold)
            {
                if (starved != null) pawn.health.RemoveHediff(starved);
                if (torpor == null) pawn.health.AddHediff(torporDef);
                return;
            }

            if (torpor != null && Value >= RecoverFromTorporThreshold)
            {
                pawn.health.RemoveHediff(torpor);
                torpor = null;
            }

            if (Value < StarvedThreshold)
            {
                if (torpor == null && starved == null) pawn.health.AddHediff(starvedDef);
            }
            else if (starved != null)
            {
                pawn.health.RemoveHediff(starved);
            }
        }
    }

    public sealed class WraithLifeForceExtension : DefModExtension
    {
        public float starvedThreshold = 0.15f;
        public float torporThreshold = 0.001f;
        public float recoverFromTorporThreshold = 0.08f;
        public float hibernationDrainFactor = 0.02f;
        public float hibernatingRegenerationFactor = 0.12f;
        public float starvedRegenerationFactor = 0.25f;
        public float lowRegenerationFactor = 0.60f;
        public float normalRegenerationFactor = 1f;
        public float highRegenerationFactor = 1.60f;
    }

    public static class WraithLifeForceUtility
    {
        public static Gene_Resource_LifeForce Get(Pawn pawn)
        {
            if (pawn?.genes == null) return null;
            return pawn.genes.GenesListForReading.OfType<Gene_Resource_LifeForce>().FirstOrDefault(g => g.Active);
        }

        public static bool IsWraith(Pawn pawn) => Get(pawn) != null;

        public static bool Offset(Pawn pawn, float amount)
        {
            Gene_Resource_LifeForce lifeForce = Get(pawn);
            if (lifeForce == null) return false;
            lifeForce.Value = Math.Max(0f, Math.Min(lifeForce.Max, lifeForce.Value + amount));
            return true;
        }
    }

    public sealed class WraithRegenerationExtension : DefModExtension
    {
        public int healIntervalTicks = 250;
        public float baseHealAmount = 0.14f;
        public float fedRecentlyMultiplier = 4.6f;
        public float lifeForceCostPerHealPoint = 0.0025f;
    }

    public sealed class Gene_WraithRegeneration : Gene
    {
        private int nextHealTick;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn == null || pawn.Dead || pawn.health?.hediffSet == null) return;

            WraithRegenerationExtension ext = def?.GetModExtension<WraithRegenerationExtension>();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextHealTick) return;
            nextHealTick = now + Math.Max(30, ext?.healIntervalTicks ?? 250);

            Gene_Resource_LifeForce lifeForce = WraithLifeForceUtility.Get(pawn);
            if (lifeForce == null || lifeForce.RegenerationFactor <= 0f) return;

            Hediff_Injury injury = pawn.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.CanHealNaturally() && x.Severity > 0f)
                .OrderByDescending(x => x.Severity)
                .FirstOrDefault();
            if (injury == null) return;

            HediffDef fedDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_FedRecently");
            bool fedRecently = fedDef != null && pawn.health.hediffSet.HasHediff(fedDef);
            float heal = Math.Max(0f, ext?.baseHealAmount ?? 0.14f) * lifeForce.RegenerationFactor;
            if (fedRecently) heal *= Math.Max(1f, ext?.fedRecentlyMultiplier ?? 4.6f);
            if (heal <= 0f) return;

            float costPerPoint = Math.Max(0f, ext?.lifeForceCostPerHealPoint ?? 0.0025f);
            float cost = heal * costPerPoint;
            if (lifeForce.Value < cost && cost > 0f) heal *= lifeForce.Value / cost;
            if (heal <= 0f) return;

            injury.Heal(heal);
            WraithLifeForceUtility.Offset(pawn, -heal * costPerPoint);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextHealTick, "wngWraithNextHealTick", 0);
        }
    }

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
            if (!Active || pawn?.abilities == null) return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextReconcileTick) return;
            nextReconcileTick = now + 300;
            Reconcile();
        }

        private void Reconcile()
        {
            if (!Active || pawn?.abilities == null || def?.abilities == null) return;
            foreach (AbilityDef ability in def.abilities)
                if (ability != null && pawn.abilities.GetAbility(ability) == null)
                    pawn.abilities.GainAbility(ability);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextReconcileTick, "wngWraithAbilityReconcileTick", 0);
        }
    }
}
