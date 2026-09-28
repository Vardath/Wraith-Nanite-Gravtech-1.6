from pathlib import Path
import re

ROOT = Path(".")
failures = []
notes = []

# Audit only the actual WNG runtime assembly. CompanionMods are separately packaged mods and
# diagnostics are developer-only probes; neither is allowed to hide production Harmony usage.
source_files = [
    p for p in (ROOT / "Source" / "WraithNaniteGravtech").rglob("*.cs")
    if "Diagnostics" not in p.parts
]

texts = {p: p.read_text(encoding="utf-8", errors="ignore") for p in source_files}
joined = "\n".join(texts.values())

# WNG currently needs no Harmony interception. That is the safest possible blast radius.
# If a future feature introduces Harmony, this audit deliberately fails until each target gets
# an explicit reviewed allow-list entry plus a dedicated regression test.
harmony_markers = {
    "HarmonyLib import": r"\busing\s+HarmonyLib\s*;",
    "HarmonyPatch attribute": r"\[\s*HarmonyPatch\b",
    "HarmonyPrefix attribute": r"\[\s*HarmonyPrefix\b",
    "HarmonyPostfix attribute": r"\[\s*HarmonyPostfix\b",
    "HarmonyTranspiler attribute": r"\[\s*HarmonyTranspiler\b",
    "HarmonyFinalizer attribute": r"\[\s*HarmonyFinalizer\b",
    "Harmony instance": r"\bnew\s+Harmony\s*\(",
    "PatchAll call": r"\bPatchAll\s*\(",
    "Patch call": r"\.Patch\s*\(",
    "Unpatch call": r"\.Unpatch(?:All)?\s*\(",
    "HarmonyMethod": r"\bHarmonyMethod\b",
    "PatchProcessor": r"\bPatchProcessor\b",
    "ReversePatcher": r"\bReversePatcher\b",
    "AccessTools patch target lookup": r"\bAccessTools\.(?:Method|DeclaredMethod|Constructor|PropertyGetter|PropertySetter|TypeByName)\s*\(",
}

hits = []
for path, text in texts.items():
    for label, pattern in harmony_markers.items():
        if re.search(pattern, text):
            hits.append((path, label))

if hits:
    for path, label in hits:
        failures.append(
            f"{path}: production Harmony/reflection patch mechanism detected ({label}). "
            "Add a reviewed target-specific allow-list entry and regression contract before merging."
        )

# Also block fully-qualified Harmony usage that bypasses a using statement.
for path, text in texts.items():
    if re.search(r"\bHarmonyLib\.", text):
        failures.append(f"{path}: fully-qualified HarmonyLib runtime usage detected")

# Defensive check for hand-rolled runtime detours or method swapping that would bypass Harmony.
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

# High-risk global systems that must never be patched silently. This doubles as a future target
# registry: if Harmony is ever intentionally introduced, target extraction should keep these blocked.
high_risk_tokens = (
    "BillStack", "Bill_Production", "ITab_Bills", "Building_WorkTable", "RecipeDef",
    "DefDatabase", "DesignationCategoryDef", "ArchitectCategoryTab", "PawnGenerator",
    "Pawn_ApparelTracker", "Pawn_EquipmentTracker", "Map", "World", "FactionManager",
    "Thing.SpawnSetup", "GenSpawn", "Scribe", "Game.LoadGame",
)
for token in high_risk_tokens:
    # Informational only while no patch machinery exists.
    if token in joined:
        notes.append(f"Production source references high-risk domain type/token: {token} (not Harmony-patched)")

print("=== D136 HARMONY BLAST-RADIUS AUDIT ===")
print(f" - Production C# files scanned: {len(source_files)}")
print(f" - Harmony/runtime-detour targets found: {len(hits)}")
print(" - Current reviewed Harmony target allow-list: EMPTY (WNG uses no production Harmony patches)")
if failures:
    print("\nFAILURES:")
    for failure in failures:
        print(" -", failure)
    raise SystemExit(1)

print("PASS: WNG production assembly contains no Harmony or alternate runtime interception.")
