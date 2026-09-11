using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public enum ReplicatorQueenStatus
    {
        NotLocated,
        VaultLocated,
        Dormant,
        Released,
        RecoveryActive,
        CapturedByAsurans,
        Dead
    }

    public sealed class GameComponent_ReplicatorQueenState : GameComponent
    {
        private Pawn queen;
        private ReplicatorQueenStatus status = ReplicatorQueenStatus.NotLocated;
        private int vaultSiteId = -1;
        private int recoveryTick = -1;
        private bool initialRecoverySpawned;
        private int recoveryOperativeCount = 4;
        private int recoverySubdualWarmupTicks = 120;
        private int recoverySubdualStunTicks = 1500;
        private int recoveryBoardingTimeoutTicks = 1800;
        private int nextMaintenanceTick;

        public GameComponent_ReplicatorQueenState(Game game) { }

        public static GameComponent_ReplicatorQueenState Current =>
            Verse.Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();

        public Pawn Queen => queen;
        public ReplicatorQueenStatus Status => status;
        public bool CanLocateVault => queen == null && status == ReplicatorQueenStatus.NotLocated;

        public void MarkVaultLocated(int siteId)
        {
            if (!CanLocateVault)
                return;
            vaultSiteId = siteId;
            status = ReplicatorQueenStatus.VaultLocated;
        }

        public bool RegisterDormantQueen(Pawn exactQueen, int siteId)
        {
            if (exactQueen == null || exactQueen.Dead)
                return false;
            if (queen != null && queen != exactQueen)
                return false;

            queen = exactQueen;
            vaultSiteId = siteId;
            status = ReplicatorQueenStatus.Dormant;
            return true;
        }

        public bool MarkReleasedAndScheduleRecovery(
            Pawn exactQueen,
            int recoveryDelayTicks,
            int operativeCount,
            int subdualWarmupTicks,
            int subdualStunTicks,
            int boardingTimeoutTicks)
        {
            if (exactQueen == null || queen != exactQueen || exactQueen.Dead || status != ReplicatorQueenStatus.Dormant)
                return false;

            status = ReplicatorQueenStatus.Released;
            initialRecoverySpawned = false;
            recoveryOperativeCount = Math.Max(1, operativeCount);
            recoverySubdualWarmupTicks = Math.Max(30, subdualWarmupTicks);
            recoverySubdualStunTicks = Math.Max(60, subdualStunTicks);
            recoveryBoardingTimeoutTicks = Math.Max(300, boardingTimeoutTicks);
            recoveryTick = SafeFutureTick(
                Find.TickManager?.TicksGame ?? 0,
                Math.Max(60, recoveryDelayTicks));
            return true;
        }

        public bool MarkRecoveryActive(Pawn exactQueen)
        {
            if (exactQueen == null || queen != exactQueen || exactQueen.Dead)
                return false;
            status = ReplicatorQueenStatus.RecoveryActive;
            return true;
        }

        public bool CommitAsuranCapture(Pawn exactQueen, Faction captor, Pawn kidnapper)
        {
            if (exactQueen == null || queen != exactQueen || exactQueen.Dead || captor?.kidnapped == null)
                return false;

            KidnappedPawnsTracker kidnapped = captor.kidnapped;
            if (!kidnapped.KidnappedPawnsListForReading.Contains(exactQueen))
                kidnapped.Kidnap(exactQueen, kidnapper);

            if (!kidnapped.KidnappedPawnsListForReading.Contains(exactQueen))
                return false;

            status = ReplicatorQueenStatus.CapturedByAsurans;
            vaultSiteId = -1;
            recoveryTick = -1;
            return true;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextMaintenanceTick)
                return;
            nextMaintenanceTick = now + 2500;

            if (queen != null && queen.Dead)
            {
                status = ReplicatorQueenStatus.Dead;
                vaultSiteId = -1;
                recoveryTick = -1;
                return;
            }

            if (status == ReplicatorQueenStatus.Released &&
                !initialRecoverySpawned &&
                recoveryTick >= 0 &&
                now >= recoveryTick &&
                queen?.Spawned == true &&
                queen.Map != null)
            {
                if (ReplicatorQueenRecoveryUtility.TrySpawnRecoveryTeam(
                        queen,
                        recoveryOperativeCount,
                        recoverySubdualWarmupTicks,
                        recoverySubdualStunTicks,
                        recoveryBoardingTimeoutTicks))
                {
                    initialRecoverySpawned = true;
                    recoveryTick = -1;
                    status = ReplicatorQueenStatus.RecoveryActive;
                }
            }

            if (status == ReplicatorQueenStatus.VaultLocated && queen == null && vaultSiteId >= 0)
            {
                WorldObject site = Find.WorldObjects?.AllWorldObjects
                    .FirstOrDefault(w => w != null && !w.Destroyed && w.ID == vaultSiteId);
                if (site == null)
                {
                    status = ReplicatorQueenStatus.NotLocated;
                    vaultSiteId = -1;
                }
            }
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref queen, "wngReplicatorQueen");
            Scribe_Values.Look(ref status, "wngReplicatorQueenStatus", ReplicatorQueenStatus.NotLocated);
            Scribe_Values.Look(ref vaultSiteId, "wngReplicatorQueenVaultSiteId", -1);
            Scribe_Values.Look(ref recoveryTick, "wngReplicatorQueenRecoveryTick", -1);
            Scribe_Values.Look(ref initialRecoverySpawned, "wngReplicatorQueenInitialRecoverySpawned", false);
            Scribe_Values.Look(ref recoveryOperativeCount, "wngReplicatorQueenRecoveryOperativeCount", 4);
            Scribe_Values.Look(ref recoverySubdualWarmupTicks, "wngReplicatorQueenRecoveryWarmupTicks", 120);
            Scribe_Values.Look(ref recoverySubdualStunTicks, "wngReplicatorQueenRecoveryStunTicks", 1500);
            Scribe_Values.Look(ref recoveryBoardingTimeoutTicks, "wngReplicatorQueenRecoveryBoardingTimeoutTicks", 1800);
            Scribe_Values.Look(ref nextMaintenanceTick, "wngReplicatorQueenNextMaintenanceTick", 0);
        }
    }

    public sealed class ReplicatorQueenVaultExtension : DefModExtension
    {
        public int recoveryDelayTicks = 1800;
        public int operativeCount = 4;
        public int subdualWarmupTicks = 120;
        public int subdualStunTicks = 1500;
        public int boardingTimeoutTicks = 1800;
    }

    public sealed class ReplicatorQueenVaultIncidentExtension : DefModExtension
    {
        public int minSiteDistance = 8;
        public int maxSiteDistance = 24;
        public int maxActiveSites = 1;
    }

    public sealed class SitePartWorker_ReplicatorQueenVault : SitePartWorker
    {
        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
            parms.threatPoints = 0f;
            return parms;
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            Site site = map?.Parent as Site;
            GameComponent_ReplicatorQueenState state = GameComponent_ReplicatorQueenState.Current;
            if (map == null || site == null || state == null)
                return;

            if (state.Queen != null)
            {
                if (state.Status == ReplicatorQueenStatus.Dormant && state.Queen.Spawned && state.Queen.Map == map)
                    map.GetComponent<MapComponent_ReplicatorQueenRecovery>()?.ArmExistingQueen(state.Queen);
                return;
            }

            PawnKindDef queenKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorQueen");
            XenotypeDef xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail("WNG_NaniteHumanoid");
            ThingDef casketDef = DefDatabase<ThingDef>.GetNamedSilentFail("CryptosleepCasket");
            if (queenKind == null || xenotype == null || casketDef == null)
            {
                Log.Error("[WNG] Replicator Queen vault generation aborted because Queen/xenotype/cryptosleep Defs are unavailable.");
                return;
            }

            IntVec3 center = CellFinder.RandomClosewalkCellNear(
                map.Center, map, 8, c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map));
            if (!center.IsValid || !GenSpawn.CanSpawnAt(casketDef, center, map))
            {
                Log.Error("[WNG] Replicator Queen vault could not find a valid cryptosleep casket cell.");
                return;
            }

            ClearVaultCell(center, map);

            PawnGenerationRequest request = new PawnGenerationRequest(
                queenKind,
                faction: null,
                context: PawnGenerationContext.NonPlayer,
                tile: map.Tile,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false,
                allowPregnant: false,
                certainlyBeenInCryptosleep: true,
                fixedBiologicalAge: 13f,
                fixedChronologicalAge: 13f,
                fixedGender: Gender.Female,
                forceNoIdeo: true,
                forceNoBackstory: true,
                forcedXenotype: xenotype,
                developmentalStages: DevelopmentalStage.Child | DevelopmentalStage.Adult,
                forceRecruitable: true,
                dontGiveWeapon: true,
                forceNoGear: true);

            Pawn queen = PawnGenerator.GeneratePawn(request);
            if (queen == null)
            {
                Log.Error("[WNG] Replicator Queen vault failed to generate the exact Queen pawn.");
                return;
            }

            Building_CryptosleepCasket casket = ThingMaker.MakeThing(casketDef) as Building_CryptosleepCasket;
            if (casket == null)
            {
                queen.Destroy(DestroyMode.Vanish);
                Log.Error("[WNG] Replicator Queen vault failed to instantiate a real cryptosleep casket.");
                return;
            }

            casket.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(casket, center, map, Rot4.North);

            if (!casket.TryAcceptThing(queen))
            {
                if (!casket.Destroyed)
                    casket.Destroy(DestroyMode.Vanish);
                if (!queen.Destroyed)
                    queen.Destroy(DestroyMode.Vanish);
                Log.Error("[WNG] Replicator Queen could not be placed in the real cryptosleep casket.");
                return;
            }

            if (!state.RegisterDormantQueen(queen, site.ID))
            {
                casket.EjectContents();
                if (!queen.Destroyed)
                    queen.Destroy(DestroyMode.Vanish);
                if (!casket.Destroyed)
                    casket.Destroy(DestroyMode.Vanish);
                Log.Error("[WNG] Replicator Queen state rejected a duplicate Queen identity.");
                return;
            }

            map.GetComponent<MapComponent_ReplicatorQueenRecovery>()?.ArmExistingQueen(queen);
        }

        private static void ClearVaultCell(IntVec3 cell, Map map)
        {
            foreach (Thing thing in cell.GetThingList(map).ToList())
            {
                if (thing is Pawn)
                    continue;
                if (thing.def.category == ThingCategory.Building ||
                    thing.def.category == ThingCategory.Plant ||
                    thing.def.category == ThingCategory.Item)
                {
                    if (!thing.Destroyed)
                        thing.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }

    public sealed class MapComponent_ReplicatorQueenRecovery : MapComponent
    {
        private Pawn queen;
        private bool releaseHandled;

        public MapComponent_ReplicatorQueenRecovery(Map map) : base(map) { }

        public void ArmExistingQueen(Pawn exactQueen)
        {
            if (exactQueen != null)
                queen = exactQueen;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            GameComponent_ReplicatorQueenState state = GameComponent_ReplicatorQueenState.Current;
            if (state == null)
                return;
            if (queen == null)
                queen = state.Queen;
            if (queen == null || queen.Dead)
                return;

            if (!releaseHandled &&
                state.Status == ReplicatorQueenStatus.Dormant &&
                queen.Spawned && queen.Map == map && IsQueenVaultMap())
            {
                HandleRelease(state);
            }
        }

        private bool IsQueenVaultMap()
        {
            Site site = map.Parent as Site;
            return site?.parts?.Any(p => p?.def?.defName == "WNG_ReplicatorQueenVault") == true;
        }

        private void HandleRelease(GameComponent_ReplicatorQueenState state)
        {
            queen.SetFaction(Faction.OfPlayer);
            Hediff sickness = queen.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.CryptosleepSickness);
            if (sickness != null)
                queen.health.RemoveHediff(sickness);

            ReplicatorQueenVaultExtension ext = VaultExtension();
            if (!state.MarkReleasedAndScheduleRecovery(
                    queen,
                    ext?.recoveryDelayTicks ?? 1800,
                    ext?.operativeCount ?? 4,
                    ext?.subdualWarmupTicks ?? 120,
                    ext?.subdualStunTicks ?? 1500,
                    ext?.boardingTimeoutTicks ?? 1800))
                return;

            releaseHandled = true;
            Find.LetterStack.ReceiveLetter(
                "Replicator Queen released",
                "The exact human-form Replicator held in the vault has awakened and joined the colony. The Asuran Lattice has detected her release and will attempt a physical recovery operation even if she is moved to another player map before they arrive.",
                LetterDefOf.PositiveEvent,
                queen);
        }

        private ReplicatorQueenVaultExtension VaultExtension()
        {
            Site site = map.Parent as Site;
            SitePartDef def = site?.parts?
                .FirstOrDefault(p => p?.def?.defName == "WNG_ReplicatorQueenVault")?.def;
            return def?.GetModExtension<ReplicatorQueenVaultExtension>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref queen, "wngQueenRecoveryExactQueen");
            Scribe_Values.Look(ref releaseHandled, "wngQueenReleaseHandled", false);
        }
    }

    public static class ReplicatorQueenRecoveryUtility
    {
        public static bool TrySpawnRecoveryTeam(
            Pawn queen,
            int operativeCount,
            int subdualWarmupTicks,
            int subdualStunTicks,
            int boardingTimeoutTicks)
        {
            Map map = queen?.Map;
            if (queen == null || queen.Dead || !queen.Spawned || map == null)
                return false;

            Faction asurans = ResolveOrCreateAsuranFaction();
            ThingDef jumperDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_AsuranRecoveryJumper");
            PawnKindDef operativeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_AsuranOperative");
            if (asurans == null || jumperDef == null || operativeKind == null)
            {
                Log.Error("[WNG] Replicator Queen recovery could not resolve the Asuran faction, recovery Jumper, or operative kind.");
                return false;
            }

            if (!CellFinder.TryFindRandomEdgeCellWith(
                    cell => !cell.Fogged(map) && GenSpawn.CanSpawnAt(jumperDef, cell, map),
                    map,
                    CellFinder.EdgeRoadChance_Hostile,
                    out IntVec3 entryCell))
                return false;

            Thing jumper = ThingMaker.MakeThing(jumperDef);
            if (jumper == null)
                return false;
            jumper.SetFaction(asurans);
            GenSpawn.Spawn(jumper, entryCell, map, Rot4.North);

            CompAsuranQueenRecoveryMission mission = jumper.TryGetComp<CompAsuranQueenRecoveryMission>();
            if (mission == null)
            {
                jumper.Destroy(DestroyMode.Vanish);
                return false;
            }

            int requiredCount = Math.Max(1, operativeCount);
            List<Pawn> operatives = new List<Pawn>(requiredCount);
            HashSet<IntVec3> usedCells = new HashSet<IntVec3>();

            for (int i = 0; i < requiredCount; i++)
            {
                PawnGenerationRequest operativeRequest = new PawnGenerationRequest(
                    operativeKind,
                    faction: asurans,
                    context: PawnGenerationContext.NonPlayer,
                    tile: map.Tile,
                    forceGenerateNewPawn: true,
                    allowDead: false,
                    allowDowned: false,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: true,
                    allowPregnant: false,
                    dontGiveWeapon: true);

                Pawn operative = PawnGenerator.GeneratePawn(operativeRequest);
                if (operative == null)
                {
                    CleanupPartialRecovery(jumper, operatives);
                    return false;
                }

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                    entryCell,
                    map,
                    7,
                    c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && !usedCells.Contains(c));
                if (!cell.IsValid)
                {
                    operative.Destroy(DestroyMode.Vanish);
                    CleanupPartialRecovery(jumper, operatives);
                    return false;
                }

                usedCells.Add(cell);
                GenSpawn.Spawn(operative, cell, map);
                operatives.Add(operative);
            }

            if (operatives.Count != requiredCount)
            {
                CleanupPartialRecovery(jumper, operatives);
                return false;
            }

            mission.BeginRecovery(
                queen,
                operatives,
                Math.Max(30, subdualWarmupTicks),
                Math.Max(60, subdualStunTicks),
                Math.Max(300, boardingTimeoutTicks));

            if (mission.Phase != AsuranQueenRecoveryPhase.Subduing)
            {
                CleanupPartialRecovery(jumper, operatives);
                return false;
            }

            Find.LetterStack.ReceiveLetter(
                "Asuran recovery team",
                requiredCount + " human-form Asuran recovery operatives have arrived with a physical Asuran Jumper. Their objective is the exact Replicator Queen. They will attempt to stun her, carry her into the Jumper and leave. Capture is not committed unless that same craft physically leaves the map with her aboard.",
                LetterDefOf.ThreatBig,
                jumper);
            return true;
        }

        private static void CleanupPartialRecovery(Thing jumper, IEnumerable<Pawn> operatives)
        {
            if (operatives != null)
            {
                foreach (Pawn operative in operatives.Where(p => p != null).Distinct().ToList())
                {
                    if (!operative.Destroyed)
                        operative.Destroy(DestroyMode.Vanish);
                }
            }

            if (jumper != null && !jumper.Destroyed)
                jumper.Destroy(DestroyMode.Vanish);
        }

        private static Faction ResolveOrCreateAsuranFaction()
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_AsuranLattice");
            if (factionDef == null || Find.FactionManager == null)
                return null;

            Faction existing = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => f != null && !f.defeated && f.def == factionDef);
            if (existing != null)
                return existing;

            Faction generated = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(factionDef));
            if (generated == null)
                return null;
            Find.FactionManager.Add(generated);
            return generated;
        }
    }

    public enum AsuranQueenRecoveryPhase
    {
        Idle,
        Subduing,
        QueenLoaded,
        Boarding,
        NativeEscapePending,
        Escaped,
        Stranded
    }

    public sealed class CompProperties_AsuranQueenRecoveryMission : CompProperties
    {
        public CompProperties_AsuranQueenRecoveryMission()
        {
            compClass = typeof(CompAsuranQueenRecoveryMission);
        }
    }

    public sealed class CompAsuranQueenRecoveryMission : ThingComp
    {
        private Pawn queen;
        private List<Pawn> operatives = new List<Pawn>();
        private AsuranQueenRecoveryPhase phase = AsuranQueenRecoveryPhase.Idle;
        private int subdualWarmupTicks = 120;
        private int subdualStunTicks = 1500;
        private int boardingTimeoutTicks = 1800;
        private int boardingDeadline = -1;
        private int nextOrderTick;
        private bool nativeLaunchIssued;

        public AsuranQueenRecoveryPhase Phase => phase;
        public Pawn Queen => queen;

        public void BeginRecovery(Pawn exactQueen, List<Pawn> exactOperatives, int warmupTicks, int stunTicks, int boardingTimeout)
        {
            if (parent?.Spawned != true || exactQueen == null || exactQueen.Dead || exactOperatives.NullOrEmpty())
                return;

            queen = exactQueen;
            operatives = exactOperatives.Where(p => p != null && !p.Dead).Distinct().ToList();
            subdualWarmupTicks = Math.Max(30, warmupTicks);
            subdualStunTicks = Math.Max(60, stunTicks);
            boardingTimeoutTicks = Math.Max(300, boardingTimeout);
            phase = AsuranQueenRecoveryPhase.Subduing;
            nextOrderTick = Find.TickManager?.TicksGame ?? 0;
            nativeLaunchIssued = false;

            CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
            if (shuttle != null)
            {
                shuttle.requiredPawns.Clear();
                foreach (Pawn operative in operatives)
                    shuttle.requiredPawns.AddUnique(operative);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null || parent.Destroyed || Find.TickManager == null)
                return;
            if (phase == AsuranQueenRecoveryPhase.Idle ||
                phase == AsuranQueenRecoveryPhase.Escaped ||
                phase == AsuranQueenRecoveryPhase.Stranded)
                return;

            if (queen == null || queen.Dead)
            {
                phase = AsuranQueenRecoveryPhase.Stranded;
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now < nextOrderTick)
                return;
            nextOrderTick = now + 60;

            switch (phase)
            {
                case AsuranQueenRecoveryPhase.Subduing:
                    DriveSubdual();
                    break;
                case AsuranQueenRecoveryPhase.QueenLoaded:
                    BeginBoarding(now);
                    break;
                case AsuranQueenRecoveryPhase.Boarding:
                    DriveBoarding(now);
                    break;
            }
        }

        private void DriveSubdual()
        {
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            if (transporter?.innerContainer?.Contains(queen) == true)
            {
                NotifyQueenLoaded();
                return;
            }

            if (!queen.Spawned || queen.Map != parent.Map)
            {
                phase = AsuranQueenRecoveryPhase.Stranded;
                return;
            }

            List<Pawn> available = AvailableOperatives().ToList();
            if (available.Count == 0)
                return;

            bool canLoad = queen.Downed || queen.stances?.stunner?.Stunned == true;
            if (canLoad)
            {
                Pawn loader = available.FirstOrDefault(p => p.CurJobDef?.defName == "WNG_AsuranLoadReplicatorQueen");
                if (loader == null)
                {
                    loader = available.First();
                    Job load = JobMaker.MakeJob(
                        DefDatabase<JobDef>.GetNamed("WNG_AsuranLoadReplicatorQueen"),
                        queen,
                        parent);
                    loader.jobs.StartJob(load, JobCondition.InterruptForced);
                }

                foreach (Pawn operative in available.Where(p => p != loader))
                    HoldRecoveryPosition(operative);
                return;
            }

            Pawn subduer = available.FirstOrDefault(p => p.CurJobDef?.defName == "WNG_AsuranSubdueReplicatorQueen");
            if (subduer == null)
            {
                subduer = available.First();
                Job subdue = JobMaker.MakeJob(
                    DefDatabase<JobDef>.GetNamed("WNG_AsuranSubdueReplicatorQueen"),
                    queen);
                subdue.count = subdualWarmupTicks;
                subdue.expiryInterval = subdualStunTicks;
                subduer.jobs.StartJob(subdue, JobCondition.InterruptForced);
            }

            foreach (Pawn operative in available.Where(p => p != subduer))
                HoldRecoveryPosition(operative);
        }

        private static void HoldRecoveryPosition(Pawn operative)
        {
            if (operative == null || operative.Dead || operative.Downed || !operative.Spawned)
                return;
            if (operative.CurJobDef?.defName == "WNG_AsuranHoldQueenRecovery")
                return;

            JobDef holdDef = DefDatabase<JobDef>.GetNamed("WNG_AsuranHoldQueenRecovery");
            Job hold = JobMaker.MakeJob(holdDef);
            hold.count = 120;
            operative.jobs.StartJob(hold, JobCondition.InterruptForced);
        }

        private void BeginBoarding(int now)
        {
            boardingDeadline = SafeFutureTick(now, boardingTimeoutTicks);
            phase = AsuranQueenRecoveryPhase.Boarding;
            DriveBoarding(now);
        }

        private void DriveBoarding(int now)
        {
            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
            if (transporter == null || shuttle == null || !transporter.innerContainer.Contains(queen))
            {
                phase = AsuranQueenRecoveryPhase.Stranded;
                return;
            }

            bool anyEligibleOutside = false;
            foreach (Pawn operative in operatives.Where(p => p != null && !p.Dead).ToList())
            {
                if (transporter.innerContainer.Contains(operative))
                    continue;
                if (!operative.Spawned || operative.Map != parent.Map || operative.Downed)
                    continue;

                anyEligibleOutside = true;
                if (operative.CurJobDef != JobDefOf.EnterTransporter)
                {
                    Job board = JobMaker.MakeJob(JobDefOf.EnterTransporter, parent);
                    operative.jobs.StartJob(board, JobCondition.InterruptForced);
                }
            }

            if (!anyEligibleOutside || now >= boardingDeadline)
                TryBeginNativeEscape();
        }

        public void NotifyQueenLoaded()
        {
            if (phase != AsuranQueenRecoveryPhase.Subduing)
                return;
            phase = AsuranQueenRecoveryPhase.QueenLoaded;
            nextOrderTick = Find.TickManager?.TicksGame ?? 0;
        }

        private bool TryBeginNativeEscape()
        {
            if (nativeLaunchIssued)
                return true;
            if (parent?.Spawned != true || parent.Map == null)
                return false;

            CompTransporter transporter = parent.TryGetComp<CompTransporter>();
            CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
            if (transporter == null || shuttle?.shipParent == null || !transporter.innerContainer.Contains(queen))
            {
                phase = AsuranQueenRecoveryPhase.Stranded;
                return false;
            }

            CompLaunchable launchable = parent.TryGetComp<CompLaunchable>();
            CompRefuelable refuelable = parent.TryGetComp<CompRefuelable>();
            float minimumFuel = Math.Max(0f, launchable?.Props?.minFuelCost ?? 0f);
            if (refuelable != null && refuelable.Fuel < minimumFuel)
            {
                phase = AsuranQueenRecoveryPhase.Stranded;
                return false;
            }

            PlanetTile destination = FindNativeEscapeDestination(parent.Tile);
            if (!destination.Valid)
            {
                phase = AsuranQueenRecoveryPhase.Stranded;
                return false;
            }

            ShipJob_FlyAway flyAway = (ShipJob_FlyAway)ShipJobMaker.MakeShipJob(ShipJobDefOf.FlyAway);
            flyAway.destinationTile = destination;
            flyAway.arrivalAction = new WNGHostileShuttleEscapeArrivalAction();
            flyAway.dropMode = TransportShipDropMode.None;

            phase = AsuranQueenRecoveryPhase.NativeEscapePending;
            nativeLaunchIssued = true;
            shuttle.shipParent.ForceJob(flyAway);

            if (parent.Spawned)
            {
                nativeLaunchIssued = false;
                phase = AsuranQueenRecoveryPhase.Stranded;
                return false;
            }

            if (refuelable != null && minimumFuel > 0f)
                refuelable.ConsumeFuel(minimumFuel);
            return true;
        }

        public bool NotifyNativeEscapeCompleted(ThingOwner transitContainer)
        {
            if (phase != AsuranQueenRecoveryPhase.NativeEscapePending ||
                transitContainer == null || queen == null ||
                !transitContainer.Contains(queen))
                return false;

            Faction captor = parent?.Faction;
            Pawn kidnapper = operatives.FirstOrDefault(p => p != null && !p.Dead);
            if (captor == null)
                return false;

            transitContainer.Remove(queen);
            if (GameComponent_ReplicatorQueenState.Current?.CommitAsuranCapture(queen, captor, kidnapper) != true)
            {
                transitContainer.TryAdd(queen);
                return false;
            }

            phase = AsuranQueenRecoveryPhase.Escaped;
            nativeLaunchIssued = false;
            Find.LetterStack.ReceiveLetter(
                "Replicator Queen captured",
                "The Asuran recovery Jumper physically left the map with the exact Replicator Queen aboard. Her capture is now committed to the Asuran Lattice.",
                LetterDefOf.NegativeEvent);
            return true;
        }

        private IEnumerable<Pawn> AvailableOperatives()
        {
            return operatives.Where(p =>
                p != null && !p.Dead && !p.Downed && p.Spawned &&
                p.Map == parent.Map && p.carryTracker?.CarriedThing != queen);
        }

        private static PlanetTile FindNativeEscapeDestination(PlanetTile origin)
        {
            if (!origin.Valid || Find.WorldGrid == null)
                return PlanetTile.Invalid;
            List<PlanetTile> neighbors = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(origin, neighbors);
            return neighbors.Count > 0 ? neighbors.RandomElement() : origin;
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override string CompInspectStringExtra()
        {
            if (phase == AsuranQueenRecoveryPhase.Idle)
                return null;
            return "Asuran Queen recovery: " + phase;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref queen, "wngAsuranRecoveryQueen");
            Scribe_Collections.Look(ref operatives, "wngAsuranRecoveryOperatives", LookMode.Reference);
            Scribe_Values.Look(ref phase, "wngAsuranRecoveryPhase", AsuranQueenRecoveryPhase.Idle);
            Scribe_Values.Look(ref subdualWarmupTicks, "wngAsuranSubdualWarmupTicks", 120);
            Scribe_Values.Look(ref subdualStunTicks, "wngAsuranSubdualStunTicks", 1500);
            Scribe_Values.Look(ref boardingTimeoutTicks, "wngAsuranBoardingTimeoutTicks", 1800);
            Scribe_Values.Look(ref boardingDeadline, "wngAsuranBoardingDeadline", -1);
            Scribe_Values.Look(ref nextOrderTick, "wngAsuranNextOrderTick", 0);
            Scribe_Values.Look(ref nativeLaunchIssued, "wngAsuranNativeLaunchIssued", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && operatives == null)
                operatives = new List<Pawn>();
        }
    }

    public sealed class JobDriver_AsuranSubdueQueen : JobDriver
    {
        private Pawn Queen => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return Queen != null && pawn.Reserve(Queen, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Queen == null || Queen.Dead || !Queen.Spawned);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil warmup = Toils_General.Wait(Math.Max(30, job.count), TargetIndex.A);
            warmup.FailOn(() => Queen == null || Queen.Dead || !Queen.Spawned);
            yield return warmup;

            Toil stun = ToilMaker.MakeToil("AsuranSubdueQueen");
            stun.initAction = delegate
            {
                if (Queen == null || Queen.Dead || !Queen.Spawned)
                    return;
                int ticks = Math.Max(60, job.expiryInterval);
                Queen.stances?.stunner?.StunFor(ticks, pawn);
            };
            stun.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return stun;
        }
    }

    public sealed class JobDriver_AsuranLoadQueen : JobDriver
    {
        private Pawn Queen => job.GetTarget(TargetIndex.A).Pawn;
        private Thing Jumper => job.GetTarget(TargetIndex.B).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Queen == null || Jumper == null)
                return false;
            return pawn.Reserve(Queen, job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(Jumper, job, 1, 1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Queen == null || Queen.Dead ||
                (!Queen.Downed && Queen.stances?.stunner?.Stunned != true));
            this.FailOnDestroyedOrNull(TargetIndex.B);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);

            Toil load = ToilMaker.MakeToil("AsuranLoadExactQueen");
            load.initAction = delegate
            {
                Pawn actor = load.actor;
                Pawn exactQueen = actor?.carryTracker?.CarriedThing as Pawn;
                CompTransporter transporter = Jumper?.TryGetComp<CompTransporter>();
                if (actor == null || exactQueen == null || exactQueen != Queen || transporter == null)
                {
                    actor?.jobs?.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                int moved = actor.carryTracker.innerContainer.TryTransferToContainer(
                    exactQueen, transporter.innerContainer, 1);
                if (moved != 1 || !transporter.innerContainer.Contains(exactQueen))
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }

                transporter.Notify_ThingAdded(exactQueen);
                Jumper.TryGetComp<CompAsuranQueenRecoveryMission>()?.NotifyQueenLoaded();
            };
            load.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return load;
        }
    }

    public sealed class JobDriver_AsuranHoldRecovery : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.Wait(Math.Max(60, job.count));
        }
    }

    public sealed class IncidentWorker_ReplicatorQueenVaultDiscovery : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            GameComponent_ReplicatorQueenState state = GameComponent_ReplicatorQueenState.Current;
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_ReplicatorQueenVault");
            ReplicatorQueenVaultIncidentExtension ext = def.GetModExtension<ReplicatorQueenVaultIncidentExtension>();
            if (!base.CanFireNowSub(parms) || state?.CanLocateVault != true || siteDef == null || Find.WorldObjects == null)
                return false;

            int maxSites = Math.Max(1, ext?.maxActiveSites ?? 1);
            return ActiveSites(siteDef).Count < maxSites;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            GameComponent_ReplicatorQueenState state = GameComponent_ReplicatorQueenState.Current;
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_ReplicatorQueenVault");
            ReplicatorQueenVaultIncidentExtension ext = def.GetModExtension<ReplicatorQueenVaultIncidentExtension>();
            if (state?.CanLocateVault != true || siteDef == null)
                return false;

            int minDistance = Math.Max(1, ext?.minSiteDistance ?? 8);
            int maxDistance = Math.Max(minDistance, ext?.maxSiteDistance ?? 24);
            if (!TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    minDistance,
                    maxDistance,
                    allowCaravans: false,
                    tileFinderMode: TileFinderMode.Near))
                return false;

            Site site = SiteMaker.MakeSite(siteDef, tile, null, ifHostileThenMustRemainHostile: false, threatPoints: 0f);
            if (site?.parts == null || !site.parts.Any(p => p != null && p.def == siteDef))
            {
                site?.Destroy();
                return false;
            }

            site.customLabel = "precursor Replicator vault";
            Find.WorldObjects.Add(site);
            state.MarkVaultLocated(site.ID);
            Find.LetterStack.ReceiveLetter(
                "Replicator vault located",
                "Long-range analysis has identified a precursor vault containing a real cryptosleep casket and a unique human-form Replicator signal. The occupant has not been duplicated or abstracted; entering the site will generate and preserve one exact pawn through the recovery storyline.",
                LetterDefOf.PositiveEvent,
                site);
            return true;
        }

        private static List<Site> ActiveSites(SitePartDef def)
        {
            return Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .Where(s => s != null && !s.Destroyed && s.parts != null &&
                            s.parts.Any(p => p != null && p.def == def))
                .ToList();
        }
    }
}
