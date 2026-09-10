#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
CONTROL = ROOT / "Source/WNGR2/Replicators/CompReplicatorControl.cs"
HIERARCHY = ROOT / "Source/WNGR2/Replicators/CompReplicatorHierarchy.cs"
errors = []


def require(text, needle, description):
    if needle not in text:
        errors.append(f"Missing sovereign hierarchy contract: {description}")


control = CONTROL.read_text(encoding="utf-8") if CONTROL.exists() else ""
hierarchy = HIERARCHY.read_text(encoding="utf-8") if HIERARCHY.exists() else ""
if not CONTROL.exists():
    errors.append("Missing CompReplicatorControl.cs")
if not HIERARCHY.exists():
    errors.append("Missing CompReplicatorHierarchy.cs")

for needle, description in [
    ("private Pawn sovereignController;", "exact controller reference"),
    ("Scribe_References.Look(ref sovereignController", "save persistence"),
    ("CopySovereignBindingTo(Pawn child, Map expectedMap)", "death-split transfer that can outlive the dead source pawn"),
    ("ControllerCanOwnOnMap", "controller/map/faction validity gate"),
    ("ReplicatorQueenUtility.HasSovereignDirectiveAuthority(controller)", "authority revalidation"),
]:
    require(control, needle, description)

for needle, description in [
    ("Pawn sovereignController = pawn.TryGetComp<CompReplicatorControl>()?.ActiveSovereignController", "seed control-domain capture"),
    ("SameSovereignControlDomain(p, sovereignController)", "recombination domain filter"),
    ("actualController == expectedController", "exact-controller equality"),
    ("upgradedControl.BindToSovereign(sovereignController)", "recombined-form binding inheritance"),
    ("sourceControl?.CopySovereignBindingTo(child, map)", "death-split binding inheritance"),
    ("latticeOverride?.CopyTemporaryOverrideTo(child)", "temporary override inheritance remains separate"),
    ("DeathBreakupRecombinationCooldownTicks = 2500", "one-hour split-born cooldown"),
]:
    require(hierarchy, needle, description)

for forbidden, description in [
    ("sovereignController = consumed[0]", "implicit first-unit control takeover"),
    ("ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(pawn) || HasActiveSovereignBinding", "merging temporary override and sovereign ownership semantics"),
]:
    if forbidden in hierarchy:
        errors.append(f"Sovereign hierarchy contract violated: {description}")

if errors:
    print("Replicator sovereign hierarchy audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Replicator sovereign hierarchy audit OK")
