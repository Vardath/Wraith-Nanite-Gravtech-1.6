using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorAdaptationEffects : CompProperties
    {
        public float armorDeflectionChance = 0.12f;
        public int shieldRechargeTicks = 1800;
        public float energyDamageFactor = 0.35f;

        public CompProperties_ReplicatorAdaptationEffects()
        {
            compClass = typeof(CompReplicatorAdaptationEffects);
        }
    }

    /// <summary>
    /// Supplies the block-form baseline energy absorption plus learned adaptation behavior.
    /// Conventional Bullet/Cut/Blunt damage is deliberately left untouched by the baseline so
    /// projectile weapons remain useful. Heat/beam/laser/plasma/pulse-style DamageDefs are reduced
    /// without depending on any optional Stargate mod's exact Def names. EMP is never absorbed.
    /// Material learning is consumed by assimilation, Power by regeneration/specialist timing,
    /// Ranged by artillery, Grav by hierarchy reach, and this comp supplies Armor/Shield responses
    /// plus the approved visible adaptation overlays.
    /// </summary>
    public sealed class CompReplicatorAdaptationEffects : ThingComp
    {
        private int shieldReadyTick;
        private Graphic armorOverlay;
        private Graphic rangedOverlay;
        private Graphic powerOverlay;
        private Graphic gravOverlay;
        private Graphic shieldOverlay;

        private CompProperties_ReplicatorAdaptationEffects Props => (CompProperties_ReplicatorAdaptationEffects)props;

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            if (state == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (dinfo.Def == DamageDefOf.EMP)
            {
                shieldReadyTick = Math.Max(shieldReadyTick, now + Math.Max(60, Props.shieldRechargeTicks));
                return;
            }

            if (IsEnergyLikeDamage(dinfo.Def) && dinfo.Amount > 0f)
            {
                float factor = Math.Max(0.05f, Math.Min(1f, Props.energyDamageFactor));
                dinfo.SetAmount(dinfo.Amount * factor);
            }

            if (state.EMPSuppressed)
                return;

            ReplicatorAdaptationFlags learned = state.Adaptations;
            if ((learned & ReplicatorAdaptationFlags.Shield) != ReplicatorAdaptationFlags.None && now >= shieldReadyTick)
            {
                absorbed = true;
                shieldReadyTick = now + Math.Max(60, Props.shieldRechargeTicks);
                return;
            }

            if ((learned & ReplicatorAdaptationFlags.Armor) != ReplicatorAdaptationFlags.None && IsPhysicalDamage(dinfo.Def) &&
                Rand.Chance(Math.Max(0f, Math.Min(0.45f, Props.armorDeflectionChance))))
            {
                absorbed = true;
            }
        }

        private static bool IsPhysicalDamage(DamageDef def)
        {
            return def == DamageDefOf.Bullet || def == DamageDefOf.Cut || def == DamageDefOf.Blunt;
        }

        private static bool IsEnergyLikeDamage(DamageDef def)
        {
            if (def == null || def == DamageDefOf.EMP || IsPhysicalDamage(def))
                return false;

            string armorCategory = def.armorCategory?.defName ?? string.Empty;
            if (string.Equals(armorCategory, "Heat", StringComparison.OrdinalIgnoreCase))
                return true;

            string name = def.defName ?? string.Empty;
            return name.IndexOf("laser", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("energy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("plasma", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("pulse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Pawn pawn = parent as Pawn;
            CompReplicatorState state = pawn?.TryGetComp<CompReplicatorState>();
            if (pawn == null || state == null || !pawn.Spawned)
                return;

            ReplicatorAdaptationFlags learned = state.Adaptations;
            int visibleCount = 0;
            if ((learned & ReplicatorAdaptationFlags.Armor) != ReplicatorAdaptationFlags.None) visibleCount++;
            if ((learned & ReplicatorAdaptationFlags.Ranged) != ReplicatorAdaptationFlags.None) visibleCount++;
            if ((learned & ReplicatorAdaptationFlags.Power) != ReplicatorAdaptationFlags.None) visibleCount++;
            if ((learned & ReplicatorAdaptationFlags.Grav) != ReplicatorAdaptationFlags.None) visibleCount++;
            if ((learned & ReplicatorAdaptationFlags.Shield) != ReplicatorAdaptationFlags.None) visibleCount++;
            if (visibleCount == 0)
                return;

            int slot = 0;
            if ((learned & ReplicatorAdaptationFlags.Armor) != ReplicatorAdaptationFlags.None)
                DrawBadge(pawn, ReplicatorAdaptationFlags.Armor, ref armorOverlay, slot++, visibleCount);
            if ((learned & ReplicatorAdaptationFlags.Ranged) != ReplicatorAdaptationFlags.None)
                DrawBadge(pawn, ReplicatorAdaptationFlags.Ranged, ref rangedOverlay, slot++, visibleCount);
            if ((learned & ReplicatorAdaptationFlags.Power) != ReplicatorAdaptationFlags.None)
                DrawBadge(pawn, ReplicatorAdaptationFlags.Power, ref powerOverlay, slot++, visibleCount);
            if ((learned & ReplicatorAdaptationFlags.Grav) != ReplicatorAdaptationFlags.None)
                DrawBadge(pawn, ReplicatorAdaptationFlags.Grav, ref gravOverlay, slot++, visibleCount);
            if ((learned & ReplicatorAdaptationFlags.Shield) != ReplicatorAdaptationFlags.None)
                DrawBadge(pawn, ReplicatorAdaptationFlags.Shield, ref shieldOverlay, slot, visibleCount);
        }

        private static void DrawBadge(Pawn pawn, ReplicatorAdaptationFlags flag, ref Graphic graphic, int slot, int count)
        {
            if (graphic == null)
            {
                string suffix = flag.ToString();
                string path = "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_" + suffix;
                float size = Mathf.Clamp(0.26f + pawn.BodySize * 0.06f, 0.28f, 0.42f);
                graphic = GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, new Vector2(size, size), Color.white);
            }

            Vector3 pos = pawn.DrawPos;
            float centered = slot - (count - 1) * 0.5f;
            pos.x += centered * 0.22f;
            pos.z += 0.44f;
            pos.y += 0.012f + slot * 0.0005f;
            graphic.Draw(pos, Rot4.North, pawn);
        }

        public override string CompInspectStringExtra()
        {
            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            if (state == null || state.Adaptations == ReplicatorAdaptationFlags.None)
                return null;

            return "Replicator adaptations: " + state.Adaptations;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shieldReadyTick, "wngReplicatorShieldReadyTick", 0);
        }
    }
}
