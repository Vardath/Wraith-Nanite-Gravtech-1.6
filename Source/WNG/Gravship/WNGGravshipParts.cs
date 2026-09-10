using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WNGGravshipPartTheme : CompProperties
    {
        public WNGGravshipTheme theme = WNGGravshipTheme.None;

        public CompProperties_WNGGravshipPartTheme()
        {
            compClass = typeof(CompWNGGravshipPartTheme);
        }
    }

    /// <summary>
    /// Theme state for native Odyssey gravship parts that WNG deliberately keeps as their real
    /// vanilla Defs (notably GravshipHull). Static WNG facilities can also use this comp so mixed
    /// gravships remain inspectable and future art/repair systems can distinguish technologies.
    /// </summary>
    public sealed class CompWNGGravshipPartTheme : ThingComp
    {
        private bool hasRuntimeTheme;
        private WNGGravshipTheme runtimeTheme;

        private CompProperties_WNGGravshipPartTheme Props => (CompProperties_WNGGravshipPartTheme)props;

        public WNGGravshipTheme Theme => hasRuntimeTheme ? runtimeTheme : Props.theme;

        public void SetTheme(WNGGravshipTheme theme)
        {
            runtimeTheme = theme;
            hasRuntimeTheme = true;
        }

        public override string CompInspectStringExtra()
        {
            switch (Theme)
            {
                case WNGGravshipTheme.Wraith:
                    return "WNG architecture: Wraith living-ship component";
                case WNGGravshipTheme.Asuran:
                    return "WNG architecture: Asuran nanite-ship component";
                case WNGGravshipTheme.Goauld:
                    return "WNG architecture: Goa'uld ship component";
                default:
                    return null;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hasRuntimeTheme, "wngHasRuntimeGravshipTheme", false);
            Scribe_Values.Look(ref runtimeTheme, "wngRuntimeGravshipTheme", WNGGravshipTheme.None);
        }
    }

    public sealed class CompProperties_WNGGravHullSeed : CompProperties
    {
        public WNGGravshipTheme theme = WNGGravshipTheme.None;

        public CompProperties_WNGGravHullSeed()
        {
            compClass = typeof(CompWNGGravHullSeed);
        }
    }

    /// <summary>
    /// Construction bridge for faction-themed hull. RimWorld 1.6's SectionLayer_GravshipHull
    /// explicitly checks ThingDefOf.GravshipHull before it draws angled/cut corners. A parallel WNG
    /// wall Def would therefore lose native Odyssey hull geometry. The seed converts to the exact
    /// vanilla hull and records the WNG technology theme on that native wall.
    /// </summary>
    public sealed class CompWNGGravHullSeed : ThingComp
    {
        private bool converted;

        private CompProperties_WNGGravHullSeed Props => (CompProperties_WNGGravHullSeed)props;

        public override void CompTick()
        {
            base.CompTick();
            if (converted || parent?.Spawned != true || parent.Map == null)
                return;

            if (!ModsConfig.OdysseyActive || ThingDefOf.GravshipHull == null)
            {
                converted = true;
                Log.Error("[WNG] WNG gravship hull seed cannot convert because Odyssey GravshipHull is unavailable.");
                return;
            }

            Map map = parent.Map;
            IntVec3 position = parent.Position;
            Rot4 rotation = parent.Rotation;
            Faction faction = parent.Faction;

            converted = true;
            parent.Destroy(DestroyMode.Vanish);

            Thing hull = ThingMaker.MakeThing(ThingDefOf.GravshipHull, ThingDefOf.Steel);
            if (hull == null)
            {
                Log.Error("[WNG] Could not create the native Odyssey GravshipHull from a WNG hull seed.");
                return;
            }

            if (faction != null)
                hull.SetFactionDirect(faction);

            Thing spawned = GenSpawn.Spawn(hull, position, map, rotation, WipeMode.Vanish);
            spawned.TryGetComp<CompWNGGravshipPartTheme>()?.SetTheme(Props.theme);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref converted, "wngHullSeedConverted", false);
        }
    }

    public sealed class CompProperties_WraithHullRegenerator : CompProperties
    {
        public int pulseIntervalTicks = 600;
        public int hitPointsPerPulse = 3;
        public int maxHullCellsPerPulse = 8;

        public CompProperties_WraithHullRegenerator()
        {
            compClass = typeof(CompWraithHullRegenerator);
        }
    }

    /// <summary>
    /// Wraith defensive analogue to a conventional gravship shield. Stargate Wraith capital ships
    /// are living vessels whose hull can regenerate; they are not being given a generic energy-
    /// shield reskin. This node repairs Wraith-themed native GravshipHull cells on its own connected
    /// Odyssey gravship. It does not create immunity and it cannot repair a disconnected ship.
    /// </summary>
    public sealed class CompWraithHullRegenerator : ThingComp
    {
        private int nextPulseTick;
        private int cursor;

        private CompProperties_WraithHullRegenerator Props => (CompProperties_WraithHullRegenerator)props;

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Spawned != true || parent.Map == null || Find.TickManager.TicksGame < nextPulseTick)
                return;

            nextPulseTick = Find.TickManager.TicksGame + Math.Max(60, Props.pulseIntervalTicks);

            CompGravshipFacility facility = parent.GetComp<CompGravshipFacility>();
            Building_GravEngine engine = facility?.engine;
            if (facility == null || engine == null || !facility.CanBeActive || !engine.Spawned)
                return;

            List<Thing> hulls = engine.Map.listerThings.ThingsOfDef(ThingDefOf.GravshipHull);
            if (hulls == null || hulls.Count == 0)
                return;

            int repaired = 0;
            int checkedCount = 0;
            int index = cursor % hulls.Count;

            while (checkedCount < hulls.Count && repaired < Math.Max(1, Props.maxHullCellsPerPulse))
            {
                Thing hull = hulls[index];
                index = (index + 1) % hulls.Count;
                checkedCount++;

                if (hull?.Spawned != true || !engine.ValidSubstructureAt(hull.Position))
                    continue;

                CompWNGGravshipPartTheme theme = hull.TryGetComp<CompWNGGravshipPartTheme>();
                if (theme == null || theme.Theme != WNGGravshipTheme.Wraith)
                    continue;

                if (hull.HitPoints >= hull.MaxHitPoints)
                    continue;

                hull.HitPoints = Math.Min(hull.MaxHitPoints, hull.HitPoints + Math.Max(1, Props.hitPointsPerPulse));
                repaired++;
            }

            cursor = index;
        }

        public override string CompInspectStringExtra()
        {
            CompGravshipFacility facility = parent.GetComp<CompGravshipFacility>();
            if (facility?.engine == null)
                return "Living hull regeneration: disconnected";
            if (!facility.CanBeActive)
                return "Living hull regeneration: inactive";
            return "Living hull regeneration: active";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextPulseTick, "wngWraithHullRegenNextPulse", 0);
            Scribe_Values.Look(ref cursor, "wngWraithHullRegenCursor", 0);
        }
    }
}
