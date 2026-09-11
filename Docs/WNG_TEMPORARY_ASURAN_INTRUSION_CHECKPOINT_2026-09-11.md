# WNG temporary Asuran lattice intrusion checkpoint — 2026-09-11

Author/final design authority: **Vardath**.

## Scope

This checkpoint records the public RimWorld 1.6 implementation of the previously reserved `ReplicatorControlAuthority.TemporaryAsuran` domain.

It does **not** grant ordinary Asurans permanent Queen sovereignty.

## Stargate mapping

Pegasus Asurans are individual nanite humanoids connected through a subspace network rather than a Milky-Way-style single hive mind. Their shared code/network can distribute information, reset/reprogram rogue Asurans, and can itself be manipulated or disrupted. WNG maps that into a bounded temporary intrusion into nearby block-Replicator controller traffic.

The intrusion is a gameplay extrapolation built from that network/base-code behavior; it is not presented as a canon-named device.

## Public implementation

`WNG_NaniteHumanoid` now includes `WNG_AsuranLatticeLink`.

The link is Def-tunable through `AsuranLatticeLinkExtension`:
- intrusion range;
- intrusion duration;
- maximum simultaneously intruded blocks per exact Asuran source;
- hostile-AI check interval;
- hostile-AI attempt chance.

Current first-build values are 18 cells, 2,500 ticks, cap 3, 600-tick checks and 0.65 attempt chance. They remain author-tunable.

Automatic use is restricted to pawns currently belonging to the hostile `WNG_AsuranLattice` faction. Recruited/player-aligned nanite humanoids do not silently steal block Replicators.

## Exact restoration transaction

A successful intrusion does not erase the previous block state.

Before replacing active controller authority, `CompReplicatorSovereignty` snapshots:
- previous authority type;
- exact previous controller pawn;
- previous original faction;
- previous control faction;
- previous domain key.

The active temporary domain stores:
- `TemporaryAsuran` authority;
- exact Asuran intruder;
- exact Asuran faction;
- a domain key containing both the exact intruder identity and the exact restoration-domain identity;
- expiry tick;
- the complete suspended snapshot above.

This means two blocks hacked by the same Asuran but originating from different Queen/Neural-Lattice/autonomous domains remain different temporary domains and cannot be merged together by the existing same-domain hierarchy rules.

## Split / recombine / save-load

Existing hierarchy transactions already call `CopyAuthorityFrom`.

That method now copies the temporary restoration snapshot as well as the active authority, so:
- genuine downward destruction breakup preserves the temporary intrusion and the exact restoration target;
- allowed same-domain upward recombination preserves the same temporary intrusion/restoration state;
- cross-restoration-domain recombination remains blocked by the temporary domain key;
- save/load persists both active intrusion and suspended authority/faction/domain metadata.

## Interruption and restoration

Temporary intrusion ends and restores the suspended state when any of the following occurs:
- the author-tunable timeout expires;
- the exact Asuran intruder dies or becomes downed;
- intruder and block no longer share valid physical presence;
- target block is EMP-suppressed;
- target block enters active Replicator containment;
- intruder's nanite lattice is EMP-disrupted;
- intruder enters active Replicator containment.

Restoration behavior:
- prior Queen authority is restored only if that exact Queen authority is still valid;
- prior Sovereign Neural Lattice authority is restored only if that exact implant/controller domain is still valid;
- if the prior controller can no longer legitimately resume authority, normal authority release restores the recorded pre-control faction/autonomous swarm behavior;
- prior autonomous/no-authority blocks return to their exact recorded faction where valid.

## Hostile behavior

Hostile Asuran Lattice pawns periodically look for nearby block Replicators and prefer:
1. player-aligned blocks;
2. blocks already carrying a non-temporary controller domain;
3. other eligible blocks.

Each exact Asuran can only hold the configured temporary cap. Another Asuran cannot overwrite an already-active temporary intrusion domain.

## Validation

Temporary GitHub Actions run **34590952017** completed successfully:
- `dotnet build Source/WNG/WNG.csproj -c Release` — success;
- all current Def/Patch XML parsed successfully.

The temporary validation workflow was then removed from the branch.

This is source/Def validation, **not live RimWorld validation**.
