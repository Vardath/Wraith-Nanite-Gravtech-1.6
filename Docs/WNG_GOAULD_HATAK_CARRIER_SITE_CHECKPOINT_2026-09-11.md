# WNG — hostile Ha'tak carrier-site checkpoint

Date: **2026-09-11**  
Author/design authority: **Vardath**

## Stargate identity used

A Ha'tak is a long-deployment Goa'uld capital ship carrying Jaffa, transport rings and bays of Death Gliders. It can land on a planetary surface. WNG therefore represents this encounter as a **landed carrier site** rather than pretending a generic colony raid is an orbital mothership.

## Implemented site slice

- `WNG_GoauldHatakCarrier` SitePart and `WNG_GoauldHatakCarrierDiscovered` world incident.
- Activates only with the verified ONAC + RimGate Biotech integration.
- At most one active carrier site at a time; discovery range 12–28 world tiles; current base chance 0.06 with 30-day minimum refire interval.
- Site faction must be an actual existing hostile `JKB_JaffaApophis`, `JKB_JaffaAnubis`, or `JKB_JaffaRa` world faction.
- No duplicate Goa'uld/Jaffa faction is created.
- Site map builds a compact diamond-shaped `WNG_GoauldSubstructure` deck.
- Spawns Odyssey's exact native `GravEngine`, assigns the real System-Lord faction, records Goa'uld runtime theme and names it for that faction.
- Spawns the exact native `GravshipHull` around the deck perimeter and records Goa'uld runtime theme on each hull section.
- Uses the already-implemented real Ha'tak facilities: pel'tac, two large liquid-Naquadria tanks, two large sublight drives, two field projectors, native shield generator, naquadah power core, two heavy plasma batteries and transport rings.
- Generated tanks are filled through native `CompRefuelable`.
- Hidden same-family liquid-Naquadria pipe paths physically join both tanks to the engine connection area.
- Hidden native power conduits connect the power core toward shield, rings and both plasma batteries.
- Eight exact verified RimGate Biotech Jaffa warriors defend the deck under a bounded `LordJob_DefendBase`.
- Two exact `WNG_GoauldDeathGlider` shuttle Things are physically parked on the carrier substructure.
- Each parked Glider contains two exact verified Jaffa crew in its native `CompTransporter`.
- A save-safe map component waits 900 ticks after the carrier is armed, then orders those exact two parked Gliders through one physical combat sortie each. They return to their exact deck cells when available.
- No fake hangar inventory, proxy Glider or copied crew is used.
- Carrier generation removes incomplete generated Things/pawns if an essential generation stage fails.

## Important limitations / not yet claimed

- This is a **landed hostile carrier encounter**, not a finished hostile mothership AI able to take off, retreat or pursue the player on the world layer.
- The generated ship layout has mechanics-first placeholder art and a compact game-scale deck; final central-pyramid/outer-superstructure Ha'tak visual treatment remains part of the production-art pass.
- The hull perimeter deliberately leaves boarding/service gaps instead of inventing a Goa'uld door Def in this slice.
- Native facility/fuel/power behavior still requires live RimWorld + ONAC validation; source/static reasoning is not a live-game test.
- Player-controlled true cross-map/orbital Ha'tak bombardment is now implemented separately on the physical heavy plasma battery; the landed hostile carrier does not yet autonomously take off into orbit or use that system as world AI.

## Next Ha'tak mechanical slice

Reconcile **hostile carrier takeoff/retreat/pursuit and hostile use of orbital fire** only through genuine Odyssey gravship/world mechanics. Do not replace the landed carrier with an abstract world proxy merely to claim that it moved.
