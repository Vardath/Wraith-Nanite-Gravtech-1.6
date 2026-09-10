#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithGrowthChamber.cs"
DEF = ROOT / "Defs" / "ThingDefs" / "Wraith_GrowthChamber.xml"

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require_text(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing Growth Chamber contract: {description}")


if not SOURCE.exists():
    fail("Missing fresh public Growth Chamber runtime")
    source = ""
else:
    source = SOURCE.read_text(encoding="utf-8", errors="replace")

if not DEF.exists():
    fail("Missing active WNG_GrowthChamber ThingDef")
    root = None
else:
    try:
        root = ET.parse(DEF).getroot()
    except ET.ParseError as ex:
        fail(f"Growth Chamber XML parse failure: {ex}")
        root = None

thing = None
if root is not None:
    for candidate in root.findall("ThingDef"):
        if (candidate.findtext("defName") or "").strip() == "WNG_GrowthChamber":
            thing = candidate
            break
    if thing is None:
        fail("Missing ThingDef WNG_GrowthChamber")

if thing is not None:
    research = [(x.text or "").strip() for x in thing.findall("researchPrerequisites/li")]
    if research != ["WNG_CloningInfrastructure"]:
        fail(f"Growth Chamber research gate must be WNG_CloningInfrastructure, found {research}")
    costs = {child.tag: (child.text or "").strip() for child in thing.findall("costList/*")}
    expected_costs = {"Steel": "160", "ComponentIndustrial": "6", "WNG_Biomass": "100"}
    if costs != expected_costs:
        fail(f"Growth Chamber fresh test construction cost changed: expected {expected_costs}, found {costs}")
    comp = None
    for candidate in thing.findall("comps/li"):
        if candidate.attrib.get("Class") == "WraithNaniteGravtech.CompProperties_WraithGrowthChamber":
            comp = candidate
            break
    if comp is None:
        fail("Growth Chamber must use fresh CompProperties_WraithGrowthChamber")
    else:
        expected = {
            "requiredResearchDefName": "WNG_CloningInfrastructure",
            "minimumDonorLifeForce": "0.55",
            "donorLifeForceCost": "0.25",
            "newbornLifeForce": "0.35",
        }
        for key, value in expected.items():
            found = (comp.findtext(key) or "").strip()
            if found != value:
                fail(f"Growth Chamber {key} must remain {value}, found {found!r}")

for needle, description in [
    ('BuildGestationCommand("WNG_WraithHunter", "Gestate Wraith hunter", 160, 120000)', "Hunter 160 biomass / 120000 ticks"),
    ('BuildGestationCommand("WNG_WraithWarrior", "Gestate Wraith warrior", 220, 180000)', "Warrior 220 biomass / 180000 ticks"),
    ('BuildGestationCommand("WNG_WraithKeeper", "Gestate Wraith keeper", 280, 240000)', "Keeper 280 biomass / 240000 ticks"),
    ('minimumDonorLifeForce = 0.55f', "0.55 minimum donor Life Force"),
    ('donorLifeForceCost = 0.25f', "0.25 donor Life Force cost"),
    ('newbornLifeForce = 0.35f', "0.35 newborn Life Force"),
    ('kind == "WNG_WraithKeeper" || kind == "WNG_WraithQueen"', "Keeper/Queen supervision"),
    ('pawnKindDefName == "WNG_WraithQueen"', "explicit Queen cloning refusal"),
    ('TryConsumeBiomass(parent.Map, biomassCost)', "real biomass payment before gestation"),
    ('donor.Value = Math.Max(0f, donor.Value - Math.Max(0f, ChamberProps.donorLifeForceCost))', "real donor Life Force payment"),
    ('PawnGenerator.GeneratePawn(kind, faction)', "actual Wraith pawn generation"),
    ('GenSpawn.Spawn(clone, spawnCell, map)', "actual clone map spawn"),
    ('int refund = investedBiomass / 2', "half-biomass cancellation refund"),
    ('Scribe_Values.Look(ref activePawnKindDefName', "save-persistent clone caste"),
    ('Scribe_Values.Look(ref finishTick', "save-persistent gestation deadline"),
    ('Scribe_Values.Look(ref investedBiomass', "save-persistent invested biomass"),
]:
    require_text(source, needle, description)

if 'BuildGestationCommand("WNG_WraithQueen"' in source:
    fail("Queen must never be offered as a Growth Chamber gestation command")
if "Gravcore" in source or "Gravcore" in (DEF.read_text(encoding="utf-8", errors="replace") if DEF.exists() else ""):
    fail("Fresh public Growth Chamber must not depend on the obsolete Gravcore shortcut")

# Donated Life Force must not be refunded by cancellation.
cancel_start = source.find("private void CancelGestation()")
clear_start = source.find("private void ClearGestation()", cancel_start + 1) if cancel_start >= 0 else -1
cancel_body = source[cancel_start:clear_start] if cancel_start >= 0 and clear_start > cancel_start else ""
if "donor" in cancel_body.lower() or "LifeForceUtility.Offset" in cancel_body:
    fail("Growth Chamber cancellation must not refund donated Life Force")

if errors:
    print("Wraith Growth Chamber audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Growth Chamber audit OK")
