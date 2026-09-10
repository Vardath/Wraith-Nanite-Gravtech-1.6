#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
FORGE = ROOT / "Defs" / "ThingDefs" / "Wraith_LivingForge.xml"
WEAPON = ROOT / "Defs" / "ThingDefs" / "Weapons_Wraith.xml"
RESEARCH = ROOT / "Defs" / "ResearchProjectDefs" / "Research_Wraith.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

forge = FORGE.read_text(encoding="utf-8", errors="replace") if FORGE.exists() else ""
weapon = WEAPON.read_text(encoding="utf-8", errors="replace") if WEAPON.exists() else ""
research = RESEARCH.read_text(encoding="utf-8", errors="replace") if RESEARCH.exists() else ""

if not FORGE.exists(): errors.append("Missing Living Forge Def")
if not WEAPON.exists(): errors.append("Missing Wraith stunner Def")
if not RESEARCH.exists(): errors.append("Missing Wraith research tree")

for needle, description in [
    ("<li>WNG_GrowWraithStunner</li>", "Living Forge must expose the grown-stunner bill"),
    ("<defName>WNG_GrowWraithStunner</defName>", "Grown-stunner recipe must exist"),
    ("<WNG_Biomass>1</WNG_Biomass>", "placeholder"),
]:
    if description == "placeholder":
        continue
    require(forge, needle, description)

# Lock recipe inputs/output and research gate without conflating the early recovered specimen.
for needle, description in [
    ("<count>28</count>", "Grown stunner must consume 28 cultured biomass"),
    ("<li>ComponentIndustrial</li>", "Grown stunner must require one industrial control component"),
    ("<WNG_WraithStunner>1</WNG_WraithStunner>", "Grown-stunner recipe must produce the real Wraith stunner"),
    ("<researchPrerequisite>WNG_LivingWeapons</researchPrerequisite>", "Stunner manufacture must remain gated behind Living Weapons"),
    ("<Crafting>7</Crafting>", "Growing a Wraith stunner must require Crafting 7"),
]:
    require(forge, needle, description)

# Make sure the first recovered weapon is still the analyzable research entry.
for needle, description in [
    ("<defName>WNG_WraithStunner</defName>", "Recovered Wraith stunner must remain the manufactured/recovered weapon identity"),
    ("Class=\"CompProperties_CompAnalyzableUnlockResearch\"", "Wraith stunner must remain analyzable"),
]:
    require(weapon, needle, description)

require(research, "<defName>WNG_LivingWeapons</defName>", "Living Weapons research node must exist")
require(research, "<li>WNG_WraithStunner</li>", "Biological Cultivation must still require recovered-stunner analysis")

# The stunner ThingDef itself must not become directly craftable before the Living Forge recipe gate.
for forbidden in ["<recipeMaker>", "<researchPrerequisite>WNG_BiologicalCultivation</researchPrerequisite>"]:
    if forbidden in weapon:
        errors.append("Wraith stunner ThingDef must not bypass the Living Weapons manufacturing gate")

if errors:
    print("Wraith Living Weapons audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Living Weapons audit OK")
