# Wraith & Nanite Gravtech — RimWorld 1.6 reconstruction workspace

This public `main` branch is the active WNG RimWorld 1.6 reconstruction workspace.

## Continuity / “refresh memory and continue”

**Read [`CONTINUE_WNG_REBUILD.md`](CONTINUE_WNG_REBUILD.md) first, then read [`Docs/WNG_REBUILD_MASTER_PLAN.md`](Docs/WNG_REBUILD_MASTER_PLAN.md) completely.**

The master plan is the public in-repository continuity authority for this rebuild and incorporates the consolidated requirements plus newer September 9–10 corrections. It is deliberately protected by a continuity audit so future work does not silently lose the rebuild rules.

Historical/private WNG builds and source are **reference evidence only**. There are no known-good historical states. Newer explicit user instructions override older implementation details and the plan must be updated when requirements change.

## Locked high-level rules

- RimWorld 1.6; Biotech + Odyssey hard dependencies; CatCraft Stargates!, ONAC and RimGate optional/dependency-safe.
- Preserve approved Replicator graphics and genuinely working split/recombine behavior.
- Wraith are one xenotype/civilization with caste PawnKinds; backstories are biography, not races/castes.
- Routine Wraith feeding is separate from strategic faction hunger/request/raid pressure.
- Exact Replicator split ladder: **Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones**.
- Split-born recombination cooldown: **2,500 ticks / one in-game hour**.
- Dangerous Replicator Matter minimum stack: **10**; dormancy: **30,000 ticks**.
- Child’s Toy gestation: **90,000 ticks**.
- Human-form Replicators/Asurans are nanite humanoids with real Nanite Reserve/reconstruction/EMP systems.
- Replicator Queen is one exact age-13 female human-form pawn; Queen authority, implant authority and temporary lattice overrides have different scopes.
- No obsolete Gravcore progression. Canonical gravship fuels are `WNG_WraithBiofluidFuel` and `WNG_AsuranNaniteSlurry`.
- Wraith Dart Stargate culling uses exactly **two real passes**.
- Final acceptance requires source/Def correctness, audits, RimWorld 1.6 compile, playable package, live startup/gameplay/save-load, visual/audio review and Player.log/RimDoctor review.

Migration/reconstruction is still in progress. Individual commits and green CI runs are checkpoints, not completion claims.
