# CONTINUE WNG REBUILD HERE

This is the first file to read when the user says **“refresh memory and continue”**, **“continue the rebuild”**, or equivalent for WNG.

## Required continuation order

1. Read `Docs/WNG_REBUILD_MASTER_PLAN.md` completely.
2. Fetch the current `main` head and recent commit history.
3. Check the workflow result for the exact current head. Never infer green from an older run.
4. Examine recent changes against the master plan.
5. Keep changes that align with the plan; repair or discard changes that do not.
6. Continue the next unfinished dependency in the master plan’s **Immediate rebuild queue**.
7. Historical/private `Vardath/Wraith-Nanite-Gravtech` code/builds are reference material only. There are **no known-good historical states** and old code is not an implementation authority.
8. Newer explicit user corrections override this file and the master plan. When that happens, update the master plan in the same rebuild pass.

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
- A Sovereign Neural Lattice implant does not make a pawn a Queen; it grants bounded target-specific block-Replicator authority.
- Temporary Asuran lattice override is separate from genuine sovereign ownership.
- CatCraft owns Stargate networking; WNG owns WNG incidents/corridors/craft behavior. Wraith Dart culling is exactly two real passes.
- Obsolete Gravcore behavior/naming must not return. Use functional Wraith/Precursor Grav Engines and canonical Wraith bio-fluid / Asuran nanite-slurry fuel systems.
- Green CI is supporting evidence only; final acceptance requires actual RimWorld behavior, save/load, packaging, visuals/audio and log review.

Do not stop after summarizing this file. Continue the rebuild.
