#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "IncidentWorker_WraithDartCulling.cs"
DEFS = ROOT / "Defs" / "IncidentDefs" / "Incidents_WraithDart.xml"
HUNGER = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithFactionHunger.cs"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

source = SOURCE.read_text(encoding="utf-8", errors="replace") if SOURCE.exists() else ""
defs = DEFS.read_text(encoding="utf-8", errors="replace") if DEFS.exists() else ""
hunger = HUNGER.read_text(encoding="utf-8", errors="replace") if HUNGER.exists() else ""

if not SOURCE.exists(): errors.append("Missing IncidentWorker_WraithDartCulling.cs")
if not DEFS.exists(): errors.append("Missing Wraith Dart culling IncidentDef")
if not HUNGER.exists(): errors.append("Missing WraithFactionHunger.cs")

for needle, description in [
    ('GetNamedSilentFail("WNG_WraithDart")', "Culling incident must spawn the real WNG Wraith Dart"),
    ("map.listerThings.ThingsOfDef(dartDef).Any", "Culling incident must prevent duplicate active Darts"),
    ("map.mapPawns.AllPawnsSpawned.Any(WraithCaptureUtility.IsValidAbductionTarget)", "Culling incident must require valid biological prey"),
    ("WraithCaptureUtility.IsWraithCaptor(f)", "Culling incident must select only Wraith factions"),
    ("f.HostileTo(Faction.OfPlayer)", "Culling incident must select only hostile Wraith factions"),
    ("public override float ChanceFactorNow(IIncidentTarget target)", "Dart culling storyteller weight must react to strategic hunger"),
    ("hunger.HighestHostileStrategicHunger()", "Dart culling chance must use the saved hostile-faction hunger state"),
    ("0.35f + 1.65f * highestHostileHunger", "Dart culling chance must rise as hostile Wraith hunger increases"),
    ("SelectDispatchingFaction(candidates)", "Dart dispatch must choose among hostile lineages using strategic state"),
    ("hunger.GetStrategicHunger(faction)", "Hungrier hostile lineages must receive greater dispatch weight"),
    ("CellFinder.TryFindRandomEdgeCellWith", "Culling incident must find a bounded map-edge arrival cell"),
    ("CellFinder.EdgeRoadChance_Hostile", "Culling incident must use hostile edge-arrival rules"),
    ("GenSpawn.CanSpawnAt(dartDef, c, map)", "Culling incident must verify the Dart can spawn at the chosen cell"),
    ("ThingMaker.MakeThing(dartDef)", "Culling incident must instantiate the actual Dart ThingDef"),
    ("dart.SetFaction(faction)", "Spawned Dart must belong to the selected hostile Wraith faction"),
    ("GenSpawn.Spawn(dart, entryCell, map)", "Culling incident must actually place the Dart on the map"),
]: require(source, needle, description)

for needle, description in [
    ("public float GetStrategicHunger(Faction faction)", "Faction hunger must expose its existing saved value without duplicate state"),
    ("public float HighestHostileStrategicHunger()", "Faction hunger must expose highest hostile pressure for storyteller weighting"),
]: require(hunger, needle, description)

for needle, description in [
    ("<defName>WNG_WraithDartCulling</defName>", "Wraith Dart culling IncidentDef is missing"),
    ("WraithNaniteGravtech.IncidentWorker_WraithDartCulling", "IncidentDef must use the fresh Wraith Dart worker"),
    ("<li>Map_PlayerHome</li>", "Dart culling incident must target player-home maps"),
    ("<minRefireDays>8</minRefireDays>", "Dart culling incident must retain its refire cooldown"),
]: require(defs, needle, description)

for forbidden in ['GetNamedSilentFail("WNG_Stargate', 'GetNamed("WNG_Stargate', "WNG_Stargate", "StargateUtility", "StargateManager"]:
    if forbidden in source or forbidden in defs:
        errors.append("Wraith Dart culling incident must remain independent of Stargate availability")
        break

for forbidden in ["PawnGenerator.GeneratePawn", "GeneratePrisoner"]:
    if forbidden in source:
        errors.append("Wraith Dart culling incident must not fabricate pawns/captives")

if errors:
    print("Wraith Dart incident audit FAILED")
    for error in errors: print("- " + error)
    sys.exit(1)
print("Wraith Dart incident audit OK")
