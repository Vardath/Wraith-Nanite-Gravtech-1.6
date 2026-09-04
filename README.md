# Wraith & Nanite Gravtech — RimWorld 1.6

Public RimWorld 1.6 manual-install mirror for **Wraith & Nanite Gravtech** by Vardath.

> **Release status:** release-candidate validation. The current build has passed the project's complete automated Def/runtime-contract audit, RimWorld 1.6 C# compilation, compiled-payload verification and playable-folder packaging. Final public release remains gated on live in-game testing and fresh Player.log/RimDoctor review.

## Quick links

- **Manual install:** [`INSTALL.md`](INSTALL.md)
- **Current release notes:** [`RELEASE_NOTES.md`](RELEASE_NOTES.md)
- **Full changelog:** [`CHANGELOG.md`](CHANGELOG.md)
- **Asset provenance:** [`ASSET_PROVENANCE.md`](ASSET_PROVENANCE.md)
- **Project/fan notice:** [`NOTICE.md`](NOTICE.md)

Steam Workshop will be the recommended installation route once the public Workshop item is live. This repository exists for manual installers, archival use and people who prefer GitHub distribution.

## Requirements

- RimWorld **1.6**
- **Biotech** DLC
- **Odyssey** DLC
- **Ideology** is optional and is only required for the Neural Interface **Enslave** operation.

CatCraft's **Stargates!** is supported as an optional integration and is not a hard dependency.

## Manual installation

Use GitHub's **Code → Download ZIP**, extract it, and rename the resulting folder to:

`Wraith-Nanite-Gravtech`

Place that folder directly inside RimWorld's local `Mods` directory. It must directly contain:

- `About/`
- `Assemblies/`
- `Defs/`
- `Languages/`
- `Patches/`
- `Sounds/`
- `Textures/`

The important sanity check is:

`Wraith-Nanite-Gravtech/About/About.xml`

Do not merge a new release over an old WNG folder; replace the old folder so stale XML/assets cannot survive between versions. Full instructions are in [`INSTALL.md`](INSTALL.md).

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

## Public mirror boundary

This repository contains the **verified playable 1.6 payload**: About metadata, compiled WNG assembly, Defs, translations, patches, sounds and textures. Internal development/audit continuity material is intentionally not published here.

Development changes are prepared and audited privately, then verified playable states are mirrored to this repository. This keeps the public repository suitable for direct manual installation rather than exposing the private working-history machinery.

## Fan-project notice

This is an unofficial, non-commercial fan-made project inspired by Stargate Atlantis themes. It is not affiliated with, endorsed by, or sponsored by the Stargate rights holders, Ludeon Studios, or any associated rights holder. No official logos, ripped models, screenshots, dialogue, music or sound recordings are intentionally included.

## Current release-candidate provenance

- Private development release-candidate source head: `d8703f91783bd831fc33e10f4143ca3c69a9f46f`
- Static Def Audit: **#2489 / run `33924271024` — SUCCESS**
- Automated gate includes RimWorld 1.6 C# compilation, compiled-payload verification, playable-folder assembly and ZIP packaging.
- Public mirror import commit: `5542879780d265941b7a0ba4b7063cfc47864384`
- Live RimWorld testing remains the final separate proof layer before the Steam release.
