# CONTINUE WNG REBUILD HERE

This is the first file to read when the user says **“refresh memory and continue”**, **“continue the rebuild”**, or equivalent for WNG.

## First facts to load before touching code

- The mod author and final design authority is **Vardath**.
- The active work/continuity repository is the public `Vardath/Wraith-Nanite-Gravtech-1.6` repository.
- Do **not** write to the private WNG repository unless Vardath explicitly re-authorizes private-repo work later.
- The plan is the target for the **first complete build**, not immutable canon. Timers, races/xenotypes, sounds, art, processes, systems, values and even whole subsystems may be changed later if Vardath is not satisfied.
- Build the mod according to the plan first; tune/redesign after there is a complete working first build.
- Do not create anti-regression checks that freeze ordinary design choices. Compile/build checks may catch real build errors; static audits do not decide design acceptance.

## Required continuation order

1. Read `Docs/WNG_REBUILD_MASTER_PLAN.md` completely.
2. Read `Docs/WNG_REBUILD_MASTER_PLAN_ADDENDUM.md` completely; it governs how all “locked/canonical/protected” wording in the older master plan is interpreted.
3. Read any active detailed rebuild contracts under `Docs/WNG_REBUILD_*.md`; while the Queen/human-form/Lattice chain is active, specifically read `Docs/WNG_REBUILD_REPLICATOR_QUEEN_CONTRACT.md`.
4. Fetch the current public `main` head and inspect the most recent work.
5. Continue the next unfinished dependency in the master plan rather than stopping after a summary.
6. Historical/private `Vardath/Wraith-Nanite-Gravtech` code/builds are reference material only when accessible. There are **no known-good historical states** and old code is not an implementation authority.
7. Newer explicit user corrections override these files. When that happens, update the public continuity documents without deleting the accumulated plan.

## Critical distinctions to remember before touching code

- Wraith are one Wraith xenotype/civilization with **castes** implemented through PawnKinds: Hunter, Warrior, Commander, Keeper, Queen, etc.
- **Backstories are biography, not races and not castes.**
- Routine pawn `Drain Life` does **not** display the strategic faction hunger request popup.
- Strategic Wraith hunger affects genuine feeding requests and raid/attack pressure; refusal of a real feeding request escalates danger.
- Mature-Hive ecology/retaliation is separate from strategic hunger/request UI.
- Block Replicators are custom machine races/forms; human-form Replicators/Asurans are Human-pawn nanite xenotypes.
- The current first-build Replicator death split target is `Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones`.
- Human-form + block mixed raid composition is intentional where the design calls for it; block recombination itself remains block-machine logic.
- The Replicator Queen is one exact age-13 female human-form pawn in the current first-build design with unique sovereign authority.
- **Releasing/retrieving her from the Queen-vault cryosleep chamber recruits her to the player immediately on spawning.** There is no neutral recruitment stage.
- Her vault recovery team remains dormant until casket release, gives a short warning, then the current first-build design uses four hostile human-form/Asuran operatives attempting physical carry-to-map-edge abduction; downing/pickup alone does not commit loss.
- If she remains with the player, the hostile Asuran/Lattice collective may launch **occasional later capture raids**, but only against a player home map where the exact Queen is physically present. Those raids again require real kidnapping and map-edge escape to take her.
- A Sovereign Neural Lattice implant does not make a pawn a Queen; it grants bounded target-specific block-Replicator authority in the current design.
- Temporary Asuran lattice override is separate from genuine sovereign ownership.
- The old Queen-captured `+1 outbreak` shortcut is obsolete; captured-Queen state instead permits genuine bounded Lattice sovereign use of block Replicators in appropriate future Lattice threats.
- CatCraft owns Stargate networking; WNG owns WNG incidents/corridors/craft behavior. The current Dart design uses two real culling passes.
- Obsolete Gravcore behavior/naming must not return in the first-build plan. Use functional Wraith/Precursor Grav Engines and Wraith bio-fluid / Asuran nanite-slurry fuel systems.

Do not stop after summarizing this file. Continue the rebuild.