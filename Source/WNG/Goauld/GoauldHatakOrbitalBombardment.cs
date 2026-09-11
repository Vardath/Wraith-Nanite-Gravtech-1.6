using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Author-tunable world/orbital firing envelope for a Ha'tak heavy plasma battery.
    /// The effect itself deliberately uses Royalty's real Bombardment Thing while WNG owns the
    /// cross-map source/target rules that bind it to an actual Goa'uld Odyssey gravship in orbit.
    /// </summary>
    public sealed class CompProperties_GoauldHatakOrbitalBombardment : CompProperties
    {
        public int maxWorldRangeTiles = 12;
        public int cooldownTicks = 60000;
        public float impactAreaRadius = 14f;
        public FloatRange explosionRadiusRange = new FloatRange(4.5f, 6.5f);
        public int bombIntervalTicks = 18;
        public int warmupTicks = 90;
        public int explosionCount = 18;
        public int randomFireRadius = 18;

        public CompProperties_GoauldHatakOrbitalBombardment()
        {
            compClass = typeof(CompGoauldHatakOrbitalBombardment);
        }
    }

    /// <summary>
    /// True cross-map Ha'tak bombardment. The command only exists for player-owned batteries and
    /// only becomes usable when the exact battery is powered, on connected substructure belonging
    /// to an actual Goa'uld-themed Odyssey GravEngine, and the source map is Odyssey's Orbit layer.
    /// A separate generated Surface-layer MapParent is selected on the world view, followed by an
    /// exact impact cell on that target map. The target receives Royalty's native Bombardment Thing;
    /// no ordinary local turret shot or renamed same-map explosion is used as an orbital proxy.
    /// </summary>
    public sealed class CompGoauldHatakOrbitalBombardment : ThingComp
    {
        private int nextFireTick;

        private CompProperties_GoauldHatakOrbitalBombardment Props =>
            (CompProperties_GoauldHatakOrbitalBombardment)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent?.Faction != Faction.OfPlayer)
                yield break;

            Command_Action command = new Command_Action
            {
                defaultLabel = "Ha'tak orbital bombardment",
                defaultDesc = "Fire this Ha'tak battery from an actual Odyssey orbit map onto a separate generated surface map using RimWorld's native orbital bombardment entity.",
                icon = TexCommand.Attack,
                action = BeginWorldTargeting
            };

            AcceptanceReport report = CanOrderBombardment();
            if (!report.Accepted)
                command.Disable(report.Reason);

            yield return command;
        }

        private AcceptanceReport CanOrderBombardment()
        {
            if (parent?.Spawned != true || parent.Map == null)
                return "The plasma battery is not deployed on a map.";

            if (!ModsConfig.OdysseyActive)
                return "Odyssey is required for Ha'tak orbital firing geometry.";

            if (!ModsConfig.RoyaltyActive || ThingDefOf.Bombardment == null)
                return "Royalty is required for the native orbital bombardment entity.";

            if (parent.Map.Tile.LayerDef != PlanetLayerDefOf.Orbit)
                return "The Ha'tak must be in Odyssey orbit before this battery can bombard another map.";

            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn)
                return "The Ha'tak heavy plasma battery has no power.";

            CompWNGGravshipPartTheme partTheme = parent.TryGetComp<CompWNGGravshipPartTheme>();
            if (partTheme == null || partTheme.Theme != WNGGravshipTheme.Goauld)
                return "This battery is not registered as Goa'uld gravship technology.";

            if (FindConnectedGoauldEngine() == null)
                return "The battery is not on connected substructure belonging to a Goa'uld Ha'tak grav engine.";

            int remaining = nextFireTick - (Find.TickManager?.TicksGame ?? 0);
            if (remaining > 0)
                return "Orbital bombardment cooling down (" + remaining.ToStringTicksToPeriod() + ").";

            return true;
        }

        private Building_GravEngine FindConnectedGoauldEngine()
        {
            Map map = parent?.Map;
            if (map == null || ThingDefOf.GravEngine == null)
                return null;

            foreach (Building_GravEngine engine in map.listerThings
                         .ThingsOfDef(ThingDefOf.GravEngine)
                         .OfType<Building_GravEngine>())
            {
                if (engine?.Spawned != true)
                    continue;

                CompWNGGravEngineTheme theme = engine.TryGetComp<CompWNGGravEngineTheme>();
                if (theme?.Theme != WNGGravshipTheme.Goauld)
                    continue;

                if (engine.ValidSubstructureAt(parent.Position))
                    return engine;
            }

            return null;
        }

        private void BeginWorldTargeting()
        {
            AcceptanceReport report = CanOrderBombardment();
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            PlanetTile sourceTile = parent.Map.Tile;
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(new GlobalTargetInfo(sourceTile)));
            Find.WorldSelector.ClearSelection();

            Find.WorldTargeter.BeginTargeting(
                ChoseWorldTarget,
                canTargetTiles: false,
                mouseAttachment: TexCommand.Attack,
                closeWorldTabWhenFinished: false,
                onUpdate: null,
                extraLabelGetter: null,
                canSelectTarget: CanSelectWorldTarget,
                originForClosest: sourceTile,
                showCancelButton: true);
        }

        private bool CanSelectWorldTarget(GlobalTargetInfo target)
        {
            MapParent targetParent = ResolveTargetMapParent(target);
            if (targetParent == null || !targetParent.HasMap || targetParent.Map == null)
                return false;

            Map sourceMap = parent?.Map;
            if (sourceMap == null || targetParent.Map == sourceMap)
                return false;

            if (targetParent.Tile.LayerDef != PlanetLayerDefOf.Surface)
                return false;

            int distance = WorldDistanceTo(targetParent.Tile);
            return distance != int.MaxValue && distance <= Math.Max(1, Props.maxWorldRangeTiles);
        }

        private static MapParent ResolveTargetMapParent(GlobalTargetInfo target)
        {
            return target.HasWorldObject ? target.WorldObject as MapParent : null;
        }

        private int WorldDistanceTo(PlanetTile targetTile)
        {
            Map sourceMap = parent?.Map;
            if (sourceMap == null || Find.WorldGrid == null || !targetTile.Valid)
                return int.MaxValue;

            return Find.WorldGrid.TraversalDistanceBetween(
                sourceMap.Tile,
                targetTile,
                passImpassable: true,
                maxDist: Math.Max(1, Props.maxWorldRangeTiles) + 1,
                canTraverseLayers: true);
        }

        private bool ChoseWorldTarget(GlobalTargetInfo target)
        {
            if (!CanSelectWorldTarget(target))
            {
                Messages.Message(
                    "Select a separate generated surface map within the Ha'tak battery's orbital firing range.",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return false;
            }

            MapParent targetParent = ResolveTargetMapParent(target);
            Map targetMap = targetParent?.Map;
            if (targetMap == null)
                return false;

            Current.Game.CurrentMap = targetMap;
            CameraJumper.TryHideWorld();
            BeginLocalTargeting(targetMap);
            return true;
        }

        private void BeginLocalTargeting(Map targetMap)
        {
            TargetingParameters targeting = TargetingParameters.ForCell();
            targeting.validator = delegate(TargetInfo target)
            {
                if (!target.Cell.InBounds(targetMap) || target.Cell.Fogged(targetMap))
                    return false;

                RoofDef roof = target.Cell.GetRoof(targetMap);
                return roof == null || !roof.isThickRoof;
            };

            Find.Targeter.BeginTargeting(
                targeting,
                delegate(LocalTargetInfo target)
                {
                    TryFireAt(targetMap, target.Cell);
                },
                caster: null,
                actionWhenFinished: null,
                mouseAttachment: TexCommand.Attack,
                requiresCastedSelected: false);
        }

        private bool TryFireAt(Map targetMap, IntVec3 targetCell)
        {
            AcceptanceReport report = CanOrderBombardment();
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, parent, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            if (targetMap == null || !targetCell.InBounds(targetMap) || targetMap.Tile.LayerDef != PlanetLayerDefOf.Surface)
                return false;

            if (WorldDistanceTo(targetMap.Tile) > Math.Max(1, Props.maxWorldRangeTiles))
            {
                Messages.Message("The selected surface map is outside Ha'tak orbital bombardment range.", MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            RoofDef roof = targetCell.GetRoof(targetMap);
            if (targetCell.Fogged(targetMap) || (roof != null && roof.isThickRoof))
            {
                Messages.Message("Ha'tak orbital fire cannot be designated on a fogged cell or beneath thick overhead mountain.", MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            Bombardment bombardment = GenSpawn.Spawn(ThingDefOf.Bombardment, targetCell, targetMap) as Bombardment;
            if (bombardment == null)
            {
                Log.Error("[WNG] Ha'tak orbital bombardment could not create Royalty's native Bombardment Thing.");
                return false;
            }

            FloatRange explosionRange = Props.explosionRadiusRange;
            float minExplosion = Mathf.Max(1f, explosionRange.min);
            float maxExplosion = Mathf.Max(minExplosion, explosionRange.max);

            bombardment.impactAreaRadius = Mathf.Max(1f, Props.impactAreaRadius);
            bombardment.explosionRadiusRange = new FloatRange(minExplosion, maxExplosion);
            bombardment.bombIntervalTicks = Math.Max(1, Props.bombIntervalTicks);
            bombardment.warmupTicks = Math.Max(1, Props.warmupTicks);
            bombardment.explosionCount = Math.Max(1, Props.explosionCount);
            bombardment.randomFireRadius = Math.Max(1, Props.randomFireRadius);
            bombardment.instigator = parent;
            bombardment.weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_Gun_GoauldHeavyPlasmaCannon") ?? parent.def;

            nextFireTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(0, Props.cooldownTicks);
            SoundDefOf.OrbitalStrike_Ordered.PlayOneShotOnCamera();
            Messages.Message(
                "Ha'tak orbital bombardment ordered.",
                new TargetInfo(targetCell, targetMap),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
            return true;
        }

        public override string CompInspectStringExtra()
        {
            if (parent?.Spawned != true)
                return null;

            int remaining = nextFireTick - (Find.TickManager?.TicksGame ?? 0);
            if (remaining > 0)
                return "Orbital bombardment: cooling down (" + remaining.ToStringTicksToPeriod() + ")";

            if (parent.Map?.Tile.LayerDef != PlanetLayerDefOf.Orbit)
                return "Orbital bombardment: requires orbit";

            return "Orbital bombardment: ready";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextFireTick, "wngHatakOrbitalBombardmentNextFireTick", 0);
        }
    }
}
