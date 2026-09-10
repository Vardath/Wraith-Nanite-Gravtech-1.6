#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
ABILITIES = ROOT / "Defs" / "AbilityDefs" / "Abilities_WraithCore.xml"
GENES = ROOT / "Defs" / "GeneDefs" / "Genes_WraithCore.xml"
HEDIFFS = ROOT / "Defs" / "HediffDefs" / "Hediffs_WraithCore.xml"
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithLifeDrain.cs"

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def load(path: Path) -> ET.Element | None:
    if not path.exists():
        fail(f"Missing required file: {path.relative_to(ROOT)}")
        return None
    try:
        return ET.parse(path).getroot()
    except ET.ParseError as ex:
        fail(f"XML parse failure in {path.relative_to(ROOT)}: {ex}")
        return None


def find_def(root: ET.Element | None, tag: str, def_name: str) -> ET.Element | None:
    if root is None:
        return None
    for node in root.findall(tag):
        if (node.findtext("defName") or "").strip() == def_name:
            return node
    fail(f"Missing {tag} {def_name}")
    return None


def require_text(node: ET.Element | None, path: str, expected: str, description: str) -> None:
    if node is None:
        return
    value = (node.findtext(path) or "").strip()
    if value != expected:
        fail(f"{description}: expected {expected!r}, found {value!r}")


abilities_root = load(ABILITIES)
genes_root = load(GENES)
hediffs_root = load(HEDIFFS)

full = find_def(abilities_root, "AbilityDef", "WNG_DrainLife")
partial = find_def(abilities_root, "AbilityDef", "WNG_PartialFeed")
feeding_gene = find_def(genes_root, "GeneDef", "WNG_LifeDrainGene")
life_drained = find_def(hediffs_root, "HediffDef", "WNG_LifeDrained")
fed_recently = find_def(hediffs_root, "HediffDef", "WNG_FedRecently")

for path, expected, description in [
    ("comps/li/victimAgeYears", "50", "Full Drain Life victim aging"),
    ("comps/li/casterRejuvenationYears", "5", "Full Drain Life Wraith rejuvenation"),
    ("comps/li/minimumCasterAgeYears", "18", "Full Drain Life Wraith age floor"),
    ("comps/li/lifeForceGain", "1", "Full Drain Life Life Force gain"),
    ("comps/li/biomassGain", "8", "Full Drain Life biomass yield"),
    ("comps/li/fatalIfAlreadyLifeDrained", "true", "Full Drain Life repeated-feed lethal rule"),
    ("comps/li/victimHediff", "WNG_LifeDrained", "Full Drain Life victim state"),
    ("comps/li/casterHediff", "WNG_FedRecently", "Full Drain Life caster state"),
]:
    require_text(full, path, expected, description)

for path, expected, description in [
    ("comps/li/victimAgeYears", "10", "Partial Feed victim aging"),
    ("comps/li/casterRejuvenationYears", "1", "Partial Feed Wraith rejuvenation"),
    ("comps/li/minimumCasterAgeYears", "18", "Partial Feed Wraith age floor"),
    ("comps/li/lifeForceGain", "0.34", "Partial Feed Life Force gain"),
    ("comps/li/biomassGain", "2", "Partial Feed biomass yield"),
    ("comps/li/fatalIfAlreadyLifeDrained", "false", "Partial Feed must remain nonfatal on repeats"),
    ("comps/li/victimHediff", "WNG_LifeDrained", "Partial Feed victim state"),
    ("comps/li/casterHediff", "WNG_FedRecently", "Partial Feed caster state"),
]:
    require_text(partial, path, expected, description)

if feeding_gene is not None:
    granted = [(n.text or "").strip() for n in feeding_gene.findall("abilities/li")]
    for required in ("WNG_DrainLife", "WNG_PartialFeed"):
        if required not in granted:
            fail(f"True-Wraith feeding gene does not grant {required}")

require_text(life_drained, "comps/li/disappearsAfterTicks", "60000~120000", "Life Drained duration")
require_text(fed_recently, "comps/li/disappearsAfterTicks", "60000~60000", "Fed Recently duration")

source = SOURCE.read_text(encoding="utf-8", errors="replace") if SOURCE.exists() else ""
if not SOURCE.exists():
    fail("Missing shared Wraith feeding runtime")
else:
    for needle, description in [
        ("victim.Downed", "feeding must require downed prey"),
        ("victim.RaceProps.IsFlesh", "feeding must require biological/flesh prey"),
        ("!victim.RaceProps.IsMechanoid", "feeding must reject mechanoids"),
        ("!IsSynthetic(victim)", "feeding must reject synthetic/nanite humanoids"),
        ("WraithLifeForceUtility.Offset", "feeding must restore the native Life Force resource"),
        ("ProduceBiomass", "feeding must create cultured biomass"),
        ("fatalIfAlreadyLifeDrained", "runtime must implement the configured repeated-full-feed lethal boundary"),
        ("victim.Kill(null)", "runtime must actually kill on a configured lethal repeat"),
        ("Replicator", "synthetic filter must cover Replicators"),
        ("Asuran", "synthetic filter must cover Asurans"),
        ("Nanite", "synthetic filter must cover nanite identities"),
    ]:
        if needle not in source:
            fail(f"Missing feeding runtime contract: {description}")

    if "Dialog_MessageBox" in source or "feeding request" in source.lower():
        fail("Ordinary pawn feeding must not open faction feeding-request UI")

if errors:
    print("Wraith feeding ecology audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith feeding ecology audit OK")
