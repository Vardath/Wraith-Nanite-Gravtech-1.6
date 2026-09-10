#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
HEART_DEF = ROOT / "Defs" / "ThingDefs" / "Wraith_HiveHeart.xml"
HEART_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithHiveHeart.cs"
POP_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithMatureHivePopulation.cs"
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing Hive infrastructure contract: {description}")


heart_source = HEART_SOURCE.read_text(encoding="utf-8", errors="replace") if HEART_SOURCE.exists() else ""
pop_source = POP_SOURCE.read_text(encoding="utf-8", errors="replace") if POP_SOURCE.exists() else ""
if not HEART_SOURCE.exists():
    fail("Missing fresh Hive Heart runtime")
if not POP_SOURCE.exists():
    fail("Missing mature Hive population runtime")

heart = None
if not HEART_DEF.exists():
    fail("Missing active WNG_HiveHeart ThingDef")
else:
    try:
        root = ET.parse(HEART_DEF).getroot()
        for candidate in root.findall("ThingDef"):
            if (candidate.findtext("defName") or "").strip() == "WNG_HiveHeart":
                heart = candidate
                break
        if heart is None:
            fail("Missing ThingDef WNG_HiveHeart")
    except ET.ParseError as ex:
        fail(f"Hive Heart XML parse failure: {ex}")

if heart is not None:
    research = [(x.text or "").strip() for x in heart.findall("researchPrerequisites/li")]
    if research != ["WNG_HiveArchitecture"]:
        fail(f"Hive Heart must remain gated by WNG_HiveArchitecture, found {research}")
    comps = heart.findall("comps/li")
    heart_comp = next((x for x in comps if x.attrib.get("Class") == "WraithNaniteGravtech.CompProperties_WraithHiveHeart"), None)
    pop_comp = next((x for x in comps if x.attrib.get("Class") == "WraithNaniteGravtech.CompProperties_MatureWraithHivePopulation"), None)
    if heart_comp is None:
        fail("Hive Heart must carry the bounded repair comp")
    else:
        expected = {
            "pulseTicks": "600",
            "repairRadius": "24",
            "maxRepairTargets": "4",
            "repairHitPoints": "7",
            "biomassCostPerPulse": "1",
        }
        for key, value in expected.items():
            found = (heart_comp.findtext(key) or "").strip()
            if found != value:
                fail(f"Hive Heart {key} must remain {value}, found {found!r}")
    if pop_comp is None:
        fail("Hive Heart must carry the mature-Hive population tracker")
    elif (pop_comp.findtext("replacementRetryTicks") or "").strip() != "30000":
        fail("Hive Heart mature population retry must remain 30000 ticks")

for needle, description in [
    ("pulseTicks = 600", "600-tick Heart pulse"),
    ("repairRadius = 24f", "24-cell Heart repair radius"),
    ("maxRepairTargets = 4", "four-target Heart repair cap"),
    ("repairHitPoints = 7", "seven-HP repair pulse"),
    ("biomassCostPerPulse = 1", "one-biomass productive pulse cost"),
    ("thing.Faction != parent.Faction", "same-faction repair boundary"),
    ("thing.HitPoints >= thing.MaxHitPoints", "damaged-existing-structure boundary"),
    ("TryConsumeBiomass(parent.Map, biomassCost)", "real biomass payment"),
    ("structure.HitPoints = Math.Min(structure.MaxHitPoints, structure.HitPoints + heal)", "bounded direct repair"),
    ('"WNG_GrowthChamber"', "Growth Chamber organic-structure recognition"),
    ('"WNG_BioelectricOrgan"', "Bioelectric Organ recognition"),
    ('"WNG_FeedingNiche"', "Feeding Niche recognition"),
    ('"WNG_HibernationPod"', "Hibernation Pod recognition"),
    ('"WNG_DormancyVault"', "future Dormancy Vault recognition"),
]:
    require(heart_source, needle, description)

for forbidden in ("PawnGenerator", "GenSpawn.Spawn", "Resurrect", "MakeThing(DefDatabase"):
    if forbidden in heart_source:
        fail(f"Hive Heart must repair existing structures only; forbidden creation/resurrection path found: {forbidden}")

# A Heart carrying the demographic comp must still remain inert until the explicit site generator call.
require(pop_source, "InitializeGeneratedHive(IEnumerable<Pawn> foundingMembers, Building exactGeneratedGrowthChamber)", "explicit mature-Hive initialization")
if "PostSpawnSetup" in pop_source and "InitializeGeneratedHive" in pop_source:
    fail("Mature-Hive population tracker must not auto-initialize from PostSpawnSetup")

if errors:
    print("Wraith Hive infrastructure audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Hive infrastructure audit OK")
