# Wraith & Nanite Gravtech — RimWorld 1.6 reconstruction workspace

**Author: Vardath**

This public `main` branch is the active WNG RimWorld 1.6 reconstruction workspace.

## Continuity / “refresh memory and continue”

Read [`CONTINUE_WNG_REBUILD.md`](CONTINUE_WNG_REBUILD.md) first, then read [`Docs/WNG_REBUILD_MASTER_PLAN.md`](Docs/WNG_REBUILD_MASTER_PLAN.md) and [`Docs/WNG_REBUILD_MASTER_PLAN_ADDENDUM.md`](Docs/WNG_REBUILD_MASTER_PLAN_ADDENDUM.md) completely.

The master plan records the intended first complete implementation. The addendum is equally authoritative and makes explicit that the design remains editable: timings, races/xenotypes, audio, art, balance, processes and whole systems may be changed later by Vardath after testing. The purpose of the plan is continuity and a complete first build, not to freeze the mod forever.

Historical/private WNG builds and source are reference evidence only when accessible. There are no known-good historical states. Current work and continuity live in this public 1.6 repository; do not write to the private WNG repository unless Vardath explicitly re-authorizes it.

## Current first-build direction

- RimWorld 1.6; Biotech + Odyssey hard dependencies; CatCraft Stargates!, ONAC and RimGate optional/dependency-safe.
- Preserve the approved Replicator graphics and genuinely working split/recombine behavior while the first build is reconstructed.
- Wraith are one xenotype/civilization with caste PawnKinds; backstories are biography, not races/castes.
- Routine Wraith feeding is separate from strategic faction hunger/request/raid pressure.
- Human-form Replicators/Asurans are nanite humanoids with real Nanite Reserve/reconstruction/EMP systems.
- Replicator Queen is a player-recruited human-form pawn when released from her casket; Asurans can attempt to kidnap her during the quest and later from a home map where she is present.
- Queen authority, implant authority and temporary lattice overrides have different scopes in the current implementation.
- No obsolete Gravcore progression in the first-build design; use functional Wraith/Precursor Grav Engines and the planned Wraith/Asuran fuel systems.
- Wraith Dart Stargate culling is currently designed as two real passes.

These are implementation targets, not permanent prohibitions on later redesign.

## Development approach

Build the planned mod first and make it work. Do not spend rebuild time freezing balance/design choices with anti-regression or release-check machinery. The GitHub workflow is for basic C# restore/build/assembly verification only. Gameplay acceptance comes from live RimWorld testing and Vardath's feedback.

Migration/reconstruction is still in progress. A successful compile means the assembly builds; it does not mean the mod is finished or approved.