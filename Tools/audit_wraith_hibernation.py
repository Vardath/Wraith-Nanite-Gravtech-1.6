#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
GENES = ROOT / "Defs" / "GeneDefs" / "Genes_WraithCore.xml"
HEDIFFS = ROOT / "Defs" / "HediffDefs" / "Hediffs_WraithCore.xml"
ABILITIES = ROOT / "Defs" / "AbilityDefs" / "Abilities_WraithCore.xml"
XENOTYPES = ROOT / "Defs" / "XenotypeDefs" / "Xenotypes_Wraith.xml"
LIFE_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithLifeForce.cs"
HIB_SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithHibernation.cs"

errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def parse(path: Path) -> ET.Element | None:
    if not path.exists():
        fail(f"Missing required file: {path.relative_to(ROOT)}")
        return None
    try:
        return ET.parse(path).getroot()
    except ET.ParseError as ex:
        fail(f"XML parse failure in {path.relative_to(ROOT)}: {ex}")
        return None


def find_def(root: ET.Element | None, tag: str, name: str) -> ET.Element | None:
    if root is None:
        return None
    for node in root.findall(tag):
        if (node.findtext("defName") or "").strip() == name:
            return node
    fail(f"Missing {tag} {name}")
    return None


def require(node: ET.Element | None, path: str, expected: str, description: str) -> None:
    if node is None:
        return
    found = (node.findtext(path) or "").strip()
    if found != expected:
        fail(f"{description}: expected {expected!r}, found {found!r}")


genes_root = parse(GENES)
hediffs_root = parse(HEDIFFS)
abilities_root = parse(ABILITIES)
xenotypes_root = parse(XENOTYPES)

life_gene = find_def(genes_root, "GeneDef", "WNG_LifeForce")
hibernation_gene = find_def(genes_root, "GeneDef", "WNG_HibernationGene")
hibernating = find_def(hediffs_root, "HediffDef", "WNG_WraithHibernating")
starved = find_def(hediffs_root, "HediffDef", "WNG_LifeForceStarved")
torpor = find_def(hediffs_root, "HediffDef", "WNG_LifeForceTorpor")
hibernate = find_def(abilities_root, "AbilityDef", "WNG_Hibernate")
wraith = find_def(xenotypes_root, "XenotypeDef", "WNG_Wraith")

require(life_gene, "resourceLossPerDay", "0.0666667", "normal Life Force drain")
if life_gene is not None:
    thresholds = [(x.text or "").strip() for x in life_gene.findall("resourceGizmoThresholds/li")]
    if thresholds != ["0.15", "0.40", "0.70"]:
        fail(f"Life Force visible thresholds must remain 0.15/0.40/0.70, found {thresholds}")

if hibernation_gene is not None:
    if (hibernation_gene.findtext("geneClass") or "").strip() != "WraithNaniteGravtech.Gene_WraithAbilityAnchor":
        fail("Hibernation gene must use the gene-owned ability reconciliation anchor")
    abilities = [(x.text or "").strip() for x in hibernation_gene.findall("abilities/li")]
    if abilities != ["WNG_Hibernate"]:
        fail(f"Hibernation gene must grant only WNG_Hibernate, found {abilities}")

require(hibernating, "comps/li/disappearsAfterTicks", "180000~180000", "hibernation duration")
require(hibernating, "stages/li/capMods/li[1]/capacity", "Moving", "first hibernation capacity")
require(hibernating, "stages/li/capMods/li[1]/setMax", "0.08", "hibernation Moving cap")
require(hibernating, "stages/li/capMods/li[2]/capacity", "Manipulation", "second hibernation capacity")
require(hibernating, "stages/li/capMods/li[2]/setMax", "0.08", "hibernation Manipulation cap")
require(hibernating, "stages/li/capMods/li[3]/capacity", "Consciousness", "third hibernation capacity")
require(hibernating, "stages/li/capMods/li[3]/setMax", "0.18", "hibernation Consciousness cap")

require(starved, "stages/li/capMods/li[1]/offset", "-0.20", "starvation Moving penalty")
require(starved, "stages/li/capMods/li[2]/offset", "-0.15", "starvation Manipulation penalty")
require(starved, "stages/li/capMods/li[3]/offset", "-0.10", "starvation Consciousness penalty")
require(torpor, "stages/li/capMods/li[1]/setMax", "0.02", "torpor Moving cap")
require(torpor, "stages/li/capMods/li[2]/setMax", "0.05", "torpor Manipulation cap")
require(torpor, "stages/li/capMods/li[3]/setMax", "0.15", "torpor Consciousness cap")

require(hibernate, "cooldownTicksRange", "180000~180000", "deliberate hibernation cooldown")
require(hibernate, "verbProperties/verbClass", "Verb_CastAbility", "hibernation verb class")
require(hibernate, "verbProperties/range", "0", "hibernation self-cast range")
require(hibernate, "verbProperties/warmupTime", "1", "hibernation warmup")
require(hibernate, "verbProperties/targetParams/canTargetSelf", "true", "hibernation self targeting")
require(hibernate, "verbProperties/targetParams/canTargetPawns", "false", "hibernation external-pawn targeting")
require(hibernate, "comps/li/hibernatingHediff", "WNG_WraithHibernating", "hibernation applied Hediff")
if hibernate is not None:
    comp = hibernate.find("comps/li")
    if comp is None or comp.attrib.get("Class") != "WraithNaniteGravtech.CompProperties_AbilityWraithHibernate":
        fail("WNG_Hibernate must use the fresh public hibernation runtime")

if wraith is not None:
    genes = [(x.text or "").strip() for x in wraith.findall("genes/li")]
    for required in ("WNG_LifeForce", "WNG_WraithRegeneration", "WNG_LifeDrainGene", "WNG_HibernationGene"):
        if required not in genes:
            fail(f"True Wraith xenotype must retain {required}")

life_source = LIFE_SOURCE.read_text(encoding="utf-8", errors="replace") if LIFE_SOURCE.exists() else ""
if not LIFE_SOURCE.exists():
    fail("Missing Wraith Life Force runtime")
else:
    for needle, description in [
        ("StarvedThreshold = 0.15f", "15% starvation threshold"),
        ("ShouldFeedThreshold = 0.20f", "20% should-feed threshold"),
        ("TorporThreshold = 0.001f", "0.1% torpor entry threshold"),
        ("RecoverFromTorporThreshold = 0.08f", "8% torpor recovery threshold"),
        ("HibernationDrainFactor = 0.02f", "2% hibernation drain factor"),
        ("def.resourceLossPerDay * (IsHibernating ? HibernationDrainFactor : 1f)", "hibernation-aware native resource drain"),
        ("return Active && !IsHibernating && Value < ShouldFeedThreshold", "hibernating Wraith must not request feeding"),
        ("if (IsHibernating)\n                    return 0.12f", "hibernation regeneration factor"),
        ("if (Value <= 0.01f)\n                    return 0.05f", "near-empty regeneration factor"),
        ("if (Value < 0.15f)\n                    return 0.25f", "starved regeneration factor"),
        ("if (Value < 0.40f)\n                    return 0.60f", "low regeneration factor"),
        ("if (Value < 0.70f)\n                    return 1.00f", "normal regeneration factor"),
        ("return 1.60f", "well-fed regeneration factor"),
        ("WNG_LifeForceStarved", "starvation Hediff handling"),
        ("WNG_LifeForceTorpor", "torpor Hediff handling"),
        ("if (IsHibernating)", "hibernation state handling"),
    ]:
        if needle not in life_source:
            fail(f"Missing Life Force physiology contract: {description}")

hib_source = HIB_SOURCE.read_text(encoding="utf-8", errors="replace") if HIB_SOURCE.exists() else ""
if not HIB_SOURCE.exists():
    fail("Missing deliberate Wraith hibernation runtime")
else:
    for needle, description in [
        ("WraithLifeForceUtility.Get(caster) != null", "hibernation must require a real Wraith Life Force gene"),
        ("!caster.health.hediffSet.HasHediff", "hibernation must reject duplicate active state"),
        ("caster.health.AddHediff", "hibernation must apply the configured state"),
    ]:
        if needle not in hib_source:
            fail(f"Missing deliberate hibernation contract: {description}")

if errors:
    print("Wraith hibernation physiology audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith hibernation physiology audit OK")
