#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
BRIDGE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithRaidKidnapBridge.cs"
CAPTURE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptureUtility.cs"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

bridge = BRIDGE.read_text(encoding="utf-8", errors="replace") if BRIDGE.exists() else ""
capture = CAPTURE.read_text(encoding="utf-8", errors="replace") if CAPTURE.exists() else ""

if not BRIDGE.exists():
    errors.append("Missing WraithRaidKidnapBridge.cs")
if not CAPTURE.exists():
    errors.append("Missing WraithCaptureUtility.cs")

for needle, description in [
    ("GameComponent_WraithRaidKidnapBridge", "Ground-raid kidnapping bridge must be a persistent game component"),
    ("nextScanTick = now + 250", "Ground-raid bridge must use a bounded low-frequency scan"),
    ("WraithCaptureUtility.IsWraithCaptor(faction)", "Ground-raid bridge must only inspect Wraith captor factions"),
    ("faction.kidnapped.KidnappedPawnsListForReading.ToList()", "Ground-raid bridge must consume completed native kidnapping records"),
    ("WraithCaptureUtility.IsValidCaptiveIdentity(pawn)", "Ground-raid bridge must enforce biological/synthetic captive identity rules"),
    ("pawn.Faction == Faction.OfPlayer", "Ground-raid bridge must recognize kidnapped player pawns"),
    ("pawn.guest?.IsPrisoner == true", "Ground-raid bridge must recognize colony prisoners"),
    ("registry.FindRecord(pawn)", "Ground-raid bridge must preserve exact-pawn identity when checking existing captivity"),
    ("registry.RegisterCapturedPawn(pawn, faction)", "Ground-raid bridge must register the same native kidnapped pawn"),
    ("faction.kidnapped.RemoveKidnappedPawn(pawn)", "WNG must take ownership away from vanilla ransom/recruitment after adoption"),
]:
    require(bridge, needle, description)

for needle, description in [
    ("IsValidCaptiveIdentity(Pawn pawn)", "Capture utility must expose off-map captive identity validation"),
    ("if (!IsValidCaptiveIdentity(pawn) || !pawn.Spawned)", "Map targeting must remain stricter than off-map captive identity"),
    ("!pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh", "Captive identity must remain biological humanlike only"),
    ("WraithLifeForceUtility.Get(pawn) != null", "Wraith pawns must remain excluded from captive identity"),
    ("LooksSynthetic", "Replicator/Asuran/nanite identities must remain excluded"),
]:
    require(capture, needle, description)

for forbidden in ["PawnGenerator.GeneratePawn", "new Pawn(", "TryCompleteAbduction("]:
    if forbidden in bridge:
        errors.append("Ground-raid bridge must adopt native exact kidnaps, not fabricate or re-abduct pawns")

if errors:
    print("Wraith ground-kidnap audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith ground-kidnap audit OK")
