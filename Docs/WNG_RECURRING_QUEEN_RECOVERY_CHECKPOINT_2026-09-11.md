# WNG recurring Replicator Queen recovery checkpoint — 2026-09-11

Author/final design authority: **Vardath**.

## Scope

This checkpoint records the public RimWorld 1.6 implementation of later recurring Asuran recovery attempts against the one exact Replicator Queen.

It extends the already-live first vault-triggered recovery operation. It does not create a second Queen, proxy victim, abstract faction marker or fake capture transaction.

## Exact target rule

Later recovery attempts are eligible only when:
- the exact persistent Queen is alive;
- the first physical recovery operation has previously become active;
- the exact Queen is currently player-owned;
- the exact Queen is physically spawned on a map;
- that exact map is a player home map;
- no real active recovery mission already exists for her.

The scheduler always passes that exact pawn to the existing recovery utility, which uses `queen.Map`. It never selects the richest/random player map and never launches against a different colony while she is elsewhere.

If the Queen is in a caravan, off-map container, another non-home map, dead or already captured by the Asurans, recurring recovery does not fire.

## Cadence

`ReplicatorQueenRecurringRecoveryExtension` is attached to `WNG_ReplicatorQueenVaultDiscovered` so the story pacing remains author-tunable through Def data.

Current first-build defaults:
- minimum interval: 120,000 ticks (2 in-game days);
- maximum interval: 240,000 ticks (4 in-game days);
- failed edge/spawn retry: 10,000 ticks;
- scheduler maintenance interval: 2,500 ticks.

These values are tuning, not immutable design rules.

## One operation at a time

The scheduler searches loaded maps for the exact Queen's real `CompAsuranQueenRecoveryMission`.

Active mission phases block another attempt:
- Subduing, while at least one of that mission's exact native `CompShuttle.requiredPawns` remains alive;
- QueenLoaded;
- Boarding;
- NativeEscapePending.

This uses the recovery Jumper's exact registered operative set rather than treating an unrelated Asuran pawn elsewhere on the map as part of the Queen operation.

If the team is destroyed/removed before capture, the old operation can no longer block future scheduling. If the Queen is currently loaded/boarding/escaping, the scheduler remains blocked until that exact physical transaction resolves.

## Physical capture boundary retained

Recurring attempts reuse the existing recovery implementation unchanged:
- all configured operatives must spawn or partial creation is rolled back;
- operatives use nonlethal stun/subdual jobs;
- the exact Queen must be physically carried into the exact Asuran recovery Jumper;
- surviving operatives board through native shuttle behavior;
- stun/down/carry/load do not commit capture;
- only the exact recovery craft physically leaving the map with that same Queen still inside its transit container commits capture;
- successful departure registers the same pawn in the exact Asuran faction's native kidnapped-pawn tracker;
- an interrupted operation before that boundary leaves the Queen recoverable/player-owned.

## Save/load / duplication safety

The recurring scheduler persists:
- whether recurring operations have unlocked after the first physical recovery;
- exact next scheduled recovery tick;
- next maintenance tick;
- number of recurring operations successfully spawned.

A loaded save with an active exact recovery mission will see that mission before attempting another. A successful recurring spawn clears the scheduled tick immediately, preventing a second team from the same schedule.

## Validation

Temporary public branch workflow run **34591602371** completed successfully after the exact-operative refinement:
- `dotnet build Source/WNG/WNG.csproj -c Release` — success;
- all current Def/Patch XML parsed successfully.

The temporary validation workflow was removed from the branch after validation.

This is source/Def validation, **not live RimWorld validation**.

## Next required Queen branch

Captured-Queen consequences remain separate and still required:
- if the Asuran Lattice genuinely retains the exact captured Queen, suitable future hostile content gains real sovereign block-Replicator access;
- threats should be physically mixed Asuran + block compositions where appropriate;
- do not model this as `+1 outbreak`, a generic faction stat buff or a duplicate/proxy Queen.
