using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Explicit WNG testing surface. Vanilla's generic Spawn thing menu remains usable, but this
    /// menu keeps WNG structures/resources discoverable during development and provides dedicated
    /// themed native-GravEngine actions because WNG intentionally does not define fake replacement
    /// GravEngine ThingDefs.
    /// </summary>
    public static class WNGDebugActions
    {
        [DebugAction("WNG", "Spawn WNG object", false, false, false, false, false, 0, false,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnWNGObject()
        {
            List<DebugActionNode> result = new List<DebugActionNode>();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefs
                         .Where(IsWNGSpawnableThing)
                         .OrderBy(d => d.defName))
            {
                ThingDef localDef = def;
                result.Add(new DebugActionNode(localDef.defName, DebugActionType.ToolMap)
                {
                    category = DebugCategory(localDef),
                    action = () => DebugThingPlaceHelper.DebugSpawn(localDef, UI.MouseCell())
                });
            }
            return result;
        }

        [DebugAction("WNG", "Spawn Wraith grav engine", false, false, false, false, false, 0, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnWraithGravEngine()
        {
            SpawnThemedNativeGravEngine(WNGGravshipTheme.Wraith, "Wraith living grav engine");
        }

        [DebugAction("WNG", "Spawn Asuran grav engine", false, false, false, false, false, 0, false,
            actionType = DebugActionType.ToolMap,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnAsuranGravEngine()
        {
            SpawnThemedNativeGravEngine(WNGGravshipTheme.Asuran, "Asuran nanite grav engine");
        }

        private static bool IsWNGSpawnableThing(ThingDef def)
        {
            if (def == null || def.thingClass == null || def.race != null)
                return false;
            if (def.defName.NullOrEmpty() || !def.defName.StartsWith("WNG_", StringComparison.Ordinal))
                return false;
            return true;
        }

        private static string DebugCategory(ThingDef def)
        {
            if (def.category == ThingCategory.Building)
                return "Buildings";
            if (def.IsApparel)
                return "Apparel";
            if (def.IsWeapon)
                return "Weapons";
            return "Items / other";
        }

        private static void SpawnThemedNativeGravEngine(WNGGravshipTheme theme, string engineName)
        {
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map))
                return;

            if (map.listerThings.ThingsOfDef(ThingDefOf.GravEngine).Any())
            {
                Messages.Message(
                    "This map already contains the native Odyssey grav engine. WNG uses that exact engine Def, so a second themed engine is not spawned.",
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }

            Building_GravEngine engine = ThingMaker.MakeThing(ThingDefOf.GravEngine) as Building_GravEngine;
            if (engine == null)
            {
                Log.Error("[WNG] Dev spawn could not create Odyssey's native GravEngine.");
                return;
            }

            engine.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(engine, cell, map, Rot4.North, WipeMode.Vanish);
            engine.TryGetComp<CompWNGGravEngineTheme>()?.SetTheme(theme);
            engine.RenamableLabel = engineName;
            engine.Inspect(silent: true);
        }
    }
}
