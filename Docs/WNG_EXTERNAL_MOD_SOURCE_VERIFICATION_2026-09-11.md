# WNG external-mod source verification — 2026-09-11

Newest explicit Vardath instructions override this checkpoint.

This checkpoint records identities verified directly from the user-supplied Workshop archives, replacing earlier temporary/title/fuzzy assumptions.

## Supplied archives

### RimGate - Jaffa, Kree! (Biotech)
- Workshop archive: `3762118088`
- name: `RimGate - Jaffa, Kree! (Biotech)`
- packageId: **`CraveMode.RimGateJaffaKreeBiotech`**
- author: CraveMode
- supported RimWorld: 1.6
- modVersion: 1.0.0
- Biotech rewrite; explicitly does not require Humanoid Alien Races.

Verified faction Defs:
- `JKB_JaffaApophis` — System Lord Apophis / Serpent Jaffas
- `JKB_JaffaAnubis` — System Lord Anubis / Jackal Jaffas
- `JKB_JaffaRa` — System Lord Ra / Horus Jaffas

Verified xenotypes:
- `JKB_Jaffa`
- `JKB_JaffaFirstPrime`

Verified relevant Jaffa pawn kinds include:
- `JKB_JaffaWarrior01`, `JKB_JaffaWarrior02`, `JKB_JaffaWarrior03`
- `JKB_SerpentGuard`
- `JKB_JackalGuard`
- `JKB_HorusGuard`
- `JKB_JaffaLeader`

The three System Lord factions are hidden, permanent enemies in the supplied RimGate Defs. WNG should attach appropriate hostile Goa'uld/Jaffa raid craft to these exact external factions rather than making duplicates.

### ONAC
- Workshop archive: `3775612635`
- name: `ONAC`
- packageId: **`idolord.ONAC`**
- author: idolord
- supported RimWorld: 1.6
- hard dependencies in ONAC itself: Harmony, Biotech, and **`CraveMode.RimGateJaffaKreeBiotech`**.

Verified Architect category:
- `ONAC_Architect` — label `ONAC`.

ONAC's own patch replaces the `designationCategory` of ThingDefs whose defName begins `ONAC_` with `ONAC_Architect`. WNG Goa'uld defs begin `WNG_`, so WNG must explicitly and conditionally place them in `ONAC_Architect` when the integration is present. Do not leave an unconditional reference to `ONAC_Architect` in standalone WNG XML.

Verified Goa'uld resource/research surface:
- solid Naquadah: `ONAC_Naquadah`
- liquid high-energy resource: **`ONAC_LiquidNaquadria`** (label `liquid Naquadria`)
- recipe: `ONAC_MakeLiquidNaquadria`
- recipe input: 10 `ONAC_Naquadah` -> 1 `ONAC_LiquidNaquadria`
- liquefaction research: **`ONAC_NaquadahLiquefaction`**
- foundry research: `ONAC_GoauldFoundryResearch`
- Goa'uld research bench: `ONAC_GoauldResearchBench`
- Naquadah liquefier: `ONAC_NaquadahLiquefier`

Vardath described the Goa'uld ship resource as liquid naquadah from ONAC. The supplied ONAC implementation calls its liquid ship/weapon-grade product **liquid Naquadria**. WNG should use the actual existing `ONAC_LiquidNaquadria` rather than duplicate or rename the external resource. If Vardath later wants a distinct liquid Naquadah resource, that is a separate explicit design change.

### Stargates!
- Workshop archive: `2831698056`
- name: `Stargates!`
- author: CatCraftYT
- packageId: **`ccyt.stargatesmod`**
- supported versions include 1.6.
- Stargates!' own dependencies include Vanilla Expanded Framework and Harmony. These remain dependencies of Stargates!, not WNG.

WNG remains dependency-free with respect to Stargates!: all CatCraft behavior is optional compatibility content and must be absent/inert when `ccyt.stargatesmod` is not active.

### Humanoid Alien Races
- Workshop archive: `839005762`
- name: `Humanoid Alien Races`
- packageId: `erdelf.HumanoidAlienRaces`

This archive confirms the HAR framework is separate from the supplied Biotech RimGate rewrite. It is not part of the supported ONAC/RimGate Biotech activation condition.

## Runtime integration corrections now required/recorded

- Stargates detection: exact package `ccyt.stargatesmod`.
- ONAC detection: exact package `idolord.ONAC`.
- RimGate Biotech detection: exact package `CraveMode.RimGateJaffaKreeBiotech`.
- No title-based fallback required for these supplied versions.
- No fuzzy `rimgate`, `jaffa`, Apophis/Ra/Anubis text matching required.
- System Lord faction resolution uses the exact three `JKB_` faction DefNames and verifies they originate from the correct package.
- Goa'uld WNG construction uses ONAC's `ONAC_Architect` only through optional activation/patching.
- Goa'uld WNG ship fuel uses `ONAC_LiquidNaquadria` only when the correct ONAC + RimGate integration is active.
- Goa'uld ship research should integrate with the actual ONAC research chain. First design target is to require an appropriate WNG shuttle/gravship research plus ONAC `ONAC_NaquadahLiquefaction` / Goa'uld technology prerequisites, rather than creating a disconnected standalone Goa'uld science tree.
- Exact UI/research patch implementation still needs compile/live validation and must not create unresolved Def references in WNG-only loadouts.

## Existing WNG code updated from this verification

`Source/WNG/Compatibility/WNGOptionalIntegrations.cs` now carries the exact package IDs, faction DefNames, xenotype IDs, Architect category ID and ONAC resource/research IDs.

`About/About.xml` optional `loadAfter` ordering now uses:
1. `ccyt.stargatesmod`
2. `CraveMode.RimGateJaffaKreeBiotech`
3. `idolord.ONAC`

These are load-order hints only; they are not WNG `modDependencies`.

## Next use of supplied source

When work resumes:
1. finish the current native Dart retreat/departure lifecycle;
2. use the verified `ONAC_Architect` and ONAC research/resource IDs for conditional Goa'uld craft buildability;
3. inventory vanilla Odyssey gravship components before building the Wraith/Asuran/Ha'tak-themed gravship families;
4. inspect the supplied Stargates! 1.6 assembly/Defs as needed for a real CatCraft adapter rather than inventing API names;
5. keep all external integrations optional and verify WNG-only loading after each integration slice.
