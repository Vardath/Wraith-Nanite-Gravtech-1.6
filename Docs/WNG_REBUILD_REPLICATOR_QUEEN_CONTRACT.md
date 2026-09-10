# WNG public 1.6 rebuild — Replicator Queen vault and sovereign-control contract

Last reconciled: 2026-09-10

This is a detailed subordinate contract to `Docs/WNG_REBUILD_MASTER_PLAN.md`. Future **refresh memory and continue** passes must read it while the Queen/human-form/Lattice chain is active.

Historical evidence came from the September 2 Queen-vault requirement/checkpoint records. Those old implementations are reference material only; newer user corrections and the current public master plan/this detailed contract override conflicting historical implementation details.

## Unique Queen identity

- Exact PawnKind: `WNG_ReplicatorQueenChild`.
- Human pawn using the `WNG_HumanFormReplicator` xenotype.
- Female.
- Biological age exactly 13.
- Chronological age exactly 13.
- Genuine RimWorld child developmental stage, not an adult mentality merely drawn young.
- No ordinary generated weapon/gear.
- One exact Queen pawn per world/storyline.
- The unique sovereign marker `WNG_ReplicatorQueenLink` is added to this exact pawn only; it is not part of the ordinary human-form xenotype.
- Exact Queen reference and outcome state must survive save/load.

## Vault/site and immediate recruitment

- Discovery progression places the Queen vault around day 84.
- It is a sealed precursor / Ancient-danger-style world site using original WNG architecture rather than copied Stargate layouts or glyphs.
- It contains a real vanilla `AncientCryptosleepCasket` holding the exact Queen pawn.
- The casket must genuinely contain/release the pawn through normal RimWorld container behavior where practical.
- **When the Queen is retrieved/released from the cryosleep chamber and becomes spawned, she is recruited to the player immediately. There is no neutral guest/recruitment stage after release.**
- The chamber release is also the trigger for the hostile Asuran/Lattice recovery attempt.
- Site generation must avoid placement overlap and must clean up failed partial generation.

## Hostile recovery trigger during the vault quest

The hostile recovery team is **not active before the Queen is released**.

Required sequence:

1. Player reaches the sealed vault.
2. Queen remains in the real cryptosleep casket.
3. Opening/releasing the Queen immediately recruits her to the player and triggers the recovery sequence.
4. Player receives a short reaction-warning window.
5. Exactly **four hostile human-form Replicator/Asuran recovery operatives** enter from a conventional map edge in the base implementation.
6. Stargates may later provide an optional flavor/arrival variant, but CatCraft Stargates! is not required for the Queen encounter.
7. Recovery operatives prioritize the Queen rather than generic base destruction.
8. They try to subdue/down her rather than intentionally kill her.
9. One operative becomes the physical carrier and carries the real Queen pawn toward a valid map exit.
10. Merely downing the Queen does not commit hostile recovery.
11. Merely picking her up does not commit hostile recovery.
12. **Abduction commits only when the carrier actually exits the map while carrying the exact Queen pawn.**
13. If the carrier is killed/downed/interrupted before reaching the exit, the Queen remains a recruited player pawn and is recoverable; the hostile outcome has not committed.
14. On successful map-edge escape, `GameComponent_ReplicatorQueenState` records the exact Queen and exact Lattice captor outcome.

All local controller/operation state required to resume the encounter must be serialized. Vanilla kidnapping is preferred for the real carry/exit transaction so WNG does not fake the point at which the Queen is lost.

## Later Asuran/Lattice attempts to recapture her

If the Queen survives the vault quest in player hands:

- the hostile Asuran/Lattice collective **may run occasional later raids specifically to capture her**;
- those special recovery raids may only target a **player home map on which the exact Queen is physically present**;
- if she is traveling in a caravan, off-map, absent from a home map, already abducted, or dead, the special Queen-capture raid must not fire there;
- the raid is a real RimWorld hostile raid, not pawns teleported directly beside the Queen;
- recovery raiders prioritize subduing/kidnapping the Queen while ordinary raid combat remains native;
- successful recapture uses the same physical carry-to-map-edge transaction and only commits after actual escape;
- interrupting the carrier before exit retains the Queen;
- raids must be **occasional**, not a constant harassment loop and not stacked while an existing Lattice recovery force is already active;
- the current public implementation may use a long randomized retry/cooldown as a fresh balance value, which can be tuned after live testing without changing the behavioral contract.

## Player Queen branch

- Cryosleep release itself is recruitment.
- Her same-faction physical presence can suppress normal feral drift for same-faction base/block Replicators.
- She may issue a sovereign directive to one block Replicator at a time.
- No map-wide instant conversion action should trivialize the Replicator threat.
- The directive is restricted to **base/block WNG Replicators** rather than ordinary human-form Replicators.

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
- Queen-capture state should modify appropriate future Lattice threat composition whenever those threats occur;
- if the Queen-capture state is later reversed, the special sovereign Lattice access must cease for future threat generation.

The **occasional Queen-recapture raids while she belongs to the player are a separate explicit requirement** and must not be confused with the post-capture sovereign-threat consequence.

## Raid composition boundary

- Block Replicator autonomous growth/recombination remains block-machine logic.
- Mixed block + human-form **raid composition is intentional** for appropriate advanced threats.
- Queen capture can legitimately make Lattice-controlled mixed forces more capable by allowing block Replicators to be generated under the Lattice faction for relevant threat groups/incidents.
- Do not make ordinary block outbreak incidents query Queen capture as a flat count increase.

## Save/load acceptance cases

At minimum verify later in live RimWorld:

- save before site entry;
- save before casket opening;
- save immediately after release/recruitment;
- save during warning window;
- save after recovery team arrival;
- save while Queen is downed;
- save while Queen is physically carried;
- save after carrier interruption;
- save after successful map-edge abduction;
- save with Queen on a player home map before a later recovery raid;
- save during a later recovery raid;
- save with Queen traveling away from all home maps;
- save with Queen stabilizing player block Replicators;
- save with implant-bound block Replicators;
- save after bound Replicator split/recombine;
- no duplicate Queen, duplicate recovery team, duplicate outcome letter, duplicate scheduled raid or double-commit after reload.

## Live checks still required before acceptance

- exact age-13 child presentation and behavior under the user's mod stack;
- real casket release;
- immediate player recruitment on spawn;
- recovery team waits for release;
- exact four-operative arrival;
- operative priority on Queen;
- subdual rather than deliberate execution;
- physical carry-to-edge path;
- carrier interruption genuinely saves her;
- successful exit commits exact pawn/captor state;
- occasional home-map capture raid only where Queen is actually present;
- no special capture raid when Queen is absent/off-map;
- Queen control of each block tier;
- implant control and hierarchy inheritance;
- Lattice post-capture mixed threat composition;
- no conflict with Stargates, HAR/appearance systems, resurrection or other major mod-stack behavior.
