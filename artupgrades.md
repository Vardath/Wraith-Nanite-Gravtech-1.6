# WNG ART UPGRADES — ACTIVE HANDOFF

**Project:** Wraith-Nanite-Gravtech (WNG), RimWorld 1.6  
**Production repo:** `Vardath/Wraith-Nanite-Gravtech-1.6`  
**Branch:** `main`  
**Updated:** 2026-09-22

This file is now the active continuation brief. The previous completed-art list, stale progress summaries, old user-instruction blocks, old website notes, and superseded audit commentary have been removed. The numbered Errors / Mistakes section at the bottom is retained.

The next GPT should start by reading the current public `main`, this file, the exact live Defs, and the current PNGs. Do not assume an old local copy, ZIP, earlier branch, generated preview, website thumbnail, or previous completion claim is authoritative.

---

# CURRENT ART TASKS

## 1. Goa'uld small sublight drive — COMPLETED 2026-09-22

**Live Def:** `Defs/Gravships/Gravship_Goauld.xml`  
**DefName:** `WNG_GoauldSmallSublightDrive`  
**World texture family:**
- `Textures/Things/Building/Goauld/Gravship/WNG_GoauldSmallSublightDrive.png`
- `..._north.png`
- `..._east.png`
- `..._south.png`
- `..._west.png`

Current game use:
- parent: `WNG_SmallThrusterBase`
- `Graphic_Multi`
- `drawSize (1.2, 2.2)`
- compact Ha'tak sublight drive
- requires the Goa'uld family power grid and Liquid-Naquadria fuel feed
- adds gravship range and has a real thruster exclusion/flame direction

Required work:
- inspect every current facing at full size and RimWorld scale;
- compare against the **actual Stargate Ha'tak / Goa'uld propulsion visual language** before drawing;
- compare with how vanilla RimWorld/Odyssey presents small gravship thrusters: clear front/back orientation, readable engine body, exhaust relationship, centred footprint and clean silhouette;
- adjust or replace the whole family if needed so it looks like professional Goa'uld ship technology rather than a generic engine;
- use dark naquadah / black structure, bronze-gold framing and restrained amber/orange energy where appropriate;
- preserve correct directional function and exhaust orientation;
- no clipped housing, floating pieces, square panel, halo, or dirty alpha.

Do not touch the large sublight drive unless direct comparison shows a shared visual problem that must be fixed for family consistency.

**Completion note (2026-09-22):**
- replaced the small-drive family with the approved compact Ha'tak module: dark naquadah/gunmetal mass, bronze-gold structural framing and restrained amber drive energy;
- preserved the fixed 1×2 thruster function and made thrust/exhaust direction explicit in north/east/south/west facings;
- for this symmetric mechanical module, the cardinal family is deterministically derived from the approved north master rather than inventing unrelated side-view artwork;
- finalizer fully decoded all five outputs as 512×512 RGBA and rejected any sprite whose alpha touches the canvas edge;
- finalizer completed successfully; source-commit static validation and release-gap audit also passed.


## 2. Goa'uld power coupler — COMPLETED 2026-09-22

**Live Def:** `Defs/Gravships/Gravship_Goauld.xml`  
**DefName:** `WNG_GoauldPowerCoupler`  
**Texture:** `Textures/Things/Building/Goauld/Gravship/WNG_GoauldPowerCoupler.png`

Current game use:
- `Graphic_Single`
- `size (1,1)`
- `drawSize (1.5,1.5)`
- intentional interface between a vanilla colony power net and the isolated Goa'uld ship grid
- family role: Coupler
- capacity: 8000 W

Required work:
- inspect current art and redo it if it does not immediately read as a compact Goa'uld power-interface/coupling device;
- ground the design in Stargate Goa'uld / Ha'tak power and crystal/naquadah technology rather than generic sci-fi;
- compare with vanilla RimWorld 1×1 power infrastructure for silhouette/readability, without copying vanilla art;
- professional centred 1×1 building sprite, strong readable mass, clean transparent edge;
- dark naquadah body, bronze/gold structure and integrated amber/orange energy/crystal cues;
- no decorative loose linework or detached pieces.

**Completion note (2026-09-22):**
- replaced the coupler with a dedicated 512×512 RGBA Goa'uld interface sprite generated from a repo-owned renderer;
- preserved the live 1×1 `Graphic_Single` role and `drawSize (1.5,1.5)`;
- the object reads as a compact power bridge rather than a generator: dark naquadah chassis, bronze/gold coupling jaws, two clear interface sockets and one restrained amber crystal junction;
- kept the glow localized to the crystal/conduits so it remains legible at game scale without turning the whole tile into an energy effect;
- generator validates non-empty alpha, 512×512 RGBA output, safe transparent margins and no edge contact before the PNG is committed;
- temporary binary-transfer fragments were deleted and are not production sources.

## 3. Ancient control chair — full redo; current art is cut off

**Primary Def:** `Defs/ThingDefs/Things_AncientControl.xml`  
**DefName:** `WNG_AncientControlChair`  
**World texture family:**
- `Textures/Things/Building/Precursor/Control/WNG_AncientControlChair.png`
- `..._north.png`
- `..._east.png`
- `..._south.png`
- `..._west.png`
**Build icon:** `Textures/UI/WNG/Build/WNG_AncientControlChair.png`

Current game use:
- `FurnitureBase`
- `Graphic_Multi`
- `size (2,2)`
- `drawSize (2.2,2.2)`
- rotatable, default south
- interaction cell south of the chair
- powered Ancient neural command station
- operates the WNG drone-control / allied command network

Known defect:
- a substantial part of the chair is cut off. Do not merely pad the broken image; **redo the chair composition** so the complete object is present.

Required work:
- research/inspect Stargate Ancient/Lantean control-chair references before drawing;
- compare with vanilla RimWorld furniture/building presentation for a 2×2 interactable object;
- create a complete, centred, professional Ancient control chair with readable seat/back/control structure and Ancient silver/white/bronze-neutral material language plus restrained blue/cyan active elements;
- ensure all authored directions make sense and the interaction-facing orientation is readable;
- make a separate clean build icon from the same approved design;
- no clipping, no half-object, no baked square background.

**Important shared-texture check:** `Defs/ThingDefs/Things_AsuranDormantFacility.xml` currently uses this same chair texture family for `WNG_AsuranDormantReconstructionPlinth` at `drawSize (1.2,1.2)`. Before committing the chair redo, inspect that use. If the plinth should not visually be a control chair, give it its own texture instead of forcing one image to serve two different objects.

## 4. Asuran Queen Recovery Carrier — preserve approved craft, finish incomplete pieces

**Def:** `Defs/ThingDefs/Things_ReplicatorQueenRecovery.xml`  
**DefName:** `WNG_AsuranQueenRecoveryCarrier`  
**World family:**
- `Textures/Things/Building/Precursor/Shuttle/WNG_AsuranQueenRecoveryCarrier.png`
- `..._north.png`
- `..._east.png`
- `..._south.png`
- `..._west.png`
**Build icon:** `Textures/UI/WNG/Build/WNG_AsuranQueenRecoveryCarrier.png`

Current game use:
- NPC-only autonomous Asuran/Lattice recovery shuttle
- `Graphic_Multi`
- `size (3,5)`, `drawSize (3,5)`
- real Odyssey passenger-shuttle/transporter/launch machinery

Current visual decision:
- **the main carrier looks good and should be preserved.**
- Some associated art pieces are incomplete.

Required work:
- visually inspect **all six current carrier art pieces** side-by-side and identify the incomplete/mismatched pieces instead of redesigning the approved ship;
- finish only the incomplete pieces so every facing and the build icon share the same hull design, lighting, detail level, crop, transparency and quality;
- preserve individually authored facings; **do not derive east/south/west by blindly rotating one master**;
- full-decode every finished PNG and verify centring/edge alpha after commit.

## 5. Replicator graphics — upgrade the entire current Replicator art family

The earlier conclusion that the existing Replicator graphics should simply be preserved is **superseded**. The user has explicitly reopened the full Replicator family for a professional upgrade.

Start by enumerating every live PNG whose path/name is Replicator-related and tracing its real Def usage. Current scope includes at least the following.

### Replicator blocks
**Def:** `Defs/ThingDefs/Things_ReplicatorMatter.xml`  
**DefName:** `WNG_ReplicatorMatter`  
**Label:** `replicator blocks`  
**Texture:** `Textures/Things/Item/Resource/Replicator/WNG_ReplicatorMatter.png`

Function:
- loose self-organizing physical Replicator blocks;
- stacks can reconstruct a hostile Drone if left uncontained.

Required visual:
- unmistakable Stargate block-Replicator material: small precision metallic interlocking blocks/components, not generic ore, rocks, scrap or nanite dust;
- readable as a stackable RimWorld resource at inventory/map scale.

### Replicator core fragment
**Def:** `Defs/ThingDefs/Things_SovereignLattice.xml`  
**DefName:** `WNG_ReplicatorCoreFragment`  
**Texture:** `Textures/Things/Item/Resource/Replicator/WNG_ReplicatorCoreFragment.png`

Function:
- fractured reusable Replicator command-lattice fragment recovered from stronger block-form Replicators;
- also reused by several other systems/icons, so trace every current use before replacing it.

Required visual:
- a fractured but obviously sophisticated Replicator command/core lattice made from the same block technology;
- visually distinct from ordinary Replicator Blocks;
- not a generic crystal/gem.

### Replicator pulse caster
**Def:** `Defs/ThingDefs/Weapons_ReplicatorAdaptation.xml`  
**DefName:** `WNG_ReplicatorPulseCaster`  
**Texture:** `Textures/Things/Item/Weapon/Replicator/WNG_ReplicatorPulseCaster.png`

Function:
- integrated machine organ reproduced from assimilated ranged technology;
- `Graphic_Single`, `drawSize (0.8,0.8)`;
- also currently reused by the Replicator artillery caster.

Required visual:
- should look **grown/assembled into a Replicator body**, not like a normal handheld human rifle;
- use modular metallic Replicator geometry with a clear emitter/aperture and readable firing direction;
- if the artillery caster needs a visually distinct asset, split it rather than reusing the pulse-caster sprite merely for convenience.

### Replicator shield disruptor
**Def:** `Defs/ThingDefs/Weapons_ReplicatorAdaptation.xml`  
**DefName:** `WNG_ReplicatorShieldDisruptor`  
**Weapon texture:** `Textures/Things/Item/Weapon/Replicator/WNG_ReplicatorShieldDisruptor.png`  
**Projectile texture:** `Textures/Things/Projectile/WNG_ReplicatorShieldDisruptor.png`

Function:
- integrated Replicator phase disruptor grown after shield engagements;
- focused EMP/phase-disruption weapon;
- `Graphic_Single`, weapon `drawSize (0.82,0.82)`, projectile `drawSize (0.42,0.42)`.

Required visual:
- clearly different from the ordinary pulse caster;
- still an integrated Replicator machine organ, not a handheld conventional gun;
- weapon and projectile should share a coherent phase-disruption visual language.

### Block-form Replicator pawns
Upgrade the current directional families under:
`Textures/Things/Pawn/Replicator/`

This currently includes:
- Artillery
- Bulwark
- Burrower
- Controller
- Drone
- Hunter
- Repairer
- Siege Mass
- Titan

For each class:
- inspect its live race/behavior Def and C# role before drawing;
- retain recognizably Stargate **block-form** construction while giving each role a clear silhouette;
- the whole family must feel assembled from the same physical block technology;
- do not make them generic RimWorld mechs;
- make north/east/south/west/base sets coherent and professionally authored.

### Other Replicator art that is part of this pass
Inspect and upgrade as needed:
- `Textures/Things/Building/Replicator/Containment/WNG_ReplicatorContainmentProjector*.png`
- `Textures/UI/WNG/Build/WNG_ReplicatorContainmentProjector.png`
- `Textures/Things/Building/Replicator/Ruins/*.png`
- `Textures/Things/Pawn/Replicator/Adaptation/*.png`
- any Replicator-related projectile, resource, weapon, UI icon or shared texture discovered by the live Def/reference audit.

**Lore distinction:** block-form SG-1 Replicators and human-form Asuran/Lantean-derived Replicators are related concepts but should not be collapsed into one generic visual style. Check the specific Def/role and the relevant Stargate reference before drawing each family.

---

# HOW TO EXECUTE THE NEXT PASS

For **every** active item above:

1. Read the exact live Def/C# use first: function, `size`, `drawSize`, graphic class, rotation, interaction cell, projectile origin, shared texture use and UI icon use.
2. Inspect the current PNGs directly from the public repo.
3. Check relevant **Stargate visual references and lore** for that exact object/faction before generating or editing art. Do not invent generic sci-fi when a recognizable Stargate visual language exists.
4. Check how **vanilla RimWorld / Odyssey** presents a comparable object at the same footprint and use that as the readability/composition benchmark, not as art to copy.
5. Produce professional transparent game art:
   - strong readable silhouette at actual RimWorld zoom;
   - complete object inside canvas;
   - no accidental crop;
   - no square background;
   - no white/bright alpha halo;
   - no detached junk unless it is intentionally part of the object;
   - logical centre and margins;
   - coherent lighting/perspective across directional families.
6. World sprites and UI/build icons are different presentation jobs. Do not blindly reuse one as the other when a dedicated icon composition is needed.
7. For directional art, preserve the correct `Graphic_Multi` behavior and authored directions. Do not delete directions merely because a broken Def fails to reference them, and do not manufacture all directions by rotating one view unless that is genuinely appropriate.
8. Commit in **small bounded families** to public `main`. Re-read `main` before each write so a stale parent cannot overwrite newer work.
9. After each family, run the existing validation/build gates and inspect the actual result:
   - full PNG decode with Pillow, not signature-only checks;
   - alpha edge / contamination check;
   - centring and clipping check;
   - XML/static validation;
   - release-gap audit;
   - managed build.
10. Do not call a family complete merely because automation is green. Visually inspect the finished family against its function, Stargate reference and RimWorld scale.
11. Update this file with a short completion note beneath the relevant active item, but **do not delete the Errors / Mistakes section**.
12. If a new failure mode occurs, append a new numbered mistake.

Recommended work order:
1. Goa'uld small sublight drive
2. Goa'uld power coupler
3. Ancient control chair
4. Queen Recovery Carrier incomplete pieces
5. Replicator blocks + core fragment
6. Replicator pulse caster + shield disruptor + projectile
7. Replicator pawn families
8. remaining Replicator buildings/ruins/adaptation/UI
9. whole-pass validation and representative in-game visual check

The WNG website art page is **not** the source for this work. After these mod-art changes are finished and accepted, regenerate `wng-art.html` from the then-current public WNG repo. Do not touch the cosmology page.

---

# ERRORS / MISTAKES — PRESERVE AND EXTEND

These are historical failures from the art rebuild. Keep them so future work does not repeat them. Add new numbered mistakes when a new failure mode is discovered; do not erase or rewrite the old ones.

## 1. Followed an obsolete local-only workflow
Mistake:
- allowed D118-D141 work to remain local/File-Library while public GitHub stayed stale.

Correct rule:
- public WNG repo is the production source;
- current checkpoints belong in GitHub.

## 2. Replaced a working display page with a ZIP-loader
Mistake:
- made the user select a WNG ZIP in the website.

Correct rule:
- the website is a generated display page only;
- no ZIP upload/file picker.

## 3. Hid art behind filters/comparison state
Mistake:
- gallery logic obscured art and confused what actually existed.

Correct rule:
- final display is a straightforward whole-mod page;
- never use gallery filters as proof of mod content.

## 4. Pointed a page at stale mod content
Mistake:
- website could technically work while GitHub still contained an old mod.

Correct rule:
- mod repository first;
- website last.

## 5. Overcomplicated asset transfer
Mistake:
- gallery archives, delta manifests, base64 transfer detours and cache systems.

Correct rule:
- keep the mod repo current;
- use bounded deterministic transfers only when needed.

## 6. Compared changed files when the user asked for all art
Mistake:
- spent time on deltas rather than the requested whole-mod view.

Correct rule:
- change history is irrelevant to the display page unless explicitly requested.

## 7. Created misleading branch names
Mistake:
- `d141-current-art` existed while still pointing at D117.

Correct rule:
- verify commit/tree before claiming branch content.

## 8. Asuran Gravship Door family

Paths:
`Textures/Things/Building/Precursor/Gravship/WNG_AsuranGravshipDoor*.png`

Current screenshot:
- gray flat rectangle;
- cyan construction-line scribbles;
- obvious placeholder look;
- not a "seamless airtight aperture in an Asuran nanite-composite hull".

Required:
- clean seamless Ancient/Asuran aperture;
- integrate with surrounding hull language;
- subtle blue luminous seam when appropriate;
- transparent edges;
- no guide-line scribbles.

Important Def issue:
- current `WNG_AsuranGravshipDoor` uses `Graphic_Single`;
- current repo nevertheless contains base + north/east/south/west images.

Before touching XML:
- verify how the parent `Door` rotates/renders this child in-game;
- do not blindly switch graphic classes;
- if a single rotation-neutral sprite is correct, remove/ignore redundant directional art;
- if true facing-specific art is needed, move deliberately to the correct multi-direction graphic setup and test every rotation.

## 9. Goa'uld Gravship Door family

Paths:
`Textures/Things/Building/Goauld/Gravship/WNG_GoauldGravshipDoor*.png`

Current screenshot:
- picture-frame style brown/gold rectangles;
- mechanically readable as "door" but too flat/decorative;
- does not feel like a heavy Ha'tak pressure door.

Required:
- heavy dark naquadah door/hatch;
- bronze/gold structural ribs;
- central amber lock/energy seam;
- less flat frame, more physical thickness;
- match Ha'tak hull and pel'tac visual language.

Same `Graphic_Single` / redundant directional caution applies.

## 10. Wraith Gravship Door family

Paths:
`Textures/Things/Building/Wraith/Gravship/WNG_WraithGravshipDoor*.png`

Current screenshot review suggests this family is better than the Asuran placeholder doors, but it still needs:
- alpha-edge check;
- centring check;
- directional consistency check;
- confirm it reads as living/organic Wraith architecture at one-tile scale.

Do not replace good art merely because the other door families need work.

## 11. Hull / wall families

The angled gravship hull artwork shown in review is comparatively strong.

Use it as a quality benchmark:
- coherent material,
- proper edge treatment,
- readable silhouette,
- no screenshot panel baked in.

Linked wall/hull atlases must preserve RimWorld linked-atlas requirements. Do not turn wall textures into ordinary independent squares.

---

## 12. Risked touching the wrong website area
Permanent rule:
- WNG work touches only WNG files/pages;
- Cosmology site/gallery is unrelated.

## 13. Used the art page as if it were part of the art-production process
Mistake:
- repeatedly treated the website viewer/gallery as a working source or QA mechanism.

Correct rule:
- **never use `wng-art.html` in the art-production process**;
- source is the mod repo + Defs;
- page is regenerated only after mod work is finished.

## 14. Equated "PNG exists / reference resolves / validation green" with "art is finished"
Mistake:
- zero missing refs was treated as visual completion.

Result:
- obviously poor, provisional, off-centre and placeholder art survived a supposedly completed pass.

Correct rule:
- structural validation and visual QA are separate gates;
- both must pass.

## 15. Marked explicitly provisional art as completed
Mistake:
- Precursor Field Armour was listed as reworked/completed while its own XML still says the visual is provisional.

Correct rule:
- read the actual Def comments/descriptions before declaring completion;
- provisional means unfinished.

## 16. Allowed borders/halos around ship sprites
Mistake:
- Puddle Jumper, Queen Recovery Carrier and other craft retained visible pale fringe/border contamination.

Correct rule:
- inspect alpha edges directly;
- preserve dark RGB under transparency;
- remove white/gray matte;
- test all facings.

## 17. Produced icons with embedded square panels and bad centring
Mistake:
- some gene/xenotype/build/ability icons contain a visible rectangular source image or off-centre content.

Correct rule:
- transparent icon canvas;
- centred alpha mass;
- consistent padding;
- dedicated UI composition.

## 18. Failed to centre the Goa'uld Grav-field Projector
Mistake:
- current art has an unconnected line/floating fragment and most of the 2×2 canvas is wasted.

Correct rule:
- read footprint/function first;
- centre the complete device;
- no disconnected artifact;
- make the silhouette communicate its function.

## 19. Accepted a poor Death Glider despite a very recognizable canonical silhouette
Mistake:
- the current Death Glider is muddy and does not strongly resemble the Stargate fighter it represents.

Correct rule:
- lore-reference recognizable craft before drawing;
- preserve canonical broad silhouette;
- adapt it to RimWorld top-down readability.

## 20. Trusted prior "apparel completed" claims without reviewing every body/direction family
Mistake:
- 217 apparel PNGs were treated as effectively solved based on targeted path checks.

Correct rule:
- inspect representative male/female/thin/hulk/fat + N/E/S views for every apparel family;
- then sample remaining variants;
- no family gets completion status from filenames alone.

## 21. Created directional files without first confirming the active graphic class
Mistake:
- Asuran/Goa'uld gravship doors currently use `Graphic_Single` while multiple directional PNGs also exist.

Correct rule:
- inspect Def graphic behavior first;
- do not create redundant art families or change XML blindly;
- test rotation in-game before deciding Single vs Multi.

## 22. Mixed world-sprite and UI-icon requirements
Mistake:
- some UI art appears to be reused world/object rendering rather than a purpose-built icon.

Correct rule:
- world sprite: footprint, direction, interaction origin;
- UI icon: square readability, centring, padding;
- share design language, not necessarily the exact same exported image.

## 23. Let direct raw-image links become a mobile nuisance
Mistake:
- raw PNG click-through can lead to mobile browser downloads that Android may index into Gallery.

Correct rule:
- do not use the website/raw links for internal QA;
- do not download the whole art set to the user's phone;
- if click behavior is revised, avoid unintended download semantics.

## 24. Generated/redrew art before establishing exact function and lore
Mistake:
- some assets were treated as generic "sci-fi art" rather than specific functional objects.

Correct rule:
- Def first;
- lore second;
- vanilla presentation benchmark third;
- then draw.

---

## 25. Deleted directional door art because the current Def said `Graphic_Single`
Mistake:
- inferred that Goa'uld/Wraith east/north/south/west door PNGs were dead art and removed them.

Why this was wrong:
- the intended design is vanilla-door parity;
- a Def that fails to use required directional/open-state art can itself be the bug;
- current implementation details must not override the intended gameplay/function specification.

Correct rule:
- establish intended vanilla-equivalent behavior first;
- preserve all potentially required directional/state art while auditing;
- compare the WNG door class/Def behavior against vanilla doors;
- then change XML/code and art together so rotation and open/closed states are correct;
- never delete art solely because the current broken/incomplete Def does not reference it.

## 26. Automatic art-generator push could overwrite approved professional art
Mistake:
- the legacy `WNG art generator` workflow ran every `.github/artgen/*.py` script automatically whenever art-generator files changed;
- several of those scripts are first-pass/procedural generators, so a successful run could overwrite later professional rendered assets such as the Death Glider and doors.

Correct rule:
- professional approved art is authoritative;
- the broad art-generator workflow is manual-only (`workflow_dispatch`) until every generator is itself a professional authoritative source;
- narrowly scoped safe finalizers may run automatically only when they deterministically derive variants from an approved master;
- never allow an old generator to overwrite approved repo art merely because a workflow was triggered.

## 27. Tried to auto-derive a four-view craft from one facing
Mistake:
- a temporary Asuran carrier finalizer was set to rotate one north-facing master into east/south/west;
- that would have overwritten the four separately authored, coherent directional renders and the first uploaded north master was also unreadable by Pillow.

Correct rule:
- for craft with genuinely authored directional views, preserve those views;
- do not replace them with geometric rotations merely because rotation is convenient;
- validate each uploaded PNG before allowing any automation to propagate it;
- narrow finalizer workflows must be manual-only unless their derivation is explicitly the intended authoritative art process.

## 28. Lost the active art target and generated unrelated door art
Mistake:
- after finishing the Asuran recovery-carrier checkpoint, the active task was the Goa'uld Grav-field Projector + Asuran Grav-field Extender;
- I lost that task state after a freeze and generated another door-art concept sheet instead.

Correct rule:
- before every image-generation call, re-read the current unchecked item in this file and the exact Def/path being worked on;
- never generate art from the previous family merely because its visual language is still in context;
- one active family at a time: verify path + Def + footprint + function, generate, commit, validate green, mark complete, then advance;
- if a freeze occurs, verify the last committed repo state and this progress list before generating anything else.

## 29. PNG signature/non-empty checks were not enough
Mistake:
- earlier validation treated a PNG as healthy if it existed, was non-empty and had a plausible PNG signature;
- three Asuran recovery-carrier directional files passed that shallow test but later failed full Pillow decoding;
- this allowed corrupt binary art to be described as validated/green.

Correct rule:
- every PNG validation pass must fully decode the image, not merely inspect its header/signature;
- use Pillow `Image.open(...).load()` (and/or `verify()` followed by reopen+load) for every PNG in the mod;
- art-family QA must also measure alpha at the canvas edge, transparent-pixel RGB contamination and logical centring;
- a workflow is not "green for PNG integrity" unless the complete decode pass succeeds for every file it claims to validate.

## 30. Copied truncated base64 into live PNG art
Mistake:
- the Goa'uld Grav-field Projector and Asuran Grav-field Extender were uploaded through a long base64 transfer path that had been visibly ellipsized/truncated;
- GitHub accepted the blob bytes, and shallow file/signature checks did not prove the images were decodable;
- the later full Pillow decode audit found both files unreadable and their stored base64 literally ended in the truncation marker.

Correct rule:
- never copy binary/base64 payloads from a tool response that may be clipped, ellipsized or summarized;
- for generated repo art, prefer deterministic in-repo generation or a verified binary transfer path;
- always full-decode the resulting PNG after commit before calling the art green;
- if a generated binary must be transferred, verify byte count/hash and decode on the destination before sign-off.


---
