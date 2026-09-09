# Wraith & Nanite Gravtech — clean RimWorld 1.6 rebuild

This public repository is the active working repository for the clean WNG rebuild.

## Source-of-truth order

1. The user's latest explicit requirement in project chat.
2. `WNG_REBUILD_REQUIREMENTS.md` in this repository.
3. Live RimWorld 1.6 behavior, Player.log and RimDoctor evidence.
4. Private WNG builds/history only as feature-presence witnesses so capabilities are not accidentally omitted.

Private historical builds are **not** implementation sources for this rebuild. Their old C#, XML and patch structures are not to be copied into the new mod. When a private build proves that a feature or art category existed, the new rebuild must independently implement that requirement cleanly against current RimWorld 1.6/Odyssey contracts.

## Current hard rules

- RimWorld 1.6.
- Biotech and Odyssey are hard dependencies.
- Stargates!, ONAC and RimGate integration is optional and dependency-safe.
- Prefer native RimWorld/Odyssey mechanisms; avoid global Harmony patches where local Def/component behavior works.
- No obsolete Gravcore progression.
- Canonical gravship fuels are `WNG_WraithBiofluidFuel` and `WNG_AsuranNaniteSlurry`.
- Replicator split-born recombination lock is **2,500 ticks (one in-game hour)**.
- Do not claim completion until compile, semantic/static audits, packaging and live-game verification all pass.

See `WNG_FEATURE_COVERAGE.md` for the anti-regression feature matrix.