using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    public sealed class AsuranCovertPresenceExtension : DefModExtension
    {
        public int checkIntervalTicks = 600;
        public int minIntervalTicks = 240000;
        public int maxIntervalTicks = 480000;
        public int retryDelayTicks = 30000;
        public float attemptChance = 0.65f;
        public int minVisitDurationTicks = 30000;
        public int maxVisitDurationTicks = 60000;
        public string infiltratorKind = "WNG_AsuranInfiltrator";
        public string trueFactionDef = "WNG_AsuranLattice";
    }

    /// <summary>
    /// Schedules rare human-looking Asuran infiltrators as ordinary visitors. The incident uses a
    /// real non-hostile human faction as the temporary assumed identity, while the exact true Asuran
    /// source faction is saved on the same pawn's infiltration hediff and restored on exposure.
    /// </summary>
    public sealed class GameComponent_AsuranCovertPresence : GameComponent
    {
        private int nextAttemptTick = -1;
        private int nextCheckTick;

        public GameComponent_AsuranCovertPresence(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Faction.OfPlayer == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextCheckTick)
                return;

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("WNG_AsuranCovertVisitor");
            AsuranCovertPresenceExtension ext = incident?.GetModExtension<AsuranCovertPresenceExtension>();
            nextCheckTick = SafeFutureTick(now, Math.Max(60, ext?.checkIntervalTicks ?? 600));
            if (incident == null || ext == null || ResolveTrueFaction(ext) == null)
            {
                nextAttemptTick = -1;
                return;
            }

            if (nextAttemptTick < 0)
            {
                nextAttemptTick = ScheduleNext(now, ext);
                return;
            }
            if (now < nextAttemptTick)
                return;

            if (AsuranCovertPresenceUtility.HasActiveCovertVisitor())
            {
                nextAttemptTick = SafeFutureTick(now, Math.Max(600, ext.retryDelayTicks));
                return;
            }

            if (!Rand.Chance(Math.Max(0f, Math.Min(1f, ext.attemptChance))))
            {
                nextAttemptTick = ScheduleNext(now, ext);
                return;
            }

            List<Map> eligibleMaps = Find.Maps
                .Where(m => m != null && m.IsPlayerHome &&
                    m.mapPawns?.FreeColonistsSpawned?.Any(p => p != null && !p.Dead) == true)
                .ToList();
            if (eligibleMaps.Count == 0)
            {
                nextAttemptTick = SafeFutureTick(now, Math.Max(600, ext.retryDelayTicks));
                return;
            }

            Map target = eligibleMaps.RandomElement();
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, target);
            if (incident.Worker.TryExecute(parms))
                nextAttemptTick = ScheduleNext(now, ext);
            else
                nextAttemptTick = SafeFutureTick(now, Math.Max(600, ext.retryDelayTicks));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextAttemptTick, "wngAsuranCovertNextAttemptTick", -1);
            Scribe_Values.Look(ref nextCheckTick, "wngAsuranCovertNextCheckTick", 0);
        }

        private static int ScheduleNext(int now, AsuranCovertPresenceExtension ext)
        {
            int min = Math.Max(600, ext?.minIntervalTicks ?? 240000);
            int max = Math.Max(min, ext?.maxIntervalTicks ?? min);
            return SafeFutureTick(now, Rand.RangeInclusive(min, max));
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        internal static Faction ResolveTrueFaction(AsuranCovertPresenceExtension ext)
        {
            if (ext?.trueFactionDef.NullOrEmpty() != false)
                return null;
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(ext.trueFactionDef);
            Faction faction = def == null ? null : Find.FactionManager?.FirstFactionOfDef(def);
            return faction != null && !faction.defeated ? faction : null;
        }
    }

    public sealed class IncidentWorker_AsuranCovertVisitor : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            AsuranCovertPresenceExtension ext = def?.GetModExtension<AsuranCovertPresenceExtension>();
            return base.CanFireNowSub(parms)
                && map != null
                && map.IsPlayerHome
                && map.mapPawns?.FreeColonistsSpawned?.Any(p => p != null && !p.Dead) == true
                && GameComponent_AsuranCovertPresence.ResolveTrueFaction(ext) != null
                && AsuranCovertPresenceUtility.TryFindCoverFaction(out _)
                && !AsuranCovertPresenceUtility.HasActiveCovertVisitor();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            AsuranCovertPresenceExtension ext = def?.GetModExtension<AsuranCovertPresenceExtension>();
            if (map == null || ext == null || !map.IsPlayerHome || AsuranCovertPresenceUtility.HasActiveCovertVisitor())
                return false;

            Faction trueFaction = GameComponent_AsuranCovertPresence.ResolveTrueFaction(ext);
            if (trueFaction == null || !AsuranCovertPresenceUtility.TryFindCoverFaction(out Faction coverFaction))
                return false;

            PawnKindDef infiltratorKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ext.infiltratorKind);
            if (infiltratorKind == null)
                return false;

            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => c.Standable(map) && !c.Fogged(map),
                    map,
                    CellFinder.EdgeRoadChance_Neutral,
                    out IntVec3 entryCell))
                return false;

            Pawn infiltrator = null;
            try
            {
                infiltrator = PawnGenerator.GeneratePawn(infiltratorKind, coverFaction);
                if (infiltrator == null)
                    return false;

                GenSpawn.Spawn(infiltrator, entryCell, map);
                Hediff_AsuranInfiltration state = AsuranInfiltrationUtility.State(infiltrator);
                if (state == null || infiltrator.guest == null)
                    return false;

                state.BeginCovertPresence(trueFaction, coverFaction);
                infiltrator.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Guest);
                if (infiltrator.HostFaction != Faction.OfPlayer)
                    return false;

                IntVec3 chillSpot;
                if (!RCellFinder.TryFindRandomSpotJustOutsideColony(infiltrator, out chillSpot))
                    chillSpot = map.Center;

                int minDuration = Math.Max(2500, ext.minVisitDurationTicks);
                int maxDuration = Math.Max(minDuration, ext.maxVisitDurationTicks);
                int duration = Rand.RangeInclusive(minDuration, maxDuration);
                LordMaker.MakeNewLord(
                    coverFaction,
                    new LordJob_VisitColony(coverFaction, chillSpot, duration),
                    map,
                    new List<Pawn> { infiltrator });

                SendStandardLetter(
                    "Visitor",
                    $"{infiltrator.LabelShortCap}, a traveler associated with {coverFaction.NameColored}, has arrived to spend some time at the colony.",
                    LetterDefOf.NeutralEvent,
                    parms,
                    infiltrator);
                return true;
            }
            finally
            {
                if (infiltrator != null && infiltrator.Spawned && infiltrator.GetLord() == null &&
                    (AsuranInfiltrationUtility.State(infiltrator) == null || infiltrator.HostFaction != Faction.OfPlayer))
                {
                    infiltrator.Destroy(DestroyMode.Vanish);
                }
            }
        }
    }

    public static class AsuranCovertPresenceUtility
    {
        public static bool HasActiveCovertVisitor()
        {
            if (Find.Maps == null)
                return false;
            return Find.Maps.Any(map => map?.mapPawns?.AllPawnsSpawned?.Any(AsuranInfiltrationUtility.IsActiveCovertPresence) == true);
        }

        public static bool TryFindCoverFaction(out Faction coverFaction)
        {
            coverFaction = null;
            if (Find.FactionManager?.AllFactionsListForReading == null || Faction.OfPlayer == null)
                return false;

            List<Faction> candidates = Find.FactionManager.AllFactionsListForReading
                .Where(f => f != null
                    && f != Faction.OfPlayer
                    && !f.defeated
                    && f.def != null
                    && f.def.humanlikeFaction
                    && !f.def.hidden
                    && !f.HostileTo(Faction.OfPlayer)
                    && !Faction.OfPlayer.HostileTo(f)
                    && f.def.defName != "WNG_AsuranLattice")
                .ToList();
            if (candidates.Count == 0)
                return false;

            coverFaction = candidates.RandomElement();
            return true;
        }

        public static bool TryActivateRevealedPawn(Pawn pawn, Hediff_AsuranInfiltration state)
        {
            if (pawn == null || pawn.Dead || state == null || !state.Revealed || state.TrueFaction == null)
                return false;

            Faction trueFaction = state.TrueFaction;

            // A captured infiltrator remains captured. Prisoners can retain their true hostile
            // faction under the native guest system; slaves belong to the player faction while
            // enslaved, so their hostile activation is deferred until that native status ends.
            if (pawn.IsSlave)
            {
                state.DeferActivation();
                return false;
            }

            Lord oldLord = pawn.GetLord();
            oldLord?.RemovePawn(pawn);

            if (pawn.IsPrisoner)
            {
                if (pawn.Faction != trueFaction)
                    pawn.SetFaction(trueFaction);
                state.MarkCoverBroken();
                return true;
            }

            if (pawn.guest?.HostFaction != null)
                pawn.guest.SetGuestStatus(null, GuestStatus.Guest);

            if (pawn.Faction != trueFaction)
                pawn.SetFaction(trueFaction);

            if (!pawn.Spawned || pawn.Map == null || pawn.Downed)
            {
                state.DeferActivation();
                return false;
            }

            state.MarkCoverBroken();
            if (trueFaction.HostileTo(Faction.OfPlayer))
            {
                LordMaker.MakeNewLord(
                    trueFaction,
                    new LordJob_AssaultColony(trueFaction),
                    pawn.Map,
                    new List<Pawn> { pawn });
            }
            return true;
        }
    }
}
