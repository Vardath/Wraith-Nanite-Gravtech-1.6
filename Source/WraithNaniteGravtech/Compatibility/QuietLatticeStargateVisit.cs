using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    internal static class QuietLatticeStargateVisitUtility
    {
        public const string QuietFactionDefName = "WNG_HumanFormEnclave";
        public const string StargateArrivalModeDefName = "StargateMod_StargateEnterMode";
        public const string HumanFormKindDefName = "WNG_HumanFormReplicator";
        public const string EngineerKindDefName = "WNG_PrecursorEngineer";
        public const string CourierDefName = "WNG_PuddleJumper_NPC";
        public const int DelegationSize = 3;
        public const int BoardingOrderIntervalTicks = 180;
        public const int BoardingGraceTicks = 1800;

        public static Faction QuietFaction
        {
            get
            {
                FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(QuietFactionDefName);
                return def == null ? null : Find.FactionManager.FirstFactionOfDef(def);
            }
        }

        public static PawnsArrivalModeDef StargateArrivalMode =>
            DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail(StargateArrivalModeDefName);

        public static bool HasUsableStargate(Map map, Faction faction)
        {
            PawnsArrivalModeDef mode = StargateArrivalMode;
            if (map == null || faction == null || mode?.Worker == null)
                return false;

            IncidentParms probe = new IncidentParms
            {
                target = map,
                faction = faction,
                raidArrivalMode = mode
            };

            try
            {
                return mode.Worker.TryResolveRaidSpawnCenter(probe) && probe.raidArrivalMode == mode;
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] CatCraft Stargate availability probe failed safely: " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// Optional CatCraft integration with no compile-time CatCraft reference. CatCraft's own
    /// PawnsArrivalModeDef resolves the gate, opens it and buffers the exact Pawn objects. WNG
    /// supplies only the Quiet-Lattice visit and later Odyssey-native physical courier departure.
    /// </summary>
    public sealed class IncidentWorker_QuietLatticeStargateVisit : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!(parms.target is Map map) || !map.IsPlayerHome)
                return false;

            MapComponent_QuietLatticeStargateVisit component = map.GetComponent<MapComponent_QuietLatticeStargateVisit>();
            if (component == null || component.HasActiveVisit)
                return false;

            Faction quiet = QuietLatticeStargateVisitUtility.QuietFaction;
            if (quiet == null || quiet.HostileTo(Faction.OfPlayer))
                return false;

            if (DefDatabase<ThingDef>.GetNamedSilentFail(QuietLatticeStargateVisitUtility.CourierDefName) == null)
                return false;

            foreach (GameCondition condition in map.GameConditionManager.ActiveConditions)
                if (condition?.def?.preventNeutralVisitors == true)
                    return false;

            return QuietLatticeStargateVisitUtility.HasUsableStargate(map, quiet) && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map) || !map.IsPlayerHome)
                return false;

            MapComponent_QuietLatticeStargateVisit component = map.GetComponent<MapComponent_QuietLatticeStargateVisit>();
            Faction quiet = QuietLatticeStargateVisitUtility.QuietFaction;
            PawnsArrivalModeDef stargateMode = QuietLatticeStargateVisitUtility.StargateArrivalMode;
            if (component == null || component.HasActiveVisit || quiet == null || quiet.HostileTo(Faction.OfPlayer) || stargateMode?.Worker == null)
                return false;

            parms.faction = quiet;
            parms.raidArrivalMode = stargateMode;
            try
            {
                if (!stargateMode.Worker.TryResolveRaidSpawnCenter(parms) || parms.raidArrivalMode != stargateMode)
                    return false; // CatCraft fell back to edge arrival: do not mislabel it a Stargate visit.
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Quiet Lattice Stargate visit could not resolve a CatCraft gate: " + ex.Message);
                return false;
            }

            List<Pawn> visitors = GenerateDelegation(quiet);
            if (visitors.Count != QuietLatticeStargateVisitUtility.DelegationSize)
            {
                DestroyUncommitted(visitors);
                return false;
            }

            Lord lord = null;
            try
            {
                stargateMode.Worker.Arrive(visitors, parms);
                IntVec3 chillSpot = CellFinder.RandomClosewalkCellNear(map.Center, map, 18);
                lord = LordMaker.MakeNewLord(
                    quiet,
                    new LordJob_VisitColony(quiet, chillSpot, 60000),
                    map,
                    visitors);

                int duration = Rand.RangeInclusive(12000, 18000);
                if (!component.TryBeginVisit(visitors, lord, duration))
                    throw new InvalidOperationException("Map visit component rejected a newly committed delegation.");
            }
            catch (Exception ex)
            {
                // CatCraft may already own buffered visitors once Arrive succeeds. Never generate
                // replacement pawns or silently retry the same transaction in this execution.
                Log.Warning("[WNG] Quiet Lattice Stargate visit failed during commit; exact generated visitors were not replaced: " + ex.Message);
                return true;
            }

            try
            {
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, new TargetInfo(parms.spawnCenter, map));
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Quiet Lattice Stargate visit committed but arrival presentation failed: " + ex.Message);
            }
            return true;
        }

        private static List<Pawn> GenerateDelegation(Faction faction)
        {
            List<Pawn> pawns = new List<Pawn>();
            PawnKindDef engineer = DefDatabase<PawnKindDef>.GetNamedSilentFail(QuietLatticeStargateVisitUtility.EngineerKindDefName);
            PawnKindDef humanForm = DefDatabase<PawnKindDef>.GetNamedSilentFail(QuietLatticeStargateVisitUtility.HumanFormKindDefName);
            if (engineer == null || humanForm == null)
                return pawns;

            try
            {
                pawns.Add(PawnGenerator.GeneratePawn(engineer, faction));
                pawns.Add(PawnGenerator.GeneratePawn(humanForm, faction));
                pawns.Add(PawnGenerator.GeneratePawn(humanForm, faction));
                pawns.RemoveAll(p => p == null);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Quiet Lattice delegation generation failed: " + ex.Message);
            }
            return pawns;
        }

        private static void DestroyUncommitted(IEnumerable<Pawn> pawns)
        {
            foreach (Pawn pawn in pawns ?? Enumerable.Empty<Pawn>())
                if (pawn != null && !pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);
        }
    }

    /// <summary>
    /// Tracks the exact CatCraft-arrived visitor objects until their separate physical Puddle
    /// Jumper extraction. The component never dials a gate, owns an address or touches CatCraft
    /// receive-buffer internals.
    /// </summary>
    public sealed class MapComponent_QuietLatticeStargateVisit : MapComponent
    {
        private List<Pawn> visitors = new List<Pawn>();
        private Lord visitorLord;
        private Building_PassengerShuttle courier;
        private int visitDurationTicks;
        private int visitStartedTick = -1;
        private int boardingStartedTick = -1;
        private int nextBoardingOrderTick;
        private bool launchIssued;

        public MapComponent_QuietLatticeStargateVisit(Map map) : base(map) { }

        public bool HasActiveVisit =>
            (visitors != null && visitors.Any(p => p != null && !p.Dead && !p.Destroyed)) ||
            (courier != null && !courier.Destroyed);

        public bool TryBeginVisit(List<Pawn> exactVisitors, Lord lord, int durationTicks)
        {
            if (HasActiveVisit || exactVisitors == null || exactVisitors.Count == 0 || lord == null)
                return false;

            visitors = new List<Pawn>(exactVisitors);
            visitorLord = lord;
            courier = null;
            visitDurationTicks = Math.Max(6000, durationTicks);
            visitStartedTick = -1;
            boardingStartedTick = -1;
            nextBoardingOrderTick = 0;
            launchIssued = false;
            return true;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!map.IsHashIntervalTick(60) || visitors == null || visitors.Count == 0)
                return;

            visitors.RemoveAll(p => p == null || p.Dead || p.Destroyed);
            if (visitors.Count == 0)
            {
                ClearVisit();
                return;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            List<Pawn> spawnedHere = visitors.Where(p => p.Spawned && p.Map == map).ToList();

            // CatCraft buffers the exact Pawn objects during its dial sequence. Start the visit
            // clock only once all still-living delegation members have physically emerged.
            if (visitStartedTick < 0)
            {
                if (spawnedHere.Count != visitors.Count)
                    return;
                visitStartedTick = now;
            }

            Faction quiet = QuietLatticeStargateVisitUtility.QuietFaction;
            bool relationsFailed = quiet == null || quiet.HostileTo(Faction.OfPlayer);
            bool scheduledDeparture = now - visitStartedTick >= visitDurationTicks;

            if (courier == null)
            {
                if (!relationsFailed && !scheduledDeparture)
                    return;
                if (!TryStageCourier(quiet ?? visitors[0].Faction, spawnedHere, now))
                {
                    // If a physical courier cannot be staged, return the survivors to native
                    // visitor exit behavior rather than inventing a second transport system.
                    RestoreNativeExitForStrandedVisitors(spawnedHere, quiet ?? visitors[0].Faction);
                    ClearVisit();
                }
                return;
            }

            if (courier.Destroyed || !courier.Spawned)
            {
                if (!launchIssued)
                {
                    RestoreNativeExitForStrandedVisitors(
                        visitors.Where(p => p.Spawned && p.Map == map).ToList(),
                        quiet ?? visitors.FirstOrDefault()?.Faction);
                }
                ClearVisit();
                return;
            }

            if (now >= nextBoardingOrderTick)
            {
                nextBoardingOrderTick = now + QuietLatticeStargateVisitUtility.BoardingOrderIntervalTicks;
                DirectVisitorsToCourier();
            }

            CompTransporter transporter = courier.TryGetComp<CompTransporter>();
            if (transporter == null)
                return;

            bool allBoardableLoaded = visitors
                .Where(p => EligibleForCourier(p) && !p.Downed)
                .All(p => transporter.GetDirectlyHeldThings().Contains(p));
            bool graceExpired = boardingStartedTick >= 0 && now - boardingStartedTick >= QuietLatticeStargateVisitUtility.BoardingGraceTicks;

            if (allBoardableLoaded || graceExpired)
                TryLaunchCourier(transporter);
        }

        private bool TryStageCourier(Faction faction, List<Pawn> spawnedVisitors, int now)
        {
            ThingDef courierDef = DefDatabase<ThingDef>.GetNamedSilentFail(QuietLatticeStargateVisitUtility.CourierDefName);
            if (courierDef == null || faction == null || spawnedVisitors == null || spawnedVisitors.Count == 0)
                return false;

            Building_PassengerShuttle staged = null;
            try
            {
                if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, 0f))
                    entry = CellFinder.RandomEdgeCell(map);

                staged = ThingMaker.MakeThing(courierDef) as Building_PassengerShuttle;
                if (staged == null)
                    throw new InvalidOperationException("NPC Puddle Jumper Def did not create Building_PassengerShuttle.");
                staged.SetFaction(faction);
                if (!GenPlace.TryPlaceThing(staged, entry, map, ThingPlaceMode.Near, rot: Rot4.East, squareRadius: 10))
                    throw new InvalidOperationException("No valid placement for Quiet Lattice Puddle Jumper courier.");

                CompRefuelable fuel = staged.TryGetComp<CompRefuelable>();
                CompTransporter transporter = staged.TryGetComp<CompTransporter>();
                CompShuttle shuttle = staged.TryGetComp<CompShuttle>();
                if (fuel == null || transporter == null || shuttle == null || staged.TryGetComp<CompLaunchable>() == null)
                    throw new InvalidOperationException("NPC Puddle Jumper is missing an Odyssey shuttle contract component.");

                fuel.Refuel(fuel.Props.fuelCapacity);
                shuttle.requiredPawns.Clear();
                foreach (Pawn pawn in spawnedVisitors)
                {
                    if (EligibleForCourier(pawn))
                        shuttle.requiredPawns.AddUnique(pawn);
                }
                TransporterUtility.InitiateLoading(Gen.YieldSingle(transporter));
            }
            catch (Exception ex)
            {
                if (staged != null && !staged.Destroyed)
                    staged.Destroy(DestroyMode.Vanish);
                Log.Warning("[WNG] Quiet Lattice return courier could not stage: " + ex.Message);
                return false;
            }

            courier = staged;
            boardingStartedTick = now;
            nextBoardingOrderTick = now;
            launchIssued = false;

            foreach (Pawn pawn in spawnedVisitors)
                pawn.GetLord()?.RemovePawn(pawn);

            try
            {
                Messages.Message("The Quiet Lattice delegation's Puddle Jumper courier has arrived for departure.", courier, MessageTypeDefOf.NeutralEvent, false);
            }
            catch { }
            return true;
        }

        private bool EligibleForCourier(Pawn pawn)
        {
            // Player custody is a real state transition, not something this visit may silently
            // override. Arrested/enslaved delegates remain on the map and are not re-lorded.
            return pawn != null && !pawn.Dead && !pawn.Destroyed && pawn.Spawned && pawn.Map == map &&
                !pawn.IsPrisoner && !pawn.IsSlave;
        }

        private void DirectVisitorsToCourier()
        {
            if (courier == null || courier.Destroyed || !courier.Spawned)
                return;

            foreach (Pawn pawn in visitors)
            {
                if (!EligibleForCourier(pawn) || pawn.Downed || pawn.jobs == null)
                    continue;
                if (pawn.CurJobDef == JobDefOf.EnterTransporter && pawn.CurJob?.targetA.Thing == courier)
                    continue;

                Job board = JobMaker.MakeJob(JobDefOf.EnterTransporter, courier);
                board.playerForced = true;
                pawn.jobs.TryTakeOrderedJob(board, JobTag.Misc);
            }
        }

        private void TryLaunchCourier(CompTransporter transporter)
        {
            if (courier == null || courier.Destroyed || !courier.Spawned || transporter == null || launchIssued)
                return;

            CompShuttle shuttle = courier.TryGetComp<CompShuttle>();
            if (shuttle == null)
                return;

            CompLaunchable launchable = courier.TryGetComp<CompLaunchable>();
            if (launchable == null || !launchable.CanLaunch().Accepted)
                return;

            // Any living visitor who is still physically on the colony map at the actual launch
            // boundary must not be abandoned without a Lord. This normally means a downed
            // visitor, or a healthy delegate that failed to board before the grace timeout.
            List<Pawn> stranded = visitors
                .Where(p => EligibleForCourier(p) && !transporter.GetDirectlyHeldThings().Contains(p))
                .ToList();
            if (stranded.Count > 0)
            {
                RestoreNativeExitForStrandedVisitors(stranded,
                    QuietLatticeStargateVisitUtility.QuietFaction ?? stranded[0].Faction);
            }

            shuttle.SetPawnToLeaveBehind(p =>
                p == null || p.Dead || p.Downed || !transporter.GetDirectlyHeldThings().Contains(p));

            List<Pawn> boardedBeforeLaunch = visitors
                .Where(p => p != null && transporter.GetDirectlyHeldThings().Contains(p))
                .ToList();
            PlanetTile destination = FindDepartureDestination();
            IntVec3 departureCell = courier.Position;
            launchIssued = true;
            Exception launchException = null;
            try
            {
                launchable.TryLaunch(destination, new TransportersArrivalAction_QuietLatticeDeparture());
            }
            catch (Exception ex)
            {
                launchException = ex;
            }

            // Match the existing exact-Queen carrier boundary: a launch only commits when the
            // physical shuttle has left and every visitor that was aboard has moved out of the
            // old map transporter into native flight/transit ownership.
            bool departedWithBoardedVisitors = !courier.Spawned && boardedBeforeLaunch.All(p =>
                p != null && !p.Spawned && p.ParentHolder != null &&
                !transporter.GetDirectlyHeldThings().Contains(p));
            if (!departedWithBoardedVisitors)
            {
                launchIssued = false;
                if (launchException != null)
                    Log.Warning("[WNG] Quiet Lattice courier launch failed before physical departure: " + launchException.Message);
                return;
            }

            if (launchException != null)
                Log.Warning("[WNG] Quiet Lattice courier reported an exception after physical departure; the exact boarded visitors remain in native transport: " + launchException.Message);

            try
            {
                Messages.Message("The Quiet Lattice Puddle Jumper has departed with the delegation members who reached the courier.", new TargetInfo(departureCell, map), MessageTypeDefOf.NeutralEvent, false);
            }
            catch { }
            ClearVisit();
        }

        private PlanetTile FindDepartureDestination()
        {
            if (TileFinder.TryFindNewSiteTile(out PlanetTile destination, 1, 4, allowCaravans: false))
                return destination;
            return map.Tile;
        }

        private void RestoreNativeExitForStrandedVisitors(List<Pawn> stranded, Faction faction)
        {
            if (stranded == null || stranded.Count == 0 || faction == null)
                return;
            List<Pawn> unassigned = stranded
                .Where(p => EligibleForCourier(p) && p.GetLord() == null)
                .ToList();
            if (unassigned.Count == 0)
                return;
            try
            {
                IntVec3 chillSpot = CellFinder.RandomClosewalkCellNear(map.Center, map, 18);
                LordMaker.MakeNewLord(faction, new LordJob_VisitColony(faction, chillSpot, 600), map, unassigned);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Stranded Quiet Lattice visitors could not be returned to native visitor exit behavior: " + ex.Message);
            }
        }

        private void ClearVisit()
        {
            visitors = visitors ?? new List<Pawn>();
            visitors.Clear();
            visitorLord = null;
            courier = null;
            visitDurationTicks = 0;
            visitStartedTick = -1;
            boardingStartedTick = -1;
            nextBoardingOrderTick = 0;
            launchIssued = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref visitors, "wngQuietLatticeVisitors", LookMode.Reference);
            Scribe_References.Look(ref visitorLord, "wngQuietLatticeVisitorLord");
            Scribe_References.Look(ref courier, "wngQuietLatticeCourier");
            Scribe_Values.Look(ref visitDurationTicks, "wngQuietLatticeDurationTicks", 0);
            Scribe_Values.Look(ref visitStartedTick, "wngQuietLatticeVisitStartedTick", -1);
            Scribe_Values.Look(ref boardingStartedTick, "wngQuietLatticeBoardingStartedTick", -1);
            Scribe_Values.Look(ref nextBoardingOrderTick, "wngQuietLatticeNextBoardingOrderTick", 0);
            Scribe_Values.Look(ref launchIssued, "wngQuietLatticeLaunchIssued", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                visitors = visitors ?? new List<Pawn>();
        }
    }

    /// <summary>
    /// Off-map cleanup for the NPC visitor flight. The exact Pawns are removed from the active
    /// transporter and handed to the world-pawn manager; no proxy visitors are generated.
    /// </summary>
    public sealed class TransportersArrivalAction_QuietLatticeDeparture : TransportersArrivalAction
    {
        public override bool GeneratesMap => false;

        public override void Arrived(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            foreach (ActiveTransporterInfo info in transporters ?? new List<ActiveTransporterInfo>())
            {
                ThingOwner contents = info?.innerContainer;
                if (contents == null)
                    continue;
                foreach (Pawn pawn in contents.OfType<Pawn>().ToList())
                {
                    contents.Remove(pawn);
                    if (!Find.WorldPawns.Contains(pawn))
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Decide);
                }
                contents.ClearAndDestroyContents();
            }
        }

        public override void ExposeData() { }
    }
}
