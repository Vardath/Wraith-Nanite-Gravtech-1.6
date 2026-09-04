# Manual installation — RimWorld 1.6

Steam Workshop will be the recommended installation route once the Workshop item is public. This repository exists as a manual-download mirror for users who prefer GitHub.

## GitHub ZIP install

1. Open this repository's **Code** menu and choose **Download ZIP**.
2. Extract the downloaded archive.
3. Rename the extracted folder from something like `Wraith-Nanite-Gravtech-1.6-main` to:

   `Wraith-Nanite-Gravtech`

4. Move that folder into RimWorld's local `Mods` directory.
5. Check that this path exists directly inside the mod folder:

   `Wraith-Nanite-Gravtech/About/About.xml`

6. Start RimWorld and enable **Wraith & Nanite Gravtech** in the Mods menu.

The folder should directly contain:

- `About/`
- `Assemblies/`
- `Defs/`
- `Languages/`
- `Patches/`
- `Sounds/`
- `Textures/`

Do not leave an extra nested `Wraith-Nanite-Gravtech-1.6-main`/`Wraith-Nanite-Gravtech` level inside the installed mod.

## Requirements

- RimWorld 1.6
- Biotech DLC
- Odyssey DLC

Ideology is optional and is only needed for the Neural Interface Enslave operation.

CatCraft's Stargates! is an optional integration rather than a hard dependency.

## Updating a manual installation

When a newer verified 1.6 mirror is published, delete or move the old `Wraith-Nanite-Gravtech` folder first, then install the new copy. Do not merge two versions together; stale XML, textures or assemblies from an older build can create misleading errors.

## Bug reports

For useful reports, include the observed behavior plus a fresh RimWorld `Player.log`. If available, a RimDoctor report is also useful. Mention whether the problem occurs with only the required DLCs + WNG or only in a larger mod list.
