#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "IncidentWorker_WraithMatureHiveDiscovery.cs"
DEFS = ROOT / "Defs" / "IncidentDefs" / "Incidents_WraithMatureHive.xml"
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing mature-Hive discovery contract: {description}")


source = SOURCE.read_text(encoding="utf-8", errors="replace") if SOURCE.exists() else ""
if not SOURCE.exists():
    fail("Missing mature-Hive discovery worker")

incident = None
if not DEFS.exists():
    fail("Missing mature-Hive discovery IncidentDef")
else:
    try:
        root = ET.parse(DEFS).getroot()
        incident = next((x for x in root.findall("IncidentDef") if (x.findtext("defName") or "").strip() == "WNG_WraithMatureHiveDiscovered"), None)
        if incident is None:
            fail("Missing IncidentDef WNG_WraithMatureHiveDiscovered")
    except ET.ParseError as ex:
        fail(f"Mature-Hive discovery IncidentDef XML parse failure: {ex}")

if incident is not None:
    expected = {
        "workerClass": "WraithNaniteGravtech.IncidentWorker_WraithMatureHiveDiscovery",
        "baseChance": "0.08",
        "minRefireDays": "25",
        "category": "Misc",
        "pointsScaleable": "false",
    }
    for key, value in expected.items():
        found = (incident.findtext(key) or "").strip()
        if found != value:
            fail(f"Mature-Hive discovery {key} must remain {value!r}, found {found!r}")
    target_tags = [(x.text or "").strip() for x in incident.findall("targetTags/li")]
    if target_tags != ["World"]:
        fail(f"Mature-Hive discovery must remain world-targeted, found {target_tags}")

if source:
    for needle, description in [
        ("public sealed class IncidentWorker_WraithMatureHiveDiscovery : IncidentWorker", "fresh discovery worker"),
        ("private const int MaxActiveMatureHives = 2", "two-site global cap"),
        ("private const int MinSiteDistance = 8", "eight-tile minimum discovery distance"),
        ("private const int MaxSiteDistance = 20", "twenty-tile maximum discovery distance"),
        ('GetNamedSilentFail("WNG_WraithMatureHive")', "audited mature-Hive SitePart use"),
        ("ActiveMatureHives(siteDef).Count >= MaxActiveMatureHives", "hard active-site cap"),
        ("WraithCaptureUtility.IsWraithCaptor(faction)", "real Wraith-lineage eligibility"),
        ("faction != Faction.OfPlayer", "player-faction exclusion"),
        ("!faction.defeated", "defeated-faction exclusion"),
        ("HashSet<Faction> represented", "one-site-per-lineage tracking"),
        ("!represented.Contains(faction)", "one-site-per-lineage exclusion"),
        ("TileFinder.TryFindNewSiteTile", "bounded world tile selection"),
        ("SiteMaker.MakeSite", "native RimWorld site creation"),
        ("ifHostileThenMustRemainHostile: true", "hostile-site diplomacy preservation"),
        ("Find.WorldObjects.Add(site)", "actual world-object registration"),
        ('"Mature Wraith Hive discovered"', "discovery letter"),
        ("faction.HostileTo(Faction.OfPlayer)", "stance-aware discovery presentation"),
    ]:
        require(source, needle, description)

    for forbidden, description in [
        ("WraithFactionHunger", "strategic hunger coupling"),
        ("GetStrategicHunger", "strategic hunger weighting"),
        ("HighestHostileStrategicHunger", "hostile hunger weighting"),
        ("Dialog_MessageBox", "feeding-style popup UI"),
        ("EligibleFeedingSubjects", "feeding-subject coupling"),
        ("Drain Life", "ordinary pawn feeding coupling"),
        ("WNG_WraithHoldingSite", "captivity-site substitution"),
    ]:
        if forbidden in source:
            fail(f"Mature-Hive discovery must remain independent of feeding/captivity systems; found {description}")

if errors:
    print("Mature Wraith Hive discovery audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Mature Wraith Hive discovery audit OK")
