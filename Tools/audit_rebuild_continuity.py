#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
MASTER = ROOT / "Docs" / "WNG_REBUILD_MASTER_PLAN.md"
QUEEN = ROOT / "Docs" / "WNG_REBUILD_REPLICATOR_QUEEN_CONTRACT.md"
CONTINUE = ROOT / "CONTINUE_WNG_REBUILD.md"
README = ROOT / "README.md"
errors = []


def fail(message):
    errors.append(message)


for path, label in [
    (MASTER, "master plan"),
    (QUEEN, "Replicator Queen detailed contract"),
    (CONTINUE, "continue-here file"),
    (README, "README"),
]:
    if not path.exists():
        fail(f"Missing rebuild continuity {label}: {path.relative_to(ROOT)}")

master = MASTER.read_text(encoding="utf-8") if MASTER.exists() else ""
queen = QUEEN.read_text(encoding="utf-8") if QUEEN.exists() else ""
cont = CONTINUE.read_text(encoding="utf-8") if CONTINUE.exists() else ""
readme = README.read_text(encoding="utf-8") if README.exists() else ""

required_master_phrases = [
    "There are no known-good historical builds.",
    "Wraith xenotype -> Wraith caste/PawnKind -> faction role/behavior -> optional biography/backstory",
    "Backstories are biography/history data. **Backstories are not races and are not castes.**",
    "Ordinary `Drain Life` / feeding:",
    "does **not** open the faction feeding-request popup",
    "Strategic Wraith hunger:",
    "refuses/does not accept an actual hunger request",
    "Mature Hives are a local ecology/settlement system and must not be conflated with strategic faction hunger.",
    "Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones/base Replicators",
    "Replicator Queen is a unique human-form Replicator individual",
    "Sovereign Neural Lattice implant bearer:",
    "target-specific bound",
    "day 84 — Replicator Queen vault",
    "`WNG_WraithBiofluidFuel`",
    "`WNG_AsuranNaniteSlurry`",
    "exactly **two real flyover/culling passes**",
    "**30 original WNG backstories**",
    "**30 distinct WNG cues**",
    "Green CI is supporting evidence",
    "Immediate rebuild queue",
    "Future continuation protocol",
]
for phrase in required_master_phrases:
    if phrase not in master:
        fail(f"Master plan lost required continuity phrase: {phrase}")

required_queen_phrases = [
    "Exactly **four hostile human-form Replicator recovery operatives**",
    "Abduction commits only when the carrier actually exits the map while carrying the exact Queen pawn.",
    "Merely picking her up does not commit hostile recovery.",
    "The current public rebuild explicitly supersedes it:",
    "remove the old `HostileOutbreakBonus` shortcut",
    "modify appropriate future Lattice threat composition whenever those threats occur",
    "do not invent an unrelated recurring raid cadence",
    "Differently bound Replicators must not silently recombine into a single command domain.",
]
for phrase in required_queen_phrases:
    if phrase not in queen:
        fail(f"Queen contract lost required continuity phrase: {phrase}")

if "Docs/WNG_REBUILD_MASTER_PLAN.md" not in cont:
    fail("CONTINUE_WNG_REBUILD.md no longer points to the master plan")
if "Docs/WNG_REBUILD_REPLICATOR_QUEEN_CONTRACT.md" not in cont:
    fail("CONTINUE_WNG_REBUILD.md no longer points to active Queen contract")
if "refresh memory and continue" not in cont.lower():
    fail("CONTINUE_WNG_REBUILD.md lost the refresh-memory trigger wording")
if "CONTINUE_WNG_REBUILD.md" not in readme or "Docs/WNG_REBUILD_MASTER_PLAN.md" not in readme:
    fail("README no longer references both root rebuild continuity documents")

if errors:
    print("WNG rebuild continuity audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("WNG rebuild continuity audit OK")
