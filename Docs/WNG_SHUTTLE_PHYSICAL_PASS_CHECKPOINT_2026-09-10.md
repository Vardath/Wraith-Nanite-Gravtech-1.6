# WNG shuttle physical-pass checkpoint — 2026-09-10

Newest explicit Vardath instructions override this checkpoint.

## Completed in this slice

### Standalone Wraith Dart culling incident
- `WNG_WraithDartCulling` is again a real standalone WNG incident and does not require CatCraft Stargates.
- It selects a hostile Wraith lineage; strategic faction hunger can increase dispatch pressure but does not become a pawn-feeding or popup system.
- One real `WNG_WraithDart` is created and the fresh mission layer begins immediately.
- Historical WNG behavior was used only as reference; obsolete withdrawal-by-destroying-the-craft was not restored.

### Physical two-pass shuttle flight
- RimWorld 1.6.4871 APIs were inspected directly from the same `Krafs.Rimworld.Ref` assembly WNG compiles against.
- Confirmed `RimWorld.Skyfaller`, `RimWorld.SkyfallerMaker.MakeSkyfaller(ThingDef, Thing)`, `ShuttleIncoming`, `FlyShipLeaving`, `PassengerShuttleIncoming`, and `PassengerShuttleLeaving`.
- WNG now uses `WNGAttackPassSkyfaller`, a real `Skyfaller` containing the exact shuttle Thing.
- No proxy/cosmetic duplicate shuttle is made.
- The exact Dart/Puddle Jumper, including comps, faction, damage state and native transporter contents, is carried through pass one, pass two and final landing.
- Dart culling can operate while the exact Dart is held by the pass skyfaller by accepting the actual mission Map explicitly.
- Puddle Jumper drone passes use the same exact-craft physical-pass wrapper.
- If creating the next pass fails, the exact craft is landed rather than deleted.
- If starting physical flight fails, the original craft is restored to-map and the mission is marked stranded rather than pretending a flyby occurred.

### Compile/XML status
- The standalone Dart incident passed C# compile and all Def XML parsed.
- The exact-craft physical-pass implementation passed C# compile and all Def XML parsed against RimWorld 1.6.4871.
- Temporary validation and API-probe workflows were removed after use.

## Important verification boundary

Compilation proves API/type correctness, not in-game animation quality. The physical pass system still requires live RimWorld testing for:
- visual trajectory/scale/rotation;
- pass timing;
- whether the chosen pass cells read visually as two genuine sweeps;
- exact landing placement around roofs/obstacles;
- culling/drone projectile presentation while the craft is airborne;
- save/reload during an active skyfaller pass.

Do not call those live-behavior points proven until tested in-game.

## Native boarding remains authoritative

The shuttle Thing itself still inherits native `ShuttleBase`. WNG physical passes temporarily hold that exact Thing in a RimWorld skyfaller; they do not replace `CompShuttle`, `CompTransporter`, native pawn loading, or native player launch controls.

## Still open in the shuttle/Stargate branch

1. Tie hostile Dart retreat to actual Wraith pilot/crew behavior after landing.
2. Tie `CommitNativeEscapeWithCaptives` to confirmed native shuttle departure completion, not merely a retreat request.
3. Concrete CatCraft adapter for outbound redial/traversal and iris/shield state once the actual optional-mod API/source is available.
4. Apply the existing light-roof punch-through / thick-rock catastrophic obstruction evaluator to real CatCraft emergence cells when that adapter exists.
5. Flesh out Wraith scout and cruiser assault/transport behavior without duplicating the Dart role.
6. Continue the broader Wraith living-tech / Growth Chamber / Grav Engine progression after the shuttle lifecycle is coherent.
7. RimGate/ONAC exact internal package/faction/xenotype verification remains pending Vardath supplying the correct Biotech RimGate source; this must not block the main WNG rebuild.
