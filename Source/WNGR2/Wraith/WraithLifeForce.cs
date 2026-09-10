using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Visible Wraith feeding reserve. RimWorld's native Gene_Resource infrastructure remains the
    /// single source of truth for the meter; WNG only defines Wraith-specific drain, starvation,
    /// torpor and hibernation behaviour around it.
    /// </summary>
    public sealed class Gene_Resource_LifeForce : Gene_Resource, IGeneResourceDrain
    {
        public const float StarvedThreshold = 0.15f;
        public const float ShouldFeedThreshold = 0.20f;
        public const float TorporThreshold = 0.001f;
        public const float RecoverFromTorporThreshold = 0.08f;
        public const float HibernationDrainFactor = 0.02f;

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

        public bool IsHibernating
        {
            get
            {
                if (pawn?.health?.hediffSet == null)
                    return false;
                HediffDef hibernating = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WraithHibernating");
                return hibernating != null && pawn.health.hediffSet.HasHediff(hibernating);
            }
        }

        public bool ShouldConsumeNow()
        {
            return Active && !IsHibernating && Value < ShouldFeedThreshold;
        }

        public float RegenerationFactor
        {
            get
            {
                if (IsHibernating)
                    return 0.12f;
                if (Value <= 0.01f)
                    return 0.05f;
                if (Value < 0.15f)
                    return 0.25f;
                if (Value < 0.40f)
                    return 0.60f;
                if (Value < 0.70f)
                    return 1.00f;
                return 1.60f;
            }
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            GeneResourceDrainUtility.TickResourceDrainInterval(this, delta);

            if (pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(60, delta))
                return;
            UpdateStarvationState();
        }

        public override void SetTargetValuePct(float val)
        {
            targetValue = Mathf.Clamp(val * Max, 0f, Max - MaxLevelOffset);
        }

        public void Restore(float amount)
        {
            if (amount <= 0f)
                return;
            Value = Math.Min(Max, Value + amount);
        }

        private void UpdateStarvationState()
        {
            if (pawn?.health?.hediffSet == null)
                return;

            HediffDef starvedDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeForceStarved");
            HediffDef torporDef = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeForceTorpor");
            if (starvedDef == null || torporDef == null)
                return;

            Hediff starved = pawn.health.hediffSet.GetFirstHediffOfDef(starvedDef);
            Hediff torpor = pawn.health.hediffSet.GetFirstHediffOfDef(torporDef);

            if (IsHibernating)
            {
                if (starved != null)
                    pawn.health.RemoveHediff(starved);
                if (torpor != null)
                    pawn.health.RemoveHediff(torpor);
                return;
            }

            if (Value <= TorporThreshold)
            {
                if (starved != null)
                    pawn.health.RemoveHediff(starved);
                if (torpor == null)
                    pawn.health.AddHediff(torporDef);
                return;
            }

            if (torpor != null && Value >= RecoverFromTorporThreshold)
            {
                pawn.health.RemoveHediff(torpor);
                torpor = null;
            }

            if (Value < StarvedThreshold)
            {
                if (torpor == null && starved == null)
                    pawn.health.AddHediff(starvedDef);
            }
            else if (starved != null)
            {
                pawn.health.RemoveHediff(starved);
            }
        }
    }

    public static class WraithLifeForceUtility
    {
        public static Gene_Resource_LifeForce Get(Pawn pawn)
        {
            if (pawn?.genes == null)
                return null;

            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene is Gene_Resource_LifeForce lifeForce && gene.Active)
                    return lifeForce;
            }
            return null;
        }

        public static bool Offset(Pawn pawn, float amount)
        {
            Gene_Resource_LifeForce lifeForce = Get(pawn);
            if (lifeForce == null)
                return false;

            lifeForce.Value = lifeForce.Value + amount;
            return true;
        }

        public static bool IsWraith(Pawn pawn)
        {
            return Get(pawn) != null;
        }

        public static bool IsKeeperOrQueen(Pawn pawn, Faction faction = null)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.Spawned)
                return false;
            if (faction != null && pawn.Faction != faction)
                return false;

            string kind = pawn.kindDef?.defName ?? string.Empty;
            return kind == "WNG_WraithKeeper" || kind == "WNG_WraithQueen";
        }

        public static bool HasKeeperOrQueen(Map map, Faction faction)
        {
            if (map == null || faction == null)
                return false;

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (IsKeeperOrQueen(pawn, faction))
                    return true;
            }
            return false;
        }
    }
}
