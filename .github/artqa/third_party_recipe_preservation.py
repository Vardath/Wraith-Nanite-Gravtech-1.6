from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT=Path(".")
failures=[]
notes=[]

# 1) WNG may not mutate global recipe collections in runtime code.
production_cs=[
    p for p in (ROOT/"Source").rglob("*.cs")
    if "Diagnostics" not in p.parts
]
source="\n".join(p.read_text(encoding="utf-8",errors="ignore") for p in production_cs)

for label,pat in {
    "DefDatabase<RecipeDef> global enumeration mutation": r"DefDatabase\s*<\s*RecipeDef\s*>\.AllDefsListForReading[^\n;]*(?:Clear|Remove|Add|=)",
    "ThingDef recipes clear": r"\.recipes\s*\.\s*Clear\s*\(",
    "ThingDef recipes assignment": r"\.recipes\s*=",
    "recipeUsers clear": r"recipeUsers\s*\.\s*Clear\s*\(",
    "recipeUsers assignment": r"recipeUsers\s*=",
}.items():
    if re.search(pat,source):
        failures.append("Runtime third-party recipe interference detected: "+label)

# 2) XML patches may not replace/remove external RecipeDefs or external bench recipe lists.
for base in (ROOT/"Patches", ROOT/"Compatibility"):
    if not base.exists():
        continue
    for path in base.rglob("*.xml"):
        raw=path.read_text(encoding="utf-8",errors="ignore")
        if "<Patch" not in raw and "PatchOperation" not in raw:
            continue
        try:
            root=ET.fromstring(raw)
        except ET.ParseError as exc:
            failures.append(f"{path}: XML parse failure: {exc}")
            continue
        for op in root.iter():
            cls=op.attrib.get("Class","")
            if not cls.startswith("PatchOperation"):
                continue
            xpath=(op.findtext("xpath") or "").strip()
            if not xpath:
                continue
            compact=re.sub(r"\s+","",xpath)
            wng_target=('defName="WNG_' in xpath or "defName='WNG_" in xpath or
                        'starts-with(defName,"WNG_")' in xpath or "starts-with(defName,'WNG_')" in xpath)
            if "RecipeDef" in compact and not wng_target:
                failures.append(f"{path}: external RecipeDef patch forbidden: {xpath}")
            if any(tok in compact for tok in ("/recipes","/recipeUsers")) and not wng_target:
                failures.append(f"{path}: external bench recipe-list patch forbidden: {xpath}")
            if cls in ("PatchOperationRemove","PatchOperationReplace") and not wng_target:
                # Other audits allow a few bounded vanilla integrations. Here we only care if
                # the operation can touch recipe ownership/enumeration.
                if "RecipeDef" in compact or "recipe" in compact.lower():
                    failures.append(f"{path}: destructive external recipe patch forbidden: {xpath}")

# 3) WNG RecipeDefs may only add themselves to a bounded allow-list of external benches.
allowed_external={
    "Human","MechGestator","TableSculpting","FabricationBench","DrugLab","ElectricSmelter","AmmoBench"
}
external_routes={}
for base in (ROOT/"Defs", ROOT/"Compatibility"):
    if not base.exists():
        continue
    for path in base.rglob("*.xml"):
        raw=path.read_text(encoding="utf-8",errors="ignore")
        if "<Patch" in raw or "PatchOperation" in raw:
            continue
        try:
            root=ET.fromstring(raw)
        except ET.ParseError:
            continue
        for node in list(root):
            name=(node.findtext("defName") or "").strip()
            if not name.startswith("WNG_"):
                continue
            blocks=[]
            if node.tag=="RecipeDef":
                blocks.append(node)
            maker=node.find("./recipeMaker")
            if maker is not None:
                blocks.append(maker)
            for b in blocks:
                ru=b.find("./recipeUsers")
                users=[(x.text or "").strip() for x in b.findall("./recipeUsers/li") if (x.text or "").strip()]
                if ru is not None and (ru.get("Inherit") or "").lower()=="false":
                    ext=[u for u in users if not u.startswith("WNG_")]
                    if ext:
                        failures.append(f"{path}: {name} uses recipeUsers Inherit=False on external user(s) {ext}")
                for u in users:
                    if u.startswith("WNG_"):
                        continue
                    external_routes.setdefault(u,[]).append(name)
                    if u not in allowed_external:
                        failures.append(f"{path}: {name} targets unexpected third-party/vanilla bench {u}")

# 4) Historical regression guard: WNG itself must never mention Nanotech Overpower's
# Nanofabricator in production source/Defs/Patches. The live diagnostic separately checks it.
for p in [*production_cs, *((ROOT/"Defs").rglob("*.xml")), *((ROOT/"Patches").rglob("*.xml")), *((ROOT/"Compatibility").rglob("*.xml"))]:
    raw=p.read_text(encoding="utf-8",errors="ignore")
    if re.search(r"\bNanofabricator\b|\bLyn\.NTO\b|Nanotech Overpower", raw, re.I):
        failures.append(f"{p}: WNG production data directly references Nanotech Overpower/Nanofabricator")

print("=== D135 THIRD-PARTY RECIPE PRESERVATION AUDIT ===")
for bench in sorted(external_routes):
    print(f" - {bench}: {len(external_routes[bench])} additive WNG route(s)")
print(" - Historical live regression target: Lyn.NTO / Nanofabricator")
if failures:
    print("\nFAILURES:")
    for f in failures:
        print(" -",f)
    raise SystemExit(1)
print("PASS: WNG has no destructive or global third-party recipe-list interference.")
