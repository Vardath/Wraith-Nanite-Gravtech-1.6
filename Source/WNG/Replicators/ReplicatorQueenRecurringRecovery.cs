using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Author-tunable cadence for recovery attempts after the first vault-triggered operation.
    /// Stored on the Queen discovery incident Def so pacing remains editable rather than buried in code.
    /// </summary>
    public sealed class ReplicatorQueenRecurringRecoveryExtension : DefModExtension
    {
        public int minIntervalTicks = 120000;
        public int maxIntervalTicks = 240000;
        public int failedSpawnRetryTicks = 10000;
        public int maintenanceIntervalTicks = 2500;
    }

    /// <summary>
    /// Schedules later Asuran recovery attempts against the exact map physically containing the
    /// exact player-owned Queen. The first recovery remains owned by GameComponent_ReplicatorQueenState;
    /// this component only unlocks after that first operation has genuinely become active.
    /// </summary>
    public sealed class GameComponent_ReplicatorQueenRecurringRecovery : GameComponent
    {
        private bool recurringUnlocked;
        private int nextRecurringRecoveryTick = -1;
        private int nextMaintenanceTick;
        private int attemptsSpawned;

        public GameComponent_ReplicatorQueenRecurringRecovery(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextMaintenanceTick)
                return;

            ReplicatorQueenRecurringRecoveryExtension cadence = Cadence;
            nextMaintenanceTick = SafeFutureTick(now, Math.Max(250, cadence?.maintenanceIntervalTicks ?? 2500));

            GameComponent_ReplicatorQueenState state = GameComponent_ReplicatorQueenState.Current;
            Pawn queen = state?.Queen;
            if (state == null || queen == null || queen.Dead ||
                state.Status == ReplicatorQueenStatus.Dead ||
                state.Status == ReplicatorQueenStatus.CapturedByAsurans)
            {
                nextRecurringRecoveryTick = -1;
                return;
            }

            // RecoveryActive is only reached after the first physical team actually spawned.
            // Once observed, recurring attempts remain unlocked for this exact Queen/save.
            if (state.Status == ReplicatorQueenStatus.RecoveryActive)
                recurringUnlocked = true;

            if (!recurringUnlocked)
                return;

            // Never overlap another real Queen-recovery mission. This covers the first operation and
            // every recurring one, including Queen-loaded/boarding/native-escape phases.
            if (HasActiveRecoveryMission(queen))
            {
                nextRecurringRecoveryTick = -1;
                return;
            }

            // Later recovery attempts are only valid on the exact player home map on which the exact
            // Queen is physically spawned. Caravans/off-map containers/other player maps are not guessed.
            if (!IsValidRecurringTarget(queen))
                return;

            if (nextRecurringRecoveryTick < 0)
            {
                nextRecurringRecoveryTick = ScheduleNext(now, cadence);
                return;
            }

            if (now < nextRecurringRecoveryTick)
                return;

            ReplicatorQueenVaultExtension recovery = RecoveryTuning;
            bool spawned = ReplicatorQueenRecoveryUtility.TrySpawnRecoveryTeam(
                queen,
                recovery?.operativeCount ?? 4,
                recovery?.subdualWarmupTicks ?? 120,
                recovery?.subdualStunTicks ?? 1500,
                recovery?.boardingTimeoutTicks ?? 1800);

            if (spawned)
            {
                attemptsSpawned++;
                nextRecurringRecoveryTick = -1;
                state.MarkRecoveryActive(queen);
                return;
            }

            // Map-edge obstruction or another transient spawn failure must not consume the scheduled
            // attempt forever; retry later on the same exact-map eligibility rules.
            nextRecurringRecoveryTick = SafeFutureTick(
                now,
                Math.Max(600, cadence?.failedSpawnRetryTicks ?? 10000));
        }

        private static bool IsValidRecurringTarget(Pawn queen)
        {
            return queen != null && !queen.Dead && queen.Spawned && queen.Map != null &&
                   queen.Faction == Faction.OfPlayer && queen.Map.IsPlayerHome;
        }

        private static bool HasActiveRecoveryMission(Pawn exactQueen)
        {
            if (exactQueen == null || Find.Maps == null)
                return false;

            foreach (Map map in Find.Maps)
            {
                if (map?.listerThings?.AllThings == null)
                    continue;

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    CompAsuranQueenRecoveryMission mission = thing?.TryGetComp<CompAsuranQueenRecoveryMission>();
                    if (mission == null || mission.Queen != exactQueen)
                        continue;

                    switch (mission.Phase)
                    {
                        case AsuranQueenRecoveryPhase.QueenLoaded:
                        case AsuranQueenRecoveryPhase.Boarding:
                        case AsuranQueenRecoveryPhase.NativeEscapePending:
                            return true;
                        case AsuranQueenRecoveryPhase.Subduing:
                            // BeginRecovery registers the exact generated recovery operatives as the
                            // native shuttle's required pawns. Use that exact set, not unrelated Asurans
                            // elsewhere on the map, when deciding whether this operation can still act.
                            CompShuttle shuttle = thing.TryGetComp<CompShuttle>();
                            if (shuttle?.requiredPawns?.Any(p => p != null && !p.Dead) == true)
                                return true;
                            break;
                    }
                }
            }
            return false;
        }

        private static ReplicatorQueenVaultExtension RecoveryTuning
        {
            get
            {
                SitePartDef vault = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_ReplicatorQueenVault");
                return vault?.GetModExtension<ReplicatorQueenVaultExtension>();
            }
        }

        private static ReplicatorQueenRecurringRecoveryExtension Cadence
        {
            get
            {
                IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("WNG_ReplicatorQueenVaultDiscovered");
                return incident?.GetModExtension<ReplicatorQueenRecurringRecoveryExtension>();
            }
        }

        private static int ScheduleNext(int now, ReplicatorQueenRecurringRecoveryExtension cadence)
        {
            int min = Math.Max(600, cadence?.minIntervalTicks ?? 120000);
            int max = Math.Max(min, cadence?.maxIntervalTicks ?? 240000);
            int delay = min == max ? min : Rand.RangeInclusive(min, max);
            return SafeFutureTick(now, delay);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref recurringUnlocked, "wngQueenRecurringRecoveryUnlocked", false);
            Scribe_Values.Look(ref nextRecurringRecoveryTick, "wngQueenNextRecurringRecoveryTick", -1);
            Scribe_Values.Look(ref nextMaintenanceTick, "wngQueenRecurringRecoveryMaintenanceTick", 0);
            Scribe_Values.Look(ref attemptsSpawned, "wngQueenRecurringRecoveryAttemptsSpawned", 0);
        }
    }
}
