#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
ORGAN = ROOT / "Defs" / "ThingDefs" / "Wraith_BioelectricOrgan.xml"
RESEARCH = ROOT / "Defs" / "ResearchProjectDefs" / "Research_Wraith.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

organ = ORGAN.read_text(encoding="utf-8", errors="replace") if ORGAN.exists() else ""
research = RESEARCH.read_text(encoding="utf-8", errors="replace") if RESEARCH.exists() else ""

if not ORGAN.exists():
    errors.append("Missing fresh Wraith bioelectric organ Def")
if not RESEARCH.exists():
    errors.append("Missing Wraith research tree")

for needle, description in [
    ("<defName>WNG_BioelectricOrgan</defName>", "Bioelectric Organ ThingDef must exist"),
    ("<WNG_Biomass>45</WNG_Biomass>", "Bioelectric Organ must cost 45 cultured biomass"),
    ("<compClass>CompPowerPlant</compClass>", "Bioelectric Organ must use RimWorld's native power-plant comp"),
    ("<basePowerConsumption>-1200</basePowerConsumption>", "Bioelectric Organ must produce 1200 W"),
    ("<transmitsPower>true</transmitsPower>", "Bioelectric Organ must connect to the native power network"),
    ("<li>WNG_BioelectricOrgans</li>", "Bioelectric Organ construction must require its research node"),
]:
    require(organ, needle, description)

for forbidden in ["CompRefuelable", "fuelFilter", "fuelConsumptionRate", "WoodLog"]:
    if forbidden in organ:
        errors.append("Bioelectric Organ must not inherit conventional fuel behavior")

require(research, "<defName>WNG_BioelectricOrgans</defName>", "Bioelectric Organs research node must exist")

if errors:
    print("Wraith bioelectric organ audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith bioelectric organ audit OK")
