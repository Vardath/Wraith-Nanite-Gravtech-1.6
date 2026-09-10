#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
WEAPON = ROOT / "Defs" / "ThingDefs" / "Weapons_Wraith.xml"
PAWNS = ROOT / "Defs" / "PawnKindDefs" / "PawnKinds_Wraith.xml"
RESEARCH = ROOT / "Defs" / "ResearchProjectDefs" / "Research_Wraith.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

weapon = WEAPON.read_text(encoding="utf-8", errors="replace") if WEAPON.exists() else ""
pawns = PAWNS.read_text(encoding="utf-8", errors="replace") if PAWNS.exists() else ""
research = RESEARCH.read_text(encoding="utf-8", errors="replace") if RESEARCH.exists() else ""

if not WEAPON.exists():
    errors.append("Missing fresh Wraith stunner Def")
if not PAWNS.exists():
    errors.append("Missing Wraith PawnKinds")
if not RESEARCH.exists():
    errors.append("Missing fresh Wraith research entry Def")

for needle, description in [
    ("<defName>WNG_WraithStunner</defName>", "Recovered Wraith stunner must exist"),
    ("<li>WNGR2_WraithCaptureWeapon</li>", "Stunner must carry the dedicated Wraith capture-weapon tag"),
    ("Class=\"CompProperties_CompAnalyzableUnlockResearch\"", "Stunner must use RimWorld's native analyzable-unlock component"),
    ("<analysisID>917201</analysisID>", "Stunner analysis identity must remain stable"),
    ("<destroyedOnAnalyzed>false</destroyedOnAnalyzed>", "Recovered stunner must survive analysis"),
    ("<allowRepeatAnalysis>false</allowRepeatAnalysis>", "Recovered stunner analysis must not be infinitely repeatable"),
]:
    require(weapon, needle, description)

for needle, description in [
    ("<defName>WNG_WraithHunter</defName>", "Wraith Hunter PawnKind must exist"),
    ("<li>WNGR2_WraithCaptureWeapon</li>", "Wraith Hunters must provide the recovered-stunner acquisition route"),
]:
    require(pawns, needle, description)

for needle, description in [
    ("<defName>WNG_BiologicalCultivation</defName>", "Biological Cultivation research gate must exist"),
    ("<baseCost>800</baseCost>", "Biological Cultivation cost must remain 800"),
    ("<techLevel>Industrial</techLevel>", "Biological Cultivation must remain Industrial-tier"),
    ("<li>Electricity</li>", "Biological Cultivation must require Electricity"),
    ("<requiredAnalyzed>", "Biological Cultivation must use an analyzed-specimen gate"),
    ("<li>WNG_WraithStunner</li>", "Biological Cultivation must require analysis of the recovered Wraith stunner"),
]:
    require(research, needle, description)

# Lock the biological progression as a graph, rather than relying on incidental string presence.
expected = {
    "WNG_BiologicalCultivation": (800, "Industrial", {"Electricity"}),
    "WNG_FeedingEcology": (1300, "Industrial", {"WNG_BiologicalCultivation"}),
    "WNG_FeedingResistance": (2200, "Industrial", {"WNG_BiologicalCultivation", "DrugProduction"}),
    "WNG_BioelectricOrgans": (1200, "Industrial", {"WNG_BiologicalCultivation", "Batteries"}),
    "WNG_LivingWeapons": (1400, "Spacer", {"WNG_BiologicalCultivation", "Machining"}),
    "WNG_AdvancedCarapace": (1800, "Spacer", {"WNG_LivingWeapons"}),
    "WNG_CloningInfrastructure": (2600, "Spacer", {"WNG_FeedingEcology", "WNG_AdvancedCarapace"}),
    "WNG_ForbiddenHybridization": (4200, "Spacer", {"WNG_CloningInfrastructure", "WNG_FeedingResistance"}),
    "WNG_HiveArchitecture": (3400, "Spacer", {"WNG_CloningInfrastructure", "WNG_BioelectricOrgans"}),
}

if RESEARCH.exists():
    try:
        root = ET.parse(RESEARCH).getroot()
        by_name = {}
        for node in root.findall("ResearchProjectDef"):
            name = node.findtext("defName")
            if name:
                by_name[name.strip()] = node
        for name, (cost, tech, prereqs) in expected.items():
            node = by_name.get(name)
            if node is None:
                errors.append(f"Missing Wraith research node: {name}")
                continue
            actual_cost = node.findtext("baseCost")
            actual_tech = node.findtext("techLevel")
            actual_prereqs = {
                li.text.strip()
                for li in node.findall("./prerequisites/li")
                if li.text and li.text.strip()
            }
            if actual_cost != str(cost):
                errors.append(f"{name} baseCost drifted: expected {cost}, found {actual_cost}")
            if actual_tech != tech:
                errors.append(f"{name} techLevel drifted: expected {tech}, found {actual_tech}")
            if actual_prereqs != prereqs:
                errors.append(
                    f"{name} prerequisite graph drifted: expected {sorted(prereqs)}, found {sorted(actual_prereqs)}"
                )
        if "WNG_OrganicFlight" in by_name:
            errors.append("Organic Flight must not be added until a valid public gravtech prerequisite exists")
    except ET.ParseError as ex:
        errors.append(f"Could not parse Wraith research progression: {ex}")

if errors:
    print("Wraith research entry/progression audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith research entry/progression audit OK")
