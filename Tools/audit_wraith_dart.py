#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
DART = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithDartAbduction.cs"
CAPTURE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptureUtility.cs"
DART_DEF = ROOT / "Defs" / "ThingDefs" / "Wraith_Dart.xml"

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

if not DART_DEF.exists():
    errors.append("Missing Wraith Dart ThingDef")
    dart_def = ""
else:
    dart_def = DART_DEF.read_text(encoding="utf-8", errors="replace")

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
    ("private CompTransporter Transporter", "Dart must require a real CompTransporter"),
    ("private CompHackable Hackable", "Dart must observe the native hackable state"),
    ("Hackable?.IsHacked == true", "A hacked Dart must stop all further abduction activity"),
    ("transporter.MassUsage + targetMass > transporter.MassCapacity", "Dart must respect transporter mass capacity"),
    ("WraithCaptureUtility.TryRegisterAbduction(target, parent.Faction)", "Dart must register the exact selected pawn before loading"),
    ("target.stances.stunner.StunFor", "Dart must stun prey during absorption"),
    ("target.DeSpawn()", "Dart must remove the exact pawn from the map before transporter loading"),
    ("transporter.innerContainer.TryAdd(target)", "Dart must place the exact captive into its transporter"),
    ("registry.ReleaseExactPawn(target)", "Failed transporter loading must roll back the captivity record"),
    ("GenSpawn.Spawn(target, originalPosition, originalMap)", "Failed transporter loading must restore the same pawn to the map"),
    ("private List<Pawn> bufferedCaptives", "Dart must track the exact pawns genuinely held in its capture buffer"),
    ("bufferedCaptives.Add(target)", "Successful absorption must add the exact captive to the tracked buffer"),
    ("Scribe_Collections.Look(ref bufferedCaptives", "Buffered captive identity must survive save/load"),
    ("public override void Notify_Hacked(Pawn hacker)", "Dart must react to native RimWorld hack completion"),
    ("retreatTick = -1", "Successful hacking must cancel pending Dart withdrawal"),
    ("RecoverBufferedCaptives()", "Successful hacking must open and recover the capture buffer"),
    ("registry?.ReleaseExactPawn(captive)", "Hack/interruption recovery must clear the exact pawn's Wraith captivity record"),
    ("Find.WorldPawns.RemovePawn(captive)", "Hack recovery must remove a world-pawn registration before map respawn when necessary"),
    ("GenSpawn.Spawn(captive, releaseCell, map)", "Hack recovery must respawn the exact captive near the Dart"),
    ("public override void PostDeSpawn(Map map, DestroyMode mode", "Interrupted or destroyed Dart must clean its buffered captivity state"),
    ("if (intentionalWithdrawal || bufferedCaptives == null || bufferedCaptives.Count == 0)", "Successful Wraith withdrawal must be distinguished from interrupted capture"),
    ("intentionalWithdrawal = true", "Successful withdrawal must mark the despawn as intentional before the Dart vanishes"),
    ("List<Pawn> captives = bufferedCaptives.Where(p => p != null).ToList()", "Withdrawal must enumerate the exact tracked captive identities"),
    ("transporter.innerContainer.Remove(captive)", "Withdrawal must remove exact captives before the craft vanishes"),
    ("Find.WorldPawns.PassToWorld(captive, PawnDiscardDecideMode.KeepForever)", "Withdrawal must preserve exact captives in WorldPawns"),
    ("parent.Destroy(DestroyMode.Vanish)", "Dart mission must withdraw only after captive handoff"),
    ("Scribe_Values.Look(ref capturedCount", "Dart captured count must persist across save/load"),
    ("Scribe_Values.Look(ref retreatTick", "Dart retreat state must persist across save/load"),
]:
    require(dart, needle, description)

for needle, description in [
    ("TryCompleteAbduction(Pawn pawn, Faction captor)", "Shared non-transporter physical abduction completion boundary is missing"),
    ("Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever)", "Shared completion boundary must retain the same pawn in WorldPawns"),
]:
    require(capture, needle, description)

for needle, description in [
    ("<defName>WNG_WraithDart</defName>", "Wraith Dart ThingDef is missing"),
    ("ParentName=\"ShuttleBase\"", "Wraith Dart must inherit a real RimWorld shuttle/transporter"),
    ("WraithNaniteGravtech.CompProperties_WraithDartAbduction", "Wraith Dart Def must attach its abduction mission comp"),
    ("Class=\"CompProperties_Hackable\"", "Wraith Dart capture buffer must use native RimWorld hacking"),
    ("<intellectualSkillPrerequisite>6</intellectualSkillPrerequisite>", "Dart hacking must require a skilled colonist"),
]:
    require(dart_def, needle, description)

for forbidden in ["PawnGenerator.GeneratePawn", "GeneratePrisoner"]:
    if forbidden in dart:
        errors.append("Wraith Dart runtime must never fabricate a substitute captive")

if errors:
    print("Wraith Dart audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith Dart audit OK")
