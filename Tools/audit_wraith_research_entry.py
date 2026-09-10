#!/usr/bin/env python3
from pathlib import Path
import sys

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

if errors:
    print("Wraith research entry audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith research entry audit OK")
