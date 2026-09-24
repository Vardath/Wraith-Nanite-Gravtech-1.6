# Z Adaptive Error Patch

Late-loading RimWorld 1.6 compatibility and error-cancellation layer for Vardath's personal modlist. Source is maintained as a companion mod inside the Wraith-Nanite-Gravtech-1.6 repository.

This mod is deliberately separate from Wraith & Nanite Gravtech and from upstream mods. It exists to absorb safe-to-repair incompatibilities in the active mod stack without editing each source mod directly.

## Current repair layers

- Guarded XML/schema/cross-reference cleanup in `Patches/AdaptiveKnownFixes.xml`.
- Runtime compatibility assembly in `Assemblies/ZAdaptiveRuntime.dll` for failures that cannot be corrected safely through Def XML alone.
- Independent per-mod startup profiler. RimDoctor is optional.

## Per-mod startup profiling

Z Adaptive installs its own startup instrumentation from its `Mod` constructor and does not reference the RimDoctor assembly at compile time.

It attributes measurable startup work to each active mod in these buckets:

- XML/Def file loading through `ModContentPack.LoadDefs`
- individual Def deserialization
- top-level `PatchOperation.Apply` cost
- `[StaticConstructorOnStartup]` execution time, attributed by assembly ownership

At the end of static-constructor startup it writes the slowest measured mods to `Player.log` even when RimDoctor is not installed.

If RimDoctor **is** installed, Z Adaptive detects `RimDoctor.ReportBuilder` by reflection and appends a `Z Adaptive per-mod startup cost` section to **Diagnostics -> Save report**. The table includes measured milliseconds by bucket, Def count, assembly count, and an on-demand installed-folder size/file-count scan so the report can identify both the largest installed mod and the highest measured startup cost.

The report explicitly separates attributed timings from RimWorld startup work that occurred before Z Adaptive could arm its profiler or that RimWorld performs as shared unified-XML/inheritance work. Unmeasured work is not falsely charged to a mod.

## Gravship rendering guards

The runtime layer currently addresses two verified RimWorld/VGE interactions:

1. **Vanilla Gravship Expanded chroma-key material misuse**
   - VGE assigns Odyssey's `GravshipChromaKey` screen-overlay material to `VGE_FakeTerrain` as a normal `Graphic_Single` material.
   - That shader deliberately lacks ordinary `_MainTex` / `_Color` properties.
   - Z Adaptive prevents the invalid chroma-key material from being inserted into the normal static texture atlas and replaces VGE's direct `Material.color` read in the current `LandingStructureBase.RenderAndSaveTexture` path with a guarded fallback.
   - Older VGE builds are still supported through the previous `LandingStructure.DoCapture` fallback target.

2. **RimWorld 1.6 static-atlas mip-count mismatch**
   - `StaticTextureAtlas.CalcRectsForAtlasNew` allocates mip levels from atlas width only.
   - `GenerateMipmapsWithCompute` later decides how many mip levels to populate from the larger of width and height.
   - Tall/non-square atlases can therefore attempt to write destination mip 2 when only mips 0 and 1 were allocated.
   - Z Adaptive corrects the empty atlas texture's mip allocation before the atlas is populated.

## Live-stack compatibility guards

3. **Vehicle Framework × GravTide early terrain-pathing race**
   - GravTide repaints tidal/foreshore terrain while a map is still being initialized.
   - Vehicle Framework's `TerrainGrid.DoTerrainChangedEffects` hook immediately calls `Vehicles.PathingHelper.RecalculatePerceivedPathCostAt`.
   - On the current stack this can run before `VehiclePathingSystem` has been added to the map, producing a repeated `NullReferenceException` from Vehicle Framework/SmashTools during GravTide terrain painting.
   - Z Adaptive now lets the refresh run normally once the vehicle pathing component exists, but skips only the premature refreshes before that component is available. Vehicle Framework then builds its path grids from the completed terrain during its normal initialization.

4. **Auto Name Babies new-game collection mutation**
   - `AutoNameBabies.BabyNamer.NameUnnamedPlayerBabies` can throw `InvalidOperationException: Collection was modified; enumeration operation may not execute` from its `Game.FinalizeInit` postfix.
   - Naming is optional, so Z Adaptive suppresses only that exact collection-modified exception and allows new-game initialization to continue. Other exceptions from the mod are not swallowed.

All runtime patches are narrowly targeted and fail open: if a target class/method is absent because a mod was removed or updated, normal game behavior continues.

Validated startup-profiler CI build: workflow run `34085998839` — SUCCESS.
