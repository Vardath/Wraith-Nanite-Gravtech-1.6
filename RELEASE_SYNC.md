# Public release synchronization record

This repository is the public RimWorld 1.6 release mirror for Wraith & Nanite Gravtech.

## Authority model

- Development authority: private repository `Vardath/Wraith-Nanite-Gravtech`, branch `develop`.
- Public release mirror: this repository, `Vardath/Wraith-Nanite-Gravtech-1.6`, branch `main`.
- Steam Workshop: once published, the Workshop payload and this public repository should represent the same released mod state.
- New features, balance work, fixes and experiments remain private until they pass the required audit/live-validation boundary and are intentionally promoted.
- A confirmed error in a released public/Steam build is fixed in private development first, audited, then promoted to both public GitHub and Steam together.

## Current synchronized candidate

Private release head promoted here: `13d437b36bc0db313881e585fc76dbf16ae2b851`

Post-merge verification:
- Static Def Audit #2492
- GitHub Actions run `33927936424`
- conclusion: SUCCESS
- playable artifact id: `9957491084`
- CI artifact digest: `sha256:b9e7c4e2e1cf5210c9f5a2b088cb279132ba0ab003e039d4d9780a3678928f6b`

The promoted live-log repair removes the obsolete `WNG_RimWorld16_RuntimeRepair.xml` patch shim and uses the corrected Wraith Boneblade/War Glaive living-equipment patch targets.

Live RimWorld testing remains the final release proof before the Steam Workshop version is published.
