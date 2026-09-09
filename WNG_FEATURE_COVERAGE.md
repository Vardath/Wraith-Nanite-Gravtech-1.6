# WNG anti-regression feature coverage matrix

Purpose: prevent the clean rebuild from accidentally becoming narrower than earlier WNG builds. Historical private builds are used only as **witnesses that a capability/category existed**. Their implementation is not copied into this repository.

## Witness checkpoints reviewed

- `e2ba63fb7a8646f8e9010cd1a34803aa4dcc96b1` — shuttle-green baseline. Confirms four real player passenger craft and native boarding expectations.
- `3039b0df7c8176032242455f0761ffb34db8fbcf` — older full-parity gravship/conduit line. Confirms conduit topology and release-gate coverage that later narrowed branches could omit.
- `33087e588b8db2b111873ffacc38097d50e6edcb` — complete gravship-art boundary. Confirms complete Wraith/Asuran gravship, conduit, furniture, structure, apparel, caste, projectile and environment-role art categories existed.
- `92f6c5779d9fbf0a5dcc3017c4d9930023cb1429` — later 2x3-era gravship-system visual checkpoint. Confirms the intended full facility families, canonical fuel-item identities, Wraith/Asuran substructures, directional system graphics and historical 32-cue package boundary. Current requirement remains 30 cues.
- Live Player.log/RimDoctor history — evidence for actual runtime failures/regressions to avoid, including invalid Odyssey inheritance, broken PatchOperations, disabled/missing shuttle behavior, missing audio and missing textures.

## Required feature groups

| Group | Clean-rebuild requirement | Historical witness role | Status |
|---|---|---|---|
| Wraith Life Force | Visible resource, regeneration economy, torpor/limb recovery | private builds/logs prove it existed and was player-facing | rebuild required |
| Drain Life | +50 victim biological years, -5 Wraith years, floor 18, temporary states | canonical requirement/history | rebuild required |
| Wraith appearance | pale/white hair enforcement; long straight where practical | screenshots/live feedback | rebuild required |
| Wraith society | hibernation, Keeper, Queen, telepathy, captive/thrall/experiment/hybrid systems | later requirement addenda | rebuild required |
| Living technology | host/corpse Living Forge + Grav Engine cultivation, one-day incubation | requirements/live testing | rebuild required |
| Replicator matter economy | material/tech assimilation, dangerous matter, dormancy | private builds prove gameplay breadth | rebuild required |
| Replicator adaptation | delayed learned specialization from assimilated tech | private builds prove specialists/adaptation existed | rebuild required |
| Replicator split ladder | Siege -> 2 Titan -> 2 Bulwark -> 2 Hunter -> 2 Drone | canonical | rebuild required |
| Split lock | split-born recombination blocked 2,500 ticks | latest user correction | locked |
| EMP/containment | suppression and containment gameplay | private builds prove these subsystems existed | rebuild required |
| Toy/player Replicator | friendly toy, 90,000-tick gestation, feral/population behavior | requirements | rebuild required |
| Human-form/Asuran | infiltration, roles, Neural Interface, exact-copy identity | private builds + requirements | rebuild required |
| Nanite Reserve | visible reserve, food conversion, healing/reconstruction costs/timers | requirements | rebuild required |
| Wraith factions | Sable/Cinder/Veiled/Pale and hostile/exile/player roles | faction history | rebuild required |
| Replicator factions | hostile block, hostile human-form, enclave/non-hostile, player variants | faction history | rebuild required |
| Backstories | 30 native RimWorld BackstoryDefs with correct pools | canonical | rebuild required |
| Independent raids | Wraith and Replicator raids work without Stargate dependency | requirements | rebuild required |
| Discovery progression | ruins/labs/vaults, salvage, analysis, reconstruction/mastery | requirements/private site inventories | rebuild required |
| Stargate optional integration | CatCraft owns gate network/buffer; WNG owns incidents/craft objectives | requirements | rebuild required |
| Dart culling | exactly two real passes, real abductees, identity persistence | chat + private behavior history | rebuild required |
| Four craft | Dart, Puddle Jumper, Strike Craft, Cruiser all board/load/fuel/launch | `e2ba63` witness | rebuild required |
| Wraith gravship | full Odyssey-compatible organic family | private gravship checkpoints | rebuild required |
| Asuran gravship | full Odyssey-compatible clean geometric family | private gravship checkpoints | rebuild required |
| Canonical fuels | Wraith bio-fluid + Asuran nanite slurry | late gravship witness + requirement | locked |
| Facility parity | pilot, extender, small/large fuel, small/large drive, optimizer, veil/jammer, shield, required native equivalents | `92f6c5` witness | rebuild required |
| Family isolation | no Wraith/Asuran cross-linking or wrong fuel use | requirements | rebuild required |
| Hull topology | straight, corners and diagonals; no broken transition rendering | chat/private gravship history | rebuild required |
| Fuel/power conduits | real straight/corner/T/cross/end topology, wall-compatible presentation | `3039b0` witness | rebuild required |
| Weapons/apparel | real functionality plus complete inventory/worn/directional art | private art boundary | rebuild required |
| Genes/abilities/icons | functional, purpose-specific, survive overwrite/deduplication | private art + live feedback | rebuild required |
| Structures/furniture | coherent Wraith/Asuran settlement and ship structures | private art boundary | rebuild required |
| Replicator graphics | retain specifically approved proven Replicator graphics | explicit user preservation rule | preserve |
| Audio | professional functional mapping; current target 30 distinct cues | historical 32-cue build only proves breadth | rebuild 30 |
| Save/load | at-most-once split/cull/reconstruction/incubation/craft/quest transactions | canonical | rebuild required |

## Known historical traps that must not be reintroduced

- Invalid concrete Odyssey `ParentName` assumptions such as `ChemfuelTank`, `GravFieldExtender` or similar when not valid inheritance parents.
- PatchOperations that assume a comp/node exists and silently fail at runtime.
- Disabled or hidden craft as a substitute for fixing boarding/launch behavior.
- Gravcore or cultured-biomass gravship progression.
- Chemfuel standing in for either canonical WNG gravship fuel.
- One generic icon/art asset reused across unrelated systems.
- Private audits that validate only a narrowed subset while dropped systems go unnoticed.
- Treating CI success as equivalent to live-game acceptance.

## Working rule

For every subsystem added to the clean rebuild, compare its feature surface against this matrix, the full current requirements, the relevant project-chat history, and at least one private build witness where available. A feature can be redesigned cleanly, but it cannot disappear without an explicit newer user decision.