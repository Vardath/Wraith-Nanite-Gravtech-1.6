# Z Adaptive Error Patch

Private late-loading RimWorld 1.6 compatibility and error-cancellation layer for Vardath's personal modlist.

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

4. **Geological Landforms × GravTide Harmony arbitration**
   - Both mods patch lightning strikes and plant-growth tile lookup. GravTide uses bool-returning prefixes that can skip the vanilla methods, while Geological Landforms relies on its own lightning prefix and a `BuildFor` transpiler.
   - Lightning: Z Adaptive preserves both mods' native prefixes. It no longer removes Geological Landforms' patch or installs a second probabilistic filter; that would alter strike odds and trigger a destructive-patch warning. Any remaining upstream ordering conflict requires a verified version-specific fix.
   - For plant-growth calculation, Z Adaptive applies Geological Landforms' pocket-map source-tile substitution to both vanilla `MapPlantGrowthRateCalculator.BuildFor` and GravTide's override prefix, so GravTide cannot bypass the landform-safe tile lookup.
   - The upstream Geological Landforms patches remain installed; the pocket-map override receives an additional targeted transpiler without replacing the original.

5. **Auto Name Babies new-game collection mutation**
   - `AutoNameBabies.BabyNamer.NameUnnamedPlayerBabies` can throw `InvalidOperationException: Collection was modified; enumeration operation may not execute` from its `Game.FinalizeInit` postfix.
   - Naming is optional, so Z Adaptive suppresses only that exact collection-modified exception and allows new-game initialization to continue. Other exceptions from the mod are not swallowed.


6. **RimWorld 1.6 ideology deity grammar bridges**
   - `NameGenerator.GenerateName` can receive `r_deityType` requests with `memeConcept` supplied by generated meme grammar but no `memeConceptDef`, causing repeated unresolved deity-type grammar.
   - Z Adaptive adds `memeConceptDef -> [memeConcept]` only for `r_deityType`, only when `memeConceptDef` is absent, and only when `memeConcept` already exists.
   - Custom deity namers that expose `r_name` but are invoked through Ideology's `r_deityName` root are bridged to their own existing `r_name` output. No deity name is invented or replaced.

7. **Monolyn / Ultima ideology rule-pack inheritance**
   - `DeityMaker_Monolyn` is given the vanilla `NamerDeityGlobal` include when missing, supplying the expected `r_deityName` root while retaining Monolyn's own name components.
   - `NamerIdeoMonolyn` and `Ultima_NamerIdeoFellowship` are given the vanilla `NamerIdeoGlobal` include when missing. This supplies shared ideology grammar such as `hyphenPrefix` rather than hard-coding replacement names.

8. **Map Mode Framework 1.6 growing-period native crash**
   - Map Mode Framework asynchronously pre-caches the Growing Period overlay on a worker `Task`.
   - In the current Odyssey/Worldbuilder stack that worker reaches Burst-backed `PlanetLayer` tile geometry through seasonal-temperature calculation, which can terminate the native RimWorld process instead of producing a recoverable managed exception.
   - Z Adaptive leaves the Growing Period map mode available but forces only its `canCache` flag off, so the unsafe background pre-cache path is not started.

9. **Current-stack stale-reference and helper-race sanitation**
   - Optional `SOA_NuclearThruster` and `BMT_WoollySpider` references are now removed when only an abstract/non-resolvable source def exists.
   - Whitespace-corrupted `MNGravitySwitch` / `MNDownpourImpact` references are removed without touching correctly-authored sound references.
   - Colony Manager helper races from the live stack that advertise humanlike meat with a null `meatDef` are normalized to `hasMeat=false`.

All runtime patches are narrowly targeted and fail open: if a target class/method is absent because a mod was removed or updated, normal game behavior continues.

Validated startup-profiler CI build: workflow run `34085998839` — SUCCESS.

10. **Phaser charging-rack authored mass (2026-10-08)**
   - RimDoctor reported missing authored Mass on `DG_PhaserChargingRack`, `DG_TypeIPhaserChargingRack` and `DG_TypeIIIPhaserChargingRack` while all three are haulable.
   - `Patches/AdaptiveKnownFixes.xml` now adds a `Mass` stat of 10 kg to those three Defs only when they have no authored Mass. Existing Mass values are preserved.
   - This removes the specific missing-mass Def validation errors; the chosen fallback mass should be checked against the original mod's intended balance.

11. **Known-description whitespace normalization (2026-10-08)**
    - `CheatShelf10k`, its generated `Frame_CheatShelf10k`, and `RR_Biological_Exterminators` produced whitespace configuration errors in the latest diagnostic log.
    - The runtime checks these exact ThingDefs at the `ThingDef.ConfigErrors` boundary and trims only leading/trailing whitespace. It preserves the mod authors' original description wording, including generated frame descriptions that may not exist during XML patch loading.
    - In-game verification is still required: CI compiles code but cannot load the full 630-mod installation.

12. **Competing F9 default shortcut (2026-10-08)**
    - `MainTab_History` and `MainTab_CQFA_LevelSchedule` both advertised F9.
    - A guarded XML patch changes only the latter's authored `defaultKeyCode` from `F9` to `None`, keeping the vanilla History shortcut and Level Schedule's tab/button available.
    - Existing per-user key-binding overrides may still need to be reset in the game's key-binding settings.


## Public WNG 1.6 consolidation (2026-10-08)

The public Wraith-Nanite-Gravtech-1.6 repository's `CompanionMods/Z-Adaptive-Error-Patch` is now the distribution source of truth. It contains the complete standalone Z Adaptive 1.6 XML/runtime patch set, including the original hospital, reference, helper-race and worker guards and subsequent gravship, graphics, startup and compatibility fixes. Do not install the separately built standalone copy at the same time: both share package ID `vardath.adaptiveerrorpatch` and runtime Harmony IDs.

The 2026-10-08 Player-prev(5).log reports third-party XML patch failures in MorrowRim - Dunmer Lamp Pack, RimShips, Progression: Gravship, Rim-Elves and Mechanoid Mechanitor. These are emitted while each upstream mod applies its own patch operations; late-loading Z Adaptive cannot undo a patch failure already logged. No invented replacement Defs or silent exception suppression were added. A missing-target patch should instead be repaired at its owning mod's source against its current dependency Def names.

The same log contains InsectWorkEverything transpiler/target failures, stale Yayo settings data, optional facial animation Defs without their type, and a repeated NullReferenceException with no original stack in the supplied log. They remain unverified for a targeted runtime fix. World generation ends with ThreadAbortException, not a reliably attributable root-cause exception in this log. Verify the installed playable ZIP in-game before claiming resolved warnings.

13. **Graphic_Multi null path with supplied texture (2026-10-09)**
    - The current log reaches `Graphic_Multi.Init` and `ContentFinder.Get` with a null texture-path key.
    - The existing fallback skipped empty paths whenever the request also contained a direct texture. Unlike `Graphic_Single`, `Graphic_Multi` still resolves its directional textures from `req.path`.
    - The `Graphic_Multi.Init` prefix now enforces a nonempty path in that case, using the existing bundled four-direction placeholder only for the malformed request. Ordinary requests and direct-texture `Graphic_Single` requests retain their behavior.

14. **Empty AudioGrain clip paths and VGE shader identity (2026-10-09)**
    - An optional, guarded XML operation discards only `SoundDef` grain entries with a present but empty `clipPath` that otherwise request `AudioClip at ''`. Other grain entries remain intact.
    - The existing gravship chroma-key `_MainTex` / `_Color` guards now recognize the shader's exact `Custom/Gravship chroma key` identity as well as the material name. The offending material is still not treated as an ordinary texture.
    - These repairs address narrowly identified warning/error paths, **not** the separate native GPU startup crash or the untraced world-generation `ArgumentOutOfRangeException`.

The optional runtime guard still requires a newly built `ZAdaptiveRuntime.dll` and installation of the CI-produced companion ZIP; editing the XML/source in the repository does not update an already-installed game automatically.

15. **Ocean Floor 1.0.0 — stale work-speed StatDef references (2026-10-09)**
    - Examined the supplied Workshop RAR (ID 3815134598): **Ocean Floor**, `we1tall.depths`, 1.6 build.
    - Its `Patches/WaterPhysics.xml` targets `WorkSpeedGlobal`, whereas the same mod uses `GeneralLaborSpeed` for RimWorld 1.6 recipes. Its Captain/Manners hediff stages also contain five `WorkSpeedGlobal` factors.
    - Guarded, Ocean Floor-only XML operations attach `Depths.StatPart_WaterWork` to `GeneralLaborSpeed` (only if missing) and migrate the five exact hediff factors, preserving their numerical values (1.2, 0.9, 0.8, 1.12 and 0.93).
    - All operations require `GeneralLaborSpeed` to exist and `WorkSpeedGlobal` to be absent, avoiding changes on builds where the older stat is valid. Existing stat parts are preserved. Z Adaptive now explicitly loads after `we1tall.depths`.
    - The supplied player log confirms `[Depths] loaded`. This repair addresses a verifiable compatibility defect, **not a proven map-generation crash**. Ocean Floor's compiled assembly includes settlement-map-generator, starting-tile and gravship hooks overlapping other underwater mods, but the available logs do not isolate a fatal Depths call stack. Those runtime interactions have intentionally not been disabled or broadly suppressed.

16. **GravTide 2026.10.09 × Ocean Floor 1.0.0 — Ocean biome XML load-order collision (2026-10-09)**
    - Inspected the supplied GravTide archive (Workshop ID 3779600989), including its 237 parseable XML files and `GravTide.dll`. The archive's changelog independently reports a **2026-10-09** fix for new-game map generation with Vanilla Gravship Expanded and Dlc collaboration - Void universe; the supplied failed session logs are from 2026-10-08.
    - GravTide's `Patches/Biomes_Ocean.xml` always adds `baseWeatherCommonalities` and `fishTypes` to the vanilla Ocean biome. Ocean Floor's `Patches/OceanRig.xml` inserts fallback versions of the same two tags only when they are absent.
    - When Ocean Floor loads first, the combined patch results in **two** weather tables and **two** fish tables; this was reproduced from the supplied files by applying their patches in both orders. Reversing load order yields singletons.
    - `Patches/GravTideOceanFloor.xml` now removes **only** Ocean Floor's fallback weather and fish tables when the complete, exact GravTide variants coexist. It is gated on both mods' names, checks the authored values rather than load order, and does not alter GravTide's marine content, worldgen, pressure system, or either mod's compiled Harmony patches.
    - The supplied logs also show two Geological Landforms warnings naming GravTide's lightning and plant-growth prefixes. The existing Z Adaptive compatibility transpiler covers the plant-growth pocket-map tile path, and the existing Vehicle Framework early-map readiness guard remains. The lightning warning remains an upstream Harmony overlap, not proven to cause a crash. No blanket prefix suppression was added.
    - A native graphics-device crash and the asynchronous `ArgumentOutOfRangeException` remain unproven to originate in GravTide. Verify the newer GravTide binary and updated Z Adaptive in game.

17. **Geological Landforms 1.7.13.1 × GravTide 2026.10.09 — linked pocket-map lightning (2026-10-09)**
    - Reviewed the user's full Geological Landforms archive (Workshop 2773943594), including its `Lunar/Components/GeologicalLandforms.dll`, and the exact source of both mods' Harmony patches. GL's private `Patch_RimWorld_WeatherEvent_LightningStrike.FireEvent(Map)` prefix directly reads `map.TileInfo.hilliness`. GravTide creates seabed pocket maps whose `Map.Tile` is invalid and whose surface location is held in `PocketMapParent.sourceMap`.
    - When both mods are installed, Z Adaptive now patches only **GL's own prefix method**, resolving hilliness from a valid parent surface map on linked pocket maps. GL's native random rule remains unchanged: impassable surface world tiles pass 30% of automatic lightning checks; other tiles always pass. On all ordinary maps, GL's original prefix runs unchanged. GravTide's native `DoStrike` and `FireEvent` targeting patches are not replaced or unpatched.
    - No blanket suppression of lightning/weather exceptions or redirection of `Map.Tile` was added. The direct collision warning may still appear, because GL detects GravTide's independent short-circuiting prefix even though both are now retained.
    - The existing plant-growth compatibility transpiler remains for backward compatibility, but GravTide 2026.10.09 already calls `SeaPlace.SurfaceTileOf(map)` before writing the growth calculator's tile, so it does **not** depend on that transpiler for the supplied release.
    - This closes a source-identified invalid-tile risk; neither the earlier `ArgumentOutOfRangeException` nor the game's native GPU crash has yet been traced to this code. In-game validation remains necessary.

18. **Live 2026-10-09 logs — VGE invalid grav-engine lookup, Giddy-Up init, ideology role tooltip**
    - `Player(20261008-232806).log` pinpoints repeated `Invalid ThingRequest(group Undefined)` exceptions to `VanillaGravshipExpanded.GravEngineTracker.GetGravEngine_ListerThings(Map)`. Upstream source loops over grav-engine defs and sends `engine.minifiedDef` straight to `ListerThings.ThingsOfDef`; null/unlistable minified definitions can trigger that exception. The new VGE-only transpiler replaces **only this method's** `ThingsOfDef` calls with a guard that skips missing/Undefined requests while preserving legitimate engine searches and boarding AI. It does not suppress exceptions in other mod methods. The supplied report aggregates **34,346** such errors.
    - The Giddy-Up 2 `Pawn_HealthTracker.SetDead` postfix calls `ExtendedDataStorage.Singleton` during early world setup, even though its `MakeDowned` postfix guards against an uninitialized singleton. A prefix on the mod's postfix now skips only the optional dismount bookkeeping when the pawn is absent or Giddy-Up's world storage is not yet initialized. Normal deaths after setup continue through the original handler.
    - An Ideology UI failure at `Precept_Role.AllApparelRequirementLabels` (`Sequence contains no elements`) breaks `Page_ConfigureIdeo` tooltips repeatedly. A finalizer on `Precept_Role.GetTip` catches **only** that exact missing-apparel sequence error and displays a diagnostic tooltip; it does not rewrite ideology roles or apparel rules. The bad source role still needs a Def-level diagnosis.
    - World/map generation actually succeeded in the supplied session (69 steps, 0 faults). After the VGE pawn-AI error flood, saving failed with `OutOfMemoryException` and Windows error 1450, indicating process/system resource exhaustion. This patch may reduce the error flood but is **not** a substitute for checking page file/RAM use and testing whether large saves can complete.
    - The report attributes the save-memory failure to GravTide, but it contains no stack identifying GravTide as the allocation source. Disabling unrelated content mods or suppressing save exceptions is not justified.

19. **Geological Landforms + bundled Map Preview, GravTide, and Custom Quest Framework (2026-10-09)**
    - Verified against the user's full archives: Geological Landforms' `1.6/Lunar/Manifest.xml` requires bundled `MapPreview.dll` and `TerrainGraph.dll`. The Dubs Mint Minimap and Alien Worlds Framework archives contain no Map Preview assembly.
    - The publicly available Custom Quest Framework `QuestEditor_Library.Patch_MapGenerate.prefix` matching the logged signature **always returns true**. It may resize CQF custom maps but does not skip vanilla map generation. Map Preview's "destructive prefix" warning is therefore not demonstrated as an actual conflict for this source version. Removing the prefix would break CQF's custom scenarios, so the patch intentionally retains it.
    - Map Preview's separate RNG-state warning (431939 vs expected 1) concerns preview accuracy; resetting the global random state or silencing the warning without a verified reproducer could corrupt actual map generation. No speculative RNG adjustment is made.
    - Corrected Z Adaptive's GravTide growth-patch discovery to recognize the supplied binary's nested `GravTide.SeabedPlantGrowth+BuildFor_Patch` and the older flat class name. The present GravTide `Prefix` calls `SeaPlace.SurfaceTileOf(map)` to resolve seabed world tiles already. The runtime now verifies that **direct compiled call** before deciding to skip its redundant extra transpiler, reducing intervention with Geological Landforms while preserving the legacy fallback.
    - The preexisting linked-pocket-map lightning and Ocean Floor/GravTide ocean-table patches remain enabled. No log evidence currently implicates Dubs Mint Minimap or Alien Worlds Framework directly.

20. **2026-10-09 failed-worldgen log — null deity naming RulePackDef inside Z Adaptive grammar bridge**
    - During `WorldGenStep_Factions`, `IdeoFoundation_Deity.FillDeity` invoked `NameGenerator.GenerateName`; its Z Adaptive prefix called `GrammarRequest.HasRule`. The exact exception originated in `GrammarRequest.HasRule`'s predicate dereferencing an included `RulePackDef`, with `NullReferenceException`. This occurred before the separate `OutOfMemoryException` in `ModSummaryWindow.DrawContents`.
    - The grammar bridge now removes **only null entries** from the `GrammarRequest.Includes` list on the two deity-specific roots. All defined naming packs, authored rules, culture-specific names and other name-generation requests remain untouched. This avoids the demonstrated null-rule-pack dereference without swallowing worldgen failures.
    - The preceding `WorldFloodFiller.FloodFill` `ArgumentOutOfRangeException` in world reachability construction is a **different, unresolved** error. Its stack contains Alien Worlds Framework, Worldbuilder, Geological Landforms and RJW hooks but does not establish a root cause. The memory failure in the long-event `ModSummaryWindow` is similarly not evidence of an intentional or persisted `ModsConfig.xml` reset.
    - The player log confirms that RimWorld **completed startup with 630 mods**, 94,713 Defs and 747 assemblies. To verify the *persisted* active-mod list, back up and inspect `ModsConfig.xml` or a RimPy profile, neither of which is supplied in these diagnostics.
