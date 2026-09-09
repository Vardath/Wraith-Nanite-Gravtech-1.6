#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptivity.cs"
CAPTURE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithCaptureUtility.cs"
RESCUE = ROOT / "Source" / "WNGR2" / "Wraith" / "WraithRescueSites.cs"
WORKER = ROOT / "Source" / "WNGR2" / "Wraith" / "SitePartWorker_WraithHoldingSite.cs"
DEFS = ROOT / "Defs" / "HediffDefs" / "Hediffs_WraithCaptivity.xml"
SITE_DEFS = ROOT / "Defs" / "SitePartDefs" / "WraithHoldingSite.xml"

errors = []

def require(text: str, needle: str, description: str) -> None:
    if needle not in text:
        errors.append(description)

def read_required(path: Path, description: str) -> str:
    if not path.exists():
        errors.append(description)
        return ""
    return path.read_text(encoding="utf-8", errors="replace")

source = read_required(SOURCE, "Missing WraithCaptivity.cs")
capture = read_required(CAPTURE, "Missing WraithCaptureUtility.cs")
rescue = read_required(RESCUE, "Missing WraithRescueSites.cs")
worker = read_required(WORKER, "Missing Wraith holding-site worker")
defs = read_required(DEFS, "Missing Wraith captivity Hediff defs")
site_defs = read_required(SITE_DEFS, "Missing Wraith holding-site Def")

for needle, description in [
    ("Scribe_References.Look(ref pawn", "Captivity record must persist the exact Pawn reference"),
    ("RegisterCapturedPawn(Pawn pawn, Faction captor)", "Captivity registry must register the captured pawn itself"),
    ("FindRecord(Pawn pawn)", "Captivity registry must resolve records by exact Pawn identity"),
    ("RecordRescueFailure(Pawn pawn)", "Failed rescue escalation must act on the same pawn"),
    ("FeedingStock = 0", "Captivity must declare feeding stock as the initial treatment stage"),
    ("Experimentation = 1", "Experimentation treatment stage is missing"),
    ("Conditioning = 2", "Conditioning treatment stage is missing"),
    ("Enthralled = 3", "Enthrallment treatment stage is missing"),
    ("RemoveStageHediffsExcept(pawn, \"WNG_WraithEnthralled\")", "Enthrallment must survive release"),
    ("Scribe_Collections.Look(ref records", "Captivity ledger must survive save/load"),
    ("FirstRescueTraceDelayTicks = 60000", "First rescue trace must be scheduled after 60000 ticks"),
    ("RescueRetryDelayTicks = 120000", "Missed rescue retry must be scheduled after 120000 ticks"),
    ("RescueSiteLifetimeTicks = 720000", "Rescue site lifetime must be 720000 ticks"),
    ("DueForRescueTrace(int now)", "Registry must expose due exact-captive rescue traces"),
    ("MarkRescueSiteOpened(Pawn pawn, int siteId, int now)", "Registry must bind a rescue window to the exact captive and exact site"),
    ("Scribe_Values.Look(ref activeRescueSiteId", "Active rescue site identity must survive save/load"),
    ("record.activeRescueSiteId = siteId", "Exact rescue site ID must be recorded on the captive record"),
    ("MarkRescueSiteMissed(Pawn pawn, int now)", "Registry must escalate and reschedule the exact captive after a missed rescue"),
    ("record.nextRescueTraceTick = SafeFutureTick(now, RescueRetryDelayTicks)", "Missed rescue must reschedule rather than delete the captive"),
]:
    require(source, needle, description)

for needle, description in [
    ("IsValidAbductionTarget(Pawn pawn)", "Capture layer must validate the actual target pawn"),
    ("pawn.RaceProps.Humanlike", "Capture targets must be humanlike"),
    ("pawn.RaceProps.IsFlesh", "Capture targets must be biological/flesh"),
    ("pawn.Faction == Faction.OfPlayer", "Capture targets must include player colony pawns"),
    ("pawn.guest?.IsPrisoner == true", "Capture targets may include prisoners"),
    ("WraithLifeForceUtility.Get(pawn) != null", "Wraith targets must be excluded"),
    ("LooksSynthetic", "Synthetic/Replicator/Asuran targets must be excluded"),
    ("registry.RegisterCapturedPawn(pawn, captor)", "Capture hand-off must register the exact pawn"),
]:
    require(capture, needle, description)

for needle, description in [
    ("SiteMinDistance = 6", "Holding sites must start at least 6 world tiles away"),
    ("SiteMaxDistance = 18", "Holding sites must be bounded to 18 world tiles away"),
    ("new ThingOwner<Pawn>(part, oneStackOnly: true)", "Holding site must own the exact captive before map generation"),
    ("part.things.TryAdd(record.pawn)", "Holding site must transfer the exact captured pawn"),
    ("registry.MarkRescueSiteOpened(record.pawn, site.ID, now)", "Holding site must bind the exact captive to the exact site ID"),
    ("if (site.HasMap)", "An actively opened rescue-site map must defer expiry"),
    ("ActiveMapExpiryDeferralTicks = 2500", "Active rescue-site expiry deferral must remain bounded"),
    ("registry.MarkRescueSiteMissed(record.pawn, now)", "Expired rescue site must escalate the same exact captive"),
    ("registry.ReleaseExactPawn(record.pawn)", "Recovery must release the exact captured pawn"),
]:
    require(rescue, needle, description)

for needle, description in [
    ("MinDefenders = 3", "Holding-site defender minimum must be 3"),
    ("MaxDefenders = 10", "Holding-site defender maximum must be 10"),
    ("WNG_WraithKeeper", "Holding-site detail must include a Keeper custodian"),
    ("WNG_WraithHunter", "Holding-site detail must include Hunters"),
    ("WNG_WraithWarrior", "Holding-site detail must include Warriors"),
    ("WNG_WraithCommander", "Harder holding-site attempts must support a Commander"),
    ("record.stage >= WraithCaptiveTreatmentStage.Conditioning || record.rescueFailures >= 2", "Commander must be restricted to later/repeated rescue attempts"),
    ("Math.Min(MaxDefenders", "Holding-site guard count must be capped"),
]:
    require(worker, needle, description)

if "WNG_WraithQueen" in worker:
    errors.append("Wraith Queens must never be generated as holding-site guards")

for needle, description in [
    ("<defName>WNG_WraithHoldingSite</defName>", "Missing Wraith holding-site SitePartDef"),
    ("<workerClass>WraithNaniteGravtech.SitePartWorker_WraithHoldingSite</workerClass>", "Holding site must use the bounded Wraith defender worker"),
    ("<wantsThreatPoints>true</wantsThreatPoints>", "Holding site must receive its threat budget"),
    ("<genStep Class=\"GenStep_PrisonerWillingToJoin\">", "Holding site must use the exact-pawn-aware prisoner-cell gen step"),
]:
    require(site_defs, needle, description)

for def_name in [
    "WNG_WraithFeedingStock",
    "WNG_WraithExperimentation",
    "WNG_WraithConditioning",
    "WNG_WraithEnthralled",
]:
    require(defs, f"<defName>{def_name}</defName>", f"Missing captivity HediffDef {def_name}")

# The identity ledger, capture boundary and rescue manager must never fabricate a substitute captive.
# Pawn generation is permitted only in the dedicated defender worker, where it creates hostile guards.
for forbidden in ["PawnGenerator.GeneratePawn", "PawnGenerator.TryGenerateNewPawnInternal"]:
    if forbidden in source or forbidden in capture or forbidden in rescue:
        errors.append("Wraith captivity/capture/rescue identity code must never generate a replacement captive")

if errors:
    print("Wraith captivity audit FAILED")
    for error in errors:
        print("- " + error)
    sys.exit(1)

print("Wraith captivity audit OK")
