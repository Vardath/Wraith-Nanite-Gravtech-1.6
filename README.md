# Wraith & Nanite Gravtech — RimWorld 1.6

Public RimWorld 1.6 release mirror for **Wraith & Nanite Gravtech** by Vardath.

> **Release status:** release-candidate validation. The current public mirror now matches the latest audited post-live-log-fix playable state from the private development repository. Final Steam publication remains gated on live in-game testing and fresh Player.log/RimDoctor review.

## Repository roles

- **Private repository — `Vardath/Wraith-Nanite-Gravtech`:** development authority. All new work, fixes, experiments and release candidates are made and audited there first.
- **This public repository — `Vardath/Wraith-Nanite-Gravtech-1.6`:** release mirror. It should match the current released Steam Workshop build and should not receive unreleased development work.
- When a private build is fully audited and accepted for release, the exact playable state is promoted here and to Steam together.
- If a released build develops a confirmed error, the fix is made privately, audited there, then promoted to both this repository and Steam so the public GitHub state and Workshop state remain aligned.

## Quick links

- **Manual install:** [`INSTALL.md`](INSTALL.md)
- **Current release notes:** [`RELEASE_NOTES.md`](RELEASE_NOTES.md)
- **Full changelog:** [`CHANGELOG.md`](CHANGELOG.md)
- **Asset provenance:** [`ASSET_PROVENANCE.md`](ASSET_PROVENANCE.md)
- **Project/fan notice:** [`NOTICE.md`](NOTICE.md)

Steam Workshop will be the recommended installation route once the public Workshop item is live. This repository exists for manual installers, archival use, source inspection and people who prefer GitHub distribution.

## Requirements

- RimWorld **1.6**
- **Biotech** DLC
- **Odyssey** DLC
- **Ideology** is optional and is only required for the Neural Interface **Enslave** operation.

CatCraft's **Stargates!** is supported as an optional integration and is not a hard dependency. RimGate and ONAC are also intended as optional/recommended companion mods where compatible.

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

This repository contains the **released/playable 1.6 payload**: About metadata, compiled WNG assembly, Defs, translations, patches, sounds and textures. Internal development/audit continuity material is intentionally not published here.

Development changes are prepared and audited privately. Only an accepted release state is mirrored here. Once Steam is live, this repository and the Steam Workshop copy are intended to be the same public release state.

## Fan-project notice

This is an unofficial, non-commercial fan-made project inspired by Stargate Atlantis themes. It is not affiliated with, endorsed by, or sponsored by the Stargate rights holders, Ludeon Studios, or any associated rights holder. No official logos, ripped models, screenshots, dialogue, music or sound recordings are intentionally included.

## Current release-candidate provenance

- Private development release head: `13d437b36bc0db313881e585fc76dbf16ae2b851`
- Static Def Audit: **#2492 / run `33927936424` — SUCCESS**
- Automated gate includes the full WNG audit suite, RimWorld 1.6 C# compilation, compiled-payload verification, playable-folder assembly and ZIP packaging.
- Fresh live-log fixes included removal of the obsolete `WNG_RimWorld16_RuntimeRepair.xml` shim and corrected living-equipment support patching for the Wraith Boneblade and War Glaive.
- Live RimWorld testing remains the final separate proof layer before the Steam release.
