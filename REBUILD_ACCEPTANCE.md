# WNG rebuild acceptance

This repository is a reconstruction, not a file-count restoration exercise.

## Authoritative repository rule — non-negotiable

The active rebuild authority is the **public RimWorld 1.6 repository**:

`Vardath/Wraith-Nanite-Gravtech-1.6`

Before every implementation pass, verify the repository identity, active branch and exact head in this repository before reading, writing, testing, or making continuity claims. Do not silently substitute `Vardath/Wraith-Nanite-Gravtech` or any other private repository because of an older instruction, remembered checkpoint, branch name, or prior assistant work.

The newest explicit user instruction about repository/workspace choice overrides every older repository rule. If repository authority is uncertain or conflicting, stop and ask the user **before making changes**.

Work performed in another repository after the currently accepted public-repository checkpoint is **not part of this rebuild unless the user explicitly authorizes it to be recreated here**. Never automatically port, cherry-pick, copy, or treat that work as progress.

## Historical-material rule

Historical code, XML/Defs, configuration, scripts, sounds/audio, tests/audits, generated outputs, build products and non-Replicator artwork are **reference evidence only**. Recover intended feature scope and player-visible behaviour from them, then implement the rebuild freshly. Approved Replicator graphics are the sole explicit reuse exception. All other art and all audio are to be redone from scratch. Do not generate images in chat unless the user explicitly asks.

There is **no required historical count** for textures, Defs, source files, sounds, incidents, recipes, research projects, genes, abilities, xenotypes, or any other asset class. Historical inventories may be used to find missing ideas or evidence, but they are not targets and are not acceptance gates.

The rebuild is allowed to add, remove, combine, split, rename, rewrite, or replace implementation pieces when that produces the intended gameplay more cleanly. Live testing is expected to result in further edits.

A feature is accepted by behaviour rather than count: coherent acquisition/progression, correct in-game behaviour, dependency-safe Def loading, successful RimWorld 1.6 compilation and packaging, save/load continuity, usable UI and art/audio, and live-test evidence from the game/logs.

Canonical user corrections override historical implementation. In particular, obsolete systems must not be restored merely because they existed in an older package.

## Mandatory green-pass gate

After **every implementation pass**, verify that the exact current public-repository head receives a real, executed, successful validation run. A queued, cancelled, runnerless, no-step, or otherwise unexecuted workflow is **not green**.

If validation is not green, stop feature work and fix the failure before continuing. Re-run validation after each fix until the exact head is green. If the failure cannot be fixed with the available repository diagnostics/tools, stop and tell the user exactly what is blocking progress rather than continuing around it.

The accepted recovery baseline immediately before this rule was added is public `main` commit `7134fa6fea08eb0ce97c2e68cdf3225fb12bf97a`, whose `WNG rebuild compile gate` run #5 / 34351725788 completed successfully. Private-repository work performed after that public checkpoint is null and void for this rebuild unless the user later explicitly says otherwise.
