# WNG — Goa'uld Death Glider checkpoint

Date: **2026-09-11**  
Author/design authority: **Vardath**

## Implemented

- Real optional ONAC-backed `WNG_GoauldDeathGlider` fighter.
- Native `Building_PassengerShuttle` foundation rather than a decorative pawn/prop.
- Native `CompShuttle`, `CompTransporter`, `CompLaunchable` and `CompRefuelable` stack.
- `ONAC_Naquadah` construction and `ONAC_LiquidNaquadria` fuel; no duplicate WNG Goa'uld resource.
- Deliberately short `fixedLaunchDistanceMax` because a standard Death Glider has no independent hyperdrive.
- Native incoming/leaving skyfallers, `TransportShipDef` and shuttle world object.
- Two-person conscious humanlike combat-crew requirement for the WNG combat-sortie command.
- Two physical attack passes using the existing exact-craft WNG skyfaller framework.
- Four staff-cannon bolts per pass, distributed over up to two nearby hostile pawn/building targets.
- Real sortie fuel consumption.
- Return-to-origin behavior: after the final pass, the exact Glider attempts to land on the exact cell it launched from before falling back to normal landing-cell search.
- Save/load state for active sortie, completed passes, shots fired and return cell.

## Ha'tak carriage contract

No fake hangar inventory is introduced. Odyssey already moves spawned shuttle Things that are standing on connected gravship substructure when the gravship launches. Therefore:

1. build or land the Death Glider on Ha'tak connected substructure;
2. leave the exact fighter physically parked aboard the ship;
3. launch the Ha'tak through Odyssey's native gravship workflow;
4. Odyssey carries that exact Death Glider with the gravship;
5. on a loaded map, crew it through the native transporter and launch a WNG staff-cannon combat sortie;
6. the exact craft returns to its original deck cell if still available.

This is the functional carrier/bay relationship. Dedicated Ha'tak bay art and encounter staging can be added later without replacing this physical model.

## Not yet claimed

- no production Death Glider texture yet; the vanilla shuttle graphic remains a mechanics placeholder;
- no dedicated Death Glider audio/VFX pass yet;
- no hostile System-Lord/Jaffa Death Glider raid/deployment integration yet;
- no claim that the feature has been live-tested inside RimWorld/ONAC in the current environment;
- no true cross-map/orbital Death Glider operation beyond the native short-range shuttle world-travel stack.

## Next Goa'uld move

Use the exact verified external System-Lord factions (`JKB_JaffaApophis`, `JKB_JaffaAnubis`, `JKB_JaffaRa`) to add bounded hostile Death Glider deployment/raid support without creating duplicate Goa'uld/Jaffa factions. Preserve exact craft/crew identity and the native shuttle/Ha'tak carriage model above.
