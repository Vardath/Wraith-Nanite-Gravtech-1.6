# WNG Replicator Queen / Asuran recovery checkpoint — 2026-09-11

Author/final design authority: **Vardath**.

## Scope

This slice implements the first physical storyline layer for the exact Replicator Queen. It does **not** yet implement her sovereign block-Replicator control, Neural Lattice implant control, temporary lattice intrusion, later recurring recovery raids, or mixed Asuran/block sovereign threats.

## Queen identity

- one exact persistent pawn reference is held by `GameComponent_ReplicatorQueenState`;
- female;
- fixed biological and chronological age 13 in the current first-build design;
- `WNG_NaniteHumanoid` xenotype;
- ordinary Asuran rank/PawnKinds remain separate from unique Queen sovereignty;
- the state component prevents a second Queen vault/Queen from being generated once the exact pawn exists.

## Vault/release

- a neutral world site `WNG_ReplicatorQueenVault` is discovered through a bounded world incident;
- entering the site generates the exact Queen only once;
- she is physically inserted into RimWorld's real vanilla `CryptosleepCasket` through `Building_CryptosleepCasket.TryAcceptThing`;
- casket ejection/destruction uses native RimWorld container behavior;
- only the `Dormant` exact Queen becoming physically spawned on the actual Queen-vault map is treated as the release boundary;
- once released, that exact pawn joins `Faction.OfPlayer` immediately;
- vanilla cryptosleep sickness is removed because this Queen uses the synthetic nanite-humanoid physiology even though the current RimWorld body shell remains `Human`.

The initial recovery schedule is stored globally in the Queen GameComponent, so moving the exact Queen to another player map before the delay expires does not evade the first recovery operation. A later map transition cannot re-run release or reset that timer, and a Queen already captured by the Asurans cannot be auto-recruited by an unrelated map component.

## Asuran recovery operation

Default current tuning is Def-driven:
- 4 recovery operatives;
- 1,800-tick reaction delay;
- 120-tick subdual warmup;
- 1,500-tick stun;
- 1,800-tick boarding timeout.

Recovery operatives:
- are exact `WNG_AsuranOperative` human-form nanite pawns;
- are generated without ordinary weapons for this capture mission;
- the requested recovery team is all-or-nothing: if all four exact operatives cannot be generated and physically spawned, the partial team/Jumper are rolled back and the globally scheduled recovery remains eligible to retry;
- do not use ordinary lethal raid AI against the Queen;
- one operative at a time receives the dedicated non-damaging subdual job;
- that job uses RimWorld's real `StunHandler.StunFor`;
- the remaining operatives use a dedicated recovery-hold job rather than ordinary combat/idle AI;
- subdue, load and hold JobDefs all use `checkOverrideOnDamage = Never`, so taking damage cannot flip the mission into normal combat AI;
- other operatives can replace a killed/intercepted active captor.

## Physical carrier / Asuran Jumper

`WNG_AsuranRecoveryJumper` is a physical `Building_PassengerShuttle` using native:
- `CompShuttle`;
- `CompTransporter`;
- `CompLaunchable`;
- `CompRefuelable`;
- `TransportShip`;
- `ShipJob_FlyAway`;
- WNG's existing native `PassengerShuttleLeaving` hook.

Stargate lore supports Asurans using/copying Ancient Puddle Jumpers. Current graphics are the same vanilla shuttle mechanics placeholder already used by the Puddle Jumper family; no art was generated.

The current chemfuel entry remains the existing temporary Ancient/Asuran power abstraction placeholder and is not claimed as Stargate lore.

## Exact capture boundary

The recovery sequence is deliberately stronger than "kidnap AI":

1. exact Queen must be physically stunned/downed;
2. an exact operative walks to her;
3. that operative physically carries her;
4. the carried exact pawn is transferred into the exact Jumper's native transporter;
5. surviving operatives physically board through native `JobDefOf.EnterTransporter`;
6. the exact Jumper launches through native `TransportShip` / `ShipJob_FlyAway`;
7. **only inside `WNGNativeShuttleLeaving.LeaveMap()`**, when the same exact Queen is still present in the same transit container, is capture committed;
8. the Queen is then removed from transit and registered in the exact Asuran faction's native `KidnappedPawnsTracker`;
9. the global WNG Queen state marks the same pawn `CapturedByAsurans`.

Therefore:
- downing != capture;
- stunning != capture;
- carrying != capture;
- loading into the Jumper != capture;
- a failed/interrupted launch != capture;
- only real map departure with the exact pawn aboard commits capture.

If the leaving hook cannot prove the exact capture, WNG refuses to consume that departure rather than silently losing or proxying the Queen.

## Still unfinished

Required later Queen/Asuran work remains:
- genuine Queen sovereign control of appropriate block Replicators;
- later recovery raids on any player map where she physically exists;
- consequences if the Asurans retain/recruit the captured Queen;
- mixed Asuran + sovereign block-Replicator threat composition;
- Sovereign Neural Lattice implant;
- temporary Asuran lattice intrusion;
- infiltration;
- final Asuran/Puddle-Jumper visuals/audio;
- live RimWorld validation.

## Validation status

Source/API behavior was checked against RimWorld 1.6 decompiled classes for:
- `Building_CryptosleepCasket`;
- `PawnGenerationRequest`;
- `StunHandler`;
- `Pawn_CarryTracker`;
- `ThingOwner.TryTransferToContainer`;
- `JobDriver_EnterTransporter`;
- `JobDef.checkOverrideOnDamage` / `CheckJobOverrideOnDamageMode.Never`;
- `CompShuttle`;
- `CompTransporter`;
- `TransportShip` / `ShipJob_FlyAway`;
- `KidnappedPawnsTracker`.

No claim is made that RimWorld was launched in this environment.
