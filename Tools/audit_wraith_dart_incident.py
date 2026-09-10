#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "IncidentWorker_WraithDartCulling.cs"
DEFS = ROOT / "Defs" / "IncidentDefs" / "Incidents_WraithDart.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

source = SOURCE.read_text(encoding="utf-8", errors="replace") if SOURCE.exists() else ""
defs = DEFS.read_text(encoding="utf-8", errors="replace") if DEFS.exists() else ""

if not SOURCE.exists():
    errors.append("Missing IncidentWorker_WraithDartCulling.cs")
if not DEFS.exists():
    errors.append("Missing Wraith Dart culling IncidentDef")

for needle, description in [
    ('GetNamedSilentFail("WNG_WraithDart")', "Culling incident must spawn the real WNG Wraith Dart"),
    ("map.listerThings.ThingsOfDef(dartDef).Any", "Culling incident must prevent duplicate active Darts"),
    ("map.mapPawns.AllPawnsSpawned.Any(WraithCaptureUtility.IsValidAbductionTarget)", "Culling incident must require valid biological prey"),
    ("WraithCaptureUtility.IsWraithCaptor(f)", "Culling incident must select only Wraith factions"),
    ("f.HostileTo(Faction.OfPlayer)", "Culling incident must select only hostile Wraith factions"),
    ("CellFinder.TryFindRandomEdgeCellWith", "Culling incident must find a bounded map-edge arrival cell"),
    ("CellFinder.EdgeRoadChance_Hostile", "Culling incident must use hostile edge-arrival rules"),
    ("GenSpawn.CanSpawnAt(dartDef, c, map)", "Culling incident must verify the Dart can spawn at the chosen cell"),
    ("ThingMaker.MakeThing(dartDef)", "Culling incident must instantiate the actual Dart ThingDef"),
    ("dart.SetFaction(faction)", "Spawned Dart must belong to the selected hostile Wraith faction"),
    ("GenSpawn.Spawn(dart, entryCell, map)", "Culling incident must actually place the Dart on the map"),
]:
    require(source, needle, description)

for needle, description in [
    ("<defName>WNG_WraithDartCulling</defName>", "Wraith Dart culling IncidentDef is missing"),
    ("WraithNaniteGravtech.IncidentWorker_WraithDartCulling", "IncidentDef must use the fresh Wraith Dart worker"),
    ("<li>Map_PlayerHome</li>", "Dart culling incident must target player-home maps"),
    ("<minRefireDays>8</minRefireDays>", "Dart culling incident must retain its refire cooldown"),
]:
    require(defs, needle, description)

# This is explicitly an independent non-Stargate Wraith attack path.
if "Stargate" in source or "Stargate" in defs:
    errors.append("Wraith Dart culling incident must remain independent of Stargate availability")

# The incident spawns a craft only; captive identity is handled by the Dart runtime.
for forbidden in ["PawnGenerator.GeneratePawn", "GeneratePrisoner"]:
    if forbidden in source:
        errors.append("Wraith Dart culling incident must not fabricate pawns/captives")

if errors:
    print("Wraith Dart incident audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Dart incident audit OK")
