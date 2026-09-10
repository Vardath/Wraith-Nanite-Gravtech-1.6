using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public enum WNGGravshipTheme
    {
        None,
        Wraith,
        Asuran,
        Goauld
    }

    public sealed class CompProperties_WNGGravEngineTheme : CompProperties
    {
        public CompProperties_WNGGravEngineTheme()
        {
            compClass = typeof(CompWNGGravEngineTheme);
        }
    }

    /// <summary>
    /// Theme marker attached by XML to the real Odyssey GravEngine Def. Keeping the runtime Thing
    /// as ThingDefOf.GravEngine is deliberate: RimWorld 1.6 still hard-codes that exact Def in
    /// player-engine discovery, substructure overlays and several placement checks. WNG therefore
    /// themes the native engine rather than substituting an incompatible parallel engine Def.
    /// </summary>
    public sealed class CompWNGGravEngineTheme : ThingComp
    {
        private WNGGravshipTheme theme;

        public WNGGravshipTheme Theme => theme;

        public void SetTheme(WNGGravshipTheme newTheme)
        {
            theme = newTheme;
        }

        public override string CompInspectStringExtra()
        {
            switch (theme)
            {
                case WNGGravshipTheme.Wraith:
                    return "WNG architecture: Wraith living grav engine";
                case WNGGravshipTheme.Asuran:
                    return "WNG architecture: Asuran nanite grav engine";
                case WNGGravshipTheme.Goauld:
                    return "WNG architecture: Goa'uld grav engine";
                default:
                    return null;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref theme, "wngGravshipTheme", WNGGravshipTheme.None);
        }
    }

    public sealed class CompProperties_WNGGravEngineSeed : CompProperties
    {
        public WNGGravshipTheme theme = WNGGravshipTheme.None;
        public string engineName;

        public CompProperties_WNGGravEngineSeed()
        {
            compClass = typeof(CompWNGGravEngineSeed);
        }
    }

    /// <summary>
    /// Construction-only bridge. A researched WNG engine seed is a normal Architect buildable, but
    /// on completion it becomes the real vanilla Odyssey GravEngine and is silently inspected.
    /// This preserves every native gravship lookup and launch path without Harmony or a replacement
    /// gravship framework.
    /// </summary>
    public sealed class CompWNGGravEngineSeed : ThingComp
    {
        private bool converted;

        private CompProperties_WNGGravEngineSeed Props => (CompProperties_WNGGravEngineSeed)props;

        public override void CompTick()
        {
            base.CompTick();
            if (converted || parent?.Spawned != true || parent.Map == null)
                return;

            Map map = parent.Map;
            IntVec3 position = parent.Position;
            Rot4 rotation = parent.Rotation;
            Faction faction = parent.Faction;

            Building_GravEngine existing = map.listerThings
                .ThingsOfDef(ThingDefOf.GravEngine)
                .OfType<Building_GravEngine>()
                .FirstOrDefault();

            if (existing != null)
            {
                // Do not destroy paid construction if another engine appeared after the blueprint
                // was placed. Leave the seed as a recoverable inert structure rather than creating
                // the unsupported multi-engine state vanilla itself is not designed around.
                converted = true;
                Messages.Message("A grav engine already exists on this map. This WNG engine seed will remain inert until deconstructed.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            converted = true;
            parent.Destroy(DestroyMode.Vanish);

            Building_GravEngine engine = ThingMaker.MakeThing(ThingDefOf.GravEngine) as Building_GravEngine;
            if (engine == null)
            {
                Log.Error("[WNG] Could not create the native Odyssey GravEngine from a WNG engine seed.");
                return;
            }

            if (faction != null)
                engine.SetFactionDirect(faction);

            GenSpawn.Spawn(engine, position, map, rotation, WipeMode.Vanish);
            engine.TryGetComp<CompWNGGravEngineTheme>()?.SetTheme(Props.theme);

            if (!string.IsNullOrWhiteSpace(Props.engineName))
                engine.RenamableLabel = Props.engineName;

            // Constructed WNG engines are researched technology, not the Odyssey discovery quest.
            engine.Inspect(silent: true);
        }

        public override string CompInspectStringExtra()
        {
            if (converted && parent?.Spawned == true)
                return "Engine seed is inert because this map already contains a grav engine.";
            return null;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref converted, "wngEngineSeedConverted", false);
        }
    }

    public sealed class PlaceWorker_WNGSingleGravEngine : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (map == null)
                return false;

            if (map.listerThings.AllThings.Any(t => t != thingToIgnore && t.GetInnerIfMinified()?.def == ThingDefOf.GravEngine))
                return "Only one grav engine can be active on a map.";

            foreach (Thing t in map.listerThings.AllThings)
            {
                if (t == thingToIgnore)
                    continue;
                BuildableDef target = t.def?.entityDefToBuild;
                if (target is ThingDef targetThing && targetThing.GetCompProperties<CompProperties_WNGGravEngineSeed>() != null)
                    return "Another WNG grav engine is already planned or under construction on this map.";
            }

            return true;
        }
    }
}
