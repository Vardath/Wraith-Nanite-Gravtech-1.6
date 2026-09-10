using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Save-persistent identity/outcome state for the single Replicator Queen.  This component stores
    /// the exact pawn reference instead of reducing the quest outcome to an abstract outbreak bonus.
    /// The Queen-vault lifecycle registers the same pawn before either automatic player recovery or
    /// a completed Lattice map-edge kidnapping is committed.
    /// </summary>
    public sealed class GameComponent_ReplicatorQueenState : GameComponent
    {
        private bool questGenerated;
        private bool queenAbducted;
        private bool queenJoinedPlayer;
        private Pawn queenPawn;
        private Faction queenCaptorFaction;

        public bool QuestGenerated => questGenerated;
        public bool QueenAbducted => queenAbducted;
        public bool QueenJoinedPlayer => queenJoinedPlayer;
        public Pawn QueenPawn => queenPawn;
        public Faction QueenCaptorFaction => queenCaptorFaction;
        public bool HostileCollectiveHasSovereignControl => queenAbducted && !queenJoinedPlayer && queenCaptorFaction != null;

        public GameComponent_ReplicatorQueenState(Game game) { }

        public void MarkQuestGenerated()
        {
            questGenerated = true;
        }

        public bool RegisterQueen(Pawn queen)
        {
            if (queen == null || queen.Destroyed || queen.kindDef?.defName != "WNG_ReplicatorQueenChild"
                || !ReplicatorQueenUtility.IsHumanFormReplicator(queen))
                return false;

            if (queenPawn != null && queenPawn != queen && !queenPawn.Destroyed)
            {
                Log.Error("[WNG] Refusing to register a second live Replicator Queen; the Queen quest must preserve one exact pawn.");
                return false;
            }

            ReplicatorQueenUtility.EnsureQueenAuthority(queen);
            queenPawn = queen;
            questGenerated = true;
            return true;
        }

        public void MarkJoined(Pawn queen)
        {
            if (!RegisterQueen(queen) || Faction.OfPlayer == null)
                return;

            bool firstJoin = !queenJoinedPlayer || queenAbducted;
            if (queen.Faction != Faction.OfPlayer)
                queen.SetFaction(Faction.OfPlayer, null);

            queenJoinedPlayer = true;
            queenAbducted = false;
            queenCaptorFaction = null;
            if (firstJoin)
            {
                Find.LetterStack.ReceiveLetter(
                    "Replicator Queen recovered",
                    "As she emerges from the cryosleep chamber, the thirteen-year-old human-form Replicator Queen immediately joins your colony. Her sovereign lattice can directly bring base Replicators under your faction while she is present to coordinate them. The hostile Asuran/Lattice collective may attempt to recover her by force.",
                    LetterDefOf.PositiveEvent,
                    queen);
            }
        }

        public void MarkAbducted(Pawn queen, Faction captor = null)
        {
            if (!RegisterQueen(queen))
                return;

            Faction resolvedCaptor = captor;
            if (resolvedCaptor == null)
            {
                FactionDef latticeDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_PrecursorCollective");
                resolvedCaptor = latticeDef == null ? null : Find.FactionManager.FirstFactionOfDef(latticeDef);
            }
            if (resolvedCaptor == null || resolvedCaptor == Faction.OfPlayer)
            {
                Log.Error("[WNG] Replicator Queen capture was not committed because no valid hostile Lattice captor faction was available.");
                return;
            }

            bool firstCapture = !queenAbducted || queenCaptorFaction != resolvedCaptor;
            queenAbducted = true;
            queenJoinedPlayer = false;
            queenCaptorFaction = resolvedCaptor;
            if (firstCapture)
            {
                Find.LetterStack.ReceiveLetter(
                    "Replicator Queen taken",
                    "The Lattice Collective escaped with the Replicator Queen. They now possess genuine sovereign access to base Replicators rather than a mere temporary lattice intrusion. Their future threat systems may field Replicators directly under Lattice control until that outcome is reversed.",
                    LetterDefOf.ThreatBig,
                    queen);
            }
        }

        public bool FactionHasCapturedQueenAuthority(Faction faction)
        {
            return faction != null && HostileCollectiveHasSovereignControl && faction == queenCaptorFaction;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref questGenerated, "wngReplicatorQueenQuestGenerated", false);
            Scribe_Values.Look(ref queenAbducted, "wngReplicatorQueenAbducted", false);
            Scribe_Values.Look(ref queenJoinedPlayer, "wngReplicatorQueenJoinedPlayer", false);
            Scribe_References.Look(ref queenPawn, "wngReplicatorQueenPawn");
            Scribe_References.Look(ref queenCaptorFaction, "wngReplicatorQueenCaptorFaction");
        }
    }

    public static class ReplicatorQueenUtility
    {
        private static readonly HashSet<string> BlockReplicatorDefs = new HashSet<string>
        {
            "WNG_ReplicatorDrone", "WNG_ReplicatorHunter", "WNG_ReplicatorBulwark", "WNG_ReplicatorTitan",
            "WNG_ReplicatorController", "WNG_ReplicatorRepairer", "WNG_ReplicatorBurrower",
            "WNG_ReplicatorArtillery", "WNG_ReplicatorSiegeMass"
        };

        public static GeneDef QueenGeneDef => DefDatabase<GeneDef>.GetNamedSilentFail("WNG_ReplicatorQueenLink");

        public static bool IsQueen(Pawn pawn)
        {
            GeneDef def = QueenGeneDef;
            return pawn?.genes != null && def != null
                && pawn.genes.GenesListForReading.Any(gene => gene.def == def && gene.Active);
        }

        public static bool EnsureQueenAuthority(Pawn pawn)
        {
            if (pawn?.genes == null || pawn.kindDef?.defName != "WNG_ReplicatorQueenChild" || !IsHumanFormReplicator(pawn))
                return false;

            GeneDef queenGene = QueenGeneDef;
            if (queenGene == null)
                return false;
            if (pawn.genes.GetGene(queenGene) == null)
                pawn.genes.AddGene(queenGene, true);
            return IsQueen(pawn);
        }

        public static bool IsBlockReplicator(Pawn pawn)
        {
            return pawn?.def != null && BlockReplicatorDefs.Contains(pawn.def.defName);
        }

        public static bool IsHumanFormReplicator(Pawn pawn)
        {
            return pawn?.genes?.Xenotype?.defName == "WNG_HumanFormReplicator"
                || pawn?.kindDef?.defName == "WNG_HumanFormReplicator"
                || pawn?.kindDef?.defName == "WNG_HumanFormReplicatorInfiltrator"
                || pawn?.kindDef?.defName == "WNG_HumanFormReplicatorSoldier"
                || pawn?.kindDef?.defName == "WNG_HumanFormReplicatorCoordinator"
                || pawn?.kindDef?.defName == "WNG_PlayerHumanFormReplicator"
                || pawn?.kindDef?.defName == "WNG_ReplicatorQueenChild";
        }

        public static bool IsReplicator(Pawn pawn)
        {
            return IsBlockReplicator(pawn) || IsHumanFormReplicator(pawn);
        }

        public static bool HasSovereignDirectiveAuthority(Pawn pawn)
        {
            return IsQueen(pawn) || WNGImplantUtility.HasSovereignNeuralLattice(pawn);
        }

        /// <summary>
        /// Passive local swarm coordination belongs to a real Queen only.  Implant bearers instead
        /// maintain explicit target-specific bindings in CompReplicatorControl.
        /// </summary>
        public static bool HasSovereignForFaction(Map map, Faction faction)
        {
            if (map == null || faction == null)
                return false;
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
            return queens.Any(queen => queen != null && !queen.Dead && queen.Spawned && queen.Faction == faction);
        }

        private void RefreshIfNeeded()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextRefreshTick)
                return;
            nextRefreshTick = now + 180;
            queens.Clear();
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (ReplicatorQueenUtility.IsQueen(pawn) && !pawn.Dead)
                    queens.Add(pawn);
            }
        }
    }

    public sealed class CompProperties_AbilityReplicatorDirective : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityReplicatorDirective()
        {
            compClass = typeof(CompAbilityEffect_ReplicatorDirective);
        }
    }

    public sealed class CompAbilityEffect_ReplicatorDirective : CompAbilityEffect
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            if (!ReplicatorQueenUtility.HasSovereignDirectiveAuthority(caster))
            {
                if (throwMessages)
                    Messages.Message("This pawn has no sovereign Replicator command authority.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn.Dead || targetPawn == caster || !ReplicatorQueenUtility.IsBlockReplicator(targetPawn))
            {
                if (throwMessages)
                    Messages.Message("The sovereign directive can only target a base/block WNG Replicator.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (caster?.Faction == null || targetPawn.Faction == caster.Faction)
            {
                if (throwMessages)
                    Messages.Message("That Replicator is already aligned with this faction.", targetPawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn authority = parent.pawn;
            Pawn targetPawn = target.Pawn;
            if (authority?.Faction == null || targetPawn == null || targetPawn.Dead
                || !ReplicatorQueenUtility.IsBlockReplicator(targetPawn)
                || !ReplicatorQueenUtility.HasSovereignDirectiveAuthority(authority))
                return;

            CompReplicatorControl control = targetPawn.TryGetComp<CompReplicatorControl>();
            if (control == null)
                return;

            if (ReplicatorQueenUtility.IsQueen(authority))
            {
                control.ClearSovereignBinding();
                targetPawn.SetFaction(authority.Faction, null);
            }
            else
            {
                control.BindToSovereign(authority);
            }

            string source = ReplicatorQueenUtility.IsQueen(authority)
                ? "the Replicator Queen"
                : authority.LabelShortCap + "'s sovereign neural lattice";
            Messages.Message(targetPawn.LabelShortCap + " accepts " + source + " directive.", targetPawn, MessageTypeDefOf.PositiveEvent, true);
        }
    }
}
