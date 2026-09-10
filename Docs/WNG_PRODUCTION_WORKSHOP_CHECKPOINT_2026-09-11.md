# WNG production/workshop checkpoint — 2026-09-11

Newest explicit Vardath instruction overrides this checkpoint.

## Validated state

Validation run: GitHub Actions `34538903080` — SUCCESS.

The validated head compiled against `Krafs.Rimworld.Ref 1.6.4871`, all Def/Patch XML parsed, and the production invariants passed. The temporary validation workflow was removed afterward.

## Production ownership now locked

### Wraith living forge
- `WNG_LivingForge` is the Wraith production bench.
- Current normal Wraith production recipes are routed onto it.
- Direct no-host Wraith workshop / Wraith grav-engine bypass recipes are exposed only when the WNG bypass option is enabled.
- Normal workshop/grav-engine acquisition remains living-host or eligible corpse growth.
- The engine path produces Odyssey's real `GravEngine` with Wraith theme state. No Gravcore Def/product path exists.

### Asuran nanite workshop
- `WNG_AsuranWorkshop` is the Asuran/Precursor nanite-production bench.
- Current Asuran production begins with `WNG_SynthesizeNaniteSludge`; later craftable Asuran recipes must be assigned here as they are rebuilt.
- Default workshop acquisition is `WNG_AssembleAsuranWorkshop`, granted by `WNG_NaniteReserve` and spending a finite Nanite Reserve only after valid successful placement.
- Failed/cancelled placement spends no reserve.
- The workshop has no default Architect category.
- When the WNG direct-bootstrap option is enabled, runtime settings assign it to the `WNG` Architect category and rebuild the category designators.
- Turning the option off removes the direct designator but leaves existing workshops untouched.

### Replicator blocks
- `WNG_SmeltReplicatorBlocks` remains an `ElectricSmelter` disposal recipe only.
- The electric smelter is not a general Wraith/Asuran production bench.

## Bypass option

Saved key remains `enableDirectWraithBootstrapCrafting` for compatibility, but the UI now describes the broader technology-acquisition bypass.

OFF by default:
- Wraith workshop/grav engine use biological implantation/growth;
- Asuran workshop uses Nanite Reserve assembly ability;
- no direct Asuran workshop Architect icon;
- no direct Wraith workshop/engine bypass recipes.

ON:
- Wraith deployable workshop/engine core recipes become available;
- Asuran workshop becomes directly buildable in WNG Architect;
- normal acquisition paths remain available.

## Explicit dev-mode testing surface

`Source/WNG/Core/WNGDebugActions.cs` adds a WNG debug category with:
- `Spawn WNG object` for WNG buildings/items/resources/equipment while excluding technical projectile/skyfaller Defs;
- `Set WNG terrain` for WNG terrains/foundations;
- `Spawn Wraith grav engine` creating Odyssey's real `GravEngine`, applying Wraith theme state and inspecting it;
- `Spawn Asuran grav engine` creating the same native engine with Asuran theme state.

This is in addition to vanilla debug spawning. It exists specifically so grav-engine testing cannot fail merely because WNG correctly avoids defining fake replacement GravEngine ThingDefs.

## Compiler correction made during validation

Fresh `CompTargetable_WraithBootstrapHost` was missing RimWorld 1.6's abstract `PlayerChoosesTarget` and `GetTargets(Thing)` members. Those are now implemented using the actual 1.6 `CompTargetable` contract.

The Asuran workshop ability property-name hiding warning was also removed.

## Next work

Continue the fresh Asuran/Precursor gravship family from:
1. Vardath's current requirements;
2. Stargate Asuran/Ancient-derived identity;
3. actual vanilla Odyssey gravship classes/Defs;
4. fresh WNG family-isolation/resource logic.

Historical WNG gravship implementations remain failure evidence only and are not design authority.
