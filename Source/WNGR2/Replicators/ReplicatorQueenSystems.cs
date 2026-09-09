using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class GameComponent_ReplicatorQueenState : GameComponent
    {
        private bool questGenerated;
        private bool queenAbducted;
        private bool queenJoinedPlayer;
        public bool QuestGenerated => questGenerated;
        public bool QueenAbducted => queenAbducted;
        public bool QueenJoinedPlayer => queenJoinedPlayer;
        public int HostileOutbreakBonus => queenAbducted ? 1 : 0;
        public GameComponent_ReplicatorQueenState(Game game) { }
        public void MarkQuestGenerated() { questGenerated = true; }
        public void MarkJoined(Pawn queen)
        {
            if (queenJoinedPlayer) return;
            queenJoinedPlayer = true;
            questGenerated = true;
            Find.LetterStack.ReceiveLetter(
                "Replicator Queen sheltered",
                "The child-sized human-form Replicator has accepted your protection. Her sovereign nanite lattice is now aligned with your colony, but the recovery signal remains active and the hostile collective will still attempt to reclaim her.",
                LetterDefOf.PositiveEvent,
                queen);
        }
        public void MarkAbducted(Pawn queen)
        {
            if (queenAbducted) return;
            queenAbducted = true;
            questGenerated = true;
            Find.LetterStack.ReceiveLetter("Replicator Queen taken", "The human-form recovery team escaped with the Replicator child. The hostile collective now has direct access to her sovereign coordination lattice. Future Replicator outbreaks will begin slightly stronger.", LetterDefOf.ThreatBig, queen);
        }
        public override void ExposeData()
        {
            Scribe_Values.Look(ref questGenerated, "wngReplicatorQueenQuestGenerated", false);
            Scribe_Values.Look(ref queenAbducted, "wngReplicatorQueenAbducted", false);
            Scribe_Values.Look(ref queenJoinedPlayer, "wngReplicatorQueenJoinedPlayer", false);
        }
    }

    public static class ReplicatorQueenUtility
    {
        private static readonly string[] BlockReplicatorDefs = { "WNG_ReplicatorDrone", "WNG_ReplicatorHunter", "WNG_ReplicatorBulwark", "WNG_ReplicatorTitan", "WNG_ReplicatorController", "WNG_ReplicatorRepairer", "WNG_ReplicatorBurrower", "WNG_ReplicatorArtillery", "WNG_ReplicatorSiegeMass" };
        public static GeneDef QueenGeneDef => DefDatabase<GeneDef>.GetNamedSilentFail("WNG_ReplicatorQueenLink");
        public static bool IsQueen(Pawn pawn)
        {
            GeneDef def = QueenGeneDef;
            return pawn?.genes != null && def != null && pawn.genes.GenesListForReading.Any(g => g.def == def && g.Active);
        }
        public static bool IsBlockReplicator(Pawn pawn) => pawn?.def != null && BlockReplicatorDefs.Contains(pawn.def.defName);
        public static bool IsHumanFormReplicator(Pawn pawn)
        {
            return pawn?.genes?.Xenotype?.defName == "WNG_HumanFormReplicator" || pawn?.kindDef?.defName == "WNG_HumanFormReplicator" || pawn?.kindDef?.defName == "WNG_PlayerHumanFormReplicator" || pawn?.kindDef?.defName == "WNG_ReplicatorQueenChild";
        }
        public static bool IsReplicator(Pawn pawn) => IsBlockReplicator(pawn) || IsHumanFormReplicator(pawn);
        public static bool HasSovereignDirectiveAuthority(Pawn pawn) => IsQueen(pawn) || WNGImplantUtility.HasSovereignNeuralLattice(pawn);
        public static bool HasSovereignForFaction(Map map, Faction faction)
        {
            if (map == null || faction == null) return false;
            MapComponent_ReplicatorQueenPresence presence = map.GetComponent<MapComponent_ReplicatorQueenPresence>();
            return presence != null && presence.HasQueenFor(faction);
        }
    }

    public sealed class MapComponent_ReplicatorQueenPresence : MapComponent
    {
        private int nextRefreshTick;
        private readonly List<Pawn> queens = new List<Pawn>();
        public MapComponent_ReplicatorQueenPresence(Map map) : base(map) { }
        public bool HasQueenFor(Faction faction)
        {
            RefreshIfNeeded();
            return queens.Any(q => q != null && !q.Dead && q.Spawned && q.Faction == faction);
        }
        private void RefreshIfNeeded()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextRefreshTick) return;
            nextRefreshTick = now + 180;
            queens.Clear();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                if (ReplicatorQueenUtility.IsQueen(pawn) && !pawn.Dead) queens.Add(pawn);
        }
    }

    public sealed class CompProperties_AbilityReplicatorDirective : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityReplicatorDirective() { compClass = typeof(CompAbilityEffect_ReplicatorDirective); }
    }

    public sealed class CompAbilityEffect_ReplicatorDirective : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            if (!ReplicatorQueenUtility.HasSovereignDirectiveAuthority(caster))
            {
                if (throwMessages) Messages.Message("This pawn has no sovereign Replicator command authority.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn.Dead || !ReplicatorQueenUtility.IsReplicator(targetPawn) || targetPawn == caster)
            {
                if (throwMessages) Messages.Message("The sovereign directive can only target another WNG Replicator.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn queen = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (queen?.Faction == null || targetPawn == null || targetPawn.Dead || !ReplicatorQueenUtility.HasSovereignDirectiveAuthority(queen)) return;
            targetPawn.SetFaction(queen.Faction);
            string authority = ReplicatorQueenUtility.IsQueen(queen) ? "the Replicator Queen" : queen.LabelShortCap + "'s sovereign lattice";
            Messages.Message(targetPawn.LabelShortCap + " accepts " + authority + " directive.", targetPawn, MessageTypeDefOf.PositiveEvent, true);
        }
    }
}
