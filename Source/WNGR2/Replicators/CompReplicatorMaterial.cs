using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorMaterial : CompProperties
    {
        public CompProperties_ReplicatorMaterial()
        {
            compClass = typeof(CompReplicatorMaterial);
        }
    }

    /// <summary>
    /// Stores the material signature inherited by a Replicator spawned from consumed matter.
    /// The signature is save-safe and works with modded StuffDefs. It supplies the visible body
    /// tint while a small hediff tier carries material-derived durability into gameplay.
    /// </summary>
    public sealed class CompReplicatorMaterial : ThingComp
    {
        private string sourceDefName;
        private float colorR = 0.62f;
        private float colorG = 0.66f;
        private float colorB = 0.68f;
        private float colorA = 1f;
        private int durabilityTier = 1;

        private Graphic cachedOverlay;
        private string cachedKey;

        public ThingDef SourceDef => string.IsNullOrEmpty(sourceDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(sourceDefName);

        public string SourceLabel => SourceDef?.LabelCap ?? "mixed matter";

        public void CopyFrom(CompReplicatorMaterial other)
        {
            if (other == null || string.IsNullOrEmpty(other.sourceDefName))
                return;

            sourceDefName = other.sourceDefName;
            colorR = other.colorR;
            colorG = other.colorG;
            colorB = other.colorB;
            colorA = other.colorA;
            durabilityTier = other.durabilityTier;
            cachedOverlay = null;
            cachedKey = null;
            ApplyTierHediff();
        }

        public void ApplyMaterialSignature(ThingDef materialDef)
        {
            if (materialDef == null)
                return;

            sourceDefName = materialDef.defName;
            if (materialDef.stuffProps != null)
            {
                Color c = materialDef.stuffProps.color;
                colorR = c.r;
                colorG = c.g;
                colorB = c.b;
                colorA = c.a <= 0f ? 1f : c.a;
                durabilityTier = TierFromStuff(materialDef.stuffProps);
            }
            else if (materialDef.graphicData != null)
            {
                Color c = materialDef.graphicData.color;
                colorR = c.r;
                colorG = c.g;
                colorB = c.b;
                colorA = c.a <= 0f ? 1f : c.a;
                durabilityTier = 1;
            }

            cachedOverlay = null;
            cachedKey = null;
            ApplyTierHediff();
        }

        private static int TierFromStuff(StuffProperties props)
        {
            float hpFactor = GetFactor(props, "MaxHitPoints", 1f);
            float flammability = GetFactor(props, "Flammability", 1f);

            float score = hpFactor;
            if (flammability > 1.25f) score *= 0.82f;
            else if (flammability <= 0.25f) score *= 1.12f;

            if (score < 0.78f) return 0;
            if (score < 1.35f) return 1;
            if (score < 2.35f) return 2;
            return 3;
        }

        private static float GetFactor(StuffProperties props, string statDefName, float fallback)
        {
            if (props?.statFactors == null) return fallback;
            for (int i = 0; i < props.statFactors.Count; i++)
            {
                StatModifier mod = props.statFactors[i];
                if (mod?.stat?.defName == statDefName)
                    return mod.value;
            }
            return fallback;
        }

        private void ApplyTierHediff()
        {
            Pawn pawn = parent as Pawn;
            if (pawn?.health == null) return;

            string desired = durabilityTier switch
            {
                0 => "WNG_ReplicatorMaterialFragile",
                2 => "WNG_ReplicatorMaterialHardened",
                3 => "WNG_ReplicatorMaterialUltra",
                _ => "WNG_ReplicatorMaterialStandard"
            };

            string[] all =
            {
                "WNG_ReplicatorMaterialFragile",
                "WNG_ReplicatorMaterialStandard",
                "WNG_ReplicatorMaterialHardened",
                "WNG_ReplicatorMaterialUltra"
            };

            for (int i = 0; i < all.Length; i++)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(all[i]);
                if (def == null) continue;
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null && all[i] != desired)
                    pawn.health.RemoveHediff(existing);
            }

            HediffDef wanted = DefDatabase<HediffDef>.GetNamedSilentFail(desired);
            if (wanted != null && !pawn.health.hediffSet.HasHediff(wanted))
                pawn.health.AddHediff(wanted);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ApplyTierHediff();
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Pawn pawn = parent as Pawn;
            if (pawn == null || string.IsNullOrEmpty(sourceDefName)) return;

            Color color = new Color(colorR, colorG, colorB, colorA);
            string bodyDef = pawn.def?.defName ?? "WNG_ReplicatorDrone";
            float drawSize = bodyDef switch
            {
                "WNG_ReplicatorHunter" => 1.25f,
                "WNG_ReplicatorBulwark" => 1.8f,
                "WNG_ReplicatorTitan" => 2.6f,
                "WNG_ReplicatorController" => 1.45f,
                _ => 0.9f
            };
            string key = bodyDef + ":" + sourceDefName + ":" + colorR + ":" + colorG + ":" + colorB;
            if (cachedOverlay == null || cachedKey != key)
            {
                cachedOverlay = GraphicDatabase.Get<Graphic_Multi>(
                    "Things/Pawn/Replicator/" + bodyDef,
                    ShaderDatabase.Cutout,
                    new Vector2(drawSize, drawSize),
                    color);
                cachedKey = key;
            }

            Vector3 pos = pawn.DrawPos;
            pos.y += 0.004f;
            cachedOverlay?.Draw(pos, pawn.Rotation, pawn);
        }

        public override string CompInspectStringExtra()
        {
            if (string.IsNullOrEmpty(sourceDefName)) return null;
            string tier = durabilityTier switch
            {
                0 => "fragile",
                2 => "hardened",
                3 => "ultra-dense",
                _ => "standard"
            };
            return $"Replication material: {SourceLabel} ({tier})";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref sourceDefName, "wngReplicatorMaterialDef");
            Scribe_Values.Look(ref colorR, "wngReplicatorColorR", 0.62f);
            Scribe_Values.Look(ref colorG, "wngReplicatorColorG", 0.66f);
            Scribe_Values.Look(ref colorB, "wngReplicatorColorB", 0.68f);
            Scribe_Values.Look(ref colorA, "wngReplicatorColorA", 1f);
            Scribe_Values.Look(ref durabilityTier, "wngReplicatorDurabilityTier", 1);
        }
    }
}
