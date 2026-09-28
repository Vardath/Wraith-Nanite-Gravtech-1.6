from pathlib import Path
import re, sys
import xml.etree.ElementTree as ET

failures=[]
notes=[]

patch_paths=[]
for base in (Path("Patches"), Path("Compatibility")):
    if base.exists():
        for p in base.rglob("*.xml"):
            raw=p.read_text(encoding="utf-8",errors="ignore")
            if "<Patch" in raw or "PatchOperation" in raw:
                patch_paths.append(p)

def is_wng_target(xpath):
    return 'defName="WNG_' in xpath or "defName='WNG_" in xpath or 'starts-with(defName,"WNG_")' in xpath or "starts-with(defName,'WNG_')" in xpath

allowed_vanilla_adds=[
    ("RitualOutcomeEffectDef", "GravshipLaunch", "/comps"),
    ("BiomeDef", None, "/wildAnimals"),
    ("TraderKindDef", None, "/stockGenerators"),
]

for path in patch_paths:
    raw=path.read_text(encoding="utf-8",errors="ignore")
    try:
        root=ET.fromstring(raw)
    except ET.ParseError as exc:
        failures.append(f"{path}: XML parse failure: {exc}")
        continue

    ops=list(root.iter())
    for op in ops:
        cls=op.attrib.get("Class","")
        if not cls.startswith("PatchOperation"):
            continue
        xpath=(op.findtext("xpath") or "").strip()
        if not xpath:
            continue

        if cls=="PatchOperationRemove":
            failures.append(f"{path}: destructive PatchOperationRemove is forbidden: {xpath}")
            continue

        if cls=="PatchOperationReplace" and not is_wng_target(xpath):
            failures.append(f"{path}: PatchOperationReplace may only target WNG defs: {xpath}")
            continue

        # Additions to WNG defs are safe by scope. Additions to vanilla defs must be
        # one of the explicitly intended additive integration points.
        if cls=="PatchOperationAdd" and not is_wng_target(xpath):
            ok=False
            for typ,name,suffix in allowed_vanilla_adds:
                if typ not in xpath or not xpath.endswith(suffix):
                    continue
                if name is not None and f'defName="{name}"' not in xpath and f"defName='{name}'" not in xpath:
                    continue
                ok=True
                break
            if not ok:
                failures.append(f"{path}: unapproved vanilla/external PatchOperationAdd target: {xpath}")

        # No patch may broadly target all ThingDefs/RecipeDefs or an entire root collection.
        compact=re.sub(r"\s+","",xpath)
        if compact in ("Defs/ThingDef","/Defs/ThingDef","Defs/RecipeDef","/Defs/RecipeDef","Defs/*","/Defs/*"):
            failures.append(f"{path}: dangerously broad XPath: {xpath}")
        if "RecipeDef" in compact and not is_wng_target(xpath):
            failures.append(f"{path}: vanilla/external RecipeDef patching is forbidden: {xpath}")

    # Optional foreign-def replacements must remain conditional.
    if any(tok in raw for tok in ("ONAC_","CombatExtended.","AmmoBench")):
        if "ONAC_" in raw and 'MayRequire="idolord.onac"' not in raw:
            failures.append(f"{path}: ONAC references are not protected by MayRequire=idolord.onac")

notes.append(f"Audited {len(patch_paths)} patch/compatibility XML files")

print("=== D133 PATCH BLAST-RADIUS AUDIT ===")
for n in notes: print(" -",n)
if failures:
    print("\nFAILURES:")
    for f in failures: print(" -",f)
    raise SystemExit(1)
print("PASS: patches are additive/bounded and destructive vanilla recipe/def replacement is blocked.")
