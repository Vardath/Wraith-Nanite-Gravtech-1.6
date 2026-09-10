#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
QUEST = ROOT / "Source/WNGR2/Replicators/ReplicatorQueenQuest.cs"
CAPTURE = ROOT / "Source/WNGR2/Replicators/ReplicatorQueenCaptureRaid.cs"
STATE = ROOT / "Source/WNGR2/Replicators/ReplicatorQueenSystems.cs"
SITE = ROOT / "Defs/SitePartDefs/ReplicatorQueenVault.xml"
INCIDENT = ROOT / "Defs/IncidentDefs/Incidents_ReplicatorQueen.xml"
CONTRACT = ROOT / "Docs/WNG_REBUILD_REPLICATOR_QUEEN_CONTRACT.md"
errors = []


def require(text, needle, description):
    if needle not in text:
        errors.append(f"Missing Queen-vault contract: {description}")


for path in [QUEST, CAPTURE, STATE, SITE, INCIDENT, CONTRACT]:
    if not path.exists():
        errors.append(f"Missing Queen-vault file: {path.relative_to(ROOT)}")

quest = QUEST.read_text(encoding="utf-8") if QUEST.exists() else ""
capture = CAPTURE.read_text(encoding="utf-8") if CAPTURE.exists() else ""
state = STATE.read_text(encoding="utf-8") if STATE.exists() else ""
contract = CONTRACT.read_text(encoding="utf-8") if CONTRACT.exists() else ""

for needle, description in [
    ("QueenVaultDay = 84", "day-84 discovery"),
    ("ThingDefOf.AncientCryptosleepCasket", "real vanilla ancient cryosleep chamber"),
    ("casket.TryAcceptThing(queen, allowSpecialEffects: false)", "exact Queen stored in real casket"),
    ("AgeBiologicalTicks = exactAge", "exact biological age"),
    ("AgeChronologicalTicks = exactAge", "exact chronological age"),
    ("state.MarkJoined(queen)", "release recruits exact Queen"),
    ("InitialRecoveryWarningTicks = 600", "short release warning"),
    ("new List<Pawn>(4)", "exact four-operative recovery transaction"),
    ("WNG_HumanFormReplicatorSoldier", "human-form recovery soldiers"),
    ("WNG_HumanFormReplicatorInfiltrator", "human-form recovery infiltrator"),
    ("WNG_HumanFormReplicatorCoordinator", "human-form recovery coordinator"),
    ("HomeCaptureRaidMinimumDelayTicks", "long occasional home-raid cooldown floor"),
    ("HomeCaptureRaidMaximumDelayTicks", "long occasional home-raid cooldown ceiling"),
    ("map.IsPlayerHome", "later raid restricted to home map"),
    ("queen.MapHeld", "exact Queen physical map targeting"),
    ("KidnappedPawnsListForReading.Contains(state.QueenPawn)", "capture commits only after vanilla kidnapping"),
]:
    require(quest, needle, description)

for needle, description in [
    ("MapComponent_ReplicatorQueenCaptureOperation", "save-persistent capture objective"),
    ("JobDefOf.AttackMelee", "subdual focus"),
    ("JobDefOf.Kidnap", "vanilla physical kidnapping"),
    ("kidnap.targetA = queen", "exact Queen kidnapping target"),
    ("kidnap.targetB = exitCell", "real map-exit target"),
    ("PawnsArrivalModeDefOf.EdgeWalkIn", "real edge-arrival later raid"),
    ("IncidentDefOf.RaidEnemy.Worker.TryExecute(parms)", "native later raid"),
    ("state.QueenJoinedPlayer", "raid requires player Queen state"),
    ("queen.Map == map", "raid target is exact Queen map"),
]:
    require(capture, needle, description)

for needle, description in [
    ("queen.SetFaction(Faction.OfPlayer, null)", "immediate player recruitment/alignment"),
    ("HostileCollectiveHasSovereignControl", "post-capture sovereign state"),
]:
    require(state, needle, description)

for needle, description in [
    ("recruited to the player immediately", "documented immediate recruitment"),
    ("occasional later raids specifically to capture her", "documented later recapture raids"),
    ("player home map on which the exact Queen is physically present", "documented exact-map restriction"),
    ("Abduction commits only when the carrier actually exits the map", "documented real capture boundary"),
]:
    require(contract, needle, description)

if SITE.exists():
    root = ET.parse(SITE).getroot()
    node = root.find("SitePartDef")
    if node is None:
        errors.append("Queen vault SitePartDef missing")
    else:
        expected = {
            "defName": "WNG_ReplicatorQueenVault",
            "workerClass": "WraithNaniteGravtech.SitePartWorker_ReplicatorQueenVault",
            "requiresFaction": "false",
            "wantsThreatPoints": "false",
        }
        for key, value in expected.items():
            actual = (node.findtext(key) or "").strip()
            if actual != value:
                errors.append(f"Queen vault {key} expected {value}, got {actual}")

if INCIDENT.exists():
    root = ET.parse(INCIDENT).getroot()
    node = root.find("IncidentDef")
    if node is None:
        errors.append("Queen capture raid IncidentDef missing")
    else:
        expected = {
            "defName": "WNG_ReplicatorQueenCaptureRaid",
            "workerClass": "WraithNaniteGravtech.IncidentWorker_ReplicatorQueenCaptureRaid",
            "baseChance": "0",
            "category": "ThreatBig",
        }
        for key, value in expected.items():
            actual = (node.findtext(key) or "").strip()
            if actual != value:
                errors.append(f"Queen capture raid {key} expected {value}, got {actual}")
        tags = [(x.text or "").strip() for x in node.findall("targetTags/li")]
        if "Map_PlayerHome" not in tags:
            errors.append("Queen capture raid must target Map_PlayerHome")

for forbidden, description in [
    ("HostileOutbreakBonus", "old +1 outbreak shortcut"),
    ("queen.Destroy(DestroyMode.Vanish)", "fake Queen-loss transaction"),
]:
    if forbidden in quest or forbidden in capture:
        errors.append(f"Queen-vault contract violated: {description}")

if errors:
    print("Replicator Queen vault/capture audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Replicator Queen vault/capture audit OK")
