# WNG — Wraith Dart absorption / salvage contract

Author/final design authority: **Vardath**.

This contract records the current intended first-build behavior for hostile Wraith Dart culling/abduction in the fresh RimWorld 1.6 rebuild. Historical implementations are reference material only; this document controls the fresh rewrite unless superseded by a newer explicit instruction.

## Core encounter loop

1. A hostile Wraith Dart performs its real culling/flyover passes.
2. Its ray-of-absorption physically captures valid pawns into the Dart's own transport/capture buffer. Captured pawns are the exact pawns taken from the map; no proxy or replacement victims are generated.
3. After the operational passes, the Dart can land as part of the encounter outcome.
4. While the Dart remains on the map, the player can hack it.
5. A successful hack opens/releases the Dart's exact captured pawns back to the map and transfers the surviving Dart to the player as a usable shuttle.
6. If surviving Wraiths successfully retreat using the Dart, they depart in it and any still-buffered captives leave with them. Those exact pawns then enter the persistent Wraith captivity/rescue lifecycle.
7. If the Wraith retreat or otherwise abandon the Dart without taking it, the Dart remains behind. It does **not** auto-despawn merely because the Wraith force withdraws. The abandoned craft remains hackable salvage.
8. Hacking an abandoned surviving Dart gives the player the craft as a free shuttle and releases any exact captives still physically buffered inside it.

## Ownership / outcome rules

- The Dart starts hostile/Wraith-owned.
- Wraith escape with Dart: Dart leaves with the Wraith; buffered captives become off-map exact-pawn Wraith captives.
- Wraith abandon Dart: Dart remains on-map hostile/neutral-to-hack until hacked or destroyed.
- Successful player hack: captured pawns are recovered from that same craft and the surviving Dart becomes player-owned/usable.
- Destroyed Dart: must resolve its buffered pawns deterministically; it may not silently delete them. Exact destruction outcome will be implemented with the craft layer and verified in-game.
- A hack must never fabricate recovered pawns. Recovery is from the Dart's actual persisted buffer.
- The craft must not be destroyed as a side effect of a successful hack.

## Separation from other Wraith systems

Dart absorption is a physical capture/craft system. It is separate from:

- ordinary Drain Life / Wither;
- strategic Wraith faction hunger and feeding-request popups;
- Mature-Hive local Feeding Niches;
- vanilla ground-raid kidnapping, although both ultimately feed the same exact-pawn Wraith captivity registry after a successful off-map capture.

## Craft implementation dependency

The fresh Dart implementation must therefore provide, as one coherent system:

- real flight/culling passes;
- ray-of-absorption targeting and capture;
- an exact-pawn persistent internal transport buffer;
- landing behavior;
- Wraith boarding/retreat use of the same craft;
- native hack interaction;
- captive release on successful hack;
- ownership transfer on successful hack;
- persistent usable player shuttle behavior after capture;
- save/load safety for craft state and buffered pawns;
- deterministic handling of destruction/interruption.

Do not implement the Dart as a disposable visual skyfaller that vanishes after its passes. The surviving landed craft is part of the reward/risk loop.