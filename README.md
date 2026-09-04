# Wraith & Nanite Gravtech — RimWorld 1.6

Public RimWorld 1.6 release mirror for **Wraith & Nanite Gravtech** by Vardath.

> **Release status:** pre-release / release-candidate validation. The current build has passed the project's full automated Def/runtime-contract audit, RimWorld 1.6 C# compilation, payload verification and packaging. Final public release remains gated on live in-game testing and fresh Player.log/RimDoctor review.

## Requirements

- RimWorld **1.6**
- **Biotech** DLC
- **Odyssey** DLC
- **Ideology** is optional and is only required for the Neural Interface **Enslave** operation.

Optional integration is available for CatCraft's **Stargates!** mod; Stargates! is not a hard dependency.

## Manual installation

Steam Workshop will be the recommended installation method once the public Workshop item is live.

For manual installation, download this repository or the packaged release build, ensure the resulting mod folder is named `Wraith-Nanite-Gravtech`, and place it in RimWorld's `Mods` directory. The mod folder must directly contain `About`, `Assemblies`, `Defs`, `Languages`, `Patches`, `Sounds` and `Textures`.

Do not place an extra nested repository folder between `Mods/Wraith-Nanite-Gravtech/` and `About/About.xml`.

## Major systems

### Wraith
- Wraith Biotech xenotype with Life Force, regeneration and life-draining abilities.
- Distinct Wraith societies and doctrine.
- Living biotechnology, host/corpse incubation routes, weapons, armor, structures and furniture.
- Wraith Dart shuttlecraft and Wraith-themed Odyssey gravship technology.

### Human-form Replicators / Asurans
- Human-form nanite bodies with ordinary Food need and food-poisoning immunity.
- Eating restores a visible **Nanite Reserve**.
- Nanite Reserve powers accelerated self-repair, missing-part reconstruction and Neural Interface copy fabrication.
- Neural Interface operations for allegiance rewrite, imprisonment, enslavement, skill/passion copying and human-form copy fabrication.
- Pattern Archive, precursor fabrication, recovered precursor equipment and precursor Odyssey technology.

### Block Replicators
- Matter-consuming mechanical swarms with material inheritance.
- Passive consume-first behavior, bounded retaliation, specialist adaptation and encounter memory.
- EMP and powered containment counterplay.
- Dangerous Replicator Matter and a player-buildable Child's Toy Replicator with feral-risk behavior.

## Discovery progression

WNG's major archaeology is deliberately staged instead of presenting unrelated advanced systems at once:

**mystery → encounter → evidence → understanding → reconstruction → mastery**

The release sequence begins with Wraith and Replicator aftermath, progresses through damaged Ancient evidence and deeper Wraith archaeology, then reaches dormant Asuran architecture, a warned deceptive precursor site and the rare Replicator Queen vault.

## Compatibility philosophy

WNG primarily uses RimWorld genes, Hediffs, abilities, comps, custom jobs and Odyssey-native systems. It avoids Humanoid Alien Races as a dependency and avoids broad global Harmony takeover. Optional integrations are designed to leave the external mod authoritative for its own systems.

## Source and development

This repository is the **public RimWorld 1.6 release mirror**. Internal development/audit continuity material is intentionally kept out of the public mirror.

The public mirror will track verified 1.6 release states. Development changes are prepared and audited privately before being mirrored here.

## Fan-project notice

This is an unofficial, non-commercial fan-made project inspired by Stargate Atlantis themes. It is not affiliated with, endorsed by, or sponsored by the Stargate rights holders, Ludeon Studios, or any associated rights holder. No official logos, ripped models, screenshots, dialogue, music or sound recordings are included.

## Current release-candidate provenance

- Private development release-candidate source head: `d8703f91783bd831fc33e10f4143ca3c69a9f46f`
- Static Def Audit: **#2489 / run 33924271024 — SUCCESS**
- Automated gate includes RimWorld 1.6 C# compilation, compiled-payload verification, playable-folder assembly and ZIP packaging.
- Live RimWorld testing remains a separate final proof layer.
