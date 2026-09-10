using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorAdaptationEffects : CompProperties
    {
        public float armorDamageMultiplier = 0.80f;
        public float shieldCapacity = 40f;
        public float shieldRechargePerTick = 0.02f;
        public float powerHealingMultiplier = 1.5f;
        public float rangedRange = 18f;
        public int rangedCooldownTicks = 240;
        public float rangedDamage = 7f;
        public float rangedArmorPenetration = 0.15f;
        public float antiShieldDamageMultiplier = 1.5f;
        public float overlayScalePerBodySize = 1.05f;

        public CompProperties_ReplicatorAdaptationEffects() => compClass = typeof(CompReplicatorAdaptationEffects);
    }

    /// <summary>
    /// Gameplay effects for adaptations learned by actual assimilation. Values are Def data.
    /// Grav is visual/state-ready here but its mobility effect remains dependent on the later
    /// concrete gravtech implementation rather than being faked with an unrelated bonus.
    /// </summary>
    public sealed class CompReplicatorAdaptationEffects : ThingComp
    {
        private static readonly Dictionary<string, Graphic> overlayGraphics = new Dictionary<string, Graphic>();
        private float shieldEnergy;
        private int nextRangedTick;

        private CompProperties_ReplicatorAdaptationEffects Props => (CompProperties_ReplicatorAdaptationEffects)props;
        private Pawn Pawn => parent as Pawn;
        private CompReplicatorState State => parent?.TryGetComp<CompReplicatorState>();

        public float PowerHealingMultiplier => State?.HasAdaptation(ReplicatorAdaptation.Power) == true
            ? Math.Max(1f, Props.powerHealingMultiplier)
            : 1f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && State?.HasAdaptation(ReplicatorAdaptation.Shield) == true)
                shieldEnergy = Math.Max(0f, Props.shieldCapacity);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null) return;

            if (State?.HasAdaptation(ReplicatorAdaptation.Shield) == true && !ReplicatorEMP.IsSuppressed(pawn))
                shieldEnergy = Math.Min(Math.Max(0f, Props.shieldCapacity), shieldEnergy + Math.Max(0f, Props.shieldRechargePerTick));

            if (pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled || ReplicatorEMP.IsSuppressed(pawn)) return;
            if (State?.HasAdaptation(ReplicatorAdaptation.Ranged) != true) return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextRangedTick) return;
            nextRangedTick = now + Math.Max(30, Props.rangedCooldownTicks);

            float range = Math.Max(1f, Props.rangedRange);
            float rangeSq = range * range;
            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && pawn.HostileTo(p)
                    && p.Position.DistanceToSquared(pawn.Position) <= rangeSq
                    && GenSight.LineOfSight(pawn.Position, p.Position, pawn.Map))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();
            if (target == null) return;

            float damage = Math.Max(1f, Props.rangedDamage);
            CompReplicatorAdaptationEffects targetEffects = target.TryGetComp<CompReplicatorAdaptationEffects>();
            if (State.HasAdaptation(ReplicatorAdaptation.AntiShield)
                && targetEffects?.State?.HasAdaptation(ReplicatorAdaptation.Shield) == true)
                damage *= Math.Max(1f, Props.antiShieldDamageMultiplier);

            target.TakeDamage(new DamageInfo(
                DamageDefOf.Bullet,
                damage,
                Math.Max(0f, Props.rangedArmorPenetration),
                instigator: pawn));
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn pawn = Pawn;
            if (pawn == null || dinfo.Def == DamageDefOf.EMP || ReplicatorEMP.IsSuppressed(pawn)) return;

            if (State?.HasAdaptation(ReplicatorAdaptation.Shield) == true && shieldEnergy > 0f)
            {
                float incoming = Math.Max(0f, dinfo.Amount);
                float blocked = Math.Min(incoming, shieldEnergy);
                shieldEnergy -= blocked;
                float remaining = incoming - blocked;
                if (remaining <= 0.001f)
                {
                    absorbed = true;
                    return;
                }
                dinfo.SetAmount(remaining);
            }

            if (State?.HasAdaptation(ReplicatorAdaptation.Armor) == true)
                dinfo.SetAmount(Math.Max(0f, dinfo.Amount * Math.Max(0.05f, Math.Min(1f, Props.armorDamageMultiplier))));
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Pawn pawn = Pawn;
            CompReplicatorState state = State;
            if (pawn == null || !pawn.Spawned || state == null) return;

            DrawOverlay(state, ReplicatorAdaptation.Armor, "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_Armor", 0.001f);
            DrawOverlay(state, ReplicatorAdaptation.Ranged, "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_Ranged", 0.002f);
            DrawOverlay(state, ReplicatorAdaptation.Power, "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_Power", 0.003f);
            DrawOverlay(state, ReplicatorAdaptation.Grav, "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_Grav", 0.004f);
            DrawOverlay(state, ReplicatorAdaptation.Shield, "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_Shield", 0.005f);
        }

        private void DrawOverlay(CompReplicatorState state, ReplicatorAdaptation adaptation, string path, float altitudeOffset)
        {
            if (!state.HasAdaptation(adaptation)) return;
            Pawn pawn = Pawn;
            if (pawn == null) return;

            float size = Math.Max(0.55f, pawn.BodySize * Math.Max(0.25f, Props.overlayScalePerBodySize));
            string key = path + "|" + size.ToString("0.00");
            Graphic graphic;
            if (!overlayGraphics.TryGetValue(key, out graphic))
            {
                graphic = GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, new Vector2(size, size), Color.white);
                overlayGraphics[key] = graphic;
            }

            Vector3 drawPos = pawn.DrawPos;
            drawPos.y += altitudeOffset;
            graphic.Draw(drawPos, pawn.Rotation, pawn);
        }

        public override string CompInspectStringExtra()
        {
            if (State?.HasAdaptation(ReplicatorAdaptation.Shield) != true) return null;
            return $"Adaptive shield: {shieldEnergy:0}/{Math.Max(0f, Props.shieldCapacity):0}";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shieldEnergy, "wngReplicatorShieldEnergy", 0f);
            Scribe_Values.Look(ref nextRangedTick, "wngReplicatorNextAdaptiveShot", 0);
        }
    }
}
