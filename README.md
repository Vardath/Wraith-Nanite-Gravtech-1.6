# Wraith & Nanite Gravtech — fresh RimWorld 1.6 rebuild

Author/design authority: **Vardath**.

This repository was deliberately reset on 2026-09-10. The old implementation remains in Git history only as reference material; it is not a known-good state and is not an implementation authority.

The durable rebuild plan and refresh/continuity instructions live in:

`Vardath/Vardath.github.io/wng-rebuild/`

Read that continuity set before resuming work after a context reset.

## Current rebuild stage

The first rebuilt subsystem is the mechanical block Replicator ecology. Approved Replicator graphics are retained; behavior is reconstructed cleanly.

Design/balance values are intentionally kept in Def data / component properties where practical so Vardath can tune them later. The plan is a first-build target, not immutable canon.

No design-locking anti-regression suite or release-check bureaucracy belongs in this fresh rebuild. Add only implementation checks that are actually needed to make the mod work.
