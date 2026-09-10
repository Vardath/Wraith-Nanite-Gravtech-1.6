#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SITE_DEF = ROOT / "Defs" / "SitePartDefs" / "WraithMatureHive.xml"
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "SitePartWorker_WraithMatureHive.cs"
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing mature-Hive site contract: {description}")


if not SITE_DEF.exists():
    fail("Missing mature-Hive SitePartDef")
    site = None
else:
    try:
        root = ET.parse(SITE_DEF).getroot()
        site = next((x for x in root.findall("SitePartDef") if (x.findtext("defName") or "").strip() == "WNG_WraithMatureHive"), None)
        if site is None:
            fail("Missing SitePartDef WNG_WraithMatureHive")
    except ET.ParseError as ex:
        fail(f"Mature-Hive SitePartDef XML parse failure: {ex}")
        site = None

if site is not None:
    expected = {
        "workerClass": "WraithNaniteGravtech.SitePartWorker_WraithMatureHive",
        "requiresFaction": "true",
        "wantsThreatPoints": "false",
        "handlesWorldObjectTimeoutInspectString": "false",
    }
    for key, value in expected.items():
        found = (site.findtext(key) or "").strip()
        if found != value:
            fail(f"Mature-Hive SitePartDef {key} must remain {value!r}, found {found!r}")
    tags = [(x.text or "").strip() for x in site.findall("tags/li")]
    if tags != ["WNG_WraithMatureHive"]:
        fail(f"Mature-Hive site tag must remain exact, found {tags}")

source = SOURCE.read_text(encoding="utf-8", errors="replace") if SOURCE.exists() else ""
if not SOURCE.exists():
    fail("Missing mature-Hive map generator source")
else:
    for needle, description in [
        ("public sealed class SitePartWorker_WraithMatureHive : SitePartWorker", "fresh SitePartWorker class"),
        ("faction == Faction.OfPlayer", "player-faction exclusion"),
        ("WraithCaptureUtility.IsWraithCaptor(faction)", "Wraith-lineage restriction"),
        ("private const int FeedingNicheCount = 3", "three Feeding Niches"),
        ("private const int HibernationPodCount = 2", "two Hibernation Pods"),
        ("private const int DormancyVaultCount = 2", "two separate Dormancy Vaults"),
        ("private const int StoredBiomass = 420", "420-unit test-balance biomass stock"),
        ('GetNamedSilentFail("WNG_HiveHeart")', "Hive Heart definition"),
        ('GetNamedSilentFail("WNG_GrowthChamber")', "Growth Chamber definition"),
        ('GetNamedSilentFail("WNG_BioelectricOrgan")', "Bioelectric Organ definition"),
        ('GetNamedSilentFail("WNG_FeedingNiche")', "Feeding Niche definition"),
        ('GetNamedSilentFail("WNG_HibernationPod")', "Hibernation Pod definition"),
        ('GetNamedSilentFail("WNG_DormancyVault")', "Dormancy Vault definition"),
        ('GetNamedSilentFail("WNG_Biomass")', "cultured biomass definition"),
        ('GetNamedSilentFail("WNG_WraithQueen")', "Queen founder definition"),
        ('GetNamedSilentFail("WNG_WraithKeeper")', "Keeper founder definition"),
        ('GetNamedSilentFail("WNG_WraithHunter")', "Hunter founder definition"),
        ('GetNamedSilentFail("WNG_WraithWarrior")', "Warrior founder definition"),
        ("TrySpawnFounder(defs.queen", "one Queen founder"),
        ("TrySpawnFounder(defs.keeper", "one Keeper founder"),
        ("TrySpawnFounder(defs.hunter", "Hunter founders"),
        ("TrySpawnFounder(defs.warrior", "Warrior founders"),
        ("List<Pawn> founders = new List<Pawn>(6)", "six-member founder cap source"),
        ("population.InitializeGeneratedHive(founders, chamber)", "explicit exact-founder/exact-chamber initialization"),
        ("DestroyGeneratedPawns(founders)", "invalid-founder cleanup"),
        ("DestroyGeneratedInfrastructure(infrastructure)", "partial-infrastructure cleanup"),
        ("LordMaker.MakeNewLord", "single generated-Hive defensive Lord"),
        ("new LordJob_DefendBase", "founder defensive behavior"),
        ("GenSpawn.CanSpawnAt(def, c, map)", "bounded building placement validation"),
        ("thing.SetFaction(faction)", "exact site-faction infrastructure ownership"),
        ("SpawnBiomass(defs.biomass, StoredBiomass, heart.Position, map)", "real stored biomass generation"),
        ("Hibernation Pods are generated physically, but are not preloaded", "explicit deferred pod-occupancy boundary"),
    ]:
        require(source, needle, description)

    if source.count("TrySpawnFounder(defs.queen") != 1:
        fail("Mature Hive must create exactly one Queen founder")
    if source.count("TrySpawnFounder(defs.keeper") != 1:
        fail("Mature Hive must create exactly one Keeper founder")
    if source.count("TrySpawnFounder(defs.hunter") != 2:
        fail("Mature Hive must create exactly two Hunter founders")
    if source.count("TrySpawnFounder(defs.warrior") != 2:
        fail("Mature Hive must create exactly two Warrior founders")

    for forbidden, description in [
        ("AllPawnsSpawned", "map-wide demographic pawn scan"),
        ("FreeColonistsSpawned", "unrelated pawn adoption scan"),
        ("CompWraithDormancyVault", "Dormancy Vault reserve adoption"),
        ("Gravcore", "obsolete Gravcore shortcut/dependency"),
        ("CurOccupants", "unverified forced Hibernation Pod occupancy"),
        ("TryStartPopulationReplacement", "generator-time replacement cloning instead of exact founding"),
    ]:
        if forbidden in source:
            fail(f"Mature-Hive generator violates bounded generation contract: found {description}")

if errors:
    print("Mature Wraith Hive site audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Mature Wraith Hive site audit OK")
