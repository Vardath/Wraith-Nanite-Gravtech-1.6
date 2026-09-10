#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
BRIDGE = ROOT / "Source/WNGR2/Replicators/ReplicatorQueenSovereignThreatBridge.cs"
OUTBREAK = ROOT / "Source/WNGR2/Replicators/IncidentWorker_ReplicatorOutbreak.cs"
STATE = ROOT / "Source/WNGR2/Replicators/ReplicatorQueenSystems.cs"
errors = []


def require(text, needle, description):
    if needle not in text:
        errors.append(f"Missing captured-Queen threat contract: {description}")


bridge = BRIDGE.read_text(encoding="utf-8") if BRIDGE.exists() else ""
outbreak = OUTBREAK.read_text(encoding="utf-8") if OUTBREAK.exists() else ""
state = STATE.read_text(encoding="utf-8") if STATE.exists() else ""
if not BRIDGE.exists():
    errors.append("Missing ReplicatorQueenSovereignThreatBridge.cs")

for needle, description in [
    ("map.IsPlayerHome", "ordinary assault augmentation restricted to player home maps"),
    ("queenState.FactionHasCapturedQueenAuthority(lattice)", "exact captured-Queen authority gate"),
    ("pawn.GetLord()", "real raid Lord detection"),
    ("entry.Lord.LordJob is LordJob_AssaultColony", "ordinary Lattice assault detection"),
    ("MaximumEscortsPerAssault = 3", "bounded escort ceiling"),
    ("HumanFormsPerEscort = 4", "escort strength tied to existing assault size"),
    ("processedLordIds.Contains(lordId)", "at-most-once augmentation per assault Lord"),
    ("lord.GetUniqueLoadID()", "stable assault identity"),
    ("Scribe_Collections.Look(ref processedLordIds", "save persistence for processed assaults"),
    ('defName = "WNG_ReplicatorDrone"', "drone sovereign escort"),
    ('defName = "WNG_ReplicatorHunter"', "hunter sovereign escort"),
    ('defName = "WNG_ReplicatorBulwark"', "bulwark sovereign escort"),
    ("PawnGenerator.GeneratePawn(kind, lattice, map.Tile)", "blocks generated directly under exact Lattice faction"),
    ("lord.AddPawn(escort)", "block escorts join the same native Lattice assault Lord"),
]:
    require(bridge, needle, description)

for needle, description in [
    ("HostileCollectiveHasSovereignControl", "save-persistent captured-Queen state"),
    ("FactionHasCapturedQueenAuthority", "exact captor authority query"),
]:
    require(state, needle, description)

for forbidden, description in [
    ("HostileOutbreakBonus", "obsolete +1 outbreak Queen consequence"),
    ("GameComponent_ReplicatorQueenState", "Queen capture coupled back into autonomous feral outbreak"),
]:
    if forbidden in outbreak:
        errors.append(f"Captured-Queen threat contract violated: {description}")

for forbidden, description in [
    ("IncidentDefOf.RaidEnemy.Worker.TryExecute", "new recurring raid timer instead of augmenting ordinary Lattice assault"),
    ("GameComponentTick", "global recurring post-capture raid scheduler"),
]:
    if forbidden in bridge:
        errors.append(f"Captured-Queen threat bridge violated: {description}")

if errors:
    print("Replicator captured-Queen sovereign threat audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Replicator captured-Queen sovereign threat audit OK")
