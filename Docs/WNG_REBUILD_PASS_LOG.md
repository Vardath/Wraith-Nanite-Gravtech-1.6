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

8. **Wraith physical living airtight gravship door** — main `957f3a2d6e637ddb66aec41ec0d3f778b3dbda1d`.
   - Added a real native `DoorBase`/`Building_Door` Wraith living bulkhead, distinct in presentation from the Asuran technological door.
   - Uses `isAirtight=true`, native Substructure placement, `WNG_WraithGravships` gating, Wraith bio-sludge construction material, native power-net support, and the Wraith technology-family marker.
   - Repository file was fetched back; XML parsing and targeted contract assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Live RimWorld pressure/door behavior remains pending.

9. **Wraith native atmosphere-support organ** — main `c155babaaab0e19451bebd4797a568374561d2a6`.
   - Added `WNG_WraithAtmosphericOrgan`, a living Wraith atmospheric component using Odyssey's native `CompProperties_OxygenPusher` rather than a custom atmosphere simulation.
   - Requires native power, drops to low idle power outside vacuum, attaches to walls, uses Wraith bio-sludge construction material, requires `WNG_WraithGravships`, and carries the Wraith technology-family marker.
   - Repository file was fetched back; XML parsing and targeted contract assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Native OxygenPump graphics remain mechanics-validation placeholders; live RimWorld atmosphere behavior remains pending.

10. **Wraith native orbital-sensor organ** — main `b7a4ce8d37d5ef3247d7283d70b01fdc5d1e3074`.
   - Added `WNG_WraithOrbitalSensor`, a cultivated living sensor using RimWorld 1.6 Odyssey's native `CompOrbitalScanner` rather than custom orbital-discovery code.
   - Native source confirms the scanner uses `CompPowerTrader` and the base game's orbital-signal/quest discovery component.
   - Uses Wraith bio-sludge construction material, requires `WNG_WraithGravships`, remains outdoors as required by the native scanner pattern, and carries the Wraith technology-family marker.
   - Repository file was fetched back; XML parsing and targeted contract assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Native OrbitalScanner graphics remain mechanics-validation placeholders; live RimWorld signal-discovery behavior remains pending.

11. **Wraith native vacuum membrane** — main `1185fbd725375f9b9f2a4fd9bd49b55b004f1254`.
   - Added `WNG_WraithVacuumMembrane`, a living free-passage pressure membrane using RimWorld 1.6 Odyssey's native `Building_VacBarrier` rather than custom vacuum logic.
   - Native source confirms powered barriers allow free pawn passage while blocking vacuum exchange; power loss reopens vacuum exchange.
   - Requires gravship Substructure, Wraith bio-sludge construction material, `WNG_WraithGravships`, native power, and the Wraith technology-family marker.
   - Repository file was fetched back; XML/native-contract assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Native VacBarrier graphics remain mechanics-validation placeholders; live RimWorld vacuum behavior remains pending.

12. **Wraith native bioelectric gravship power organ** — main `1acd5c799fa5fbc2578dc8bd6219fd09cf0dd737`.
   - Added `WNG_WraithBioelectricOrgan`, a living Wraith power generator using Odyssey's native `CompPowerPlantGravcore` rather than a custom electrical system.
   - Native source confirms power output is forced to zero whenever the organ is not standing on gravship substructure.
   - Produces 2,200 W into RimWorld's native power network, uses Wraith bio-sludge construction material, requires `WNG_WraithGravships`, and carries the Wraith technology-family marker.
   - Repository file was fetched back; XML parsing and targeted contract assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Native GravcorePowerCell graphics remain mechanics-validation placeholders; live RimWorld power behavior remains pending.

13. **Wraith native visible/hidden power conduits** — main `8a419389a8f394956083520299c57a61e8b6ceb6`.
   - Added `WNG_WraithBioelectricConduit` and `WNG_WraithHiddenBioelectricConduit` as Wraith-themed presentations of RimWorld's native `PowerConduit` contract.
   - Both inherit the native `CompPowerTransmitter` network behavior and transmitter link grammar; no parallel WNG electrical network is introduced.
   - Both require gravship Substructure and `WNG_WraithGravships`; visible conduit uses Wraith bio-sludge/steel and the hidden variant preserves the native hidden-conduit interaction pattern.
   - Repository file was fetched back; XML parsing and targeted inheritance/link/research/material assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Native conduit atlas/icon art remains mechanics-validation presentation; live RimWorld power-link visuals and replacement behavior remain pending.

14. **Asuran native visible/hidden power conduits** — main `f92b39aa0a5c68f9142bcfe7298a8ce40313c25e`.
   - Added `WNG_AsuranPowerConduit` and `WNG_AsuranHiddenPowerConduit` as Asuran/Ancient-derived presentations of RimWorld's native `PowerConduit` contract.
   - Both inherit native `CompPowerTransmitter` behavior and transmitter linking; no parallel nanite electrical network is introduced.
   - Both require gravship Substructure, `WNG_AsuranGravships`, and nanite-sludge/Plasteel construction; the hidden variant preserves native concealed-conduit interaction flags.
   - Repository file was fetched back; XML parsing and targeted inheritance/link/research/material/hidden-visibility assertions passed.
   - No C# changed in this pass; no redundant C# compile was run.
   - Native conduit atlas/icon art remains mechanics-validation presentation; live RimWorld power-link visuals and replacement behavior remain pending.

15. **Wraith power-conduit family marker** — branch `rebuild/wraith-power-conduit-theme-marker-20260912`.
   - Added explicit `CompProperties_WNGGravshipPartTheme` with `Wraith` theme to both visible and hidden Wraith bioelectric conduits.
   - This keeps native `PowerConduit` / `CompPowerTransmitter` behavior intact while making the structures explicit participants in WNG's gravship-family identity and inspection contract.
   - Repository file was fetched back; XML structure and both Wraith theme markers were verified.
   - No C# changed in this pass; no redundant C# compile was run.
   - Live RimWorld conduit networking and family-inspection presentation remain pending.
