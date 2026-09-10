# WNG RimWorld 1.6 — Master Plan Addendum: Author Authority and Design Flexibility

Added: 2026-09-10

This file is part of the active public WNG 1.6 rebuild plan and must be read together with `Docs/WNG_REBUILD_MASTER_PLAN.md`.

## Author and design authority

The mod author and final design authority is **Vardath**.

The rebuild plan describes the intended **first complete implementation**. It is not an immutable specification and must never be treated as though individual timings, races/xenotypes, sounds, artwork, processes, systems, values, raid compositions, progression schedules or implementation details are permanently fixed.

If Vardath is dissatisfied with a result, any part of WNG may be changed, redesigned or rebuilt again. The purpose of the plan is to get the complete mod implemented coherently first; tuning and redesign happen afterward from actual testing and author feedback.

## No hard-coded design doctrine

Numbers and structures recorded in the master plan are current implementation targets or remembered design choices unless Vardath explicitly says a point must remain exact.

That means the following are editable later without being treated as regressions merely because they differ from an earlier plan value:

- timers, delays, cooldowns and event cadence;
- race/xenotype/caste/PawnKind implementation choices;
- sounds and sound count;
- artwork, icons, textures and visual style details;
- recipes, costs, resource values and balance;
- progression order and discovery timing;
- raid composition, population sizes and encounter strength;
- UI flows;
- processes and systems;
- quest structure;
- compatibility implementation;
- gravship and craft implementation details;
- any other WNG subsystem when testing or author direction requires a change.

The plan remains useful because it records **what we are trying to build first**, not because it forbids later edits.

## Build-first workflow

The current development priority is:

1. build the complete mod according to the best current understanding of the plan;
2. make each implemented subsystem actually function in RimWorld 1.6;
3. avoid knowingly dangling Defs, missing classes and obviously broken runtime paths;
4. continue through the remaining plan instead of repeatedly polishing or freezing unfinished systems;
5. after the complete first build exists, use live testing and Vardath's feedback to rebalance, replace, redesign or rebuild anything necessary.

Do not spend development time creating anti-regression rules that make ordinary design choices difficult to change. Do not use audits to force code to preserve an old value or implementation simply because it was once written down.

Compile/build checks may be used to catch actual C# build breakage. Runtime testing is what determines whether the mod works. Design acceptance belongs to Vardath, not to static audits.

## Repository continuity rule

The active reconstruction and continuity repository is:

`Vardath/Wraith-Nanite-Gravtech-1.6`

For current work and future **“refresh memory and continue”** sessions, begin from this public repository and its continuity documents.

Do **not** write to the private WNG repository unless Vardath explicitly re-authorizes private-repo work in a later conversation. Private/historical material may be used only when access is available and useful as reference evidence; the public 1.6 repository must contain enough plan and handoff information to continue without depending on private access.

## Interpretation rule

If wording elsewhere in `WNG_REBUILD_MASTER_PLAN.md`, the Queen contract, README or older notes says a value is “locked”, “canonical”, “protected”, “required”, or similar, interpret that wording as the **current first-build target** unless Vardath has explicitly stated that the specific detail must remain exact.

Newer explicit instructions from Vardath always override older plan text. Update the public continuity documents when those instructions materially change the intended first build.

This addendum does not erase the existing plan. It changes how the entire plan is to be interpreted and used.