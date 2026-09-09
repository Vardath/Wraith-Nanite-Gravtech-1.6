#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
DART = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithDartAbduction.cs"
CAPTURE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptureUtility.cs"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

if not DART.exists():
    errors.append("Missing WraithDartAbduction.cs")
    dart = ""
else:
    dart = DART.read_text(encoding="utf-8", errors="replace")

if not CAPTURE.exists():
    errors.append("Missing WraithCaptureUtility.cs")
    capture = ""
else:
    capture = CAPTURE.read_text(encoding="utf-8", errors="replace")

for needle, description in [
    ("maxCaptives = 3", "Dart capture quota must default to three pawns"),
    ("scanIntervalTicks = 500", "Dart prey scan cadence must default to 500 ticks"),
    ("acquisitionRadius = 28f", "Dart acquisition radius must default to 28 cells"),
    ("stunTicks = 1200", "Dart absorption stun must default to 1200 ticks"),
    ("retreatDelayTicks = 600", "Dart retreat delay must default to 600 ticks"),
    ("WraithCaptureUtility.IsWraithCaptor(faction)", "Dart mission must require a Wraith captor faction"),
    ("faction.HostileTo(Faction.OfPlayer)", "Dart mission must only run for hostile Wraith craft"),
    ("WraithCaptureUtility.IsValidAbductionTarget(p)", "Dart target selection must use the shared biological prey boundary"),
    ("OrderByDescending(p => p.Downed)", "Dart must prioritize already-downed prey"),
    ("target.stances.stunner.StunFor", "Dart must stun standing prey during absorption"),
    ("WraithCaptureUtility.TryCompleteAbduction(target, parent.Faction)", "Dart must hand off the exact selected pawn to captivity"),
    ("capturedCount >= Math.Max(1, Props.maxCaptives)", "Dart must stop taking pawns at its bounded quota"),
    ("parent.Destroy(DestroyMode.Vanish)", "Dart mission must withdraw after its retreat delay"),
    ("Scribe_Values.Look(ref capturedCount", "Dart captured count must persist across save/load"),
    ("Scribe_Values.Look(ref retreatTick", "Dart retreat state must persist across save/load"),
]:
    require(dart, needle, description)

for needle, description in [
    ("TryCompleteAbduction(Pawn pawn, Faction captor)", "Physical abduction completion boundary is missing"),
    ("pawn.DeSpawn()", "Completed abduction must despawn the exact pawn from the map"),
    ("Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever)", "Completed abduction must retain the same pawn in WorldPawns"),
]:
    require(capture, needle, description)

for forbidden in ["PawnGenerator.GeneratePawn", "GeneratePrisoner"]:
    if forbidden in dart:
        errors.append("Wraith Dart runtime must never fabricate a substitute captive")

if errors:
    print("Wraith Dart audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Dart audit OK")
