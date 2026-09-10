using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public static class ReplicatorQueenCaptureUtility
    {
        public static Faction ResolveHostileLatticeFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_PrecursorCollective");
            Faction faction = def == null ? null : Find.FactionManager?.FirstFactionOfDef(def);
            if (faction == null || faction.defeated || faction == Faction.OfPlayer)
                return null;
            return faction;
        }
    }

    /// <summary>
    /// Marks a particular map as an active Queen-capture operation.  Vanilla Lord/raid AI remains
    /// responsible for ordinary combat.  This component supplies only the special objective:
    /// concentrate on the exact Queen until she is downed, then start the vanilla Kidnap job so the
    /// real carry-to-edge transaction determines whether she is actually lost.
    /// </summary>
    public sealed class MapComponent_ReplicatorQueenCaptureOperation : MapComponent
    {
        private const int DirectiveIntervalTicks = 30;
        private const int EmptyOperationTimeoutTicks = 600;

        private bool active;
        private int nextDirectiveTick;
        private int noOperativesSinceTick = -1;

        public bool Active => active;

        public MapComponent_ReplicatorQueenCaptureOperation(Map map) : base(map)
        {
        }

        public void Activate()
        {
            active = true;
            nextDirectiveTick = 0;
            noOperativesSinceTick = -1;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!active || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextDirectiveTick)
                return;
            nextDirectiveTick = now + DirectiveIntervalTicks;

            GameComponent_ReplicatorQueenState state = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();
            Pawn queen = state?.QueenPawn;
            if (state == null || state.QueenAbducted || queen == null || queen.Destroyed || queen.Dead)
            {
                active = false;
                return;
            }

            if (queen.MapHeld != map)
            {
                active = false;
                return;
            }

            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            if (lattice == null)
            {
                active = false;
                return;
            }

            List<Pawn> operatives = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn != null && !pawn.Dead && !pawn.Downed && pawn.Faction == lattice)
                .ToList();
            if (operatives.Count == 0)
            {
                if (noOperativesSinceTick < 0)
                    noOperativesSinceTick = now;
                else if (now - noOperativesSinceTick >= EmptyOperationTimeoutTicks)
                    active = false;
                return;
            }
            noOperativesSinceTick = -1;

            // While carried in a vanilla Kidnap job the Queen is no longer Spawned, but MapHeld
            // remains this map.  Do not interfere with that transaction; the game-level bridge will
            // commit capture only after vanilla records the actual map-edge kidnapping.
            if (!queen.Spawned)
                return;

            if (queen.Downed)
                DirectKidnap(operatives, queen);
            else
                DirectSubdual(operatives, queen);
        }

        private static void DirectSubdual(List<Pawn> operatives, Pawn queen)
        {
            for (int i = 0; i < operatives.Count; i++)
            {
                Pawn operative = operatives[i];
                Job current = operative.CurJob;
                if (current != null
                    && (current.def == JobDefOf.AttackMelee || current.def == JobDefOf.AttackStatic)
                    && current.targetA.Thing == queen)
                    continue;
                if (!operative.CanReach(queen, PathEndMode.Touch, Danger.Deadly))
                    continue;

                // Recovery teams deliberately close for subdual rather than choosing a kill-at-range
                // objective.  The moment the Queen becomes downed, the next 30-tick directive pass
                // interrupts remaining Queen-attack jobs and assigns one physical kidnap carrier.
                Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, queen);
                job.expiryInterval = 240;
                job.checkOverrideOnExpire = false;
                operative.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc);
            }
        }

        private static void DirectKidnap(List<Pawn> operatives, Pawn queen)
        {
            Pawn existingCarrier = operatives.FirstOrDefault(operative =>
                operative.CurJob != null
                && operative.CurJob.def == JobDefOf.Kidnap
                && operative.CurJob.targetA.Thing == queen);
            if (existingCarrier != null)
                return;

            // Stop lingering Queen-directed melee jobs immediately after she is downed so the
            // recovery force does not keep striking the target it is explicitly trying to retrieve.
            for (int i = 0; i < operatives.Count; i++)
            {
                Pawn operative = operatives[i];
                Job job = operative.CurJob;
                if (job != null
                    && (job.def == JobDefOf.AttackMelee || job.def == JobDefOf.AttackStatic)
                    && job.targetA.Thing == queen)
                    operative.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }

            Pawn carrier = operatives
                .Where(operative => operative.CanReach(queen, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(operative => operative.Position.DistanceToSquared(queen.Position))
                .FirstOrDefault();
            if (carrier == null)
                return;

            IntVec3 exitCell;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out exitCell, queen.Map, 0f))
                exitCell = CellFinder.RandomEdgeCell(queen.Map);

            Job kidnap = JobMaker.MakeJob(JobDefOf.Kidnap);
            kidnap.targetA = queen;
            kidnap.targetB = exitCell;
            kidnap.count = 1;
            kidnap.expiryInterval = 5000;
            kidnap.checkOverrideOnExpire = false;
            carrier.jobs.StartJob(kidnap, JobCondition.InterruptForced, null, false, true, null, JobTag.Misc);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref active, "wngQueenCaptureOperationActive", false);
            Scribe_Values.Look(ref nextDirectiveTick, "wngQueenCaptureNextDirectiveTick", 0);
            Scribe_Values.Look(ref noOperativesSinceTick, "wngQueenCaptureNoOperativesSinceTick", -1);
        }
    }

    /// <summary>
    /// Dedicated non-random incident used by the Queen director for occasional later recovery raids.
    /// It delegates raid size/composition to RimWorld's real RaidEnemy worker, forces an edge walk-in
    /// so the spawned Lattice force is immediately present on the Queen's map, then marks the map's
    /// capture objective active.  It never fires when the Queen is absent from that player home map.
    /// </summary>
    public sealed class IncidentWorker_ReplicatorQueenCaptureRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            GameComponent_ReplicatorQueenState state = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>();
            Pawn queen = state?.QueenPawn;
            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            return map != null
                && map.IsPlayerHome
                && state != null
                && state.QueenJoinedPlayer
                && !state.QueenAbducted
                && queen != null
                && !queen.Dead
                && queen.Spawned
                && queen.Map == map
                && queen.Faction == Faction.OfPlayer
                && lattice != null
                && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            if (map == null)
                return false;

            Faction lattice = ReplicatorQueenCaptureUtility.ResolveHostileLatticeFaction();
            if (lattice == null)
                return false;

            parms.faction = lattice;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
                return false;

            map.GetComponent<MapComponent_ReplicatorQueenCaptureOperation>()?.Activate();
            Pawn queen = Current.Game?.GetComponent<GameComponent_ReplicatorQueenState>()?.QueenPawn;
            Messages.Message(
                "The Lattice Collective has launched a recovery raid for the Replicator Queen. The raiders will attempt to take her alive.",
                queen ?? (LookTargets)map.Center,
                MessageTypeDefOf.ThreatBig,
                historical: true);
            return true;
        }
    }
}
