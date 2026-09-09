#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptivity.cs"
DEFS = ROOT / "Defs" / "HediffDefs" / "Hediffs_WraithCaptivity.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

if not SOURCE.exists():
    errors.append("Missing WraithCaptivity.cs")
    source = ""
else:
    source = SOURCE.read_text(encoding="utf-8", errors="replace")

if not DEFS.exists():
    errors.append("Missing Wraith captivity Hediff defs")
    defs = ""
else:
    defs = DEFS.read_text(encoding="utf-8", errors="replace")

for needle, description in [
    ("Scribe_References.Look(ref pawn", "Captivity record must persist the exact Pawn reference"),
    ("RegisterCapturedPawn(Pawn pawn, Faction captor)", "Captivity registry must register the captured pawn itself"),
    ("FindRecord(Pawn pawn)", "Captivity registry must resolve records by exact Pawn identity"),
    ("RecordRescueFailure(Pawn pawn)", "Failed rescue escalation must act on the same pawn"),
    ("FeedingStock = 0", "Captivity must declare feeding stock as the initial treatment stage"),
    ("Experimentation = 1", "Experimentation treatment stage is missing"),
    ("Conditioning = 2", "Conditioning treatment stage is missing"),
    ("Enthralled = 3", "Enthrallment treatment stage is missing"),
    ("RemoveStageHediffsExcept(pawn, \"WNG_WraithEnthralled\")", "Enthrallment must survive release"),
    ("Scribe_Collections.Look(ref records", "Captivity ledger must survive save/load"),
]:
    require(source, needle, description)

for def_name in [
    "WNG_WraithFeedingStock",
    "WNG_WraithExperimentation",
    "WNG_WraithConditioning",
    "WNG_WraithEnthralled",
]:
    require(defs, f"<defName>{def_name}</defName>", f"Missing captivity HediffDef {def_name}")

# This foundation deliberately must not fabricate a substitute captive.
for forbidden in ["PawnGenerator.GeneratePawn", "PawnGenerator.TryGenerateNewPawnInternal"]:
    if forbidden in source:
        errors.append("Captivity registry must never generate a replacement pawn")

if errors:
    print("Wraith captivity audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith captivity audit OK")
