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

# Exact Replicator hierarchy contract once the subsystem exists.
hierarchy_path = ROOT / "Source/WraithNaniteGravtech/Replicators/CompReplicatorHierarchy.cs"
if hierarchy_path.is_file():
    hierarchy = hierarchy_path.read_text(encoding="utf-8")
    required_tokens = (
        "SplitBornRecombinationLockTicks = 2500",
        "splitChildPawnKind",
        "splitCount",
        "upgradePawnKind",
        "unitsRequired",
        "DestroyMode.Vanish",
        "deathSplitEmitted",
        "recombinationBlockedUntilTick",
        "PostExposeData",
    )
    for token in required_tokens:
        if token not in hierarchy:
            fail(f"Replicator hierarchy lost required contract token: {token}")
    if "SplitBornRecombinationLockTicks = 60000" in hierarchy:
        fail("Replicator split-born recombination lock regressed to one day")

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
