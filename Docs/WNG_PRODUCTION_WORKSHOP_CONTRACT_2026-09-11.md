# WNG production/workshop contract — 2026-09-11

Newest explicit Vardath instruction overrides older notes.

## Standing production split

### Wraith living forge / workshop

`WNG_LivingForge` is the Wraith production bench.

- It should expose **all current player-craftable Wraith recipes** as those recipes are rebuilt.
- Normal Wraith production belongs here rather than being scattered across unrelated vanilla benches.
- The direct no-host **Wraith workshop** and **Wraith grav engine** bypass recipes are the exception: they appear only when the WNG direct-bootstrap option is enabled.
- The normal biological acquisition path remains host/corpse implantation/growth.
- Living colonists, prisoners, slaves and eligible non-dessicated humanlike corpses remain valid Wraith growth scaffolds.
- The final engine is a real Odyssey `GravEngine` carrying Wraith theme state. **Never restore Gravcore.**
- The first workshop-growth implant may retain a bootstrap acquisition route outside the Wraith workshop until its encounter/salvage acquisition is rebuilt; this must not become an excuse to move ordinary Wraith recipes back to generic benches.

### Asuran nanite workshop

A dedicated WNG Asuran nanite workshop is the Asuran production bench.

- It should expose **all current player-craftable Asuran/Precursor nanite-engineered recipes** as those recipes are rebuilt.
- Default acquisition is through a player-controlled Asuran/human-form Replicator ability that assembles the workshop by spending the caster's **Nanite Reserve**.
- **2026-09-11 Vardath correction:** Nanite Reserve is the human-form pawn's renamed native food need. Eating ordinary edible matter refills the same reserve; repair and fabrication spend it. There is no second Gene_Resource reserve bar.
- This personal matter-reserve rule is specific to human-form nanite bodies and does not turn block Replicators into hunger-driven pawns.
- The workshop build operation must spend the reserve only after a valid placement succeeds; failed/cancelled placement spends nothing.
- The workshop itself is not normally present in the Architect menu.
- When the same WNG direct-bootstrap/bypass option is enabled, the Asuran workshop additionally becomes directly buildable from the **WNG Architect** category after its research prerequisite.
- Turning the option off removes that direct Architect build designator again; it does not destroy an existing workshop.
- Asuran production uses the Asuran/nanite economy, including `WNG_NaniteSludge` where appropriate, and remains separate from Wraith bio-sludge/Life Force.

### Replicator blocks

The vanilla **Electric Smelter** remains the disposal station for Replicator blocks.

- `WNG_SmeltReplicatorBlocks` belongs on the Electric Smelter.
- The smelter is not a general Wraith or Asuran production bench.
- Wraith/Asuran crafting must not be moved to the smelter for convenience.

## Mod option behavior

The existing direct-bootstrap setting is broadened to cover both living-tech and nanite-workshop bypasses while retaining its saved setting key for compatibility.

When OFF (default):
- Wraith workshop and Wraith grav engine use their normal implantation/growth acquisition paths;
- Asuran workshop uses the Nanite Reserve assembly ability;
- no direct Asuran workshop Architect entry;
- no direct Wraith workshop/grav-engine bypass recipes.

When ON:
- direct Wraith workshop and Wraith grav-engine deployable-core recipes become available while the normal implantation routes remain valid;
- the Asuran workshop receives a direct WNG Architect build designator while the Nanite Reserve ability remains valid.

## Recipe ownership rule going forward

Every time a Wraith or Asuran craftable item is added/rebuilt, its acquisition audit must answer which family workshop owns its recipe.

- Wraith craftable -> `WNG_LivingForge` unless an explicit design says otherwise.
- Asuran/Precursor nanite craftable -> Asuran nanite workshop unless an explicit Ancient/relic-only acquisition rule says otherwise.
- Replicator block disposal -> Electric Smelter.

Do not silently use FabricationBench/MachiningTable/ElectricSmelter as generic dumping grounds.

## Lore reconciliation

- Wraith technology is organic/living and should be grown/cultivated; the workbench is a RimWorld abstraction for Wraith living-technology production.
- Asurans are human-form Replicators built from programmable nanites and Ancient-derived technology. Their food-as-matter personal reserve is a WNG gameplay extrapolation explicitly approved by Vardath; it uses native RimWorld food behavior as the matter-input interface while keeping block Replicator mass consumption separate.
- Historical WNG implementations are reference evidence only. For this fresh branch, the requirements above are authoritative.
