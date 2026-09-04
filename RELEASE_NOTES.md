# Wraith & Nanite Gravtech — RimWorld 1.6 release candidate

This public mirror currently tracks the audited 1.6 release candidate being live-tested before the Steam Workshop release.

## Current candidate

- Private development source head: `d8703f91783bd831fc33e10f4143ca3c69a9f46f`
- Static Def Audit: **#2489 / run 33924271024 — SUCCESS**
- Automated gate: XML/reference checks, RimWorld 1.6 runtime-contract checks, gameplay-system audits, art/audio validation, actual C# compilation, compiled-payload verification and playable-folder assembly.
- Final live-game evidence: still in progress. The user is testing this exact build and collecting screenshots/logs before Workshop publication.

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

## Installation

See `INSTALL.md` for manual GitHub installation. Steam Workshop will be the recommended route after the final live-validation pass.

## Release boundary

This mirror is suitable for testing and manual installation, but it should not yet be described as the final published release until the exact build has completed the live RimWorld test/log review and the Workshop version has been uploaded and verified.
