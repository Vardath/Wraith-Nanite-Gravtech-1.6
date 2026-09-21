# WNG ART + GITHUB CONTINUATION HANDOFF
## Authoritative continuation file — replaces the old art checklist

**Date:** 2026-09-21  
**Project:** Wraith-Nanite-Gravtech (WNG), RimWorld 1.6  
**Public mod repo:** `Vardath/Wraith-Nanite-Gravtech-1.6`  
**Website repo:** `Vardath/Vardath.github.io`  
**WNG art page:** `wng-art.html`  
**Cosmology gallery:** OUT OF SCOPE. DO NOT TOUCH IT.

---

# READ THIS FIRST

This file supersedes the old art backlog and the obsolete local-only/no-commit workflow.

## Permanent repository rule from now on
**Current WNG production work belongs in the public `Vardath/Wraith-Nanite-Gravtech-1.6` GitHub repository.**

Do NOT fall back to the older rule that WNG work should remain local/File-Library-only with no incremental GitHub commits.

That old rule is obsolete.

Safe workflow:
1. Work from the current WNG build.
2. Put the current work into the public `Vardath/Wraith-Nanite-Gravtech-1.6` repo on a current working/checkpoint branch when it is not yet ready for `main`.
3. Validate every checkpoint.
4. Keep the File Library package as a backup/checkpoint, not as a replacement for GitHub.
5. Never allow GitHub to fall many revisions behind the actual mod again.

---

# CURRENT AUTHORITATIVE MOD STATE

## Current build
`Wraith-Nanite-Gravtech-D141-RIMWORLD-READABILITY.zip`

Library path:
`/WNG/Wraith-Nanite-Gravtech-D141-RIMWORLD-READABILITY.zip`

SHA-256:
`41a79ddae91ae734e8256f64806bd0733f45ad05328e2cda753992821fde0b32`

This D141 package is the current source of truth for the mod state at handoff.

## D141 validation already achieved locally
- 258 XML files parsed
- 0 XML parse errors
- 696 PNG files
- 0 corrupt PNGs
- 0 empty PNGs
- 299 WNG-owned XML art references checked
- 177 unique WNG art paths
- 0 missing WNG art references
- 0 targeted apparel/furniture directional files missing
- Precursor Field Armour has its own independent art path

**User requirement:** everything must go green. Do not call a GitHub sync/checkpoint complete until the GitHub version has been revalidated and all relevant checks remain green.

---

# D141 ART WORK ALREADY COMPLETED

## Apparel correction
Reworked as RimWorld-style worn sprites:
- `WNG_HunterCoat`
- `WNG_WarriorCarapace`
- `WNG_CommanderCarapace`
- `WNG_QueenRaiment`
- `WNG_HumanFormCombatArmor`
- `WNG_HumanFormUniform`
- `WNG_PrecursorCommandArmor`
- `WNG_PrecursorFieldArmor`

Important result:
- no more full standing/concept-character renders pretending to be worn apparel
- proper worn torso silhouettes
- body-type variants preserved
- N/E/S/W directionals preserved
- `WNG_PrecursorFieldArmor` no longer reuses the combat armour art family

## Furniture readability correction
Reworked to read as functional RimWorld furniture first:
- `WNG_GrowthSeat`
- `WNG_LivingLounge`
- `WNG_FeastTable1x2`
- `WNG_FeastTable2x2`
- `WNG_BedsideNode`
- `WNG_RestCradle`
- `WNG_RestNestDouble`

`WNG_HibernationPod` was intentionally retained because it already read well.

## Targeted simplistic-art polish
Updated:
- `WNG_ProjectileLivingCarbine`
- `WNG_ProjectileHeavyBio`
- `WNG_ProjectileStunner`
- `WNG_ProjectileStunStaff`
- `WNG_ProjectilePrecursorPulse`

Replicator self-art remains preserved unless separately requested.

---

# GITHUB STATE AT HANDOFF — IMPORTANT

Verified immediately before writing this handoff:

## Public WNG repo
`Vardath/Wraith-Nanite-Gravtech-1.6`

### `main`
Commit:
`ed710fdecf1ce44a35dc328638fe8ebf58391280`

This is still the D117-era state.

### `d141-current-art`
Commit:
`ed710fdecf1ce44a35dc328638fe8ebf58391280`

This branch currently points to the SAME D117 commit.

**Therefore `d141-current-art` is only a stub branch right now. It does NOT yet contain D141.**

Several attempted D141 syncs in the prior chat failed or hit connector/tool-call limits before committing. Do not assume any of those uploads succeeded.

## Correct next repository action
The next chat must put the **entire current D141 mod state** into the public 1.6 repo.

Do not sync only a gallery.
Do not sync only selected art.
Do not build a separate art archive as the final solution.

The mod repo itself must contain the current mod.

Recommended safe sequence:
1. Start from authoritative D141 ZIP above.
2. Sync the actual D141 mod tree into `d141-current-art` or another clearly named current checkpoint branch in `Vardath/Wraith-Nanite-Gravtech-1.6`.
3. Verify the branch contents against D141.
4. Run/verify all green checks.
5. Only after successful verification should `main` be updated/merged according to the user's normal release/playtest workflow.
6. Keep the current branch in GitHub from this point onward; do not let local work diverge again.

---

# WNG ART PAGE — EXACT USER REQUIREMENT

The user wants a very simple art page.

## Required design
`https://vardath.github.io/wng-art.html`

It must be:

**one page showing ALL art from the current WNG mod**

Mechanism:
1. Read the current WNG GitHub repo branch recursively.
2. Find every `.png`.
3. Create a thumbnail/card for every PNG.
4. Load each thumbnail directly from that exact PNG in the mod repository using the raw GitHub path.
5. Clicking the thumbnail should open/link directly to that exact mod PNG.
6. Put every image on one continuous page.

## No unnecessary interface
Do NOT add:
- ZIP upload
- file picker
- local browser cache requirement
- changed/new/preserved comparisons
- review-state system
- category selector
- filter menus
- branch comparison UI
- separate website-hosted copies of the art

The user specifically said:
> find the art in the mod, create thumbnail links for each piece and put them on the page as one art page for the whole mod

That is the specification.

## Version-1 asset strategy was correct
The original working page used this pattern:

- `REPO='Vardath/Wraith-Nanite-Gravtech-1.6'`
- recursive GitHub tree API
- filter `*.png`
- raw GitHub image URL for each exact mod path

That strategy should be used again.

The page itself does NOT need copies of the PNGs.

## Current art-page repo state
Verified at handoff:
- website file is a live-GitHub viewer, not the ZIP loader
- it currently uses:
  - repo: `Vardath/Wraith-Nanite-Gravtech-1.6`
  - branch: `main`
- because `main` is still D117, it currently shows old artwork

Once the actual D141 mod is in GitHub:
- point `wng-art.html` at the branch containing the real current D141 mod
- simplify it to the all-PNG one-page gallery above
- verify the live deployed page, not just the source file

**Do not touch `vardath-cosmology.html` or the Cosmology gallery.**

---

# NEXT CHAT — DO THESE TASKS IN THIS ORDER

## Task 1 — restore GitHub continuity
Put the entire authoritative D141 mod into `Vardath/Wraith-Nanite-Gravtech-1.6`.

Do not spend time comparing D117 vs D141 for the art page.
Do not make a gallery package first.
The repository must become current.

## Task 2 — verify everything green
After GitHub contains D141:
- verify expected files are present
- validate XML
- validate PNGs
- validate WNG-owned art refs
- validate directionals / targeted apparel and furniture assets
- check any existing repo CI/actions
- fix failures before proceeding

## Task 3 — fix WNG art page
Make `wng-art.html`:
- one continuous page
- all PNGs from the current mod branch
- every thumbnail directly sourced from that exact mod file
- every thumbnail links to that exact mod file
- no menus
- no file loading
- no comparison/filter system

## Task 4 — verify deployment
Open/check the deployed WNG art page after GitHub Pages completes.
Confirm it is showing current D141 art rather than old D117 art.

## Task 5 — user visual review
Only after the page shows current D141 art should the user inspect remaining art quality before the live RimWorld playtest.

---

# MISTAKES FROM THE PREVIOUS CHAT — DO NOT REPEAT

These are explicit failure modes to avoid.

## 1. Followed an obsolete local-only workflow
Mistake:
- D118-D141 work was allowed to remain in local/File-Library packages while the public 1.6 GitHub repo stayed at D117.

Correct rule:
- the public 1.6 repo is the production repository now
- current checkpoints must exist in GitHub

## 2. Replaced a working live-GitHub art page with a ZIP-loader page
Mistake:
- `wng-art.html` was changed so the user had to select a D140/D141 ZIP in the browser.

Correct rule:
- art page reads the mod repo directly
- no ZIP upload/file picker

## 3. Hid most art behind filters
Mistake:
- page defaulted to “new + changed” and made mod icon/abilities/preserved art appear missing.

Correct rule:
- page shows ALL art by default
- user now wants no menus at all

## 4. Restored the page but pointed it at stale D117 `main`
Mistake:
- live repo logic worked, but it displayed old art because the mod repo itself had not been brought current.

Correct rule:
- update the mod repository FIRST
- then point the page at the branch containing current D141

## 5. Overcomplicated the asset strategy
Mistake:
- attempted separate gallery archives, delta manifests, base64 transfer manifests, compatibility workarounds, and browser cache systems.

Correct rule:
- mod repo is the asset source
- page enumerates `.png`
- raw GitHub link per image
- done

## 6. Started comparing changed files when the user did not ask for that
Mistake:
- spent time calculating D117/D140/D141 art deltas for the gallery.

Correct rule:
- gallery does not care what changed
- it shows every PNG in the current mod

## 7. Created `d141-current-art` but it still pointed at D117
Mistake:
- branch name implied D141 before D141 content existed there.

Correct rule:
- treat it as a stub until actual D141 tree is committed
- never claim a branch contains current work without verifying its commit/tree

## 8. Used huge individual GitHub blob loops and hit tool-call ceilings
Mistake:
- attempted hundreds of per-file blob uploads in single orchestration calls
- caused repeated freezes/tool-call-limit failures

Correct rule:
- use a bounded, reliable repository-sync method
- make smaller deterministic operations
- checkpoint after each successful GitHub mutation
- verify branch SHA after each commit
- never sit in a giant uncommitted upload loop

## 9. Said work was done before checking the live page
Mistake:
- source changes were treated as success while GitHub Pages deployment/browser still showed the wrong version.

Correct rule:
- verify repository source
- verify deployment run
- verify live page
- only then report complete

## 10. Repeated explanations instead of completing the requested operation
Mistake:
- too much “you are right / here is what happened” while the requested fix remained unfinished.

Correct rule:
- execute first
- report concrete completed state and remaining blocker only

## 11. Wrong file ID used while updating this handoff
Mistake:
- first Library overwrite attempt returned `Not Found` because the wrong generated file ID was supplied.
- a later overwrite accidentally preserved stale checklist content.

Correct rule:
- use exact returned file/library IDs
- verify the persisted file contents AFTER mutation
- do not assume an overwrite succeeded merely because the mutation call returned success

## 12. Risk of touching the wrong website area
Correct permanent scope rule:
- WNG art belongs only on `wng-art.html`
- Cosmology gallery is unrelated and must remain untouched

---

# OBSOLETE RULE — EXPLICITLY REVOKED

The following old instruction must NOT be followed anymore:

> “No commits until ready / keep WNG rebuild local-only.”

It was valid during an earlier rebuild stage, but was superseded when production work moved back into the public `Vardath/Wraith-Nanite-Gravtech-1.6` repo.

**Current rule: keep the public repo current using safe working/checkpoint branches, validate green, then merge/update main according to the active workflow.**

---

# TEMPORARY / FAILED-SYNC DEBRIS

Do not mistake these for authoritative source:
- `d141-current-art` branch currently still equals D117
- `/WNG/.d141_transfer` is temporary failed-transfer staging if still present
- local/base64/delta transfer manifests created in the failed chat are implementation debris
- the authoritative build is the D141 ZIP named above until GitHub is successfully brought current

Clean temporary transfer debris only after D141 is safely present and verified in GitHub.

---

# FINAL CURRENT STATUS

### Mod art/build
D141 exists and is locally/File-Library green.

### GitHub mod repo
Still D117 at handoff. Needs D141 sync.

### WNG art page
Live-GitHub architecture restored, but currently shows old D117 art because it reads `main`.

### Immediate continuation objective
**Put D141 into the public WNG repo, make all checks green, then make `wng-art.html` a bare one-page thumbnail gallery of every PNG directly from that current mod branch.**

Do not restart the art pass.
Do not rebuild the gallery architecture.
Do not ask the user to re-explain these requirements.