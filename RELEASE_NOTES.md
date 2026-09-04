# Wraith & Nanite Gravtech — RimWorld 1.6 release candidate

This public mirror tracks the current audited 1.6 release candidate being live-tested before the Steam Workshop release.

## Current candidate

- Private development source head: `13d437b36bc0db313881e585fc76dbf16ae2b851`
- Static Def Audit: **#2492 / run `33927936424` — SUCCESS**
- Automated gate: XML/reference checks, RimWorld 1.6 runtime-contract checks, gameplay-system audits, art/audio validation, actual C# compilation, compiled-payload verification and playable-folder assembly.
- Final live-game evidence: still in progress. The user is testing this exact build and collecting screenshots/logs before Workshop publication.

## Fresh Player.log fixes included

A fresh live RimWorld log exposed WNG-owned patch-operation failures that were not visible in the earlier release-candidate gate. This public mirror now includes the audited corrections:

- removed the obsolete `Patches/WNG_RimWorld16_RuntimeRepair.xml` transitional shim after its repairs had already been folded into the actual source Defs;
- corrected Wraith Boneblade and War Glaive living-equipment support patching so the patch adds a `<comps>` node at ThingDef level instead of targeting a nonexistent pre-inheritance `/comps` path;
- the corresponding private regression audit now blocks the obsolete shim and bad melee-patch targets from returning.

These fixes passed the private PR gate and the merged `develop` post-merge gate before being promoted here.

## Release-polish highlights

### Nanite reserve economy
- Asuran/human-form nanite bodies once again have the normal Food need.
- They can eat ordinary food without food-poisoning risk.
- Eating replenishes a visible **Nanite Reserve**.
- Neural Interface human-form copy fabrication costs **60%** Nanite Reserve.
- Reserve powers accelerated injury repair and missing-part reconstruction; depleted nanites retain slower emergency repair.

### Player communication
- Vital Resistance now explains its approximate five-day protection window, strong-but-not-total feeding resistance, immediate treatment strain, victim-side rejection crisis and Wraith-side backlash.
- Existing Replicator adaptation, Wraith doctrine, recovered precursor equipment, Pattern Archive and Stargate objective telemetry was retained where it was already clear and useful.

### Discovery pacing
Major archaeology now follows a deliberately staged release sequence:

| Earliest day | Discovery |
| ---: | --- |
| 20 | Ruined Wraith laboratory |
| 28 | Replicator-consumed ruin |
| 36 | Ancient precursor laboratory |
| 44 | Abandoned Wraith cloning installation |
| 52 | Ancient precursor vault |
| 60 | Dormant Asuran facility |
| 72 | Deceptive Ancient survey annex |
| 84 | Replicator Queen vault |

The intended arc is:

**mystery → encounter → evidence → understanding → reconstruction → mastery**

Base incident chances remain individually bounded; the earliest-day sequence reduces accidental clustering of unrelated advanced discoveries.

## Repository/release policy

- The private `Vardath/Wraith-Nanite-Gravtech` repository is the development authority.
- This public `Vardath/Wraith-Nanite-Gravtech-1.6` repository is the release mirror.
- Once Steam is published, this repository and the Steam Workshop build are intended to remain the same public release state.
- Future updates are developed and audited privately first, then promoted here and to Steam only when ready.

## Installation

See `INSTALL.md` for manual GitHub installation. Steam Workshop will be the recommended route after the final live-validation pass.

## Release boundary

This mirror is suitable for testing and manual installation, but it should not yet be described as the final published release until the exact build has completed the live RimWorld test/log review and the Workshop version has been uploaded and verified.
