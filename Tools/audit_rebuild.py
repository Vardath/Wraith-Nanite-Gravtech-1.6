#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from collections import defaultdict
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DEFS = ROOT / "Defs"
SOURCE = ROOT / "Source"
TEXTURES = ROOT / "Textures"

# Explicit base-game visual placeholders allowed while new WNG art is being authored.
# Keep this list narrow: adding a Core path here is an intentional temporary presentation debt,
# not a general exemption from local texture validation.
CORE_TEXTURE_PLACEHOLDERS = {
    "Things/Building/Misc/Shuttle",  # WNG_WraithDart temporary visual only
    "Things/Item/Resource/RawFungus",  # WNG_Biomass temporary visual only
}

errors: list[str] = []
warnings: list[str] = []
notes: list[str] = []

def err(message: str) -> None:
    errors.append(message)

def warn(message: str) -> None:
    warnings.append(message)

def note(message: str) -> None:
    notes.append(message)

xml_files = sorted(DEFS.rglob("*.xml")) if DEFS.exists() else []
cs_files = sorted(SOURCE.rglob("*.cs")) if SOURCE.exists() else []

# 1. XML must actually parse.
parsed: list[tuple[Path, ET.Element]] = []
for path in xml_files:
    try:
        parsed.append((path, ET.parse(path).getroot()))
    except ET.ParseError as ex:
        err(f"XML parse failure: {path.relative_to(ROOT)}: {ex}")

# 2. Duplicate definitions are illegal within a Def database/type. Same defName across
#    different Def types (for example ThingDef + PawnKindDef) is valid RimWorld XML.
seen_defs: dict[tuple[str, str], Path] = {}
all_def_names: set[str] = set()
all_named_parents: set[str] = set()
for path, root in parsed:
    for node in list(root):
        def_name_node = node.find("defName")
        if def_name_node is not None and def_name_node.text:
            name = def_name_node.text.strip()
            all_def_names.add(name)
            key = (node.tag, name)
            if key in seen_defs:
                err(
                    f"Duplicate {node.tag} defName {name}: "
                    f"{seen_defs[key].relative_to(ROOT)} and {path.relative_to(ROOT)}"
                )
            else:
                seen_defs[key] = path
        named = node.attrib.get("Name")
        if named:
            key = (node.tag, f"@Name:{named}")
            if key in seen_defs:
                err(
                    f"Duplicate named {node.tag} {named}: "
                    f"{seen_defs[key].relative_to(ROOT)} and {path.relative_to(ROOT)}"
                )
            else:
                seen_defs[key] = path
            all_named_parents.add(named)

# 3. Every WNG custom C# class named by XML must exist in the reconstructed source.
source_text_by_file: dict[Path, str] = {}
class_names: set[str] = set()
class_pattern = re.compile(
    r"\b(?:public|internal|private|protected)?\s*(?:sealed\s+|static\s+|abstract\s+|partial\s+)*class\s+([A-Za-z_][A-Za-z0-9_]*)"
)
for path in cs_files:
    text = path.read_text(encoding="utf-8", errors="replace")
    source_text_by_file[path] = text
    class_names.update(class_pattern.findall(text))

class_ref_fields = {"driverClass", "workerClass", "geneClass", "thingClass", "hediffClass", "incidentClass"}
for path, root in parsed:
    for node in root.iter():
        class_attr = node.attrib.get("Class")
        refs: list[str] = []
        if class_attr:
            refs.append(class_attr)
        if node.tag in class_ref_fields and node.text:
            refs.append(node.text.strip())
        for ref in refs:
            if ref.startswith("WraithNaniteGravtech.") or ref.startswith("WNGR2."):
                simple = ref.rsplit(".", 1)[-1]
                if simple not in class_names:
                    err(f"Missing custom class {ref}, referenced by {path.relative_to(ROOT)}")

# 4. WNG ParentName links must resolve locally. External vanilla/mod ParentNames are allowed.
for path, root in parsed:
    for node in root.iter():
        parent = node.attrib.get("ParentName")
        if parent and parent.startswith("WNG_") and parent not in all_named_parents:
            err(f"Unresolved WNG ParentName {parent} in {path.relative_to(ROOT)}")

# 5. Check common WNG Def-reference fields. These are deliberately restricted to reference
#    fields so labels/descriptions/texture paths are not mistaken for Def links.
ref_tags = {
    "race", "pawnKindDef", "upgradePawnKind", "splitChildPawnKind", "integratedWeaponDefName",
    "victimHediff", "casterHediff", "researchPrerequisite", "researchPrerequisites",
    "prerequisites", "thingDef", "hediffDef", "ability", "abilityDef", "factionDef",
    "projectile", "turretGunDef", "soundDef", "weaponDef"
}
for path, root in parsed:
    for node in root.iter():
        if node.tag not in ref_tags or not node.text:
            continue
        value = node.text.strip()
        if value.startswith("WNG_") and value not in all_def_names:
            warn(f"WNG Def reference not found locally: {value} in {path.relative_to(ROOT)}")

# 6. Texture paths normally require an actual local texture file. Graphic_Multi may legitimately
#    use directional suffixes rather than a base PNG, so either exact or prefix files satisfy it.
#    A tiny explicit allow-list permits documented Core placeholders while replacement WNG art is
#    still pending; these remain warnings so they cannot be mistaken for completed presentation art.
texture_paths: list[tuple[Path, str]] = []
for path, root in parsed:
    for node in root.iter("texPath"):
        if node.text and node.text.strip():
            texture_paths.append((path, node.text.strip().replace("\\", "/")))

texture_rel_no_ext: set[str] = set()
if TEXTURES.exists():
    for tex in TEXTURES.rglob("*"):
        if tex.is_file() and tex.suffix.lower() in {".png", ".jpg", ".jpeg", ".tga", ".psd"}:
            texture_rel_no_ext.add(tex.relative_to(TEXTURES).with_suffix("").as_posix())

for owner, tex_path in texture_paths:
    exact = tex_path in texture_rel_no_ext
    directional = any(p.startswith(tex_path + "_") for p in texture_rel_no_ext)
    if exact or directional:
        continue
    if tex_path in CORE_TEXTURE_PLACEHOLDERS:
        warn(f"Temporary Core texture placeholder still pending replacement: {tex_path} in {owner.relative_to(ROOT)}")
        continue
    err(f"Missing texture for texPath {tex_path} referenced by {owner.relative_to(ROOT)}")

# 7. Forbidden obsolete gameplay concept. Documentation/history outside playable Source/Defs is
#    intentionally ignored; the active mod may not resurrect Grav Core/Gravcore progression.
for path in xml_files + cs_files:
    text = (source_text_by_file.get(path) if path in source_text_by_file else path.read_text(encoding="utf-8", errors="replace"))
    if re.search(r"\bgrav\s*core\b", text, flags=re.IGNORECASE):
        err(f"Obsolete Grav Core concept remains in gameplay content: {path.relative_to(ROOT)}")

# 8. Locked gameplay contracts. These are behavior requirements, not historical inventory counts.
combined_source = "\n".join(source_text_by_file.values())
combined_xml = "\n".join(path.read_text(encoding="utf-8", errors="replace") for path in xml_files)

if not re.search(r"DeathBreakupRecombinationCooldownTicks\s*=\s*2500\s*;", combined_source):
    err("Replicator split-born recombination cooldown is not locked to 2500 ticks")
if "<minimumStack>10</minimumStack>" not in combined_xml:
    err("Replicator Matter dangerous minimum stack is not locked to 10")
if "<dormantTicks>30000</dormantTicks>" not in combined_xml:
    err("Replicator Matter dormancy is not locked to 30000 ticks")
if "<formingTicks>90000</formingTicks>" not in combined_xml:
    err("Child's Toy controlled gestation is not locked to 90000 ticks")

for needle, description in [
    ("<victimAgeYears>50</victimAgeYears>", "Drain Life victim aging +50 years"),
    ("<casterRejuvenationYears>5</casterRejuvenationYears>", "Drain Life Wraith rejuvenation 5 years"),
    ("<minimumCasterAgeYears>18</minimumCasterAgeYears>", "Drain Life age floor 18 years"),
]:
    if needle not in combined_xml:
        err(f"Missing locked Wraith contract: {description}")

# Wraith faction hunger is a strategic faction system, never a side effect of ordinary Drain Life.
hunger_path = SOURCE / "WNGR2" / "Wraith" / "WraithFactionHunger.cs"
if not hunger_path.exists():
    err("Missing fresh Wraith faction hunger system")
else:
    hunger_source = hunger_path.read_text(encoding="utf-8", errors="replace")
    for needle, description in [
        ("RequestThreshold = 0.65f", "feeding requests require genuine strategic hunger"),
        ("RaidThreshold = 0.80f", "high hunger increases Wraith raid pressure"),
        ("RefuseRequest", "refusing an actual feeding request triggers attack handling"),
        ("ScheduleForcedRaid", "refused feeding requests persistently schedule a raid"),
        ("WNG_WraithBrood", "Sable Brood participates in strategic hunger"),
        ("WNG_WraithCinderCourt", "Cinder Court participates in strategic hunger"),
        ("WNG_WraithVeiledHive", "Veiled Hive participates in strategic hunger"),
        ("WNG_WraithExiles", "Pale Covenant participates in strategic hunger"),
        ("guest?.IsPrisoner", "controlled faction feeding uses prisoners rather than arbitrary pawns"),
        ("LooksSynthetic", "synthetic/nanite humanoids are excluded from feeding stock"),
    ]:
        if needle not in hunger_source:
            err(f"Missing locked Wraith faction-hunger contract: {description}")

    drain_path = SOURCE / "WNGR2" / "Wraith" / "WraithLifeDrain.cs"
    if drain_path.exists():
        drain_source = drain_path.read_text(encoding="utf-8", errors="replace")
        if "Dialog_MessageBox" in drain_source or "feeding request" in drain_source.lower():
            err("Ordinary Drain Life must not open Wraith faction feeding-request UI")

# 9. Required core Replicator split ladder must be present in code as an operational death mapping.
for parent, child in [
    ("WNG_ReplicatorSiegeMass", "WNG_ReplicatorTitan"),
    ("WNG_ReplicatorTitan", "WNG_ReplicatorBulwark"),
    ("WNG_ReplicatorBulwark", "WNG_ReplicatorHunter"),
    ("WNG_ReplicatorHunter", "WNG_ReplicatorDrone"),
]:
    pattern = re.compile(rf'case\s+"{re.escape(parent)}"\s*:\s*childDefName\s*=\s*"{re.escape(child)}"')
    if not pattern.search(combined_source):
        err(f"Replicator death split mapping missing: {parent} -> {child}")

# 10. WNG runtime lookups are useful reconstruction leads. Report unresolved local lookups as
#     warnings because some can intentionally be provided by optional integrations later.
lookup_pattern = re.compile(r'GetNamedSilentFail\(\s*"(WNG_[A-Za-z0-9_]+)"\s*\)')
for path, text in source_text_by_file.items():
    for name in lookup_pattern.findall(text):
        if name not in all_def_names:
            warn(f"Runtime WNG lookup has no local Def yet: {name} in {path.relative_to(ROOT)}")

note(f"Parsed XML files: {len(parsed)}")
note(f"C# source files: {len(cs_files)}")
note(f"Custom classes discovered: {len(class_names)}")
note(f"Texture references checked: {len(texture_paths)}")
note("Historical texture/Def counts are intentionally not acceptance criteria.")

report = {
    "ok": not errors,
    "errors": errors,
    "warnings": sorted(set(warnings)),
    "notes": notes,
}
print(json.dumps(report, indent=2))

if errors:
    sys.exit(1)
