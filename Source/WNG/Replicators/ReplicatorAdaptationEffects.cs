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
        public float gravRange = 7f;
        public float gravMinDistance = 3f;
        public int gravCooldownTicks = 360;
        public int gravLandingRadius = 2;
        public float overlayScalePerBodySize = 1.05f;
        public float organicWeakDamageMultiplier = 1.15f;
        public float organicWeakFireMultiplier = 1.75f;
        public float reinforcedDamageMultiplier = 0.90f;
        public float advancedDamageMultiplier = 0.78f;

        public CompProperties_ReplicatorAdaptationEffects() => compClass = typeof(CompReplicatorAdaptationEffects);
    }

    /// <summary>
    /// Gameplay effects for learned adaptations and accumulated material phenotype.
    /// Grav uses RimWorld's native PawnFlyer jump transaction as a short local gravitic
    /// reposition: no passive speed buff, sustained flight, teleport or pawn replacement.
    /// </summary>
    public sealed class CompReplicatorAdaptationEffects : ThingComp
    {
        private static readonly Dictionary<string, Graphic> overlayGraphics = new Dictionary<string, Graphic>();
        private static readonly VerbProperties gravVerbProps = new VerbProperties();
        private float shieldEnergy;
        private int nextRangedTick;
        private int nextGravTick;

        private CompProperties_ReplicatorAdaptationEffects Props => (CompProperties_ReplicatorAdaptationEffects)props;
        private Pawn Pawn => parent as Pawn;
        internal CompReplicatorState State => parent?.TryGetComp<CompReplicatorState>();

        public float PowerHealingMultiplier => State?.HasAdaptation(ReplicatorAdaptation.Power) == true
            ? Math.Max(1f, Props.powerHealingMultiplier)
            : 1f;

        internal int AdaptiveProjectileBodyDamage => Math.Max(1, (int)Math.Round(Math.Max(1f, Props.rangedDamage)));
        internal float AdaptiveProjectileArmorPenetration => Math.Max(0f, Props.rangedArmorPenetration);
        internal float AdaptiveAntiShieldMultiplier => Math.Max(1f, Props.antiShieldDamageMultiplier);

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

            if (ReplicatorEMP.IsSuppressed(pawn)) return;
            if (pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled) return;
            if (!ReplicatorCombatPermission.CanAttack(pawn)) return;

            TryAutonomousGravReposition(pawn);

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

            TryLaunchAdaptiveProjectile(pawn, target);
        }

        private bool TryLaunchAdaptiveProjectile(Pawn pawn, Pawn target)
        {
            if (pawn == null || target == null || pawn.Map == null || !pawn.Spawned || !target.Spawned)
                return false;

            ThingDef projectileDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_ReplicatorAdaptiveBolt");
            Projectile projectile = projectileDef == null ? null : ThingMaker.MakeThing(projectileDef) as Projectile;
            if (projectile == null)
            {
                Log.Error("[WNG] Replicator adaptive ranged fire could not create WNG_ReplicatorAdaptiveBolt.");
                return false;
            }

            GenSpawn.Spawn(projectile, pawn.Position, pawn.Map);
            projectile.Launch(
                pawn,
                target,
                target,
                ProjectileHitFlags.IntendedTarget | ProjectileHitFlags.NonTargetWorld,
                preventFriendlyFire: true);
            return true;
        }

        private void TryAutonomousGravReposition(Pawn pawn)
        {
            if (State?.HasAdaptation(ReplicatorAdaptation.Grav) != true || pawn.Downed)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextGravTick)
                return;

            float range = Math.Max(1f, Props.gravRange);
            float minDistance = Math.Max(0f, Math.Min(range, Props.gravMinDistance));
            float rangeSq = range * range;
            float minSq = minDistance * minDistance;

            Pawn target = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && pawn.HostileTo(p))
                .Where(p =>
                {
                    float distSq = p.Position.DistanceToSquared(pawn.Position);
                    return distSq >= minSq && distSq <= rangeSq &&
                           GenSight.LineOfSight(pawn.Position, p.Position, pawn.Map);
                })
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();

            if (target == null)
            {
                nextGravTick = now + 60;
                return;
            }

            IntVec3 destination = CellFinder.RandomClosewalkCellNear(
                target.Position,
                pawn.Map,
                Math.Max(1, Props.gravLandingRadius),
                c => c != target.Position && ValidGravTarget(pawn, c, ignoreCooldown: true));

            if (!destination.IsValid || !TryGravJump(pawn, destination, ignoreCooldown: true, out _))
                nextGravTick = now + 60;
        }

        private bool TryGravJump(Pawn pawn, IntVec3 destination, bool ignoreCooldown, out string rejection)
        {
            rejection = null;
            if (pawn == null || State?.HasAdaptation(ReplicatorAdaptation.Grav) != true)
            {
                rejection = "This Replicator has not learned gravitic adaptation.";
                return false;
            }
            if (!ignoreCooldown && (Find.TickManager?.TicksGame ?? 0) < nextGravTick)
            {
                rejection = "Gravitic field is recharging.";
                return false;
            }
            if (ReplicatorEMP.IsSuppressed(pawn))
            {
                rejection = "EMP disruption prevents gravitic repositioning.";
                return false;
            }
            if (pawn.Map == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
            {
                rejection = "The Replicator is not physically able to reposition.";
                return false;
            }
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
            {
                rejection = "Active Replicator containment suppresses the gravitic field.";
                return false;
            }
            if (!ValidGravTarget(pawn, destination, ignoreCooldown: true))
            {
                rejection = "Choose a visible, walkable cell within gravitic reposition range and outside active Replicator containment.";
                return false;
            }

            bool moved = JumpUtility.DoJump(pawn, destination, null, gravVerbProps);
            if (!moved)
            {
                rejection = "The gravitic reposition failed to form a valid native movement transaction.";
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            nextGravTick = now + Math.Max(60, Props.gravCooldownTicks);
            return true;
        }

        private bool ValidGravTarget(Pawn pawn, IntVec3 cell, bool ignoreCooldown)
        {
            if (pawn == null || pawn.Map == null || !pawn.Spawned || pawn.Dead || pawn.Downed)
                return false;
            if (State?.HasAdaptation(ReplicatorAdaptation.Grav) != true)
                return false;
            if (!ignoreCooldown && (Find.TickManager?.TicksGame ?? 0) < nextGravTick)
                return false;
            if (ReplicatorEMP.IsSuppressed(pawn))
                return false;
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
                return false;
            if (!JumpUtility.ValidJumpTarget(pawn, pawn.Map, cell))
                return false;
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, cell))
                return false;

            float range = Math.Max(1f, Props.gravRange);
            float minDistance = Math.Max(0f, Math.Min(range, Props.gravMinDistance));
            float distSq = pawn.Position.DistanceToSquared(cell);
            if (distSq > range * range || distSq < minDistance * minDistance)
                return false;
            return GenSight.LineOfSight(pawn.Position, cell, pawn.Map);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer || !pawn.Spawned || pawn.Map == null ||
                State?.HasAdaptation(ReplicatorAdaptation.Grav) != true)
                yield break;

            TargetingParameters targetParams = new TargetingParameters
            {
                canTargetLocations = true,
                canTargetPawns = false,
                canTargetBuildings = false,
                canTargetItems = false,
                validator = info => ValidGravTarget(pawn, info.Cell, ignoreCooldown: false)
            };

            Command_Target command = new Command_Target
            {
                defaultLabel = "Grav reposition",
                defaultDesc = $"Use learned gravitic field control to reposition this exact Replicator up to {Math.Max(1f, Props.gravRange):0.#} cells through RimWorld's native flyer movement. This is a short maneuver, not sustained flight or teleportation.",
                targetingParams = targetParams,
                action = target =>
                {
                    if (!TryGravJump(pawn, target.Cell, ignoreCooldown: false, out string rejection) && !rejection.NullOrEmpty())
                        Messages.Message(rejection, pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
            };

            int now = Find.TickManager?.TicksGame ?? 0;
            if (ReplicatorEMP.IsSuppressed(pawn))
                command.Disable("EMP disruption prevents gravitic repositioning.");
            else if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
                command.Disable("Active Replicator containment suppresses the gravitic field.");
            else if (now < nextGravTick)
                command.Disable($"Gravitic field recharging: {Math.Max(0, nextGravTick - now)} ticks remaining.");

            yield return command;
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

            ApplyMaterialPhenotype(ref dinfo);
        }

        private void ApplyMaterialPhenotype(ref DamageInfo dinfo)
        {
            if (State == null || State.MaterialSamples <= 0) return;
            float multiplier = 1f;
            switch (State.MaterialPhenotype)
            {
                case ReplicatorMaterialPhenotype.OrganicWeak:
                    multiplier = Math.Max(1f, Props.organicWeakDamageMultiplier);
                    if (dinfo.Def == DamageDefOf.Flame)
                        multiplier *= Math.Max(1f, Props.organicWeakFireMultiplier);
                    break;
                case ReplicatorMaterialPhenotype.Reinforced:
                    multiplier = Math.Max(0.05f, Math.Min(1f, Props.reinforcedDamageMultiplier));
                    break;
                case ReplicatorMaterialPhenotype.Advanced:
                    multiplier = Math.Max(0.05f, Math.Min(1f, Props.advancedDamageMultiplier));
                    break;
            }
            dinfo.SetAmount(Math.Max(0f, dinfo.Amount * multiplier));
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
            List<string> lines = new List<string>();
            if (State?.HasAdaptation(ReplicatorAdaptation.Shield) == true)
                lines.Add($"Adaptive shield: {shieldEnergy:0}/{Math.Max(0f, Props.shieldCapacity):0}");
            if (State?.HasAdaptation(ReplicatorAdaptation.Grav) == true)
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                lines.Add(now >= nextGravTick ? "Gravitic reposition: ready" : $"Gravitic reposition: {Math.Max(0, nextGravTick - now)} ticks");
            }
            if (State?.MaterialSamples > 0)
                lines.Add($"Body stock: {State.MaterialPhenotype}");
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref shieldEnergy, "wngReplicatorShieldEnergy", 0f);
            Scribe_Values.Look(ref nextRangedTick, "wngReplicatorNextAdaptiveShot", 0);
            Scribe_Values.Look(ref nextGravTick, "wngReplicatorNextGravReposition", 0);
        }
    }
}