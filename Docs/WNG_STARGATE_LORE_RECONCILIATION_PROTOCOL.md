# WNG — Stargate Lore Reconciliation Protocol

Author/final design authority: **Vardath**.

WNG is a Stargate-derived RimWorld project. Every new or rebuilt feature must therefore be checked against Stargate lore before implementation, then reconciled against the current WNG plan and historical WNG behavior.

## Mandatory questions before implementation

Before creating or rebuilding any pawn, faction, item, ability, structure, craft, incident, AI behavior, technology, implant, weapon, resource, quest interaction, or visual/audio identity, answer these questions in order:

1. **What is it?**
   - Identify the feature precisely rather than treating it as a generic sci-fi mechanic.

2. **How does it derive from Stargate?**
   - Identify the relevant species, faction, technology, episode behavior, recurring lore concept, or visual/function lineage.
   - Determine whether the feature is direct canon, a reasonable Stargate-derived extrapolation, or a WNG-specific extension.

3. **What does it actually do in Stargate?**
   - Check reliable Stargate reference material and, where useful, episode-specific evidence.
   - Separate demonstrated behavior from inference.
   - Do not add generic hunger, fuel, combat, magic, or technology mechanics merely because they are convenient for RimWorld.

4. **What does the current listed WNG plan say?**
   - Read the current public rebuild contracts, corrections, checkpoints, and relevant subsystem plan.
   - Newer explicit Vardath instructions take precedence over older notes.

5. **What does the historical WNG codebase say?**
   - Inspect the relevant old implementation as reference evidence only.
   - Recover useful mechanics, tuning ideas, naming, relationships, and edge cases.
   - Do not copy broken architecture or assume the historical implementation is authoritative.

6. **How should those four sources reconcile into RimWorld?**
   - Preserve the Stargate identity and function.
   - Preserve Vardath's current gameplay intent.
   - Use vanilla RimWorld/Odyssey systems where they naturally model the feature, especially boarding, transport, rooms, bills, power, jobs, factions, incidents, damage, and world-pawn persistence.
   - Add custom WNG code only where Stargate-specific behavior requires it.

## Authority order when sources disagree

1. Newest explicit instruction from Vardath.
2. Current public WNG rebuild plan/contracts/corrections.
3. Stargate canon and demonstrated lore behavior.
4. Historical WNG codebase as reference material.
5. Implementation convenience.

Implementation convenience must never silently override the first four.

## Lore fidelity rules

- Never convert a non-biological Stargate behavior into a biological need unless Stargate or the current plan actually supports that need.
- **Block Replicators:** do not turn their consumed material into hunger or survival fuel. Their map-matter economy exists to reproduce, construct, adapt and increase mass/forms.
- **Human-form Replicators/Asurans — explicit Vardath exception (2026-09-11):** their personal body reserve uses RimWorld's native food system as a renamed Nanite Reserve. Edible matter refuels their microscopic nanite body, while repair/fabrication spends that same reserve. This does not alter the block-Replicator economy.
- Wraith hunger/Life Force is a Wraith biological/faction system and must remain separate from both Replicator systems.
- Craft should preserve their Stargate role: e.g. Wraith Darts are culling/interceptor craft with culling-beam storage; Puddle Jumpers are Ancient gate-capable shuttles with Ancient control/drone technology.
- Asuran technology should derive from their Lantean/Ancient technological lineage rather than becoming unrelated generic nanite technology.
- If a WNG extension goes beyond canon, record the extrapolation and why it is consistent with the source setting.

## Current examples

### Block Replicators
- Stargate basis: self-replicating block machines consume available material and technological systems to make more of themselves and adapt.
- WNG plan: consume map matter, reproduce, combine into larger forms, inherit material phenotype, retaliate locally when provoked, then enter terminal attack behavior after roughly 90–95% of eligible map matter has been stripped.
- Historical code: useful reference for assimilation, adaptation, block reformation, hierarchy, specialists, and containment, but not authoritative.
- Fresh implementation rule: no starvation/hunger/fuel-to-survive model.

### Human-form Replicators / Asurans
- Stargate basis: humanoid Replicators and Pegasus Asurans are human-form bodies composed of microscopic nanites rather than block-machine bodies; Asurans derive from Ancient anti-Wraith nanite technology.
- WNG direction: use a finite personal Nanite Reserve represented by native Need_Food, refilled through edible matter and spent by repair/fabrication.
- This food-as-matter behavior is an explicit WNG gameplay extrapolation approved by Vardath, not a claim that Stargate canon depicts Asurans eating ordinary meals.
- Queen sovereignty, infiltration and block-control authority remain separate later systems.

### Wraith Dart
- Stargate basis: single-pilot gate-capable Wraith culling/interceptor craft; culling beam dematerializes and stores captured people aboard the craft.
- WNG plan: real flyby passes, up to three exact captured pawns, landing, native boarding, Wraith retreat using the same Dart, abandoned hackable salvage, exact-pawn recovery/captivity.
- Historical code: exact-pawn transporter buffering and max-three logic are useful references; disposable auto-despawn retreat is not.

### Puddle Jumper
- Stargate basis: Ancient/Lantean gate-capable shuttle, controlled through Ancient interfaces and armed with drone weapons.
- WNG plan: native RimWorld/Odyssey boarding and shuttle behavior, Ancient chair/drone attack identity, Stargate-entry flyby and landing support.
- Historical code: reference only; any prior custom boarding substitute is rejected.

## Required implementation note

For every substantial subsystem pass, the implementation checkpoint should briefly record:
- Stargate source behavior checked;
- current WNG plan requirement;
- historical code consulted;
- any intentional divergence from canon or old code;
- resulting fresh RimWorld implementation.

This protocol is a standing requirement for the remainder of the WNG rebuild.
