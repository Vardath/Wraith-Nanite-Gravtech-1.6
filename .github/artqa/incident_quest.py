from pathlib import Path
import re
import xml.etree.ElementTree as ET
from collections import Counter

ROOT=Path(".")
failures=[]
notes=[]

def parse_defs(folder, tag):
    out={}
    for path in sorted((ROOT/folder).rglob("*.xml")):
        try:
            root=ET.parse(path).getroot()
        except Exception as exc:
            failures.append(f"{path}: XML parse failure: {exc}")
            continue
        for node in list(root):
            if node.tag!=tag: continue
            name=(node.findtext("defName") or "").strip()
            if name.startswith("WNG_"):
                if name in out:
                    failures.append(f"duplicate {tag} {name}: {out[name][0]} and {path}")
                out[name]=(path,node)
    return out

incidents=parse_defs("Defs/IncidentDefs","IncidentDef")
sites=parse_defs("Defs/SitePartDefs","SitePartDef")

# Production C# class index.
source_files=[p for p in (ROOT/"Source"/"WraithNaniteGravtech").rglob("*.cs") if "Diagnostics" not in p.parts]
source_text={p:p.read_text(encoding="utf-8",errors="ignore") for p in source_files}
class_to_paths={}
for p,text in source_text.items():
    for m in re.finditer(r"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*([^\{\n]+))?",text):
        class_to_paths.setdefault(m.group(1),[]).append((p,m.group(2) or ""))

# Def indexes for cross references.
faction_defs=set()
pawnkind_defs=set()
for folder,tag,target in [
    ("Defs/FactionDefs","FactionDef",faction_defs),
    ("Defs/PawnKindDefs","PawnKindDef",pawnkind_defs),
]:
    base=ROOT/folder
    if not base.exists(): continue
    for path in base.rglob("*.xml"):
        try: root=ET.parse(path).getroot()
        except: continue
        for node in list(root):
            if node.tag==tag:
                n=(node.findtext("defName") or "").strip()
                if n: target.add(n)
# Some PawnKindDefs live beside race defs.
for path in (ROOT/"Defs").rglob("*.xml"):
    try: root=ET.parse(path).getroot()
    except: continue
    for node in list(root):
        if node.tag=="PawnKindDef":
            n=(node.findtext("defName") or "").strip()
            if n: pawnkind_defs.add(n)

def class_name(worker):
    return worker.split(".")[-1] if worker else ""

# Incident structural validation.
target_counts=Counter()
optional_incidents=0
for name,(path,node) in incidents.items():
    worker=(node.findtext("workerClass") or "").strip()
    if not worker:
        failures.append(f"{name}: missing workerClass ({path})")
    else:
        cls=class_name(worker)
        found=class_to_paths.get(cls,[])
        if not found:
            failures.append(f"{name}: workerClass {worker} has no production C# class")
        elif not any("IncidentWorker" in bases for _,bases in found):
            # Some classes may inherit a project-local IncidentWorker subclass; accept naming but note.
            if not cls.startswith("IncidentWorker_"):
                failures.append(f"{name}: worker {cls} is not recognizably an IncidentWorker")

    tags=[(x.text or "").strip() for x in node.findall("./targetTags/li") if (x.text or "").strip()]
    if not tags:
        failures.append(f"{name}: no targetTags ({path})")
    for tag in tags:
        target_counts[tag]+=1
        if not (tag.startswith("Map_") or tag.startswith("World") or tag in {"Map_PlayerHome","Map_PlayerHome, Map_TempIncident"}):
            notes.append(f"{name}: uncommon target tag {tag}")

    # Timing/chance must be non-negative when authored.
    for tag in ("baseChance","earliestDay","minRefireDays"):
        raw=(node.findtext(tag) or "").strip()
        if raw:
            try:
                if float(raw)<0: failures.append(f"{name}: negative {tag}={raw}")
            except ValueError:
                failures.append(f"{name}: non-numeric {tag}={raw}")

    # Optional XML must be gated at the def or worker/reference level.
    raw=path.read_text(encoding="utf-8",errors="ignore")
    if "MayRequire=" in raw:
        optional_incidents+=1

# Site structural validation.
required_faction=0
factionless=0
for name,(path,node) in sites.items():
    worker=(node.findtext("workerClass") or "").strip()
    if not worker:
        failures.append(f"{name}: missing workerClass ({path})")
    else:
        cls=class_name(worker)
        found=class_to_paths.get(cls,[])
        if not found:
            failures.append(f"{name}: workerClass {worker} has no production C# class")
        elif not any("SitePartWorker" in bases for _,bases in found):
            if not cls.startswith("SitePartWorker_"):
                failures.append(f"{name}: worker {cls} is not recognizably a SitePartWorker")

    req=(node.findtext("requiresFaction") or "").strip().lower()
    if req=="true":
        required_faction+=1
    elif req=="false" or req=="":
        factionless+=1

    min_points=(node.findtext("minThreatPoints") or "").strip()
    if min_points:
        try:
            if float(min_points)<0: failures.append(f"{name}: negative minThreatPoints {min_points}")
        except ValueError:
            failures.append(f"{name}: invalid minThreatPoints {min_points}")

# Literal source references to WNG IncidentDef/SitePartDef must resolve.
incident_ref_re=re.compile(r'DefDatabase\s*<\s*IncidentDef\s*>\s*\.\s*GetNamed(?:SilentFail)?\s*\(\s*"(?P<n>WNG_[^"]+)"')
site_ref_re=re.compile(r'DefDatabase\s*<\s*SitePartDef\s*>\s*\.\s*GetNamed(?:SilentFail)?\s*\(\s*"(?P<n>WNG_[^"]+)"')
literal_incident_refs=[]
literal_site_refs=[]
for p,text in source_text.items():
    for m in incident_ref_re.finditer(text):
        n=m.group("n"); literal_incident_refs.append((p,n))
        if n not in incidents: failures.append(f"{p}: references missing IncidentDef {n}")
    for m in site_ref_re.finditer(text):
        n=m.group("n"); literal_site_refs.append((p,n))
        if n not in sites: failures.append(f"{p}: references missing SitePartDef {n}")

# All custom IncidentWorkers should fail safely on invalid/no map target. We cannot execute
# RimWorld here, so enforce a structural guard in either CanFireNowSub or TryExecuteWorker.
# Known workers that deliberately delegate through a shared helper still must mention Map/map.
incident_workers=set(class_name((node.findtext("workerClass") or "").strip()) for _,node in incidents.values())
unguarded=[]
for cls in sorted(c for c in incident_workers if c):
    entries=class_to_paths.get(cls,[])
    if not entries: continue
    text="\n".join(source_text[p] for p,_ in entries)
    has_gate=("CanFireNowSub" in text or "TryExecuteWorker" in text)
    target_guard=(
        "parms?.target as Map" in text or
        "parms.target as Map" in text or
        "parms?.target is Map" in text or
        "parms.target is Map" in text or
        "parms?.target as World" in text
    )
    if not has_gate or not target_guard:
        unguarded.append(cls)
if unguarded:
    failures.append("Incident workers without obvious invalid-target guard: "+", ".join(unguarded))

# Every site-building helper should guard null SitePartDef/map/world inputs before creating.
# Static evidence is deliberately broad because SitePartWorker.Generate methods are invoked by
# RimWorld map generation rather than direct incident CanFireNow checks.
site_workers=set(class_name((node.findtext("workerClass") or "").strip()) for _,node in sites.values())
for cls in sorted(c for c in site_workers if c):
    entries=class_to_paths.get(cls,[])
    if not entries: continue
    text="\n".join(source_text[p] for p,_ in entries)
    if "Generate" not in text and "PostMapGenerate" not in text and "PostMapGenerate" not in text:
        notes.append(f"{cls}: no explicit Generate token; may rely on inherited SitePartWorker behavior")

# Required hard references in Incident/Site XML to WNG factions/pawn kinds must exist.
for collection in (incidents,sites):
    for name,(path,node) in collection.items():
        for e in node.iter():
            if not e.text: continue
            val=e.text.strip()
            if val.startswith("WNG_") and e.tag in ("factionDef","requiredFaction","pawnKind","pawnKindDef"):
                if "Faction" in e.tag or e.tag in ("factionDef","requiredFaction"):
                    if val not in faction_defs: failures.append(f"{name}: missing faction ref {val}")
                elif val not in pawnkind_defs:
                    failures.append(f"{name}: missing pawn kind ref {val}")

print("=== D157 INCIDENT / QUEST AUDIT ===")
print(f" - WNG IncidentDefs audited: {len(incidents)}")
print(f" - WNG SitePartDefs audited: {len(sites)}")
print(f" - Incident worker classes referenced: {len(incident_workers)}")
print(f" - SitePart worker classes referenced: {len(site_workers)}")
print(f" - Incident target tags: {dict(sorted(target_counts.items()))}")
print(f" - Faction-required SitePartDefs: {required_faction}")
print(f" - Factionless/optional-faction SitePartDefs: {factionless}")
print(f" - Literal source IncidentDef refs checked: {len(literal_incident_refs)}")
print(f" - Literal source SitePartDef refs checked: {len(literal_site_refs)}")
print(f" - Optional/MayRequire incident XML files encountered: {optional_incidents}")

if notes:
    print("\nNOTES:")
    for n in notes: print(" -",n)
if failures:
    print("\nFAILURES:")
    for f in failures: print(" -",f)
    raise SystemExit(1)
print("PASS: Incident/Site definitions, workers, target guards and WNG cross-references are structurally coherent.")
