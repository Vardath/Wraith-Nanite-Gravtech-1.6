#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
errors = []


def fail(msg):
    errors.append(msg)


def load(path):
    try:
        return ET.parse(ROOT / path).getroot()
    except Exception as ex:
        fail(f"{path}: {ex}")
        return None


genes = load("Defs/GeneDefs/Genes_NaniteHumanForm.xml")
xenos = load("Defs/XenotypeDefs/Xenotypes_NaniteHumanForm.xml")
kinds = load("Defs/PawnKindDefs/PawnKinds_NaniteHumanForm.xml")
abilities = load("Defs/AbilityDefs/Abilities_ReplicatorSovereign.xml")
hediffs = load("Defs/HediffDefs/Hediffs_NaniteHumanForm.xml")

if genes is not None:
    gene_names = {(x.findtext("defName") or "").strip() for x in genes.findall("GeneDef")}
    for required in ["WNG_NaniteBody", "WNG_NaniteReserve", "WNG_NaniteReconstruction", "WNG_EMPSensitiveNanites", "WNG_NeuralInterface", "WNG_AsuranCollectiveLink", "WNG_ReplicatorQueenLink"]:
        if required not in gene_names:
            fail(f"missing gene {required}")

if xenos is not None:
    by_name = {(x.findtext("defName") or "").strip(): x for x in xenos.findall("XenotypeDef")}
    for required in ["WNG_NanitePrecursor", "WNG_HumanFormReplicator"]:
        if required not in by_name:
            fail(f"missing xenotype {required}")
    human = by_name.get("WNG_HumanFormReplicator")
    if human is not None:
        human_genes = [(x.text or "").strip() for x in human.findall("genes/li")]
        if "WNG_ReplicatorQueenLink" in human_genes:
            fail("ordinary human-form Replicator xenotype must never contain the Queen sovereign marker")
        for required in ["WNG_NaniteBody", "WNG_NaniteReserve", "WNG_NaniteReconstruction", "WNG_EMPSensitiveNanites", "WNG_NeuralInterface", "WNG_AsuranCollectiveLink"]:
            if required not in human_genes:
                fail(f"human-form xenotype missing {required}")

if kinds is not None:
    concrete = {(x.findtext("defName") or "").strip(): x for x in kinds.findall("PawnKindDef") if x.find("defName") is not None}
    for required in ["WNG_PrecursorEngineer", "WNG_PrecursorSoldier", "WNG_PrecursorCommander", "WNG_HumanFormReplicator", "WNG_PlayerHumanFormReplicator", "WNG_ReplicatorQueenChild"]:
        if required not in concrete:
            fail(f"missing PawnKind {required}")
    queen = concrete.get("WNG_ReplicatorQueenChild")
    if queen is not None:
        if (queen.findtext("fixedGender") or "").strip() != "Female":
            fail("Replicator Queen must be female")
        if (queen.findtext("minGenerationAge") or "").strip() != "13" or (queen.findtext("maxGenerationAge") or "").strip() != "13":
            fail("Replicator Queen must generate at exactly age 13")

source = (ROOT / "Source/WNGR2/Replicators/ReplicatorQueenSystems.cs").read_text(encoding="utf-8")
control = (ROOT / "Source/WNGR2/Replicators/CompReplicatorControl.cs").read_text(encoding="utf-8")
nanites = (ROOT / "Source/WNGR2/Replicators/NaniteHumanFormGenes.cs").read_text(encoding="utf-8")
for needle, description in [
    ('pawn.kindDef?.defName != "WNG_ReplicatorQueenChild"', "Queen authority restricted to exact Queen PawnKind"),
    ('!ReplicatorQueenUtility.IsBlockReplicator(targetPawn)', "sovereign directive restricted to block Replicators"),
    ('Scribe_References.Look(ref queenPawn', "exact Queen pawn persistence"),
    ('Scribe_References.Look(ref queenCaptorFaction', "captor faction persistence"),
    ('HostileCollectiveHasSovereignControl', "strategic Lattice sovereign state"),
]:
    if needle not in source:
        fail(f"Queen system missing {description}")
for forbidden in ['HostileOutbreakBonus => queenAbducted ? 1 : 0', 'Future Replicator outbreaks will begin slightly stronger']:
    if forbidden in source:
        fail("old +1 outbreak-only Queen consequence is still present")
for needle, description in [
    ('private Pawn sovereignController;', "target-specific sovereign controller reference"),
    ('BindToSovereign(Pawn controller)', "implant target binding"),
    ('HasActiveSovereignBinding()', "binding validity check"),
    ('Scribe_References.Look(ref sovereignController', "save persistence for sovereign binding"),
]:
    if needle not in control:
        fail(f"Replicator control missing {description}")
for needle, description in [
    ('public const float FastHealCost = 0.015f', "1.5% fast-heal cost"),
    ('public const float ReconstructPartCost = 0.25f', "25% reconstruction cost"),
    ('private const int FastInjuryTicks = 450', "450-tick reserve healing"),
    ('private const int EmergencyInjuryTicks = 1800', "1800-tick emergency healing"),
    ('private const int FastPartTicks = 30000', "30000-tick reserve part reconstruction"),
    ('private const int EmergencyPartTicks = 90000', "90000-tick emergency part reconstruction"),
    ('FoodPoisoning', "food poisoning cleanup"),
]:
    if needle not in nanites:
        fail(f"nanite physiology missing {description}")

# Faction references which previously compiled despite missing PawnKinds must now resolve.
all_kind_names = set()
for path in (ROOT / "Defs/PawnKindDefs").glob("*.xml"):
    try:
        r = ET.parse(path).getroot()
        all_kind_names.update((x.findtext("defName") or "").strip() for x in r.findall("PawnKindDef") if x.find("defName") is not None)
    except ET.ParseError:
        pass
for faction_path in ["Defs/FactionDefs/Factions_WraithPrecursor.xml", "Defs/FactionDefs/Factions_Replicator.xml"]:
    r = load(faction_path)
    if r is None:
        continue
    for faction in r.findall("FactionDef"):
        basic = (faction.findtext("basicMemberKind") or "").strip()
        if basic.startswith("WNG_") and basic not in all_kind_names:
            fail(f"{faction_path} unresolved basicMemberKind {basic}")
        for options in faction.findall("pawnGroupMakers/li/options"):
            for entry in list(options):
                if entry.tag.startswith("WNG_") and entry.tag not in all_kind_names:
                    fail(f"{faction_path} unresolved pawn-group PawnKind {entry.tag}")

if errors:
    print("Nanite/human-form identity audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)
print("Nanite/human-form identity audit OK")