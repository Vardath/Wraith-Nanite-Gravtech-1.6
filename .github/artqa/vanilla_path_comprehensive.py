from pathlib import Path
import collections
import xml.etree.ElementTree as ET

ROOT=Path(".")
failures=[]
warnings=[]
notes=[]

roots=[]
for base in (ROOT/"Defs", ROOT/"Compatibility"):
    if not base.exists():
        continue
    for path in base.rglob("*.xml"):
        raw=path.read_text(encoding="utf-8",errors="ignore")
        if "<Patch" in raw or "PatchOperation" in raw:
            continue
        try:
            roots.append((path,ET.fromstring(raw)))
        except ET.ParseError as exc:
            failures.append(f"{path}: XML parse error: {exc}")

by_type=collections.defaultdict(dict)
for path,root in roots:
    for node in list(root):
        name=(node.findtext("defName") or "").strip()
        if name:
            by_type[node.tag][name]=(path,node)

things=by_type["ThingDef"]
recipes=by_type["RecipeDef"]

def cats(node):
    return [(x.text or "").strip() for x in node.findall("./thingCategories/li") if (x.text or "").strip()]

def users(node):
    return [(x.text or "").strip() for x in node.findall("./recipeUsers/li") if (x.text or "").strip()]

def has_drug_comp(node):
    return any((li.get("Class") or "")=="CompProperties_Drug" for li in node.findall("./comps/li"))

def is_building(node):
    parent=node.get("ParentName","")
    category=(node.findtext("category") or "").strip()
    cls=(node.findtext("thingClass") or "").strip()
    return category=="Building" or "Building" in parent or "Bench" in parent or "Building" in cls or "WorkTable" in cls

def is_worktable(node):
    parent=node.get("ParentName","")
    cls=(node.findtext("thingClass") or "").strip()
    return "Bench" in parent or "WorkTable" in parent or "Building_WorkTable" in cls or node.find("./inspectorTabs/li[.='ITab_Bills']") is not None

# Intentional non-vanilla production systems.
wraith_biological_prefixes=("WNG_Wraith","WNG_RawWraith","WNG_RefinedWraith")
asuran_pattern_recipes={
    "Make_WNG_AsuranSleeperStatue","Make_WNG_AsuranFeederStatue","Make_WNG_AsuranReplicatorReliquary"
}
intentional_no_generic_fabrication={
    "WNG_GrowWraithGravEngineSeed","WNG_CultureBiomass","WNG_RecoverBiomass",
    "WNG_StabilizeAsuranNaniteSlurry","WNG_SynthesizeAsuranGravEngine"
}

# Build product->recipe index.
product_recipes=collections.defaultdict(list)
for rname,(path,node) in recipes.items():
    for prod in node.findall("./products/*"):
        product_recipes[prod.tag].append((rname,path,node))

# ---- ThingDef native-path checks ----
for name,(path,node) in things.items():
    if not (name.startswith("WNG_") or name.startswith("Plant_WNG_")):
        continue
    parent=node.get("ParentName","")
    tcats=cats(node)
    ingest=node.find("ingestible")
    apparel=node.find("apparel")
    race=node.find("race")
    plant=node.find("plant")
    stat=node.find("statBases")

    # Portable resources/items should use native stockpile categories.
    portable=(node.findtext("category")=="Item" or parent in {
        "ResourceBase","DrugBase","MakeableDrugBase","BodyPartBase","BodyPartProstheticBase"
    } or node.find("stackLimit") is not None or node.findtext("alwaysHaulable")=="true")
    hidden={"WNG_AnimalDiversitySurvey","WNG_WhispersHybridStudyGate"}
    if portable and name not in hidden and not tcats:
        failures.append(f"{name}: portable item has no vanilla stockpile category ({path})")

    # Food/drugs must use native ingestible and drug machinery.
    if ingest is not None:
        nutrition=stat.find("Nutrition") if stat is not None else None
        if nutrition is not None and not any(c in tcats for c in ("Foods","FoodRaw","PlantFoodRaw","Drugs")):
            warnings.append(f"{name}: nutritious ingestible is not in a native food/drug stockpile leaf ({path})")
        drug_cat=(ingest.findtext("drugCategory") or "").strip()
        if drug_cat:
            if not has_drug_comp(node):
                failures.append(f"{name}: ingestible drug has no CompProperties_Drug ({path})")
            if not any(c in tcats for c in ("Drugs","Medicine")):
                failures.append(f"{name}: ingestible drug is not in Drugs/Medicine category ({path})")

    if has_drug_comp(node):
        if ingest is None:
            failures.append(f"{name}: CompProperties_Drug item has no ingestible block ({path})")
        if not any(c in tcats for c in ("Drugs","Medicine")):
            failures.append(f"{name}: drug comp item is not stored as Drugs/Medicine ({path})")

    # Apparel follows vanilla apparel data.
    if apparel is not None:
        if not any(c.startswith("Apparel") or c in ("Headgear","ArmorHeadgear") for c in tcats):
            failures.append(f"{name}: apparel lacks vanilla apparel stockpile category ({path})")
        if not node.findall("./apparel/bodyPartGroups/li"):
            failures.append(f"{name}: apparel has no bodyPartGroups ({path})")
        if not node.findall("./apparel/layers/li"):
            failures.append(f"{name}: apparel has no apparel layers ({path})")
        if stat is None or stat.find("Mass") is None or stat.find("EquipDelay") is None:
            failures.append(f"{name}: apparel lacks native Mass/EquipDelay stats ({path})")

    # Weapons follow vanilla equipment metadata.
    if "WeaponsRanged" in tcats or "WeaponsMelee" in tcats:
        if not node.findall("./weaponTags/li"):
            failures.append(f"{name}: weapon has no weaponTags ({path})")
        if stat is None or stat.find("Mass") is None:
            failures.append(f"{name}: weapon has no Mass ({path})")
        if "WeaponsRanged" in tcats:
            for fld in ("AccuracyTouch","AccuracyShort","AccuracyMedium","AccuracyLong","RangedWeapon_Cooldown"):
                if stat is None or stat.find(fld) is None:
                    failures.append(f"{name}: ranged weapon missing vanilla stat {fld} ({path})")
            if not node.findall("./verbs/li"):
                failures.append(f"{name}: ranged weapon has no verb ({path})")
        if "WeaponsMelee" in tcats and not node.findall("./tools/li"):
            failures.append(f"{name}: melee weapon has no melee tools ({path})")

    # Plants should be true PlantBase-style defs.
    if plant is not None:
        if "Plant" not in parent and parent!="PlantBase":
            warnings.append(f"{name}: plant data does not inherit a vanilla plant base ({path}, parent={parent})")
        if not plant.findtext("harvestedThingDef") and name.startswith("Plant_WNG_"):
            warnings.append(f"{name}: crop plant has no harvestedThingDef ({path})")

    # Race defs inherit their pawn category/native handling from vanilla race bases.
    if race is not None and not parent:
        warnings.append(f"{name}: race ThingDef has no vanilla/inherited race parent ({path})")

    # Worktables should be native bill-givers.
    if is_worktable(node):
        if node.find("./inspectorTabs/li[.='ITab_Bills']") is None:
            failures.append(f"{name}: worktable has no ITab_Bills ({path})")
        if node.find("./building") is None:
            warnings.append(f"{name}: worktable has no native building block/Bills concept ({path})")

# ---- Recipe->vanilla workstation checks ----
def require_route(recipe_name,node,bench,path,why):
    us=users(node)
    if bench not in us:
        failures.append(f"{recipe_name}: {why}; missing vanilla route {bench} ({path}), users={us}")

for rname,(path,node) in recipes.items():
    if not (rname.startswith("WNG_") or rname.startswith("Make_WNG_")):
        continue
    prods=[p.tag for p in node.findall("./products/*")]

    # Surgery recipes use the native surgery worker/effect path; they are attached to races/body parts
    # through vanilla surgery resolution rather than ordinary workbench recipeUsers.
    surgery=(node.find("addsHediff") is not None or node.find("removesHediff") is not None or
             (node.get("ParentName") or "").startswith("Surgery") or
             "Recipe_Surgery" in (node.findtext("workerClass") or "") or
             "Recipe_Install" in (node.findtext("workerClass") or ""))
    if surgery:
        wc=(node.findtext("workerClass") or "").strip()
        effect=(node.findtext("effectWorking") or "").strip()
        if not wc and not (node.get("ParentName") or "").startswith("Surgery"):
            warnings.append(f"{rname}: surgery has no explicit/inherited workerClass ({path})")
        if effect and effect!="Surgery":
            warnings.append(f"{rname}: surgery uses non-Surgery effect {effect} ({path})")

    for prod in prods:
        t=things.get(prod)
        if not t:
            continue
        tpath,tnode=t
        tcats=cats(tnode)
        ingest=tnode.find("ingestible")
        drug=has_drug_comp(tnode) or (ingest is not None and bool((ingest.findtext("drugCategory") or "").strip()))

        # All makeable WNG drugs/medicines should work through vanilla DrugLab.
        if drug or "Medicine" in tcats or "Drugs" in tcats:
            require_route(rname,node,"DrugLab",path,"drug/medicine production should follow vanilla Drug Lab")

        # High-tech non-biological weapons should work at vanilla Fabrication Bench.
        if "WeaponsRanged" in tcats or "WeaponsMelee" in tcats:
            if not prod.startswith(wraith_biological_prefixes) and not prod.startswith("WNG_Replicator"):
                require_route(rname,node,"FabricationBench",path,"manufactured high-tech weapon should follow vanilla fabrication")

        # High-tech non-biological apparel should work at Fabrication Bench.
        if tnode.find("apparel") is not None and not prod.startswith("WNG_Wraith"):
            require_route(rname,node,"FabricationBench",path,"manufactured high-tech apparel should follow vanilla fabrication")

        # Bionic/body-part production follows vanilla Fabrication Bench unless it is a pure surgery recipe.
        if any(c.startswith("BodyParts") for c in tcats) and not surgery:
            biological_growth = "WNG_WraithGrowthChamber" in users(node) and rname.startswith("WNG_Grow")
            if not biological_growth:
                require_route(rname,node,"FabricationBench",path,"manufactured implant/body part should follow vanilla fabrication")

        # Ordinary cooked foods/meals should be available on a vanilla stove.
        if ingest is not None and not drug and any(c in tcats for c in ("Foods","FoodMeals")):
            us=users(node)
            if not any(b in us for b in ("FueledStove","ElectricStove")):
                failures.append(f"{rname}: cooked food has no vanilla stove route ({path}), users={us}")

# Explicit native-path contracts.
for name in ("WNG_ReprocessReplicatorMatter","WNG_DestroyReplicatorCoreFragment"):
    if name in recipes:
        require_route(name,recipes[name][1],"ElectricSmelter",recipes[name][0],"Replicator disposal belongs on vanilla Electric Smelter")

if "WNG_MakeChildsToy" in recipes:
    require_route("WNG_MakeChildsToy",recipes["WNG_MakeChildsToy"][1],"MechGestator",recipes["WNG_MakeChildsToy"][0],"Child's Toy is a mech gestation recipe")

for name in asuran_pattern_recipes:
    if name in recipes:
        us=users(recipes[name][1])
        if "WNG_PrecursorFabricator" not in us:
            failures.append(f"{name}: Asuran-only pattern recipe lost Asuran Fabricator route")
        if "TableSculpting" in us:
            warnings.append(f"{name}: Asuran-only pattern recipe is also on vanilla sculpting table; verify this is intentional")

# No custom production RecipeWorker should be necessary for ordinary fabrication/drug/cooking paths.
for rname,(path,node) in recipes.items():
    wc=(node.findtext("workerClass") or "").strip()
    if wc.startswith("WraithNaniteGravtech.RecipeWorker_") and node.findall("./products/*"):
        warnings.append(f"{rname}: custom production RecipeWorker {wc} remains; prefer vanilla recipe path where possible ({path})")

print("=== WNG COMPREHENSIVE VANILLA-PATH AUDIT ===")
print(f"ThingDefs: {len(things)}")
print(f"RecipeDefs: {len(recipes)}")
print(f"Failures: {len(failures)}")
print(f"Warnings: {len(warnings)}")
if warnings:
    print("\nWARNINGS:")
    for w in warnings:
        print(" -",w)
if failures:
    print("\nFAILURES:")
    for f in failures:
        print(" -",f)
    raise SystemExit(1)
print("PASS: WNG content uses native RimWorld pathways unless explicitly alien/specialized.")
