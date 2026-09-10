using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Save-persistent day-84 Queen-vault lifecycle.  The Queen is generated once into a real
    /// AncientCryptosleepCasket by the site worker.  The instant that exact pawn is actually spawned
    /// out of the casket she is aligned to the player; only then does the four-operative recovery
    /// attempt begin.  Later home-map recovery raids are separately scheduled while she remains with
    /// the player.
    /// </summary>
    public sealed class GameComponent_ReplicatorQueenQuestDirector : GameComponent
    {
        public const int QueenVaultDay = 84;
        public const int TicksPerDay = 60000;
        public const int InitialRecoveryWarningTicks = 600;
        public const int HomeCaptureRaidMinimumDelayTicks = 600000;   // 10 days; fresh balance value.
        public const int HomeCaptureRaidMaximumDelayTicks = 1080000;  // 18 days; fresh balance value.
        private const int ScanIntervalTicks = 120;
        private const int FailedRaidRetryTicks = 60000;
        private const int MinimumVaultDistance = 8;
        private const int MaximumVaultDistance = 20;

        private int nextScanTick;
        private int vaultSiteId = -1;
        private bool initialRecoveryWarningIssued;
        private int initialRecoveryDeployTick;
        private bool initialRecoveryDeployed;
        private int nextHomeCaptureRaidTick;

        public GameComponent_ReplicatorQueenQuestDirector(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            GameComponent_ReplicatorQueenState state = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();
            if (state == null || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;

            // Recruitment is deliberately checked every game tick so casket release does not create
            // a neutral/hostile grace period in which the unique Queen can be misclassified.
            TryRecruitReleasedQueenImmediately(state, now);
            TryCommitCompletedLatticeKidnap(state);

            if (now < nextScanTick)
                return;
            nextScanTick = now + ScanIntervalTicks;

            if (!state.QuestGenerated && now >= QueenVaultDay * TicksPerDay)
                TryCreateQueenVaultSite(state);

            ProcessInitialRecovery(state, now);
            ProcessHomeCaptureRaidSchedule(state, now);
        }

        private void TryRecruitReleasedQueenImmediately(GameComponent_ReplicatorQueenState state, int now)
        {
            Pawn queen = state.QueenPawn;
            if (queen == null || queen.Destroyed || queen.Dead || state.QueenAbducted || state.QueenJoinedPlayer)
                return;
            if (!queen.Spawned || !IsQueenVaultMap(queen.Map))
                return;

            state.MarkJoined(queen);
            if (!state.QueenJoinedPlayer)
                return;

            if (!initialRecoveryWarningIssued)
            {
                initialRecoveryWarningIssued = true;
                initialRecoveryDeployTick = SafeFutureTick(now, InitialRecoveryWarningTicks);
                Find.LetterStack.ReceiveLetter(
                    "Asuran recovery signal",
                    "The Replicator Queen has been recovered from cryosleep and has immediately joined your colony. A hostile Lattice recovery signal answered the chamber release. Four human-form Asuran recovery operatives are inbound and will try to subdue her, carry her away and leave the map with her alive.",
                    LetterDefOf.ThreatBig,
                    queen);
            }
        }

        private void ProcessInitialRecovery(GameComponent_ReplicatorQueenState state, int now)
        {
            if (!state.QueenJoinedPlayer || state.QueenAbducted || initialRecoveryDeployed || !initialRecoveryWarningIssued)
                return;
            if (now < initialRecoveryDeployTick)
                return;

            Pawn queen = state.QueenPawn;
            if (queen == null || queen.Dead || queen.Destroyed)
            {
                initialRecoveryDeployed = true;
                return;
            }

            Map map = queen.MapHeld;
            if (!IsQueenVaultMap(map))
            {
                // If the player manages to extract her from the vault during the very short warning
                // window, the local recovery force has missed its intercept.  Later home-map raids
                // remain available instead of teleporting the four operatives after her.
                initialRecoveryDeployed = true;
                ScheduleNextHomeCaptureRaid(now);
                return;
            }

            if (TrySpawnInitialRecoveryTeam(map, queen))
            {
                initialRecoveryDeployed = true;
                ScheduleNextHomeCaptureRaid(now);
            }
            else
            {
                initialRecoveryDeployTick = SafeFutureTick(now, InitialRecoveryWarningTicks);
            }
        }

        private void ProcessHomeCaptureRaidSchedule(GameComponent_ReplicatorQueenState state, int now)
        {
            if (!initialRecoveryDeployed || !state.QueenJoinedPlayer || state.QueenAbducted)
                return;

            Pawn queen = state.QueenPawn;
            if (queen == null || queen.Dead || queen.Destroyed)
                return;

            if (nextHomeCaptureRaidTick <= 0)
                ScheduleNextHomeCaptureRaid(now);
            if (now < nextHomeCaptureRaidTick)
                return;

            Map map = queen.MapHeld;
            if (map == null || !map.IsPlayerHome || !queen.Spawned || queen.Faction != Faction.OfPlayer)
            {
                nextHomeCaptureRaidTick = SafeFutureTick(now, FailedRaidRetryTicks);
                return;
            }

            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            if (lattice == null || map.mapPawns.AllPawnsSpawned.Any(pawn => pawn != null && !pawn.Dead && pawn.Faction == lattice))
            {
                nextHomeCaptureRaidTick = SafeFutureTick(now, FailedRaidRetryTicks);
                return;
            }

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("WNG_ReplicatorQueenCaptureRaid");
            if (incident == null)
            {
                nextHomeCaptureRaidTick = SafeFutureTick(now, FailedRaidRetryTicks);
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = lattice;
            if (incident.Worker.TryExecute(parms))
                ScheduleNextHomeCaptureRaid(now);
            else
                nextHomeCaptureRaidTick = SafeFutureTick(now, FailedRaidRetryTicks);
        }

        private static bool TrySpawnInitialRecoveryTeam(Map map, Pawn queen)
        {
            if (map == null || queen == null || queen.MapHeld != map)
                return false;

            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            if (lattice == null)
                return false;

            PawnKindDef[] kinds =
            {
                DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_HumanFormReplicatorSoldier"),
                DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_HumanFormReplicatorSoldier"),
                DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_HumanFormReplicatorInfiltrator"),
                DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_HumanFormReplicatorCoordinator")
            };
            if (kinds.Any(kind => kind == null))
                return false;

            if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entryCell, map, 0f))
                return false;

            List<Pawn> spawned = new List<Pawn>(4);
            try
            {
                for (int i = 0; i < kinds.Length; i++)
                {
                    Pawn pawn = PawnGenerator.GeneratePawn(kinds[i], lattice, map.Tile);
                    if (pawn == null)
                        throw new InvalidOperationException("recovery operative generation returned null");

                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(entryCell, map, 6);
                    GenSpawn.Spawn(pawn, cell, map);
                    if (!pawn.Spawned || pawn.Map != map)
                        throw new InvalidOperationException("recovery operative did not enter the vault map");
                    spawned.Add(pawn);
                }

                Lord lord = LordMaker.MakeNewLord(
                    lattice,
                    new LordJob_AssaultColony(lattice, canKidnap: true, canTimeoutOrFlee: true),
                    map);
                for (int i = 0; i < spawned.Count; i++)
                    lord.AddPawn(spawned[i]);

                map.GetComponent<MapComponent_ReplicatorQueenCaptureOperation>()?.Activate();
                Find.LetterStack.ReceiveLetter(
                    "Asuran Queen-recovery team",
                    "Four hostile human-form Replicators have entered the area. Their objective is the Replicator Queen: they will attempt to subdue her and physically kidnap her from the map rather than simply destroy the colony.",
                    LetterDefOf.ThreatBig,
                    queen);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Replicator Queen recovery-team deployment failed; partial operatives are being removed: " + ex);
                for (int i = 0; i < spawned.Count; i++)
                {
                    Pawn pawn = spawned[i];
                    if (pawn != null && !pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                }
                return false;
            }
        }

        private void TryCreateQueenVaultSite(GameComponent_ReplicatorQueenState state)
        {
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_ReplicatorQueenVault");
            if (siteDef == null || Find.WorldObjects == null)
                return;

            Site existing = Find.WorldObjects.AllWorldObjects
                .OfType<Site>()
                .FirstOrDefault(site => site != null && !site.Destroyed && site.parts != null
                    && site.parts.Any(part => part != null && part.def == siteDef));
            if (existing != null)
            {
                vaultSiteId = existing.ID;
                state.MarkQuestGenerated();
                return;
            }

            if (!TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    MinimumVaultDistance,
                    MaximumVaultDistance,
                    allowCaravans: false,
                    tileFinderMode: TileFinderMode.Near))
                return;

            Site site = SiteMaker.MakeSite(
                siteDef,
                tile,
                faction: null,
                ifHostileThenMustRemainHostile: false,
                threatPoints: 0f);
            if (site?.parts == null || site.parts.Count == 0)
                return;

            site.customLabel = "sealed Replicator Queen vault";
            Find.WorldObjects.Add(site);
            vaultSiteId = site.ID;
            state.MarkQuestGenerated();

            Find.LetterStack.ReceiveLetter(
                "Sealed precursor vault located",
                "Long-range analysis has located a sealed precursor vault. Internal life-sign and nanite telemetry indicate that a unique human-form Replicator has remained in cryosleep there for years. The site can now be investigated.",
                LetterDefOf.NeutralEvent,
                site);
        }

        private void TryCommitCompletedLatticeKidnap(GameComponent_ReplicatorQueenState state)
        {
            if (!state.QueenJoinedPlayer || state.QueenAbducted || state.QueenPawn == null)
                return;

            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            if (lattice?.kidnapped == null)
                return;

            // Vanilla adds the exact pawn to the faction's kidnapped tracker only after the carrier
            // actually leaves the map.  This is therefore the transaction boundary: downing or
            // picking the Queen up never commits the hostile outcome by itself.
            if (lattice.kidnapped.KidnappedPawnsListForReading.Contains(state.QueenPawn))
                state.MarkAbducted(state.QueenPawn, lattice);
        }

        private void ScheduleNextHomeCaptureRaid(int now)
        {
            int delay = Rand.RangeInclusive(HomeCaptureRaidMinimumDelayTicks, HomeCaptureRaidMaximumDelayTicks);
            nextHomeCaptureRaidTick = SafeFutureTick(now, delay);
        }

        private static bool IsQueenVaultMap(Map map)
        {
            Site site = map?.Parent as Site;
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_ReplicatorQueenVault");
            return site != null && siteDef != null && site.parts != null
                && site.parts.Any(part => part != null && part.def == siteDef);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(0, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextScanTick, "wngQueenQuestNextScanTick", 0);
            Scribe_Values.Look(ref vaultSiteId, "wngQueenVaultSiteId", -1);
            Scribe_Values.Look(ref initialRecoveryWarningIssued, "wngQueenInitialRecoveryWarningIssued", false);
            Scribe_Values.Look(ref initialRecoveryDeployTick, "wngQueenInitialRecoveryDeployTick", 0);
            Scribe_Values.Look(ref initialRecoveryDeployed, "wngQueenInitialRecoveryDeployed", false);
            Scribe_Values.Look(ref nextHomeCaptureRaidTick, "wngQueenNextHomeCaptureRaidTick", 0);
        }
    }

    /// <summary>
    /// Fresh Queen-vault site worker.  It creates one exact age-13 Queen and places her, unspawned,
    /// into a real vanilla AncientCryptosleepCasket.  Release is therefore a genuine container
    /// transaction; the game component observes the same pawn becoming spawned and recruits her.
    /// </summary>
    public sealed class SitePartWorker_ReplicatorQueenVault : SitePartWorker
    {
        private const int PlacementRadius = 16;
        private const int PlacementAttempts = 120;

        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
            parms.threatPoints = 0f;
            return parms;
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null)
                return;

            GameComponent_ReplicatorQueenState state = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();
            if (state == null || (state.QueenPawn != null && !state.QueenPawn.Destroyed))
                return;

            PawnKindDef queenKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorQueenChild");
            if (queenKind == null || ThingDefOf.AncientCryptosleepCasket == null)
                return;

            Building_AncientCryptosleepCasket casket = TrySpawnCasket(map);
            if (casket == null)
                return;

            Pawn queen = PawnGenerator.GeneratePawn(queenKind, null, map.Tile);
            if (queen == null)
            {
                casket.Destroy(DestroyMode.Vanish);
                return;
            }

            long exactAge = 13L * GenDate.TicksPerYear;
            queen.ageTracker.AgeBiologicalTicks = exactAge;
            queen.ageTracker.AgeChronologicalTicks = exactAge;
            ReplicatorQueenUtility.EnsureQueenAuthority(queen);

            if (!casket.TryAcceptThing(queen, allowSpecialEffects: false))
            {
                Log.Error("[WNG] Replicator Queen vault could not place the exact Queen pawn into the AncientCryptosleepCasket.");
                Find.WorldPawns.PassToWorld(queen, PawnDiscardDecideMode.Discard);
                casket.Destroy(DestroyMode.Vanish);
                return;
            }

            if (!state.RegisterQueen(queen))
                Log.Error("[WNG] Replicator Queen vault generated a casket occupant but could not register the exact Queen identity.");
        }

        private static Building_AncientCryptosleepCasket TrySpawnCasket(Map map)
        {
            for (int attempt = 0; attempt < PlacementAttempts; attempt++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                    map.Center,
                    map,
                    PlacementRadius,
                    candidate => candidate.InBounds(map)
                        && GenSpawn.CanSpawnAt(ThingDefOf.AncientCryptosleepCasket, candidate, map));
                if (!cell.IsValid || !GenSpawn.CanSpawnAt(ThingDefOf.AncientCryptosleepCasket, cell, map))
                    continue;

                Building_AncientCryptosleepCasket casket = ThingMaker.MakeThing(ThingDefOf.AncientCryptosleepCasket)
                    as Building_AncientCryptosleepCasket;
                if (casket == null)
                    return null;

                try
                {
                    GenSpawn.Spawn(casket, cell, map);
                    if (casket.Spawned)
                        return casket;
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Queen-vault cryosleep chamber placement retry failed: " + ex.Message);
                }

                if (!casket.Destroyed)
                    casket.Destroy(DestroyMode.Vanish);
            }

            Log.Error("[WNG] Replicator Queen vault could not place its AncientCryptosleepCasket; no Queen was generated.");
            return null;
        }
    }
}
