#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
RESOURCE = ROOT / "Defs" / "ThingDefs" / "Resources_Wraith.xml"
FORGE = ROOT / "Defs" / "ThingDefs" / "Wraith_LivingForge.xml"
RESEARCH = ROOT / "Defs" / "ResearchProjectDefs" / "Research_Wraith.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

resource = RESOURCE.read_text(encoding="utf-8", errors="replace") if RESOURCE.exists() else ""
forge = FORGE.read_text(encoding="utf-8", errors="replace") if FORGE.exists() else ""
research = RESEARCH.read_text(encoding="utf-8", errors="replace") if RESEARCH.exists() else ""

if not RESOURCE.exists():
    errors.append("Missing cultured biomass resource Def")
if not FORGE.exists():
    errors.append("Missing fresh Living Forge biomass-production Def")
if not RESEARCH.exists():
    errors.append("Missing Wraith research gate")

for needle, description in [
    ("<defName>WNG_Biomass</defName>", "Cultured biomass resource must exist"),
    ("<stackLimit>80</stackLimit>", "Cultured biomass stack limit must remain 80"),
    ("<MarketValue>4.2</MarketValue>", "Cultured biomass market value must remain 4.2"),
    ("<Mass>0.18</Mass>", "Cultured biomass mass must remain 0.18"),
]:
    require(resource, needle, description)

for needle, description in [
    ("<defName>WNG_LivingForge</defName>", "Living Forge must exist"),
    ("<thingClass>Building_WorkTable</thingClass>", "Living Forge must remain a real billable worktable"),
    ("<basePowerConsumption>250</basePowerConsumption>", "Living Forge must remain powered at 250 W"),
    ("<li>WNG_BiologicalCultivation</li>", "Living Forge construction must require Biological Cultivation"),
    ("<defName>WNG_CultureBiomass</defName>", "Biomass culture recipe must exist"),
    ("<li>MeatRaw</li>", "Biomass culture recipe must consume raw meat"),
    ("<count>20</count>", "Biomass culture recipe must consume 20 raw meat"),
    ("<WNG_Biomass>16</WNG_Biomass>", "Biomass culture recipe must produce 16 cultured biomass"),
    ("<researchPrerequisite>WNG_BiologicalCultivation</researchPrerequisite>", "Biomass culture recipe must require Biological Cultivation"),
]:
    require(forge, needle, description)

# The first Forge must be buildable before any cultured biomass exists.
cost_start = forge.find("<costList>")
cost_end = forge.find("</costList>", cost_start)
if cost_start >= 0 and cost_end >= 0:
    construction_cost = forge[cost_start:cost_end]
    if "WNG_Biomass" in construction_cost:
        errors.append("Living Forge construction must not require cultured biomass; that creates bootstrap circularity")
else:
    errors.append("Living Forge construction cost list is missing")

require(research, "<defName>WNG_BiologicalCultivation</defName>", "Biological Cultivation research gate must exist")

if errors:
    print("Wraith biomass audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith biomass audit OK")
