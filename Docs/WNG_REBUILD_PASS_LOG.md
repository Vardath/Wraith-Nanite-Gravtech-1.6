# WNG rebuild pass log

Purpose: persistent continuity record for the current public RimWorld 1.6 rebuild. Newer explicit Vardath instructions override older notes. Historical/private builds remain reference material only; no historical state is treated as known-good.

## Pass discipline

- One discrete gameplay mechanic per rebuild pass.
- Record the result here before that pass is merged.
- Static compile/XML/contract checks are evidence, not proof of live RimWorld behavior.
- Never record a pass as live-validated unless it was actually tested in RimWorld.
- Final art/audio and live gameplay acceptance remain separate from mechanics-validation placeholders.

## Backfilled verified passes — 2026-09-12

1. **Asuran evidence-analysis bridge** — main `2b86da292f531e6ccb9a2de2d9011f56432f5d31`.
   - Analyzable Asuran nanite residue/evidence gate added.
   - Windows C# build, all Def/Patch XML, and targeted evidence invariants passed.
   - Live RimWorld behavior not yet acceptance-tested.

2. **Wraith flight evidence gate** — main `9b741406fa029efebd11d783397eec20395de7ae`.
   - Wraith shuttle/gravship progression now inherits the analyzed Hive Heart -> Wraith living technology gate.
   - Windows build/XML/chain invariant passed.
   - Live RimWorld behavior not yet acceptance-tested.

3. **Native PilotSubpersonaCore support on themed gravships** — main `ff6811316fecc5f7ce5e100833a714151a4ac250`.
   - Exact native `PilotSubpersonaCore` is the only deliberate unthemed gravship-facility exception; ordinary vanilla tanks/thrusters/etc. remain rejected by family isolation.
   - Windows build/XML/narrow whitelist invariants passed.
   - Live RimWorld behavior not yet acceptance-tested.

4. **Human-form nanite repair/reconstruction** — main `80b5dec4d0a96eb3b071e9ff55fb3d954af2f484`.
   - Injury repair restored to 450 ticks / fixed 1.5% reserve per actual pulse; depleted emergency 1,800 ticks.
   - Missing-part reconstruction added at 30,000 ticks / fixed 25% reserve; depleted emergency 90,000 ticks.
   - Native missing-part ancestry/RestorePart path and save-persistent timer validated by Windows build/XML/contract checks.
   - Live RimWorld behavior not yet acceptance-tested.

5. **Legacy Nanite Reserve save migration** — main `5a97e6e78cb91b8f0fc0367474abeea0ef46de16`.
   - Non-generating legacy `WNG_NaniteReserve` compatibility shim preserves old saved resource percentage and removes itself only after successful transfer to the current reserve.
   - Conservative one-shot migration build/XML/transaction invariants passed.
   - Live old-save loading not yet acceptance-tested.

6. **Neural Interface exact-person copy** — main `6f3d1bb5a63bcaf25cd521c052a5b5d6eea77807`.
   - Generated shell genome is cleared first; source endogene/xenogene split, biography, skills/passions/XP and additional appearance fields are copied before WNG nanite identity is layered back in.
   - Windows build/XML/exact-copy ordering invariants passed.
   - Live RimWorld behavior not yet acceptance-tested.

7. **Asuran physical airtight gravship door** — main `2890e0ae6d9ce33764a0428e0dce2c57f9f38acf`.
   - Added real native `DoorBase`/`Building_Door` Asuran bulkhead distinct from the free-passage VacBarrier.
   - Closed airtight door uses RimWorld 1.6 native vacuum blocking; open door permits exchange normally.
   - One-file XML/native-contract verification completed; live pressure/door behavior remains pending.

## Current pass

8. **Wraith physical living airtight gravship door** — branch `rebuild/wraith-living-door-20260912`.
   - In progress.
