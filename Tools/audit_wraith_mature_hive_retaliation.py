#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithMatureHiveRetaliation.cs"
SITE = ROOT / "Source" / "WNGR2" / "Wraith" / "SitePartWorker_WraithMatureHive.cs"
DEFS = ROOT / "Defs" / "IncidentDefs" / "Incidents_WraithMatureHiveRetaliation.xml"
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing mature-Hive retaliation contract: {description}")


source = SOURCE.read_text(encoding="utf-8", errors="replace") if SOURCE.exists() else ""
site = SITE.read_text(encoding="utf-8", errors="replace") if SITE.exists() else ""
if not SOURCE.exists():
    fail("Missing mature-Hive retaliation runtime")
if not SITE.exists():
    fail("Missing mature-Hive site worker")

incident = None
if not DEFS.exists():
    fail("Missing mature-Hive retaliation IncidentDef")
else:
    try:
        root = ET.parse(DEFS).getroot()
        incident = next((x for x in root.findall("IncidentDef") if (x.findtext("defName") or "").strip() == "WNG_WraithMatureHiveRetaliation"), None)
        if incident is None:
            fail("Missing IncidentDef WNG_WraithMatureHiveRetaliation")
    except ET.ParseError as ex:
        fail(f"Mature-Hive retaliation IncidentDef XML parse failure: {ex}")

if incident is not None:
    expected = {
        "workerClass": "WraithNaniteGravtech.IncidentWorker_WraithMatureHiveRetaliation",
        "baseChance": "0",
        "category": "ThreatBig",
        "pointsScaleable": "true",
    }
    for key, value in expected.items():
        found = (incident.findtext(key) or "").strip()
        if found != value:
            fail(f"Mature-Hive retaliation {key} must remain {value!r}, found {found!r}")
    target_tags = [(x.text or "").strip() for x in incident.findall("targetTags/li")]
    if target_tags != ["Map_PlayerHome"]:
        fail(f"Mature-Hive retaliation must remain player-home targeted, found {target_tags}")

if source:
    for needle, description in [
        ("public sealed class WraithMatureHiveRetaliationRegistry : GameComponent", "save-persistent retaliation registry"),
        ("private const int CheckIntervalTicks = 600", "600-tick response polling"),
        ("private const int RetryDelayTicks = 60000", "one-day failed-launch retry"),
        ("private const int MinimumDelayTicks = 120000", "two-day minimum retaliation delay"),
        ("private const int MaximumDelayTicks = 240000", "four-day maximum retaliation delay"),
        ("public bool ScheduleHiveRetaliation(int siteId, string factionDefName)", "exact site and lineage scheduling API"),
        ("sourceSiteIds.Contains(siteId)", "duplicate source-site suppression"),
        ("Rand.RangeInclusive(MinimumDelayTicks, MaximumDelayTicks)", "bounded random retaliation delay"),
        ("sourceFactionDefNames.Add(factionDefName)", "source lineage persistence"),
        ('GetNamedSilentFail("WNG_WraithMatureHiveRetaliation")', "dedicated retaliation incident lookup"),
        ("StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, target)", "native player-home threat parameters"),
        ("parms.faction = faction", "exact retaliation faction assignment"),
        ("retaliationTicks[i] = SafeFutureTick(now, RetryDelayTicks)", "failed response retry"),
        ("retaliationTicks[i] = Math.Min(retaliationTicks[i], retaliationTicks[j])", "corrupt duplicate merge keeps earliest response"),
        ('Scribe_Collections.Look(ref sourceSiteIds, "wngMatureHiveRetaliationSourceSites", LookMode.Value)', "source-site save persistence"),
        ('Scribe_Collections.Look(ref retaliationTicks, "wngMatureHiveRetaliationTicks", LookMode.Value)', "due-tick save persistence"),
        ('Scribe_Collections.Look(ref sourceFactionDefNames, "wngMatureHiveRetaliationFactionDefs", LookMode.Value)', "lineage save persistence"),
        ("public sealed class IncidentWorker_WraithMatureHiveRetaliation : IncidentWorker", "fresh retaliation incident worker"),
        ("map != null && map.IsPlayerHome", "player-home execution boundary"),
        ("WraithCaptureUtility.IsWraithCaptor(requested)", "requested Wraith-lineage validation"),
        ("parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack", "immediate-attack raid strategy"),
        ("IncidentDefOf.RaidEnemy.Worker.TryExecute(parms)", "real RimWorld raid launch"),
        ("points >= 1800f", "cruiser threat threshold"),
        ('"WNG_WraithStrikeCraft"', "strike-craft response tier"),
        ('"WNG_WraithStrikeCraftIncoming"', "strike-craft skyfaller tier"),
        ('"WNG_WraithCruiser"', "cruiser response tier"),
        ('"WNG_WraithCruiserIncoming"', "cruiser skyfaller tier"),
        ("TryStageOptionalCraft(map, faction, craftDefName, incomingDefName);", "best-effort post-raid craft staging"),
        ("if (craftDef == null || incomingDef == null)", "missing-craft safe fallback"),
        ("SkyfallerMaker.SpawnSkyfaller", "native optional craft arrival"),
        ("return true;", "successful committed retaliation completion"),
    ]:
        require(source, needle, description)

    for forbidden, description in [
        ("WraithFactionHunger", "strategic hunger coupling"),
        ("GetStrategicHunger", "strategic hunger lookup"),
        ("Dialog_MessageBox", "feeding request UI"),
        ("EligibleFeedingSubjects", "feeding-subject coupling"),
        ("WNG_WraithHoldingSite", "captivity-site substitution"),
    ]:
        if forbidden in source:
            fail(f"Mature-Hive retaliation must remain independent of feeding/captivity systems; found {description}")

if site:
    for needle, description in [
        ("public override void SitePartWorkerTick(SitePart sitePart)", "mature-Hive lifecycle tick"),
        ("site.IsHashIntervalTick(250)", "250-tick neutralization check"),
        ("WraithCaptureUtility.IsWraithCaptor(faction)", "exact Wraith-site faction validation"),
        ("!faction.HostileTo(Faction.OfPlayer)", "non-hostile Hive retaliation safety guard"),
        ("GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: true)", "active and dormant threat clearance test"),
        ("Current.Game?.GetComponent<WraithMatureHiveRetaliationRegistry>()", "fresh retaliation registry lookup"),
        ("registry?.ScheduleHiveRetaliation(site.ID, faction.def?.defName)", "exact source site and lineage scheduling"),
    ]:
        require(site, needle, description)

    for forbidden, description in [
        ("WraithFactionHunger", "strategic hunger coupling"),
        ("Dialog_MessageBox", "feeding-style popup UI"),
    ]:
        if forbidden in site:
            fail(f"Mature-Hive site retaliation trigger must remain independent of feeding hunger; found {description}")

if errors:
    print("Mature Wraith Hive retaliation audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Mature Wraith Hive retaliation audit OK")
