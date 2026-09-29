from pathlib import Path
import collections, sys
import xml.etree.ElementTree as ET

failures=[]
warnings=[]
notes=[]

roots=[]
for base in (Path("Defs"), Path("Compatibility")):
    if not base.exists(): continue
    for p in base.rglob("*.xml"):
        raw=p.read_text(encoding="utf-8",errors="ignore")
        if "<Patch" in raw or "PatchOperation" in raw:
            continue
        try: roots.append((p,ET.fromstring(raw)))
        except ET.ParseError as exc: failures.append(f"{p}: XML parse failure: {exc}")

by_type=collections.defaultdict(dict)
for path,root in roots:
    for node in list(root):
        name=(node.findtext("defName") or "").strip()
        if name:
            by_type[node.tag][name]=(path,node)

thingdefs=by_type["ThingDef"]
recipes=by_type["RecipeDef"]

allowed_external_users={
    "Human","MechGestator","TableSculpting","FabricationBench","DrugLab","ElectricSmelter","AmmoBench"
}

external_usage=collections.defaultdict(list)
wng_usage=collections.defaultdict(list)

# RecipeDefs: validate every production route.
for name,(path,node) in recipes.items():
    if not name.startswith("WNG_"):
        continue
    users=[(x.text or "").strip() for x in node.findall("./recipeUsers/li") if (x.text or "").strip()]
    products=node.find("./products")
    surgery=node.find("addsHediff") is not None or node.find("removesHediff") is not None or node.get("ParentName","").startswith("Surgery")

    if products is not None and not users:
        failures.append(f"{name}: physical production RecipeDef has no recipeUsers ({path})")

    for user in users:
        if user.startswith("WNG_"):
            wng_usage[user].append((name,path))
            if user not in thingdefs:
                failures.append(f"{name}: missing WNG recipe user ThingDef {user} ({path})")
        else:
            external_usage[user].append((name,path))
            if user not in allowed_external_users:
                failures.append(f"{name}: unexpected external recipe user {user} ({path})")

    # Inherit=False is dangerous on external/vanilla routing because it can erase inherited users.
    ru=node.find("./recipeUsers")
    if ru is not None and (ru.get("Inherit") or "").lower()=="false":
        ext=[u for u in users if not u.startswith("WNG_")]
        if ext:
            failures.append(f"{name}: recipeUsers Inherit=False targets external user(s) {ext} ({path})")

    # Production recipes with ingredients should bind filters explicitly.
    if node.findall("./ingredients/li") and node.find("./fixedIngredientFilter") is None and not surgery:
        warnings.append(f"{name}: production recipe has ingredients without fixedIngredientFilter ({path})")

# ThingDef recipeMaker implied recipes.
for name,(path,node) in thingdefs.items():
    if not name.startswith("WNG_"):
        continue
    maker=node.find("./recipeMaker")
    if maker is None:
        continue
    users=[(x.text or "").strip() for x in maker.findall("./recipeUsers/li") if (x.text or "").strip()]
    if not users:
        failures.append(f"{name}: recipeMaker has no recipeUsers ({path})")
    for user in users:
        if user.startswith("WNG_"):
            wng_usage[user].append((name+" [recipeMaker]",path))
            if user not in thingdefs:
                failures.append(f"{name}: recipeMaker targets missing WNG ThingDef {user} ({path})")
        else:
            external_usage[user].append((name+" [recipeMaker]",path))
            if user not in allowed_external_users:
                failures.append(f"{name}: recipeMaker targets unexpected external user {user} ({path})")
    ru=maker.find("./recipeUsers")
    if ru is not None and (ru.get("Inherit") or "").lower()=="false":
        ext=[u for u in users if not u.startswith("WNG_")]
        if ext:
            failures.append(f"{name}: recipeMaker recipeUsers Inherit=False targets external user(s) {ext} ({path})")

# Any WNG workstation used by recipes must be a building-like ThingDef.
for user,refs in wng_usage.items():
    path,node=thingdefs[user]
    category=(node.findtext("category") or "").strip()
    parent=node.get("ParentName","")
    thingclass=(node.findtext("thingClass") or "").strip()
    buildingish = category=="Building" or "Building" in parent or "WorkTable" in parent or "Building" in thingclass
    if not buildingish:
        failures.append(f"{user}: used as recipe workstation but does not look building/worktable-like ({path})")

# Representative vanilla benches must not be replaced by WNG defs and additive routes stay bounded.
for bench in ("ElectricStove","FueledStove","HandTailoringBench","ElectricTailoringBench"):
    if external_usage.get(bench):
        failures.append(f"{bench}: WNG unexpectedly injects {len(external_usage[bench])} recipe(s)")

# Expected intentional external integration points.
expected={
    "FabricationBench": lambda n,p: False,
    "ElectricSmelter": lambda n,p: p.name=="Recipes_ReplicatorSalvage.xml",
}
for bench,pred in expected.items():
    for name,path in external_usage.get(bench,[]):
        if not pred(name,path):
            failures.append(f"{bench}: unexpected WNG route {name} from {path}")

notes.append(f"Parsed {len(recipes)} RecipeDefs and {len(thingdefs)} ThingDefs")
notes.append(f"WNG workstations referenced: {len(wng_usage)}")
for user in sorted(external_usage):
    notes.append(f"{user}: {len(external_usage[user])} WNG recipe route(s)")

print("=== D134 PRODUCTION ROUTING AUDIT ===")
for n in notes: print(" -",n)
if warnings:
    print("\nWARNINGS:")
    for w in warnings: print(" -",w)
if failures:
    print("\nFAILURES:")
    for f in failures: print(" -",f)
    raise SystemExit(1)
print("PASS: production routing is additive, bounded, and all WNG workstations resolve.")
