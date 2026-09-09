# WNGR2 clean rebuild contract

Authority baseline: known-good checkpoint `e2ba63fb7a8646f8e9010cd1a34803aa4dcc96b1` plus the WNG requirement ledger and live-test evidence.

This public rebuild branch is the active WNGR2 workspace. The private repository is reference-only from this point onward.

## Hard rules

- Do not publish or overwrite public `main` until the rebuilt mod is independently compile-clean and live-tested.
- Preserve proven Replicator graphics, hierarchy, splitting, recombination and swarm behaviour while rebuilding unrelated systems.
- Preserve established faction architecture and relationships unless a recorded requirement explicitly changes them.
- Biotech and Odyssey are hard dependencies. Stargates!, ONAC and RimGate integrations remain optional and dependency-safe.
- Prefer native RimWorld/Odyssey systems. Avoid global Harmony patches where local Def/components work.
- Obsolete `gravcore` and cultured-biomass gravship paths are excluded. Canonical gravship fuels are `WNG_WraithBiofluidFuel` and `WNG_AsuranNaniteSlurry`.
- Do not reintroduce missing-art Defs merely because they existed in the old mod.
- No completion claim until source compiles, Def/reference audits pass, the packaged mod loads, and live Player.log/RimDoctor evidence is clean enough for release.

## Replicator invariants

- Death breakup: Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones.
- Drone is irreducible.
- Split children inherit material signature, specialization/adaptation and temporary lattice override.
- Intentional hierarchy recombination/destruction does not trigger death breakup.
- Split-born recombination block is **60,000 ticks (one RimWorld day)**.
- Dangerous Replicator Matter requires a minimum stack of **10** and remains dormant for **30,000 ticks** before reassembly risk.
- Child's Toy controlled gestation remains **90,000 ticks**.
- The abandoned 2,500-tick/one-hour split-cooldown override is forbidden.

## Public branch strategy

The rebuild will become the root mod on `rebuild/wngr2-clean-20260909`. Existing public graphics and sounds may remain as inert assets while the old assembly, patches and unwanted Def load surface are removed. Only rebuilt or explicitly preserved systems are allowed back into the active Def/source surface.
