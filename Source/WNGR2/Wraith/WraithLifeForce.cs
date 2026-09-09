using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Visible Wraith feeding reserve.  This deliberately uses RimWorld's native Gene_Resource
    /// infrastructure so the reserve is save-safe, gets a normal resource gizmo, and can be
    /// balanced from Def data without Harmony patches.
    /// </summary>
    public sealed class Gene_Resource_LifeForce : Gene_Resource, IGeneResourceDrain
    {
        public Gene_Resource Resource => this;
        public Pawn Pawn => pawn;
        public bool CanOffset => Active && pawn != null && !pawn.Dead && !pawn.Suspended;
        public string DisplayLabel => Label + " (gene)";
        public float ResourceLossPerDay => def.resourceLossPerDay;

        public override float InitialResourceMax => 1f;
        public override float MinLevelForAlert => 0.15f;
        public override float MaxLevelOffset => 0.05f;

        protected override Color BarColor => new Color(0.13f, 0.52f, 0.43f);
        protected override Color BarHighlightColor => new Color(0.28f, 0.72f, 0.60f);

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            GeneResourceDrainUtility.TickResourceDrainInterval(this, delta);
        }

        public override void SetTargetValuePct(float val)
        {
            targetValue = Mathf.Clamp(val * Max, 0f, Max - MaxLevelOffset);
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
    }
}
