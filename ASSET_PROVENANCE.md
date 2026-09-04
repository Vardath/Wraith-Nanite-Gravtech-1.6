# Asset provenance — public 1.6 release mirror

This file records the public release boundary for Wraith & Nanite Gravtech assets.

## Runtime art

The shipped WNG runtime art was produced by project-authored generation/drawing logic in the private development repository, then regenerated and validated before this public playable payload was mirrored.

The intended visual boundary is original project art that may evoke broad faction/genre ideas without reproducing official logos, exact screen-used ship outlines, actor likenesses, traced costume layouts, official glyphs or ripped game/franchise assets.

## Audio

The current WNG 1.6 release line contains exactly **30 original procedural audio cues**. The private release pipeline regenerates and checks clip resolution, format, quality, identity and broken paths before packaging.

The project does not intentionally ship ripped Stargate dialogue, music, sound recordings or one-to-one effect recreations. Audio direction is role-based: organic biotechnology, crystalline/field technology, metallic Replicator motion and nanite-interface effects.

## Code and XML

The compiled `Assemblies/WraithNaniteGravtech.dll`, XML Defs, Patches and translations are project implementation material built against normal RimWorld/Biotech/Odyssey modding APIs.

RimWorld managed assemblies may be referenced privately for compilation but are **not** included in this public mod payload. The `Assemblies/` directory contains only WNG's own compiled assembly and its debug-symbol file from the verified build.

## Third-party dependencies / excluded ownership

This project does not claim ownership of:

- RimWorld or its DLCs, including Biotech and Odyssey
- Ludeon Studios trademarks, code or proprietary assemblies
- Stargate, Stargate Atlantis, Wraith, Replicator, Puddle Jumper or other franchise names/concepts owned by their respective rights holders
- Steam/Steam Workshop platform materials

References to those properties identify compatibility, inspiration or fan context only.

## Release-boundary checks

Before the current candidate was mirrored publicly, the private CI pipeline:

- regenerated WNG runtime art and audio;
- rejected known placeholder/duplicate texture regressions;
- verified dedicated weapon, armor, Replicator and shuttle asset sets;
- validated custom texture and sound references;
- ran the complete encoded RimWorld 1.6 Def/gameplay/runtime-contract audit suite;
- compiled the WNG DLL against RimWorld 1.6 references;
- verified the compiled payload; and
- assembled the exact playable mod folder mirrored here.

Release-candidate source head: `d8703f91783bd831fc33e10f4143ca3c69a9f46f`  
Static Def Audit: **#2489 / run 33924271024 — SUCCESS**

Automated checks do not replace final visual judgement or live RimWorld testing. Any future externally sourced asset must have its redistribution terms recorded before it is added to a public build.
