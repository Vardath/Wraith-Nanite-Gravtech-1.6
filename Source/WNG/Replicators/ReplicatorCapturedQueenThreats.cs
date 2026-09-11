using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    public sealed class CapturedQueenThreatExtension : DefModExtension
    {
        public int checkIntervalTicks = 600;
        public int minIntervalTicks = 180000;
        public int maxIntervalTicks = 360000;
        public int retryDelayTicks = 30000;
        public float attemptChance = 0.70f;

        public int minAsuranCount = 2;
        public int maxAsuranCount = 6;
        public int minBlockCount = 3;
        public int maxBlockCount = 10;
        public float asuranPointShare = 0.45f;

        public float commanderChance = 0.35f;
        public float technicianChance = 0.30f;
        public float specialistChance = 0.30f;
        public float heavyChance = 0.14f;
        public float titanChance = 0.035f;

        public string operativeKind = "WNG_AsuranOperative";
        public string technicianKind = "WNG_AsuranTechnician";
        public string commanderKind = "WNG_AsuranCommander";
        public string droneKind = "WNG_ReplicatorDrone";
        public string hunterKind = "WNG_ReplicatorHunter";
        public string bulwarkKind = "WNG_ReplicatorBulwark";
        public string controllerKind = "WNG_ReplicatorController";
        public string repairerKind = "WNG_ReplicatorRepairer";
        public string burrowerKind = "WNG_ReplicatorBurrower";
        public string artilleryKind = "WNG_ReplicatorArtillery";
        public string titanKind = "WNG_ReplicatorTitan";
    }

    /// <summary>
    /// Save-persistent scheduler for the consequence of the Asuran Lattice genuinely retaining the
    /// exact Replicator Queen. It schedules no abstract outbreak bonus: every successful event is a
    /// physical mixed force spawned by the incident worker below.
    /// </summary>
    public sealed class GameComponent_CapturedQueenThreats : GameComponent
    {
        private int nextThreatTick = -1;
        private int nextCheckTick;

        public GameComponent_CapturedQueenThreats(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Faction.OfPlayer == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextCheckTick)
                return;

            IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("WNG_CapturedQueenSovereignStrike");
            CapturedQueenThreatExtension ext = incident?.GetModExtension<CapturedQueenThreatExtension>();
            nextCheckTick = SafeFutureTick(now, Math.Max(60, ext?.checkIntervalTicks ?? 600));

            Pawn queen = GameComponent_ReplicatorQueenState.Current?.Queen;
            if (incident == null || ext == null ||
                !ReplicatorSovereigntyUtility.TryGetCapturedQueenAsuranFaction(queen, out Faction captor))
            {
                nextThreatTick = -1;
                return;
            }

            if (nextThreatTick < 0)
            {
                nextThreatTick = ScheduleNext(now, ext);
                return;
            }
            if (now < nextThreatTick)
                return;

            if (HasActiveCapturedQueenForce(queen, captor))
            {
                nextThreatTick = SafeFutureTick(now, Math.Max(600, ext.retryDelayTicks));
                return;
            }

            float chance = Math.Max(0f, Math.Min(1f, ext.attemptChance));
            if (!Rand.Chance(chance))
            {
                nextThreatTick = ScheduleNext(now, ext);
                return;
            }

            List<Map> eligible = Find.Maps
                .Where(m => m != null && m.IsPlayerHome &&
                    m.mapPawns?.FreeColonistsSpawned?.Any(p => p != null && !p.Dead) == true)
                .ToList();
            if (eligible.Count == 0)
            {
                nextThreatTick = SafeFutureTick(now, Math.Max(600, ext.retryDelayTicks));
                return;
            }

            Map target = eligible.RandomElement();
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, target);
            parms.faction = captor;
            if (incident.Worker.TryExecute(parms))
                nextThreatTick = ScheduleNext(now, ext);
            else
                nextThreatTick = SafeFutureTick(now, Math.Max(600, ext.retryDelayTicks));
        }

        private static bool HasActiveCapturedQueenForce(Pawn queen, Faction captor)
        {
            if (queen == null || captor == null || Find.Maps == null)
                return false;

            return Find.Maps.Any(map => map?.mapPawns?.AllPawnsSpawned?.Any(p =>
            {
                CompReplicatorSovereignty comp = p?.TryGetComp<CompReplicatorSovereignty>();
                return p != null && !p.Dead && p.Faction == captor &&
                    comp?.Authority == ReplicatorControlAuthority.Queen &&
                    comp.Controller == queen && comp.ControlFaction == captor;
            }) == true);
        }

        private static int ScheduleNext(int now, CapturedQueenThreatExtension ext)
        {
            int min = Math.Max(600, ext?.minIntervalTicks ?? 180000);
            int max = Math.Max(min, ext?.maxIntervalTicks ?? min);
            return SafeFutureTick(now, Rand.RangeInclusive(min, max));
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextThreatTick, "wngCapturedQueenNextSovereignThreatTick", -1);
            Scribe_Values.Look(ref nextCheckTick, "wngCapturedQueenThreatNextCheckTick", 0);
        }
    }

    public sealed class IncidentWorker_CapturedQueenSovereignStrike : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            Pawn queen = GameComponent_ReplicatorQueenState.Current?.Queen;
            return base.CanFireNowSub(parms)
                && map != null
                && map.IsPlayerHome
                && map.mapPawns?.FreeColonistsSpawned?.Any(p => p != null && !p.Dead) == true
                && ReplicatorSovereigntyUtility.TryGetCapturedQueenAsuranFaction(queen, out Faction captor)
                && (parms.faction == null || parms.faction == captor)
                && ReplicatorSovereigntyUtility.ResolveAutonomousSwarmFaction() != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            IncidentDef incident = def;
            CapturedQueenThreatExtension ext = incident?.GetModExtension<CapturedQueenThreatExtension>();
            Pawn queen = GameComponent_ReplicatorQueenState.Current?.Queen;
            if (map == null || ext == null || !map.IsPlayerHome ||
                !ReplicatorSovereigntyUtility.TryGetCapturedQueenAsuranFaction(queen, out Faction captor))
                return false;
            if (parms.faction != null && parms.faction != captor)
                return false;

            Faction autonomous = ReplicatorSovereigntyUtility.ResolveAutonomousSwarmFaction();
            if (autonomous == null)
                return false;

            PawnKindDef operative = ResolveKind(ext.operativeKind);
            PawnKindDef drone = ResolveKind(ext.droneKind);
            PawnKindDef hunter = ResolveKind(ext.hunterKind);
            if (operative == null || drone == null || hunter == null)
                return false;

            if (!CellFinder.TryFindRandomEdgeCellWith(
                    c => !c.Fogged(map) && c.Standable(map),
                    map,
                    CellFinder.EdgeRoadChance_Hostile,
                    out IntVec3 entryCell))
                return false;

            float points = Math.Max(300f, parms.points);
            float asuranShare = Math.Max(0.20f, Math.Min(0.75f, ext.asuranPointShare));
            int asuranCount = ClampCount(
                (int)Math.Round(points * asuranShare / Math.Max(1f, operative.combatPower)),
                ext.minAsuranCount,
                ext.maxAsuranCount);
            int blockCount = ClampCount(
                (int)Math.Round(points * (1f - asuranShare) / Math.Max(1f, hunter.combatPower)),
                ext.minBlockCount,
                ext.maxBlockCount);

            List<Pawn> force = new List<Pawn>();
            bool success = false;
            try
            {
                for (int i = 0; i < asuranCount; i++)
                {
                    PawnKindDef kind = ChooseAsuranKind(ext, i, asuranCount) ?? operative;
                    Pawn pawn = PawnGenerator.GeneratePawn(kind, captor);
                    if (pawn == null)
                        continue;
                    SpawnNearEntry(pawn, entryCell, map, 7);
                    if (pawn.Spawned && pawn.Map == map)
                        force.Add(pawn);
                    else if (!pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                }

                int sovereignBlocks = 0;
                for (int i = 0; i < blockCount; i++)
                {
                    PawnKindDef kind = ChooseBlockKind(ext, points, i) ?? (i % 3 == 0 ? drone : hunter);
                    Pawn block = PawnGenerator.GeneratePawn(kind, autonomous);
                    if (block == null)
                        continue;
                    SpawnNearEntry(block, entryCell, map, 8);
                    if (!block.Spawned || block.Map != map)
                    {
                        if (!block.Destroyed) block.Destroy(DestroyMode.Vanish);
                        continue;
                    }

                    if (!ReplicatorSovereigntyUtility.TryAcquireForCapturedQueen(queen, captor, block, out string rejection))
                    {
                        Log.Warning("[WNG] Captured-Queen strike could not bind a block Replicator: " + rejection);
                        if (!block.Destroyed) block.Destroy(DestroyMode.Vanish);
                        continue;
                    }

                    force.Add(block);
                    sovereignBlocks++;
                }

                if (force.Count == 0 || sovereignBlocks == 0 || !force.Any(p => AsuranNaniteUtility.IsNaniteHumanoid(p)))
                    return false;

                parms.faction = captor;
                LordMaker.MakeNewLord(captor, new LordJob_AssaultColony(captor), map, force);
                SendStandardLetter(def.letterLabel, def.letterText, def.letterDef, parms, new TargetInfo(entryCell, map));
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    foreach (Pawn pawn in force.ToList())
                    {
                        if (pawn != null && !pawn.Destroyed)
                            pawn.Destroy(DestroyMode.Vanish);
                    }
                }
            }
        }

        private static void SpawnNearEntry(Pawn pawn, IntVec3 entry, Map map, int radius)
        {
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(
                entry, map, Math.Max(1, radius), c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map));
            if (!cell.IsValid)
                cell = entry;
            GenSpawn.Spawn(pawn, cell, map);
        }

        private static PawnKindDef ChooseAsuranKind(CapturedQueenThreatExtension ext, int index, int total)
        {
            PawnKindDef operative = ResolveKind(ext.operativeKind);
            if (index == 0 && total >= 4 && Rand.Chance(ClampChance(ext.commanderChance)))
                return ResolveKind(ext.commanderKind) ?? operative;
            if (Rand.Chance(ClampChance(ext.technicianChance)))
                return ResolveKind(ext.technicianKind) ?? operative;
            return operative;
        }

        private static PawnKindDef ChooseBlockKind(CapturedQueenThreatExtension ext, float points, int index)
        {
            if (points >= 2400f && Rand.Chance(ClampChance(ext.titanChance)))
                return ResolveKind(ext.titanKind);
            if (points >= 1200f && Rand.Chance(ClampChance(ext.heavyChance)))
                return ResolveKind(ext.bulwarkKind);
            if (Rand.Chance(ClampChance(ext.specialistChance)))
            {
                string[] specialistNames =
                {
                    ext.controllerKind,
                    ext.repairerKind,
                    ext.burrowerKind,
                    ext.artilleryKind
                };
                return ResolveKind(specialistNames.Where(n => !n.NullOrEmpty()).RandomElement());
            }
            return ResolveKind(index % 3 == 0 ? ext.droneKind : ext.hunterKind);
        }

        private static PawnKindDef ResolveKind(string defName)
            => defName.NullOrEmpty() ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);

        private static int ClampCount(int value, int min, int max)
        {
            int low = Math.Max(1, min);
            int high = Math.Max(low, max);
            return Math.Max(low, Math.Min(high, value));
        }

        private static float ClampChance(float value)
            => Math.Max(0f, Math.Min(1f, value));
    }
}
