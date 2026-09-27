using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Verse;

namespace ZAdaptiveRuntime
{
    [StaticConstructorOnStartup]
    public static class ZAdaptiveRuntimeBootstrap
    {
        private static Type vehiclePathingSystemType;
        private static bool vehiclePathingDeferredLogged;
        private static bool vehiclePathingGuardFailureLogged;
        private static bool autoNameBabiesSuppressedLogged;

        static ZAdaptiveRuntimeBootstrap()
        {
            var harmony = new Harmony("vardath.adaptiveerrorpatch.runtime");
            PatchStaticAtlas(harmony);
            PatchGraphicSingleAtlasInsertion(harmony);
            PatchVanillaGravshipExpanded(harmony);
            PatchVehicleFrameworkGravTide(harmony);
            PatchGeologicalLandformsGravTide(harmony);
            PatchAutoNameBabies(harmony);
        }

        private static void PatchStaticAtlas(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(StaticTextureAtlas), "CalcRectsForAtlasNew");
            MethodInfo postfix = AccessTools.Method(typeof(ZAdaptiveRuntimeBootstrap), nameof(StaticAtlasPostfix));
            if (target != null && postfix != null)
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            else
                Log.Warning("[Z Adaptive] Could not locate StaticTextureAtlas.CalcRectsForAtlasNew; mip-allocation guard not applied.");
        }

        private static void PatchGraphicSingleAtlasInsertion(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(Graphic_Single), nameof(Graphic_Single.TryInsertIntoAtlas));
            MethodInfo prefix = AccessTools.Method(typeof(ZAdaptiveRuntimeBootstrap), nameof(GraphicSingleAtlasPrefix));
            if (target != null && prefix != null)
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        }

        private static void PatchVanillaGravshipExpanded(Harmony harmony)
        {
            Type landingBaseType = AccessTools.TypeByName("VanillaGravshipExpanded.LandingStructureBase");
            Type landingType = AccessTools.TypeByName("VanillaGravshipExpanded.LandingStructure");
            if (landingBaseType == null && landingType == null)
                return;

            // Current VGE performs the chroma-key Material.color read in
            // LandingStructureBase.RenderAndSaveTexture.  Older builds used a DoCapture path,
            // so keep the fallback for backwards compatibility with the user's long-lived stack.
            MethodInfo capturePath = landingBaseType == null
                ? null
                : AccessTools.Method(landingBaseType, "RenderAndSaveTexture");
            if (capturePath == null && landingType != null)
                capturePath = AccessTools.Method(landingType, "DoCapture");

            MethodInfo transpiler = AccessTools.Method(typeof(ZAdaptiveRuntimeBootstrap), nameof(VgeDoCaptureTranspiler));
            if (capturePath != null && transpiler != null)
                harmony.Patch(capturePath, transpiler: new HarmonyMethod(transpiler));
            else
                Log.Warning("[Z Adaptive] Vanilla Gravship Expanded is loaded but its landing capture render path could not be patched.");
        }

        private static void PatchVehicleFrameworkGravTide(Harmony harmony)
        {
            Type pathingHelperType = AccessTools.TypeByName("Vehicles.PathingHelper");
            Type gravTideType = AccessTools.TypeByName("GravTide.TidalPainter");
            if (pathingHelperType == null || gravTideType == null)
                return;

            vehiclePathingSystemType = AccessTools.TypeByName("Vehicles.VehiclePathingSystem");
            MethodInfo target = AccessTools.Method(
                pathingHelperType,
                "RecalculatePerceivedPathCostAt",
                new[] { typeof(IntVec3), typeof(Map) });
            MethodInfo prefix = AccessTools.Method(
                typeof(ZAdaptiveRuntimeBootstrap),
                nameof(VehiclePathingReadyPrefix));

            if (target != null && prefix != null && vehiclePathingSystemType != null)
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            else
                Log.Warning("[Z Adaptive] Vehicle Framework + GravTide detected, but the early-map vehicle pathing guard could not be installed.");
        }

        private static void PatchGeologicalLandformsGravTide(Harmony harmony)
        {
            Type gravLightningPatchType = AccessTools.TypeByName("GravTide.WeatherEvent_LightningStrike_FireEvent_Patch");
            Type gravGrowthPatchType = AccessTools.TypeByName("GravTide.BuildFor_Patch");
            Type landformsLightningPatchType = AccessTools.TypeByName("GeologicalLandforms.Patches.Patch_RimWorld_WeatherEvent_LightningStrike");
            Type landformsGrowthPatchType = AccessTools.TypeByName("GeologicalLandforms.Patches.Patch_Verse_MapPlantGrowthRateCalculator");

            if ((gravLightningPatchType == null && gravGrowthPatchType == null) ||
                (landformsLightningPatchType == null && landformsGrowthPatchType == null))
                return;

            PatchGeologicalLandformsGravTideLightning(
                harmony,
                gravLightningPatchType,
                landformsLightningPatchType);

            PatchGeologicalLandformsGravTidePlantGrowth(
                harmony,
                gravGrowthPatchType,
                landformsGrowthPatchType);
        }

        private static void PatchGeologicalLandformsGravTideLightning(
            Harmony harmony,
            Type gravLightningPatchType,
            Type landformsLightningPatchType)
        {
            if (gravLightningPatchType == null || landformsLightningPatchType == null)
                return;

            try
            {
                MethodInfo target = AccessTools.Method(typeof(WeatherEvent_LightningStrike), "FireEvent");
                MethodInfo gravPrefix = AccessTools.Method(gravLightningPatchType, "Prefix");
                MethodInfo landformsPrefix = AccessTools.Method(landformsLightningPatchType, "FireEvent");
                MethodInfo compatPrefix = AccessTools.Method(
                    typeof(ZAdaptiveRuntimeBootstrap),
                    nameof(GeologicalLandformsLightningPrefix));

                if (target == null || gravPrefix == null || landformsPrefix == null || compatPrefix == null)
                {
                    Log.Warning("[Z Adaptive] Geological Landforms + GravTide lightning compatibility targets were not all found.");
                    return;
                }

                HarmonyMethod compat = new HarmonyMethod(compatPrefix)
                {
                    priority = Priority.First
                };

                Patches patchInfo = Harmony.GetPatchInfo(target);
                if (patchInfo != null)
                {
                    foreach (Patch patch in patchInfo.Prefixes)
                    {
                        if (patch.PatchMethod == gravPrefix && !string.IsNullOrEmpty(patch.owner))
                        {
                            compat.before = new[] { patch.owner };
                            break;
                        }
                    }
                }

                // Move Geological Landforms' filter into Z Adaptive so it is guaranteed to run
                // before GravTide's bool-returning prefix.  If the landform filter suppresses the
                // event, GravTide has nothing to process; otherwise GravTide retains full control.
                harmony.Patch(target, prefix: compat);
                harmony.Unpatch(target, landformsPrefix);

                Log.Message("[Z Adaptive] Installed Geological Landforms + GravTide lightning arbitration.");
            }
            catch (Exception ex)
            {
                Log.Warning("[Z Adaptive] Geological Landforms + GravTide lightning compatibility failed open: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void PatchGeologicalLandformsGravTidePlantGrowth(
            Harmony harmony,
            Type gravGrowthPatchType,
            Type landformsGrowthPatchType)
        {
            if (gravGrowthPatchType == null || landformsGrowthPatchType == null)
                return;

            try
            {
                MethodInfo target = AccessTools.Method(
                    typeof(MapPlantGrowthRateCalculator),
                    "BuildFor",
                    new[] { typeof(Map) });
                MethodInfo gravPrefix = AccessTools.Method(gravGrowthPatchType, "Prefix");
                MethodInfo landformsTranspiler = AccessTools.Method(landformsGrowthPatchType, "BuildFor_Transpiler");
                MethodInfo compatTranspiler = AccessTools.Method(
                    typeof(ZAdaptiveRuntimeBootstrap),
                    nameof(GeologicalLandformsTileCompatTranspiler));

                if (target == null || gravPrefix == null || landformsTranspiler == null || compatTranspiler == null)
                {
                    Log.Warning("[Z Adaptive] Geological Landforms + GravTide plant-growth compatibility targets were not all found.");
                    return;
                }

                // Geological Landforms replaces Map.Tile with the source tile for pocket maps.
                // GravTide can skip the original BuildFor method, so the original transpiler alone
                // cannot protect GravTide's override path.  Apply the same tile substitution to
                // both paths, then remove only the upstream transpiler that we have superseded.
                HarmonyMethod compat = new HarmonyMethod(compatTranspiler);
                harmony.Patch(target, transpiler: compat);
                harmony.Patch(gravPrefix, transpiler: compat);
                harmony.Unpatch(target, landformsTranspiler);

                Log.Message("[Z Adaptive] Installed Geological Landforms + GravTide pocket-map plant-growth compatibility.");
            }
            catch (Exception ex)
            {
                Log.Warning("[Z Adaptive] Geological Landforms + GravTide plant-growth compatibility failed open: " +
                    ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static bool GeologicalLandformsLightningPrefix(Map ___map)
        {
            if (___map == null)
                return true;

            return ___map.TileInfo.hilliness != Hilliness.Impassable || Rand.Value < 0.3f;
        }

        private static IEnumerable<CodeInstruction> GeologicalLandformsTileCompatTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getTile = AccessTools.PropertyGetter(typeof(Map), nameof(Map.Tile));
            MethodInfo safeTile = AccessTools.Method(
                typeof(ZAdaptiveRuntimeBootstrap),
                nameof(TileForMapCompat));

            foreach (CodeInstruction instruction in instructions)
            {
                if (getTile != null && safeTile != null && instruction.Calls(getTile))
                {
                    // Mutate the existing instruction so Harmony labels/exception blocks remain
                    // attached to the exact IL position.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = safeTile;
                }

                yield return instruction;
            }
        }

        private static PlanetTile TileForMapCompat(Map map)
        {
            return map.Tile < 0 &&
                   map.Parent is PocketMapParent parent &&
                   parent.sourceMap != null &&
                   parent.sourceMap.Tile >= 0
                ? parent.sourceMap.Tile
                : map.Tile;
        }

        private static void PatchAutoNameBabies(Harmony harmony)
        {
            Type babyNamerType = AccessTools.TypeByName("AutoNameBabies.BabyNamer");
            if (babyNamerType == null)
                return;

            MethodInfo target = AccessTools.Method(babyNamerType, "NameUnnamedPlayerBabies");
            MethodInfo finalizer = AccessTools.Method(
                typeof(ZAdaptiveRuntimeBootstrap),
                nameof(AutoNameBabiesFinalizer));

            if (target != null && finalizer != null)
                harmony.Patch(target, finalizer: new HarmonyMethod(finalizer));
            else
                Log.Warning("[Z Adaptive] Auto Name Babies is loaded but its new-game naming guard could not be installed.");
        }

        private static bool VehiclePathingReadyPrefix(Map map)
        {
            // GravTide paints and replaces terrain during map initialization. Vehicle Framework's
            // TerrainGrid hook immediately asks its cached VehiclePathingSystem to refresh costs,
            // but that map component may not exist yet. Skipping those pre-initialization refreshes
            // is safe because Vehicle Framework builds its path grids from the finished terrain once
            // VehiclePathingSystem is constructed.
            if (map == null || map.Disposed)
                return false;

            try
            {
                List<MapComponent> components = map.components;
                if (components != null)
                {
                    for (int i = 0; i < components.Count; i++)
                    {
                        MapComponent component = components[i];
                        if (component != null && vehiclePathingSystemType.IsInstanceOfType(component))
                            return true;
                    }
                }

                if (!vehiclePathingDeferredLogged)
                {
                    vehiclePathingDeferredLogged = true;
                    Log.Message("[Z Adaptive] Deferred Vehicle Framework terrain path-cost refresh until VehiclePathingSystem is initialized (GravTide map setup).");
                }
                return false;
            }
            catch (Exception ex)
            {
                // If the compatibility probe itself ever becomes stale, fail open and preserve the
                // upstream behavior rather than disabling vehicle pathing.
                if (!vehiclePathingGuardFailureLogged)
                {
                    vehiclePathingGuardFailureLogged = true;
                    Log.Warning("[Z Adaptive] Vehicle Framework + GravTide pathing readiness probe failed open: " + ex.GetType().Name + ": " + ex.Message);
                }
                return true;
            }
        }

        private static Exception AutoNameBabiesFinalizer(Exception __exception)
        {
            if (__exception is InvalidOperationException &&
                __exception.Message != null &&
                __exception.Message.IndexOf("Collection was modified", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!autoNameBabiesSuppressedLogged)
                {
                    autoNameBabiesSuppressedLogged = true;
                    Log.Warning("[Z Adaptive] Auto Name Babies modified its pawn collection while iterating during Game.FinalizeInit; suppressed that optional naming pass so new-game initialization can continue.");
                }
                return null;
            }

            return __exception;
        }

        private static bool GraphicSingleAtlasPrefix(Graphic_Single __instance)
        {
            try
            {
                Material mat = __instance?.MatSingle;
                if (mat != null && mat.name == "GravshipChromaKey" && !mat.HasProperty("_MainTex"))
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[Z Adaptive] Gravship chroma-key atlas guard failed safely: " + ex.GetType().Name + ": " + ex.Message);
            }
            return true;
        }

        private static void StaticAtlasPostfix(StaticTextureAtlas __instance)
        {
            try
            {
                FieldInfo field = AccessTools.Field(typeof(StaticTextureAtlas), "colorTexture");
                Texture2D current = field?.GetValue(__instance) as Texture2D;
                if (current == null)
                    return;

                int maxAxis = Math.Max(current.width, current.height);
                int requiredMipCount = Mathf.FloorToInt(Mathf.Log(Math.Max(1f, maxAxis / 512f), 2f)) + 1;
                requiredMipCount = Math.Max(1, requiredMipCount);
                if (current.mipmapCount >= requiredMipCount)
                    return;

                Texture2D replacement = new Texture2D(
                    current.width,
                    current.height,
                    current.graphicsFormat,
                    requiredMipCount,
                    TextureCreationFlags.MipChain | TextureCreationFlags.DontInitializePixels);
                replacement.name = current.name;
                replacement.filterMode = current.filterMode;
                replacement.wrapMode = current.wrapMode;
                replacement.anisoLevel = current.anisoLevel;
                replacement.mipMapBias = current.mipMapBias;
                field.SetValue(__instance, replacement);
                UnityEngine.Object.DestroyImmediate(current);
                Log.Message($"[Z Adaptive] Corrected static texture atlas mip allocation to {requiredMipCount} level(s) for {replacement.width}x{replacement.height} atlas.");
            }
            catch (Exception ex)
            {
                Log.Warning("[Z Adaptive] Static atlas mip-allocation guard failed safely: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static IEnumerable<CodeInstruction> VgeDoCaptureTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getColor = AccessTools.PropertyGetter(typeof(Material), nameof(Material.color));
            MethodInfo safeColor = AccessTools.Method(typeof(ZAdaptiveRuntimeBootstrap), nameof(SafeMaterialColor));

            foreach (CodeInstruction instruction in instructions)
            {
                if (getColor != null && safeColor != null && instruction.Calls(getColor))
                {
                    yield return new CodeInstruction(OpCodes.Call, safeColor);
                    continue;
                }
                yield return instruction;
            }
        }

        public static Color SafeMaterialColor(Material material)
        {
            if (material == null)
                return Color.clear;

            if (material.name == "GravshipChromaKey" && !material.HasProperty("_Color"))
                return Color.clear;

            return material.color;
        }
    }
}
