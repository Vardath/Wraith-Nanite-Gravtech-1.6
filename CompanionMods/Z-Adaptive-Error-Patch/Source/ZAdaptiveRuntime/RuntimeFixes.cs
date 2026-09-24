using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
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
