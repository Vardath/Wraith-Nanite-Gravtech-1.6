using System;
using System.Collections.Generic;
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
    /// Theme marker attached to Odyssey's real GravEngine. RimWorld 1.6 hard-codes the exact native
    /// engine Def in critical discovery, substructure and launch paths, so WNG themes that engine
    /// rather than substituting a parallel fake one.
    ///
    /// Odyssey's facility linking is Def-based and has no concept of WNG technology families. Once
    /// an engine is themed, linked gravship facilities therefore normally need the same WNG theme.
    /// The one deliberate exception is Odyssey's native PilotSubpersonaCore: it is a rare, shared
    /// quest-reward flight-assist module rather than a fuel/control/thruster technology family, so
    /// WNG themed ships may use it without turning it into a constructible faction reskin.
    /// </summary>
    public sealed class CompWNGGravEngineTheme : ThingComp
    {
        private const string SharedPilotSubpersonaCoreDefName = "PilotSubpersonaCore";

        private WNGGravshipTheme theme;

        public WNGGravshipTheme Theme => theme;

        public void SetTheme(WNGGravshipTheme newTheme)
        {
            theme = newTheme;
            EnforceFamilyLinks();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (theme != WNGGravshipTheme.None && parent?.Spawned == true && parent.IsHashIntervalTick(60))
                EnforceFamilyLinks();
        }

        public void EnforceFamilyLinks()
        {
            Building_GravEngine engine = parent as Building_GravEngine;
            if (engine?.Spawned != true || theme == WNGGravshipTheme.None)
                return;

            CompAffectedByFacilities affected = engine.AffectedByFacilities;
            if (affected == null || affected.LinkedFacilitiesListForReading.NullOrEmpty())
                return;

            List<Thing> linked = affected.LinkedFacilitiesListForReading.ToList();
            foreach (Thing facilityThing in linked)
            {
                if (facilityThing?.TryGetComp<CompGravshipFacility>() == null)
                    continue;

                CompWNGGravshipPartTheme partTheme = facilityThing.TryGetComp<CompWNGGravshipPartTheme>();
                if (partTheme != null && partTheme.Theme == theme)
                    continue;

                if (IsSharedNativeFacility(facilityThing))
                    continue;

                CompFacility facility = facilityThing.TryGetComp<CompFacility>();
                if (facility != null && facility.LinkedBuildings.Contains(engine))
                    facility.Notify_LinkRemoved(engine);

                if (affected.LinkedFacilitiesListForReading.Contains(facilityThing))
                    affected.Notify_LinkRemoved(facilityThing);
            }
        }

        private static bool IsSharedNativeFacility(Thing facilityThing)
        {
            return facilityThing?.def?.defName == SharedPilotSubpersonaCoreDefName;
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
    /// Construction-only bridge. A researched WNG engine seed can produce the real vanilla Odyssey
    /// GravEngine and silently inspect it. Wraith normally reaches this seed through biological
    /// growth; other technology families may expose their seed through their own progression.
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
