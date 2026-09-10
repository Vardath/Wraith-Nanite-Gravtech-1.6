# WNG public 1.6 rebuild — Replicator Queen vault and sovereign-control contract

Last reconciled: 2026-09-10

This is a detailed subordinate contract to `Docs/WNG_REBUILD_MASTER_PLAN.md`. Future **refresh memory and continue** passes must read it while the Queen/human-form/Lattice chain is active.

Historical evidence came from the September 2 Queen-vault requirement/checkpoint records. Those old implementations are reference material only; newer user corrections and the current public master plan override conflicting historical implementation details.

## Unique Queen identity

- Exact PawnKind: `WNG_ReplicatorQueenChild`.
- Human pawn using the `WNG_HumanFormReplicator` xenotype.
- Female.
- Biological age exactly 13.
- Chronological age exactly 13.
- Genuine RimWorld child developmental stage, not an adult mentality merely drawn young.
- No ordinary generated weapon/gear.
- Recruitable/rescuable.
- One exact Queen pawn per world/storyline.
- The unique sovereign marker `WNG_ReplicatorQueenLink` is added to this exact pawn only; it is not part of the ordinary human-form xenotype.
- Exact Queen reference and outcome state must survive save/load.

## Vault/site

- Discovery progression places the Queen vault around day 84.
- It is a sealed precursor / Ancient-danger-style world site using original WNG architecture rather than copied Stargate layouts or glyphs.
- It contains a real vanilla `AncientCryptosleepCasket` holding the exact Queen pawn.
- The encounter is a race/defense event, not a static loot room.
- The casket must genuinely contain/release the pawn through normal RimWorld container behavior where practical.
- Site generation must avoid placement overlap and must clean up failed partial generation.

## Hostile recovery trigger

The hostile Lattice recovery team is **not active before the Queen is released**.

Required sequence:

1. Player reaches the sealed vault.
2. Queen remains in the real cryptosleep casket.
3. Opening/releasing the Queen triggers the recovery sequence.
4. Player receives a short reaction-warning window.
5. Exactly **four hostile human-form Replicator recovery operatives** enter from a conventional map edge in the base implementation.
6. Stargates may later provide an optional flavor/arrival variant, but CatCraft Stargates! is not required for the Queen encounter.
7. Recovery operatives prioritize the Queen rather than generic base destruction.
8. They must first subdue/down her if necessary.
9. One operative becomes the physical carrier and carries the real Queen pawn toward a valid map exit.
10. Merely downing the Queen does not commit hostile recovery.
11. Merely picking her up does not commit hostile recovery.
12. **Abduction commits only when the carrier actually exits the map while carrying the exact Queen pawn.**
13. If the carrier is killed/downed/interrupted before reaching the exit, the Queen remains recoverable and the hostile outcome has not yet committed.
14. On successful map-edge escape, the exact Queen changes/alines to the hostile Lattice captor as appropriate and `GameComponent_ReplicatorQueenState` records the abducted/captor outcome.

All local controller, Queen and carrier references required to resume the encounter must be serialized.

## Player shelter/recovery branch

- Player can rescue/recruit/shelter the exact Queen.
- Her same-faction physical presence can suppress normal feral drift for same-faction base/block Replicators.
- She may issue a sovereign directive to one block Replicator at a time.
- No map-wide instant conversion action should trivialize the Replicator threat.
- Current newer rule: the directive is restricted to **base/block WNG Replicators** rather than ordinary human-form Replicators.

## Sovereign Neural Lattice implant

The implant is related to Queen authority but is not Queen identity.

- It does not turn the bearer into a Queen.
- It grants bounded target-specific control of block Replicators.
- Exact controller reference is persisted.
- The binding remains valid only while the controller remains alive/present/on the same relevant map and still possesses sovereign implant authority.
- Differently bound Replicators must not silently recombine into a single command domain.
- Valid same-controller bindings survive legitimate recombination.
- Valid controller binding is copied to death-split children when the controller still qualifies.
- A real Queen's broad same-faction presence remains separate from implant-level explicit bindings.

## Temporary Asuran lattice intrusion

- Temporary Asuran override is not sovereign ownership.
- It keeps its own timer/original-faction restoration state.
- It must not be merged with Queen/implant ownership semantics.
- Death-split inheritance carries only the same remaining temporary window.
- Later genuine sovereign control can supersede a temporary override cleanly according to the relevant interaction rules.

## Hostile Lattice capture consequence — newer rule supersedes old +1

Historical implementation used a deliberately bounded `3 -> 4` ordinary outbreak count after successful Queen abduction. That historical behavior is **not the current rebuild requirement**.

The current public rebuild explicitly supersedes it:

- remove the old `HostileOutbreakBonus` shortcut;
- autonomous block-swarm outbreaks remain their own feral ecology;
- successful Lattice capture records **genuine sovereign access to base/block Replicators**;
- future hostile Lattice threat/raid systems may field block Replicators as directly Lattice-controlled assets because the captured Queen provides real sovereign coordination;
- this must be bounded by normal raid/threat points and encounter composition, not an exponential free-spawn multiplier;
- do not invent an unrelated recurring raid cadence solely because the Queen was captured if no requirement specifies one;
- instead, Queen-capture state should modify appropriate future Lattice threat composition whenever those threats occur;
- if the Queen-capture state is later reversed, the special sovereign Lattice access must cease for future threat generation.

## Raid composition boundary

- Block Replicator autonomous growth/recombination remains block-machine logic.
- Mixed block + human-form **raid composition is intentional** for appropriate advanced threats.
- Queen capture can legitimately make Lattice-controlled mixed forces more capable by allowing block Replicators to be generated under the Lattice faction for relevant threat groups/incidents.
- Do not make ordinary block outbreak incidents query Queen capture as a flat count increase.

## Save/load acceptance cases

At minimum verify later in live RimWorld:

- save before site entry;
- save before casket opening;
- save during warning window;
- save after recovery team arrival;
- save while Queen is downed;
- save while Queen is physically carried;
- save after carrier interruption;
- save after successful map-edge abduction;
- save after Queen joins player;
- save with Queen stabilizing player block Replicators;
- save with implant-bound block Replicators;
- save after bound Replicator split/recombine;
- no duplicate Queen, duplicate recovery team, duplicate outcome letter or double-commit after reload.

## Live checks still required before acceptance

- exact age-13 child presentation and behavior under the user's mod stack;
- real casket release;
- recovery team waits for release;
- exact four-operative arrival;
- operative priority on Queen;
- physical carry-to-edge path;
- carrier interruption genuinely rescues her;
- successful exit commits exact pawn/captor state;
- player recruitment/alignment;
- Queen control of each block tier;
- implant control and hierarchy inheritance;
- Lattice post-capture mixed threat composition;
- no conflict with Stargates, HAR/appearance systems, resurrection or other major mod-stack behavior.
