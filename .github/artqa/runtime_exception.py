from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(".")
SRC = ROOT / "Source" / "WraithNaniteGravtech"
failures = []
notes = []

files = [p for p in SRC.rglob("*.cs") if "Diagnostics" not in p.parts]
texts = {p: p.read_text(encoding="utf-8", errors="ignore") for p in files}

# A missing Def should degrade through a checked null path, not throw from DefDatabase.GetNamed.
throwing_lookup_re = re.compile(
    r"DefDatabase\s*<\s*[^>]+\s*>\s*\.\s*GetNamed\s*\(([^)]*)\)",
    re.S,
)
throwing_lookups = []
for path, text in texts.items():
    for m in throwing_lookup_re.finditer(text):
        args = re.sub(r"\s+", " ", m.group(1)).strip()
        # GetNamed(name, false) is explicitly non-throwing; one-argument GetNamed is not.
        if re.search(r",\s*false\s*$", args, re.I):
            continue
        throwing_lookups.append((path, args))
        failures.append(f"{path}: throwing DefDatabase.GetNamed remains in production code: {args}")

# Enumerable.First/Single are common hidden runtime throw sites. The only current First() uses
# are reviewed: a list with a Count==0 guard, and GroupBy groups which are non-empty by definition.
first_sites = []
single_sites = []
for path, text in texts.items():
    for m in re.finditer(r"\.First\s*\(\s*\)", text):
        first_sites.append((path, m.start()))
        before = text[max(0, m.start()-900):m.start()]
        safe = (
            re.search(r"\.Count\s*==\s*0", before) is not None
            or ".GroupBy(" in before
            or re.search(r"\.Any\s*\(", before) is not None
        )
        if not safe:
            failures.append(f"{path}: Enumerable.First() lacks a reviewed non-empty guard near offset {m.start()}")
    for m in re.finditer(r"\.Single\s*\(\s*\)", text):
        single_sites.append((path, m.start()))
        failures.append(f"{path}: Enumerable.Single() is forbidden in production runtime paths; use a non-throwing selection")

# Empty/general exception swallowing hides the very failures Audit 32 is meant to expose.
empty_catch_re = re.compile(r"catch\s*(?:\([^)]*\))?\s*\{\s*\}", re.S)
empty_catches = []
for path, text in texts.items():
    for _ in empty_catch_re.finditer(text):
        empty_catches.append(path)
        failures.append(f"{path}: empty catch block hides runtime failure")

# Inventory explicit throws/catches. Intentional throws are allowed when callers use transactional
# try/catch/finally; the count is recorded so future growth is visible in Audit 33 log-diff work.
throw_count = sum(len(re.findall(r"\bthrow\s+new\s+", text)) for text in texts.values())
catch_count = sum(len(re.findall(r"\bcatch\s*(?:\(|\{)", text)) for text in texts.values())
log_error_count = sum(len(re.findall(r"\bLog\.Error(?:Once)?\s*\(", text)) for text in texts.values())

# Config-level XML parsing must remain clean here as a first-line runtime boot prerequisite.
xml_files = []
for base in (ROOT / "Defs", ROOT / "Patches", ROOT / "Compatibility"):
    if not base.exists():
        continue
    for path in base.rglob("*.xml"):
        xml_files.append(path)
        try:
            ET.parse(path)
        except ET.ParseError as exc:
            failures.append(f"{path}: XML parse/config error: {exc}")

# Debug-only Audit 32 monitor must exist and must not be mixed into production scans.
diag = ROOT / "Source" / "WraithNaniteGravtech" / "Diagnostics" / "Audit32RuntimeExceptionDiagnostics.cs"
diag_text = diag.read_text(encoding="utf-8", errors="ignore") if diag.exists() else ""
for token in (
    'Audit 32 - runtime exception monitor',
    'Application.logMessageReceived += HandleLogMessage',
    'Application.logMessageReceived -= HandleLogMessage',
    'MonitorTicks = 6000',
    'MapComponentTick()',
    'WNG-origin suspicious log entries',
    'external/stack suspicious log entries',
):
    if token not in diag_text:
        failures.append(f"Audit 32 live monitor contract missing: {token}")

print("=== D163 RUNTIME EXCEPTION AUDIT ===")
print(f" - production C# files scanned: {len(files)}")
print(f" - XML/config files parsed: {len(xml_files)}")
print(f" - throwing DefDatabase.GetNamed calls: {len(throwing_lookups)}")
print(f" - reviewed Enumerable.First() sites: {len(first_sites)}")
print(f" - Enumerable.Single() sites: {len(single_sites)}")
print(f" - empty catch blocks: {len(empty_catches)}")
print(f" - explicit throw-new sites inventoried: {throw_count}")
print(f" - catch blocks inventoried: {catch_count}")
print(f" - production Log.Error/ErrorOnce calls inventoried: {log_error_count}")
print(" - cross-reference, null/lifecycle and Harmony audits run as separate D163 workflow gates")
print(" - real boot/map/tick exceptions remain a live RimWorld soak requirement")

if failures:
    print("\nFAILURES:")
    for f in failures:
        print(" -", f)
    raise SystemExit(1)

print("PASS: static runtime-exception risk checks are clean; in-game 6000-tick soak monitor remains the runtime authority.")
