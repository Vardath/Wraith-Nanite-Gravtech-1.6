#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
HEART_DEF = ROOT / "Defs" / "ThingDefs" / "Wraith_HiveHeart.xml"
VAULT_DEF = ROOT / "Defs" / "ThingDefs" / "Wraith_DormancyVault.xml"
HEART_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithHiveHeart.cs"
VAULT_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithDormancyVault.cs"
POP_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithMatureHivePopulation.cs"
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing Hive infrastructure contract: {description}")


def find_thing(path: Path, def_name: str) -> ET.Element | None:
    if not path.exists():
        fail(f"Missing active {def_name} ThingDef file")
        return None
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as ex:
        fail(f"{path.name} XML parse failure: {ex}")
        return None
    for candidate in root.findall("ThingDef"):
        if (candidate.findtext("defName") or "").strip() == def_name:
            return candidate
    fail(f"Missing ThingDef {def_name}")
    return None


heart_source = HEART_SOURCE.read_text(encoding="utf-8", errors="replace") if HEART_SOURCE.exists() else ""
vault_source = VAULT_SOURCE.read_text(encoding="utf-8", errors="replace") if VAULT_SOURCE.exists() else ""
pop_source = POP_SOURCE.read_text(encoding="utf-8", errors="replace") if POP_SOURCE.exists() else ""
if not HEART_SOURCE.exists():
    fail("Missing fresh Hive Heart runtime")
if not VAULT_SOURCE.exists():
    fail("Missing fresh Dormancy Vault runtime")
if not POP_SOURCE.exists():
    fail("Missing mature Hive population runtime")

heart = find_thing(HEART_DEF, "WNG_HiveHeart")
vault = find_thing(VAULT_DEF, "WNG_DormancyVault")

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

if vault is not None:
    if vault.find("designationCategory") is not None:
        fail("Dormancy Vault must remain generator-only and absent from player designation menus")
    if (vault.findtext("tickerType") or "").strip() != "Normal":
        fail("Dormancy Vault must tick normally so finite wake waves can execute")
    comps = vault.findall("comps/li")
    vault_comp = next((x for x in comps if x.attrib.get("Class") == "WraithNaniteGravtech.CompProperties_WraithDormancyVault"), None)
    if vault_comp is None:
        fail("Dormancy Vault must carry its finite-reserve comp")
    else:
        expected = {
            "dormantCount": "4",
            "triggerRadius": "18",
            "awakenIntervalTicks": "180",
            "awakenPerWave": "2",
        }
        for key, value in expected.items():
            found = (vault_comp.findtext(key) or "").strip()
            if found != value:
                fail(f"Dormancy Vault {key} must remain {value}, found {found!r}")

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
    ('"WNG_DormancyVault"', "Dormancy Vault organic-structure recognition"),
]:
    require(heart_source, needle, description)

for forbidden in ("PawnGenerator", "GenSpawn.Spawn", "Resurrect", "MakeThing(DefDatabase"):
    if forbidden in heart_source:
        fail(f"Hive Heart must repair existing structures only; forbidden creation/resurrection path found: {forbidden}")

for needle, description in [
    ("dormantCount = 4", "four-unit sealed reserve"),
    ("triggerRadius = 18f", "18-cell proximity trigger"),
    ("awakenIntervalTicks = 180", "180-tick wake cadence"),
    ("awakenPerWave = 2", "two-Wraith wake wave"),
    ("remainingDormants = Math.Max(0, VaultProps.dormantCount)", "one-time reserve initialization"),
    ("parent.Faction.HostileTo(Faction.OfPlayer)", "hostile-only activation"),
    ("parent.HitPoints < parent.MaxHitPoints", "damage activation trigger"),
    ("parent.Map.mapPawns.FreeColonistsSpawned", "free-colonist proximity trigger"),
    ('"WNG_WraithHunter"', "Hunter reserve caste"),
    ('"WNG_WraithWarrior"', "Warrior reserve caste"),
    ("remainingDormants--", "reserve depletion after successful spawn"),
    ("new LordJob_DefendBase", "released reserve defending its Hive"),
    ("Scribe_Values.Look(ref remainingDormants", "saved remaining reserve"),
    ("Scribe_Values.Look(ref nextWakeTick", "saved wake cadence"),
    ("Scribe_Values.Look(ref activated", "saved activation state"),
]:
    require(vault_source, needle, description)

for forbidden, description in [
    ("remainingDormants++", "reserve refill increment"),
    ("remainingDormants +=", "reserve refill addition"),
    ("CompWraithGrowthChamber", "Growth Chamber linkage"),
    ("MatureWraithHivePopulation", "demographic population linkage"),
    ("TryStartPopulationReplacement", "clone replacement linkage"),
]:
    if forbidden in vault_source:
        fail(f"Dormancy Vault must remain finite and demographically separate; found {description}")

# A Heart carrying the demographic comp must remain inert until the explicit mature-Hive generator call.
require(pop_source, "InitializeGeneratedHive(IEnumerable<Pawn> foundingMembers, Building exactGeneratedGrowthChamber)", "explicit mature-Hive initialization")
if "PostSpawnSetup" in pop_source and "InitializeGeneratedHive" in pop_source:
    fail("Mature-Hive population tracker must not auto-initialize from PostSpawnSetup")
for forbidden in ("WNG_DormancyVault", "CompWraithDormancyVault"):
    if forbidden in pop_source:
        fail("Dormancy Vault reserve must never enter mature-Hive demographic population logic")

if errors:
    print("Wraith Hive infrastructure audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Hive infrastructure audit OK")
