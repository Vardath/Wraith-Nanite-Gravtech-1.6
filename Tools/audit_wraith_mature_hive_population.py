#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
POP = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithMatureHivePopulation.cs"
CHAMBER = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithGrowthChamber.cs"
errors: list[str] = []


def fail(message: str) -> None:
    errors.append(message)


def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        fail(f"Missing mature Hive population contract: {description}")


pop = POP.read_text(encoding="utf-8", errors="replace") if POP.exists() else ""
chamber = CHAMBER.read_text(encoding="utf-8", errors="replace") if CHAMBER.exists() else ""
if not POP.exists(): fail("Missing fresh mature Hive population runtime")
if not CHAMBER.exists(): fail("Missing fresh Growth Chamber runtime")

for needle, description in [
    ("replacementRetryTicks = 30000", "30,000-tick bounded replacement retry"),
    ("dormantWakeRadius = 18f", "18-cell ordinary-dormant wake radius"),
    ("activeLossesBeforeDormantWake = 2", "two-active-loss wake threshold"),
    ("InitializeGeneratedHive(IEnumerable<Pawn> foundingMembers, Building exactGeneratedGrowthChamber)", "backward-compatible explicit initializer"),
    ("IEnumerable<Pawn> hibernatingFounders", "explicit ordinary hibernating founder input"),
    ("IEnumerable<Building_Bed> exactHibernationPods", "explicit exact Hibernation Pod input"),
    ("parent.Faction == Faction.OfPlayer", "player Hive initialization rejection"),
    ("exactGeneratedGrowthChamber.GetComp<CompWraithGrowthChamber>()", "exact generated Growth Chamber validation"),
    ("CollectExactWraiths(hibernatingFounders, activeFounders)", "exact dormant founder collection"),
    ("CollectExactPods(exactHibernationPods)", "exact Pod collection"),
    ("sleepingFounders.Count != sleepingPods.Count", "one exact Pod per ordinary hibernator"),
    ("demographicMembers.AddRange(exactFounders)", "active and dormant exact founders in one demographic population"),
    ("dormantMembers.AddRange(sleepingFounders)", "separate ordinary dormant cohort tracking"),
    ("dormantBeds.AddRange(sleepingPods)", "separate exact dormant Pod tracking"),
    ("foundingPopulationCap = exactFounders.Count", "fixed active-plus-dormant founding cap"),
    ("initialActiveFounderCount = activeFounders.Count", "fixed initial active cohort size"),
    ("MaintainDormantCohort()", "ordinary dormancy maintenance"),
    ('GetNamedSilentFail("WNG_WraithHibernating")', "real hibernation Hediff maintenance"),
    ("JobMaker.MakeJob(JobDefOf.LayDown, bed)", "normal RimWorld Pod occupancy job"),
    ("pawn.jobs.StartJob", "actual exact-pawn LayDown assignment"),
    ("FreeColonistsSpawned", "player-proximity wake observation"),
    ("LivingActiveDemographicCount()", "active population accounting separated from sleepers"),
    ("WakeDormantCohort()", "bounded ordinary sleeper wake path"),
    ("pawn.health.RemoveHediff(existing)", "hibernation removal on wake"),
    ("LordMaker.MakeNewLord", "woken ordinary cohort defense Lord"),
    ("Scribe_Collections.Look(ref demographicMembers", "save-persistent exact demographic references"),
    ("Scribe_Collections.Look(ref dormantMembers", "save-persistent ordinary dormant Pawn references"),
    ("Scribe_Collections.Look(ref dormantBeds", "save-persistent exact Hibernation Pod references"),
    ("Scribe_References.Look(ref generatedGrowthChamber", "save-persistent exact Growth Chamber reference"),
    ("TakeCompletedPopulationClone()", "exact completed replacement handoff"),
    ("TryStartPopulationReplacement(replacementKind)", "replacement through real Growth Chamber payment path"),
    ("queenAlive && !keeperAlive", "Keeper priority while Queen survives"),
    ('return "WNG_WraithKeeper"', "Keeper replacement selection"),
    ('return hunterNext ? "WNG_WraithHunter" : "WNG_WraithWarrior"', "Hunter/Warrior alternation"),
    ('kind == "WNG_WraithQueen"', "completed Queen rejection"),
    ("LivingDemographicCount() >= foundingPopulationCap", "fixed population ceiling enforcement"),
]:
    require(pop, needle, description)

for forbidden, description in [
    ("AllPawnsSpawned", "map-wide pawn adoption/scanning"),
    ("DormancyVault", "sealed Dormancy Vault reserve entering demographic population"),
    ("PawnGenerator.GeneratePawn", "population tracker bypassing the Growth Chamber"),
    ('TryStartPopulationReplacement("WNG_WraithQueen")', "automatic Queen replacement"),
]:
    if forbidden in pop:
        fail(f"Mature Hive population tracker must not use {description}: found {forbidden}")

for needle, description in [
    ("public bool TryStartPopulationReplacement(string pawnKindDefName)", "bounded NPC replacement entry point"),
    ("parent.Faction == Faction.OfPlayer", "NPC-only replacement entry point"),
    ('case "WNG_WraithHunter"', "Hunter replacement mapping"),
    ('case "WNG_WraithWarrior"', "Warrior replacement mapping"),
    ('case "WNG_WraithKeeper"', "Keeper replacement mapping"),
    ("completedPopulationClone = clone", "exact clone retention"),
    ("public Pawn TakeCompletedPopulationClone()", "exact clone claim API"),
    ("Scribe_References.Look(ref completedPopulationClone", "save-persistent exact completed clone"),
    ("parent.Faction == Faction.OfPlayer && !ResearchFinished", "player research gate without blocking NPC Hive technology"),
]:
    require(chamber, needle, description)

if 'case "WNG_WraithQueen"' in chamber:
    fail("Population replacement API must never map a Queen caste")
if "Gravcore" in chamber:
    fail("Mature Hive replacement must not depend on obsolete Gravcore logic")

if errors:
    print("Mature Wraith Hive population audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Mature Wraith Hive population audit OK")
