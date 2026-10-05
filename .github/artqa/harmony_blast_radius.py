from pathlib import Path
import re

ROOT = Path(".")
failures = []

source_files = [
    p for p in (ROOT / "Source" / "WraithNaniteGravtech").rglob("*.cs")
    if "Diagnostics" not in p.parts
]
texts = {p: p.read_text(encoding="utf-8", errors="ignore") for p in source_files}

allowed_path = ROOT / "Source" / "WraithNaniteGravtech" / "Gravships" / "GravshipFamilySystems.cs"
allowed = texts.get(allowed_path, "")

required_contracts = (
    'using HarmonyLib;',
    'new Harmony("vardath.wraithnanitegravtech.gravship-native-bridge")',
    '.PatchAll(typeof(WNGGravshipVanillaBridgeBootstrap).Assembly);',
    '[HarmonyPatch(typeof(RitualObligationTargetWorker_GravshipLaunch),',
    'nameof(RitualObligationTargetWorker_GravshipLaunch.GetTargets))]',
    'internal static class WNGRitualObligationTargetWorkerGravshipLaunchPatch',
    '[HarmonyPatch(typeof(Gravship), "AddThing")]',
    'internal static class WNGGravshipAddThingPilotConsolePatch',
    'thing.TryGetComp<CompPilotConsole_WNGFamily>() != null',
    '___pilotConsole = building;',
)
for token in required_contracts:
    if token not in allowed:
        failures.append(f"reviewed gravship Harmony contract missing: {token}")

# Exactly two production patch targets are reviewed. No prefixes/transpilers/finalizers are allowed.
if allowed.count("[HarmonyPatch") != 2:
    failures.append(f"reviewed gravship bridge must contain exactly 2 HarmonyPatch attributes, found {allowed.count('[HarmonyPatch')}")
if allowed.count("Postfix(") != 2:
    failures.append(f"reviewed gravship bridge must contain exactly 2 postfixes, found {allowed.count('Postfix(')}")
for forbidden in ("HarmonyPrefix", "HarmonyTranspiler", "HarmonyFinalizer", "ReversePatcher", "PatchProcessor"):
    if forbidden in allowed:
        failures.append(f"reviewed gravship bridge contains forbidden Harmony mechanism: {forbidden}")

# No other production file may use Harmony or alternate runtime interception.
markers = (
    r"\busing\s+HarmonyLib\s*;",
    r"\[\s*HarmonyPatch\b",
    r"\bnew\s+Harmony\s*\(",
    r"\.PatchAll\s*\(",
    r"\.Patch\s*\(",
    r"\.Unpatch(?:All)?\s*\(",
    r"\bHarmonyMethod\b",
    r"\bHarmonyLib\.",
)
for path, text in texts.items():
    if path == allowed_path:
        continue
    for pattern in markers:
        if re.search(pattern, text):
            failures.append(f"{path}: unreviewed production Harmony usage detected ({pattern})")

dangerous_runtime_patch_patterns = {
    "RuntimeHelpers.PrepareMethod detour": r"RuntimeHelpers\.PrepareMethod\s*\(",
    "function pointer replacement": r"GetFunctionPointer\s*\(",
    "unsafe method detour": r"Marshal\.WriteIntPtr\s*\(",
    "MonoMod runtime detour": r"MonoMod\.RuntimeDetour|HookEndpointManager|new\s+Hook\s*\(",
}
for path, text in texts.items():
    for label, pattern in dangerous_runtime_patch_patterns.items():
        if re.search(pattern, text):
            failures.append(f"{path}: alternate runtime interception detected ({label})")

print("=== D136 HARMONY BLAST-RADIUS AUDIT ===")
print(f" - Production C# files scanned: {len(source_files)}")
print(" - Reviewed production Harmony targets: 2")
print("   * RitualObligationTargetWorker_GravshipLaunch.GetTargets (postfix only)")
print("   * Gravship.AddThing (postfix only)")
print(" - Purpose: allow WNG custom PilotConsole ThingDefs through Odyssey hard-coded vanilla-def checks")
if failures:
    print("\nFAILURES:")
    for failure in failures:
        print(" -", failure)
    raise SystemExit(1)

print("PASS: Harmony use is limited to the two reviewed Odyssey gravship hard-code bridges.")
