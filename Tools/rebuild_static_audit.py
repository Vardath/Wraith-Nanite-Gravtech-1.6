#!/usr/bin/env python3
from __future__ import annotations

import pathlib
import re
import sys
import xml.etree.ElementTree as ET

ROOT = pathlib.Path(__file__).resolve().parents[1]
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def read(relative: str) -> str:
    path = ROOT / relative
    if not path.is_file():
        fail(f"missing required file: {relative}")
        return ""
    return path.read_text(encoding="utf-8")


# Repository contract.
for required in (
    "README.md",
    "WNG_REBUILD_REQUIREMENTS.md",
    "WNG_FEATURE_COVERAGE.md",
    "About/About.xml",
    "Source/WraithNaniteGravtech/WraithNaniteGravtech.csproj",
    "Source/WraithNaniteGravtech/Core/WNGMod.cs",
    "Source/WraithNaniteGravtech/Replicators/ReplicatorConstants.cs",
    "Source/WraithNaniteGravtech/Replicators/CompReplicatorState.cs",
    "Source/WraithNaniteGravtech/Replicators/CompReplicatorHierarchy.cs",
    "Source/WraithNaniteGravtech/Replicators/CompReplicatorMatterDormancy.cs",
    "Source/WraithNaniteGravtech/Replicators/CompReplicatorSuppression.cs",
    "Source/WraithNaniteGravtech/Replicators/CompReplicatorToyGestation.cs",
    "Source/WraithNaniteGravtech/Replicators/ReplicatorConsumptionUtility.cs",
    "Source/WraithNaniteGravtech/Replicators/CompReplicatorAdaptation.cs",
    "Source/WraithNaniteGravtech/Replicators/MapComponent_ReplicatorLineageMemory.cs",
):
    if not (ROOT / required).is_file():
        fail(f"missing rebuild contract file: {required}")

# XML well-formedness and duplicate concrete defNames.
def_names: dict[str, pathlib.Path] = {}
xml_roots: list[tuple[pathlib.Path, ET.Element]] = []
for base in (ROOT / "About", ROOT / "Defs"):
    if not base.exists():
        continue
    for path in sorted(base.rglob("*.xml")):
        try:
            root = ET.parse(path).getroot()
            xml_roots.append((path, root))
        except Exception as exc:
            fail(f"invalid XML {path.relative_to(ROOT)}: {exc}")
            continue

        if path.parts[-2:] == ("About", "About.xml"):
            continue
        for node in root.iter():
            name = node.findtext("defName")
            if not name:
                continue
            previous = def_names.get(name)
            if previous is not None:
                fail(
                    f"duplicate defName {name}: {previous.relative_to(ROOT)} and {path.relative_to(ROOT)}"
                )
            else:
                def_names[name] = path

# About contract.
try:
    about = ET.parse(ROOT / "About/About.xml").getroot()
    if about.findtext("packageId") != "vardath.wraithnanitegravtech":
        fail("About.xml packageId drifted")
    dependencies = {
        node.findtext("packageId")
        for node in about.findall("./modDependencies/li")
    }
    for required_dep in ("ludeon.rimworld.biotech", "ludeon.rimworld.odyssey"):
        if required_dep not in dependencies:
            fail(f"About.xml lost hard dependency {required_dep}")
    for optional in ("ccyt.stargatesmod", "idolord.onac", "cravemode.rimgatejaffakree"):
        if optional in dependencies:
            fail(f"optional integration was accidentally made a hard dependency: {optional}")
except Exception as exc:
    fail(f"could not audit About.xml: {exc}")

# Hard anti-regression tokens in live implementation files only.
implementation_paths = [ROOT / "Source", ROOT / "Defs", ROOT / "Patches"]
for base in implementation_paths:
    if not base.exists():
        continue
    for path in base.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".cs", ".xml", ".py", ".ps1"}:
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        if re.search(r"grav\s*core|gravcore", text, re.IGNORECASE):
            fail(f"obsolete Gravcore implementation token in {path.relative_to(ROOT)}")

# Exact Replicator hierarchy contract.
hierarchy = read("Source/WraithNaniteGravtech/Replicators/CompReplicatorHierarchy.cs")
for token in (
    "SplitBornRecombinationLockTicks = 2500",
    "splitChildPawnKind",
    "splitCount",
    "upgradePawnKind",
    "unitsRequired",
    "DestroyMode.Vanish",
    "deathSplitEmitted",
    "recombinationBlockedUntilTick",
    "CompReplicatorAdaptation",
    "PostExposeData",
):
    if token not in hierarchy:
        fail(f"Replicator hierarchy lost required contract token: {token}")
if "SplitBornRecombinationLockTicks = 60000" in hierarchy:
    fail("Replicator split-born recombination lock regressed to one day")

# Exact Replicator matter/toy timing contract.
constants = read("Source/WraithNaniteGravtech/Replicators/ReplicatorConstants.cs")
for token in (
    "DangerousMatterMinimumStack = 10",
    "DormancyTicks = 30000",
    "ToyGestationTicks = 90000",
):
    if token not in constants:
        fail(f"Replicator canonical constant missing or changed: {token}")

matter = read("Source/WraithNaniteGravtech/Replicators/CompReplicatorMatterDormancy.cs")
for token in (
    "ReplicatorConstants.DangerousMatterMinimumStack",
    "ReplicatorConstants.DormancyTicks",
    "parent.stackCount -= consumed",
    "PawnGenerator.GeneratePawn",
    "PostExposeData",
):
    if token not in matter:
        fail(f"Replicator Matter lost required behavior token: {token}")

# Spawn-before-consume is intentional: a failed assembly must never delete the matter stack.
if matter.find("GenSpawn.Spawn") > matter.find("parent.stackCount -= consumed"):
    fail("Replicator Matter transaction order regressed: matter can be consumed before successful spawn")

toy = read("Source/WraithNaniteGravtech/Replicators/CompReplicatorToyGestation.cs")
for token in (
    "ReplicatorConstants.ToyGestationTicks",
    "PawnGenerator.GeneratePawn",
    "Faction.OfPlayer",
    "gestationCompleted",
    "PostExposeData",
):
    if token not in toy:
        fail(f"Child's Toy gestation lost required behavior token: {token}")
if toy.find("GenSpawn.Spawn") > toy.find("gestationCompleted = true"):
    fail("Child's Toy transaction order regressed: completion can latch before successful spawn")

suppression = read("Source/WraithNaniteGravtech/Replicators/CompReplicatorSuppression.cs")
for token in (
    "PostPreApplyDamage",
    "DamageDefOf.EMP",
    "suppressedUntilTick",
    "BlockRecombinationForTicks",
    "PostExposeData",
):
    if token not in suppression:
        fail(f"Replicator EMP suppression lost required behavior token: {token}")

consumption = read("Source/WraithNaniteGravtech/Replicators/ReplicatorConsumptionUtility.cs")
for token in (
    "replicator.Faction == Faction.OfPlayer",
    "target.Map?.IsPlayerHome == true",
    "reachableMaterialExists",
    "BiologicalFallbackAllowed",
):
    if token not in consumption:
        fail(f"Replicator autonomous-consumption safety lost required token: {token}")

# Learned adaptation: only completed assimilation may award lineage credit; exact thresholds are locked.
adaptation = read("Source/WraithNaniteGravtech/Replicators/CompReplicatorAdaptation.cs")
for token in (
    "RangedAssimilationsRequired = 3",
    "ArmorAssimilationsRequired = 4",
    "PowerConstructionAssimilationsRequired = 4",
    "GravtechAssimilationsRequired = 5",
    "ShieldAssimilationsRequired = 8",
    "NotifySuccessfulAssimilation",
    "ancientAsuranOrPrecursorGrade",
    "adaptationDelayTicks",
    "TryAssignSpecialization",
    "HasSpecialization",
    "CopyFrom",
    "PostExposeData",
):
    if token not in adaptation:
        fail(f"Replicator adaptation lost required behavior token: {token}")

lineage = read("Source/WraithNaniteGravtech/Replicators/MapComponent_ReplicatorLineageMemory.cs")
for token in (
    "ClearMapRetentionTicks = 60000",
    "RegisterAssimilation",
    "pawn.HostileTo(Faction.OfPlayer)",
    "ClearKnowledge",
    "ExposeData",
):
    if token not in lineage:
        fail(f"Replicator lineage memory lost required behavior token: {token}")

requirements = read("WNG_REBUILD_REQUIREMENTS.md")
for token in (
    "ranged 3 completed relevant assimilations",
    "armour 4",
    "power/construction 4",
    "gravtech 5",
    "shields 8",
    "Merely seeing technology or being attacked by it teaches nothing",
):
    if token not in requirements:
        fail(f"Replicator adaptation requirement dropped from rebuild contract: {token}")

# Never re-import an old source dump/decompiler tree into the clean rebuild.
for forbidden_dir in ("Decompiled", "LegacySource", "OldSource", "RecoveredSource"):
    if (ROOT / forbidden_dir).exists():
        fail(f"forbidden historical-source directory present: {forbidden_dir}")

if errors:
    print("WNG clean rebuild audit FAILED:", file=sys.stderr)
    for error in errors:
        print(f" - {error}", file=sys.stderr)
    sys.exit(1)

print(
    f"WNG clean rebuild audit passed: {len(xml_roots)} XML files parsed, "
    f"{len(def_names)} concrete defNames unique, public rebuild contract intact."
)
