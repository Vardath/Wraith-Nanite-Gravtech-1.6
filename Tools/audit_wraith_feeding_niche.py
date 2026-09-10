#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DEF_PATH = ROOT / "Defs" / "ThingDefs" / "Wraith_FeedingNiche.xml"
SOURCE_PATH = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithFeedingNiche.cs"
CAPTURE_PATH = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptureUtility.cs"

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require_text(node: ET.Element | None, path: str, expected: str, description: str) -> None:
    if node is None:
        return
    value = (node.findtext(path) or "").strip()
    if value != expected:
        fail(f"{description}: expected {expected!r}, found {value!r}")


thing = None
if not DEF_PATH.exists():
    fail("Missing active WNG_FeedingNiche ThingDef")
else:
    try:
        root = ET.parse(DEF_PATH).getroot()
        for candidate in root.findall("ThingDef"):
            if (candidate.findtext("defName") or "").strip() == "WNG_FeedingNiche":
                thing = candidate
                break
        if thing is None:
            fail("Wraith_FeedingNiche.xml does not define WNG_FeedingNiche")
    except ET.ParseError as ex:
        fail(f"Feeding Niche XML parse failure: {ex}")

if thing is not None:
    if thing.attrib.get("ParentName") != "BedBase":
        fail("Feeding Niche must remain a native BedBase/Building_Bed so prisoner assignment and occupancy stay native")

    for path, expected, description in [
        ("tickerType", "Normal", "Feeding Niche must tick normally"),
        ("costList/WNG_Biomass", "35", "Feeding Niche cultured-biomass construction cost"),
        ("researchPrerequisites/li", "WNG_FeedingEcology", "Feeding Niche research gate"),
        ("comps/li/intervalTicks", "120000", "ration-feed interval"),
        ("comps/li/prisonerAgeYears", "2", "prisoner aging per ration cycle"),
        ("comps/li/lifeForcePerRecipient", "0.12", "Life Force restored per recipient"),
        ("comps/li/recipientLimit", "3", "maximum Wraith recipients per cycle"),
        ("comps/li/biomassYield", "4", "biomass yield per ration cycle"),
        ("comps/li/lifeForceCutoff", "0.85", "Life Force hunger cutoff"),
    ]:
        require_text(thing, path, expected, description)

    comp = thing.find("comps/li")
    if comp is None or comp.attrib.get("Class") != "WraithNaniteGravtech.CompProperties_WraithFeedingNiche":
        fail("Feeding Niche must attach the fresh Wraith feeding-niche comp")

source = SOURCE_PATH.read_text(encoding="utf-8", errors="replace") if SOURCE_PATH.exists() else ""
if not SOURCE_PATH.exists():
    fail("Missing fresh Wraith Feeding Niche runtime")
else:
    for needle, description in [
        ("public int intervalTicks = 120000", "runtime default interval must remain 120000 ticks"),
        ("public int prisonerAgeYears = 2", "runtime default prisoner aging must remain 2 years"),
        ("public float lifeForcePerRecipient = 0.12f", "runtime default Life Force restoration must remain 0.12"),
        ("public int recipientLimit = 3", "runtime default recipient cap must remain 3"),
        ("public int biomassYield = 4", "runtime default biomass yield must remain 4"),
        ("public float lifeForceCutoff = 0.85f", "runtime default hunger cutoff must remain 0.85"),
        ("Building_Bed niche = parent as Building_Bed", "runtime must operate through native Building_Bed occupancy"),
        ("WraithCaptureUtility.IsValidCaptiveIdentity(pawn)", "niche must reuse biological/synthetic captive eligibility"),
        ("pawn.guest?.IsPrisoner != true", "niche must require an actual prisoner"),
        ("pawn.guest.HostFaction != owner", "niche must require the prisoner to be hosted by the niche faction"),
        ("lifeForce.Value >= threshold", "niche must feed only Wraith below the configured Life Force threshold"),
        ("result.Sort", "niche must prioritize the hungriest eligible Wraith"),
        ("WNG_WraithKeeper", "Keeper supervision must remain valid"),
        ("WNG_WraithQueen", "Queen supervision must remain valid"),
        ("WraithLifeForceUtility.Offset", "ration cycles must restore the existing Life Force resource"),
        ("WNG_LifeDrained", "ration feeding must visibly mark/refresh Life Drained on the prisoner"),
        ("WNG_Biomass", "ration feeding must produce cultured biomass"),
        ("GenPlace.TryPlaceThing", "ration biomass must be physically placed on the map"),
        ("Scribe_Values.Look(ref nextCycleTick", "next ration cycle must be save-persistent"),
        ("Scribe_Values.Look(ref rationFeedingEnabled", "player ration-feeding toggle must be save-persistent"),
    ]:
        if needle not in source:
            fail(f"Missing Feeding Niche runtime contract: {description}")

    forbidden = [
        ("victim.Kill", "Feeding Niche must never use the lethal repeat-feed path"),
        ("fatalIfAlreadyLifeDrained", "Feeding Niche must remain separate from full Drain Life lethal-repeat configuration"),
        ("Dialog_MessageBox", "Feeding Niche must not open faction feeding-request UI"),
        ("ScheduleForcedRaid", "Feeding Niche must not manipulate faction raid pressure"),
        ("RefuseRequest", "Feeding Niche must not participate in faction feeding-request refusal handling"),
    ]
    for needle, description in forbidden:
        if needle in source:
            fail(description)

capture = CAPTURE_PATH.read_text(encoding="utf-8", errors="replace") if CAPTURE_PATH.exists() else ""
if not CAPTURE_PATH.exists():
    fail("Missing shared Wraith capture eligibility boundary used by the Feeding Niche")
else:
    for needle, description in [
        ("!pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh", "shared captive boundary must remain biological humanlike/flesh only"),
        ("WraithLifeForceUtility.Get(pawn) != null", "shared captive boundary must exclude Wraith"),
        ("Replicator", "shared captive boundary must exclude Replicator identities"),
        ("Asuran", "shared captive boundary must exclude Asuran identities"),
        ("Nanite", "shared captive boundary must exclude nanite identities"),
    ]:
        if needle not in capture:
            fail(f"Missing shared Feeding Niche captive boundary: {description}")

if errors:
    print("Wraith Feeding Niche audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Feeding Niche audit OK")
