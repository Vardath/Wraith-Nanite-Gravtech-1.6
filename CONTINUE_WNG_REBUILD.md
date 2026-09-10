# CONTINUE WNG REBUILD HERE

This is the first file to read when the user says **“refresh memory and continue”**, **“continue the rebuild”**, or equivalent for WNG.

## Required continuation order

1. Read `Docs/WNG_REBUILD_MASTER_PLAN.md` completely.
2. Read any active detailed rebuild contracts under `Docs/WNG_REBUILD_*.md`; while the Queen/human-form/Lattice chain is active, specifically read `Docs/WNG_REBUILD_REPLICATOR_QUEEN_CONTRACT.md`.
3. Fetch the current `main` head and recent commit history.
4. Check the workflow result for the exact current head. Never infer green from an older run.
5. Examine recent changes against the master plan and active detailed contracts.
6. Keep changes that align with the plan; repair or discard changes that do not.
7. Continue the next unfinished dependency in the master plan’s **Immediate rebuild queue**.
8. Historical/private `Vardath/Wraith-Nanite-Gravtech` code/builds are reference material only. There are **no known-good historical states** and old code is not an implementation authority.
9. Newer explicit user corrections override these files. When that happens, update the public continuity documents in the same rebuild pass.

## Critical distinctions to remember before touching code

- Wraith are one Wraith xenotype/civilization with **castes** implemented through PawnKinds: Hunter, Warrior, Commander, Keeper, Queen, etc.
- **Backstories are biography, not races and not castes.**
- Routine pawn `Drain Life` does **not** display the strategic faction hunger request popup.
- Strategic Wraith hunger affects genuine feeding requests and raid/attack pressure; refusal of a real feeding request escalates danger.
- Mature-Hive ecology/retaliation is separate from strategic hunger/request UI.
- Block Replicators are custom machine races/forms; human-form Replicators/Asurans are Human-pawn nanite xenotypes.
- Replicator death split ladder is `Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones`.
- Human-form + block mixed raid composition is intentional where the design calls for it; block recombination itself remains block-machine logic.
- The Replicator Queen is one exact age-13 female human-form pawn with unique sovereign authority.
- **Releasing/retrieving her from the Queen-vault cryosleep chamber recruits her to the player immediately on spawning.** There is no neutral recruitment stage.
- Her vault recovery team remains dormant until casket release, gives a short warning, then exactly four hostile human-form/Asuran operatives attempt physical carry-to-map-edge abduction; downing/pickup alone does not commit loss.
- If she remains with the player, the hostile Asuran/Lattice collective may launch **occasional later capture raids**, but only against a player home map where the exact Queen is physically present. Those raids again require real kidnapping and map-edge escape to take her.
- A Sovereign Neural Lattice implant does not make a pawn a Queen; it grants bounded target-specific block-Replicator authority.
- Temporary Asuran lattice override is separate from genuine sovereign ownership.
- The old Queen-captured `+1 outbreak` shortcut is obsolete; captured-Queen state instead permits genuine bounded Lattice sovereign use of block Replicators in appropriate future Lattice threats.
- CatCraft owns Stargate networking; WNG owns WNG incidents/corridors/craft behavior. Wraith Dart culling is exactly two real passes.
- Obsolete Gravcore behavior/naming must not return. Use functional Wraith/Precursor Grav Engines and canonical Wraith bio-fluid / Asuran nanite-slurry fuel systems.
- Green CI is supporting evidence only; final acceptance requires actual RimWorld behavior, save/load, packaging, visuals/audio and log review.

Do not stop after summarizing this file. Continue the rebuild.
