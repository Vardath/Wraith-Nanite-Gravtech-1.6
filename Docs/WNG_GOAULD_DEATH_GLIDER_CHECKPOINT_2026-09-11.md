# WNG — Goa'uld Death Glider checkpoint

Date: **2026-09-11**  
Author/design authority: **Vardath**

## Implemented fighter

- Real optional ONAC-backed `WNG_GoauldDeathGlider` fighter.
- Native `Building_PassengerShuttle` foundation rather than a decorative pawn/prop.
- Native `CompShuttle`, `CompTransporter`, `CompLaunchable` and `CompRefuelable` stack.
- `ONAC_Naquadah` construction and `ONAC_LiquidNaquadria` fuel; no duplicate WNG Goa'uld resource.
- Deliberately short world range because a standard Death Glider has no independent hyperdrive.
- Native incoming/leaving skyfallers, `TransportShipDef` and shuttle world object.
- Two-person conscious humanlike combat-crew requirement for the WNG combat-sortie command.
- Two physical attack passes using the existing exact-craft WNG skyfaller framework.
- Four staff-cannon bolts per pass, distributed over up to two nearby hostile pawn/building targets.
- Real sortie fuel consumption.
- Return-to-origin behavior: after the final pass, the exact Glider attempts to land on the exact cell it launched from before falling back to normal landing-cell search.
- Save/load state for active sortie, hostile departure intent, completed passes, shots fired and return cell.
- Hackable controls so a stranded/captured hostile Glider can transfer to colony control through the existing native hacking component.

## Hostile System Lord strike

`WNG_GoauldDeathGliderStrike` is now a bounded optional ThreatBig incident:
- activates only when the verified ONAC + RimGate Biotech ecosystem is active;
- chooses only existing world factions whose exact Def is one of `JKB_JaffaApophis`, `JKB_JaffaAnubis`, or `JKB_JaffaRa` and which are actually hostile to the player;
- never creates a duplicate Goa'uld/Jaffa faction;
- verifies crew PawnKinds against the exact RimGate Biotech package and uses only `JKB_JaffaWarrior01/02/03`;
- loads two exact generated Jaffa into the Glider's native `CompTransporter` before flight;
- runs the same exact-craft physical staff-cannon sortie used by the player fighter;
- after the second pass, the exact Glider returns to its edge cell and attempts RimWorld's native `TransportShip` / `ShipJob_FlyAway` withdrawal using the existing hostile-shuttle terminal arrival action;
- if native withdrawal cannot occur, the exact hostile craft and exact loaded Jaffa remain physically on the map instead of being silently deleted;
- one active hostile System-Lord Glider is allowed per map by the incident gate; base chance is 0.18 with a 12-day minimum refire interval.

## Ha'tak carriage contract

No fake hangar inventory is introduced. Odyssey already moves spawned shuttle Things that are standing on connected gravship substructure when the gravship launches. Therefore a Death Glider parked on Ha'tak connected substructure is physically carried with the mothership. Dedicated bay art/staging may be added later, but it must never replace this exact-craft relationship.

## Not yet claimed

- no production Death Glider texture yet; the vanilla shuttle graphic remains a mechanics placeholder;
- no dedicated Death Glider audio/VFX pass yet;
- no hostile Ha'tak map/site encounter that physically begins with its own parked/deployed Death Gliders yet; the implemented storyteller strike enters from the map edge;
- no claim that the feature has been live-tested inside RimWorld/ONAC in the current environment;
- no true cross-map/orbital Death Glider operation beyond the native short-range shuttle world-travel stack.

## Next Goa'uld move

Continue the Ha'tak branch without replacing native systems: reconcile a real hostile Ha'tak encounter/carrier deployment path and the still-missing true cross-map/orbital bombardment layer, then move into final sensors/presentation only where Stargate function justifies them.
