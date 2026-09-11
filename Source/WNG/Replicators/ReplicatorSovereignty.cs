using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public enum ReplicatorControlAuthority
    {
        None,
        Queen,
        NeuralLattice,
        TemporaryAsuran
    }

    public sealed class CompProperties_ReplicatorSovereignty : CompProperties
    {
        public int validationIntervalTicks = 60;

        public CompProperties_ReplicatorSovereignty()
        {
            compClass = typeof(CompReplicatorSovereignty);
        }
    }

    public sealed class ReplicatorQueenSovereigntyExtension : DefModExtension
    {
        public float directControlRange = 40f;
        public float swarmControlRadius = 24f;
        public int maxControlledBlocks = 12;
    }

    /// <summary>
    /// Persistent authority metadata attached to every real WNG block Replicator. Authority is
    /// separate from learned adaptation and stored matter. The current implementation activates the
    /// Queen authority path; Neural Lattice and temporary Asuran authority are reserved enum/domain
    /// values so later layers can use the same persistence/transaction model instead of inventing a
    /// competing controller system.
    /// </summary>
    public sealed class CompReplicatorSovereignty : ThingComp
    {
        private ReplicatorControlAuthority authority;
        private Pawn controller;
        private Faction originalFaction;
        private Faction controlFaction;
        private string domainKey;
        private int temporaryUntil = -1;
        private int nextValidationTick;
        private bool lastInterferenceBlocked;

        private Pawn Pawn => parent as Pawn;
        private CompProperties_ReplicatorSovereignty Props => (CompProperties_ReplicatorSovereignty)props;

        public ReplicatorControlAuthority Authority => authority;
        public Pawn Controller => controller;
        public Faction OriginalFaction => originalFaction;
        public Faction ControlFaction => controlFaction;
        public string DomainKey => domainKey;
        public bool HasAuthority => authority != ReplicatorControlAuthority.None;
        public bool IsQueenControlled => authority == ReplicatorControlAuthority.Queen && controller != null;
        public bool AuthorityValid => HasAuthority && ReplicatorSovereigntyUtility.IsAuthorityValid(this);
        public bool InterferenceBlocked => HasAuthority && ReplicatorSovereigntyUtility.IsInterferenceBlocking(this);
        public bool Operational => AuthorityValid && !InterferenceBlocked;

        public bool IsQueenControlledBy(Pawn exactQueen)
            => IsQueenControlled && controller == exactQueen && !string.IsNullOrEmpty(domainKey);

        public bool TryAssignQueen(Pawn exactQueen, out string rejection)
        {
            rejection = null;
            Pawn pawn = Pawn;
            if (!ReplicatorSovereigntyUtility.IsExactQueen(exactQueen))
            {
                rejection = "Only the exact Replicator Queen has innate sovereign authority.";
                return false;
            }
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                rejection = "The target Replicator is not physically available.";
                return false;
            }
            if (!ReplicatorSovereigntyUtility.SharesPhysicalPresence(exactQueen, pawn))
            {
                rejection = "The Queen and target Replicator must be physically present together.";
                return false;
            }
            if (ReplicatorSovereigntyUtility.IsQueenSignalDisrupted(exactQueen))
            {
                rejection = "The Queen's nanite lattice is disrupted by EMP.";
                return false;
            }
            if (ReplicatorEMP.IsSuppressed(pawn))
            {
                rejection = "The target Replicator is disrupted by EMP.";
                return false;
            }
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
            {
                rejection = "An active Replicator containment field is blocking sovereign acquisition.";
                return false;
            }
            if (HasAuthority && !IsQueenControlledBy(exactQueen))
            {
                rejection = "That Replicator already belongs to a different control domain.";
                return false;
            }
            if (IsQueenControlledBy(exactQueen))
                return true;
            if (exactQueen.Faction == null)
            {
                rejection = "The Queen has no active faction authority.";
                return false;
            }

            originalFaction = pawn.Faction;
            controlFaction = exactQueen.Faction;
            controller = exactQueen;
            authority = ReplicatorControlAuthority.Queen;
            domainKey = ReplicatorSovereigntyUtility.QueenDomainKey(exactQueen);
            temporaryUntil = -1;
            lastInterferenceBlocked = false;

            if (pawn.Faction != controlFaction)
                pawn.SetFaction(controlFaction, exactQueen);
            pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            return true;
        }

        public void CopyAuthorityFrom(CompReplicatorSovereignty other)
        {
            if (other == null || !other.HasAuthority)
            {
                ClearMetadata();
                return;
            }

            authority = other.authority;
            controller = other.controller;
            originalFaction = other.originalFaction;
            controlFaction = other.controlFaction;
            domainKey = other.domainKey;
            temporaryUntil = other.temporaryUntil;
            lastInterferenceBlocked = other.lastInterferenceBlocked;
        }

        public bool ReleaseAuthority(bool restoreFaction = true)
        {
            Pawn pawn = Pawn;
            if (!HasAuthority)
                return false;

            Faction restore = originalFaction;
            ClearMetadata();

            if (pawn != null && !pawn.Destroyed)
            {
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                if (restoreFaction)
                {
                    if (restore == null || restore.defeated)
                        restore = ReplicatorSovereigntyUtility.ResolveAutonomousSwarmFaction();
                    if (pawn.Faction != restore)
                        pawn.SetFaction(restore);
                }
            }
            return true;
        }

        private void ClearMetadata()
        {
            authority = ReplicatorControlAuthority.None;
            controller = null;
            originalFaction = null;
            controlFaction = null;
            domainKey = null;
            temporaryUntil = -1;
            lastInterferenceBlocked = false;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!HasAuthority || Pawn == null || Pawn.Dead || Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextValidationTick)
                return;
            nextValidationTick = now + Math.Max(15, Props.validationIntervalTicks);

            if (temporaryUntil >= 0 && now >= temporaryUntil)
            {
                ReleaseAuthority();
                return;
            }
            if (!ReplicatorSovereigntyUtility.IsAuthorityValid(this))
            {
                ReleaseAuthority();
                return;
            }

            bool blocked = ReplicatorSovereigntyUtility.IsInterferenceBlocking(this);
            if (blocked && !lastInterferenceBlocked)
                Pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            lastInterferenceBlocked = blocked;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            Pawn pawn = Pawn;
            if (pawn == null || !IsQueenControlled || controller?.Faction != Faction.OfPlayer || pawn.Faction != Faction.OfPlayer)
                yield break;

            bool blocked = !AuthorityValid || InterferenceBlocked;
            string blockedReason = !AuthorityValid
                ? "The Queen is no longer physically present as this Replicator's sovereign controller."
                : "Sovereign command is blocked by EMP or an active containment field.";

            Command_Target move = new Command_Target
            {
                defaultLabel = "Sovereign move",
                defaultDesc = "Order this Queen-controlled Replicator to move to a chosen cell without requiring a mechanitor overseer.",
                targetingParams = TargetingParameters.ForCell(),
                action = target => IssueMove(target.Cell)
            };
            if (blocked) move.Disable(blockedReason);
            yield return move;

            Command_Target attack = new Command_Target
            {
                defaultLabel = "Sovereign attack",
                defaultDesc = "Order this Queen-controlled Replicator to attack an exact hostile target.",
                targetingParams = TargetingParameters.ForAttackAny(),
                action = IssueAttack
            };
            if (blocked) attack.Disable(blockedReason);
            yield return attack;

            if (pawn.def?.defName == "WNG_ReplicatorRepairer")
            {
                TargetingParameters repairParams = new TargetingParameters
                {
                    canTargetLocations = false,
                    canTargetPawns = true,
                    canTargetBuildings = false,
                    canTargetAnimals = false,
                    canTargetHumans = false,
                    canTargetMechs = true,
                    validator = info => info.Thing is Pawn target &&
                        target != pawn &&
                        ReplicatorSovereigntyUtility.IsBlockReplicator(target) &&
                        ReplicatorSovereigntyUtility.SameDomain(pawn, target)
                };
                Command_Target repair = new Command_Target
                {
                    defaultLabel = "Sovereign repair",
                    defaultDesc = "Order this Repairer to repair an exact Replicator in the same sovereign domain.",
                    targetingParams = repairParams,
                    action = IssueRepair
                };
                if (blocked) repair.Disable(blockedReason);
                yield return repair;
            }

            if (pawn.def?.defName == "WNG_ReplicatorBurrower")
            {
                TargetingParameters breachParams = new TargetingParameters
                {
                    canTargetLocations = false,
                    canTargetPawns = false,
                    canTargetBuildings = true,
                    canTargetItems = false,
                    validator = info => info.Thing != null && info.Thing.Spawned && info.Thing.Map == pawn.Map
                };
                Command_Target breach = new Command_Target
                {
                    defaultLabel = "Sovereign breach",
                    defaultDesc = "Order this Burrower to use its real breaching job against a selected structure.",
                    targetingParams = breachParams,
                    action = IssueBreach
                };
                if (blocked) breach.Disable(blockedReason);
                yield return breach;
            }

            CompReplicatorHierarchy hierarchy = pawn.TryGetComp<CompReplicatorHierarchy>();
            if (hierarchy?.CanUpgradeNow == true)
            {
                Command_Action recombine = new Command_Action
                {
                    defaultLabel = "Sovereign recombine",
                    defaultDesc = "Explicitly order nearby same-domain Replicators of this form to perform their normal upward hierarchy transaction. Stored matter, adaptations and sovereign authority are conserved.",
                    action = delegate
                    {
                        if (!hierarchy.TrySovereignRecombine(controller, out string reason) && !reason.NullOrEmpty())
                            Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    }
                };
                if (blocked) recombine.Disable(blockedReason);
                yield return recombine;
            }

            yield return new Command_Action
            {
                defaultLabel = "Release sovereign control",
                defaultDesc = "Return this block Replicator to its pre-Queen autonomous faction/domain.",
                action = delegate { ReleaseAuthority(); }
            };
        }

        private void IssueMove(IntVec3 cell)
        {
            Pawn pawn = Pawn;
            if (!Operational || pawn?.Map == null || !cell.IsValid || !cell.InBounds(pawn.Map))
                return;
            if (!pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
            {
                Messages.Message("The Replicator cannot reach that cell.", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            Job move = JobMaker.MakeJob(JobDefOf.Goto, cell);
            move.locomotionUrgency = LocomotionUrgency.Jog;
            pawn.jobs.StartJob(move, JobCondition.InterruptForced);
        }

        private void IssueAttack(LocalTargetInfo target)
        {
            Pawn pawn = Pawn;
            Thing thing = target.Thing;
            if (!Operational || pawn?.Map == null || thing == null || thing.Destroyed || !thing.Spawned || thing.Map != pawn.Map)
                return;
            if (!pawn.HostileTo(thing))
            {
                Messages.Message("Choose a target hostile to the Queen's current faction.", thing, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Job job;
            if (pawn.def?.defName == "WNG_ReplicatorArtillery" && thing is Pawn artilleryTarget)
            {
                ReplicatorSpecialistExtension ext = ReplicatorSpecialistUtility.Extension(pawn);
                float min = Math.Max(0f, ext?.artilleryMinRange ?? 8f);
                float max = Math.Max(min + 1f, ext?.artilleryRange ?? 42f);
                float distanceSq = artilleryTarget.Position.DistanceToSquared(pawn.Position);
                JobDef artilleryJob = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorArtilleryFire");
                if (artilleryJob != null && distanceSq >= min * min && distanceSq <= max * max &&
                    GenSight.LineOfSight(pawn.Position, artilleryTarget.Position, pawn.Map))
                    job = JobMaker.MakeJob(artilleryJob, artilleryTarget);
                else
                    job = JobMaker.MakeJob(JobDefOf.AttackMelee, thing);
            }
            else
            {
                job = JobMaker.MakeJob(JobDefOf.AttackMelee, thing);
            }
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        private void IssueRepair(LocalTargetInfo target)
        {
            Pawn pawn = Pawn;
            Pawn repairTarget = target.Pawn;
            JobDef repairJob = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorRepair");
            if (!Operational || pawn == null || repairTarget == null || repairJob == null ||
                !ReplicatorSovereigntyUtility.SameDomain(pawn, repairTarget))
                return;
            pawn.jobs.StartJob(JobMaker.MakeJob(repairJob, repairTarget), JobCondition.InterruptForced);
        }

        private void IssueBreach(LocalTargetInfo target)
        {
            Pawn pawn = Pawn;
            Thing building = target.Thing;
            JobDef breachJob = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorBreach");
            if (!Operational || pawn == null || building == null || breachJob == null)
                return;
            if (building.Faction != null && !pawn.HostileTo(building))
            {
                Messages.Message("Choose a hostile or factionless blocking structure.", building, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }
            pawn.jobs.StartJob(JobMaker.MakeJob(breachJob, building), JobCondition.InterruptForced);
        }

        public override string CompInspectStringExtra()
        {
            if (!HasAuthority)
                return null;
            string controllerLabel = controller?.LabelShort ?? "unresolved controller";
            string status = !AuthorityValid ? "authority lost" : InterferenceBlocked ? "signal disrupted" : "operational";
            return $"Replicator control: {authority} — {controllerLabel}\nSovereign signal: {status}";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref authority, "wngReplicatorAuthority", ReplicatorControlAuthority.None);
            Scribe_References.Look(ref controller, "wngReplicatorController");
            Scribe_References.Look(ref originalFaction, "wngReplicatorOriginalFaction");
            Scribe_References.Look(ref controlFaction, "wngReplicatorControlFaction");
            Scribe_Values.Look(ref domainKey, "wngReplicatorControlDomain");
            Scribe_Values.Look(ref temporaryUntil, "wngReplicatorTemporaryAuthorityUntil", -1);
            Scribe_Values.Look(ref nextValidationTick, "wngReplicatorAuthorityNextValidation", 0);
            Scribe_Values.Look(ref lastInterferenceBlocked, "wngReplicatorAuthorityWasBlocked", false);
        }
    }

    public static class ReplicatorSovereigntyUtility
    {
        public static bool IsBlockReplicator(Pawn pawn)
            => pawn != null && pawn.TryGetComp<CompReplicatorState>() != null && pawn.TryGetComp<CompReplicatorSovereignty>() != null;

        public static bool IsExactQueen(Pawn pawn)
            => pawn != null && !pawn.Dead && GameComponent_ReplicatorQueenState.Current?.Queen == pawn;

        public static string QueenDomainKey(Pawn queen)
            => queen == null ? null : "Queen:" + queen.GetUniqueLoadID();

        public static bool IsQueenSignalDisrupted(Pawn queen)
        {
            if (queen?.health?.hediffSet == null)
                return true;
            HediffDef disrupted = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_AsuranEMPDisrupted");
            return disrupted != null && queen.health.hediffSet.HasHediff(disrupted);
        }

        public static bool SharesPhysicalPresence(Pawn controller, Pawn block)
        {
            if (controller == null || block == null || controller.Dead || block.Dead)
                return false;
            if (controller.Spawned && block.Spawned)
                return controller.Map != null && controller.Map == block.Map;
            if (!controller.Spawned && !block.Spawned)
            {
                Caravan controllerCaravan = controller.GetCaravan();
                return controllerCaravan != null && controllerCaravan == block.GetCaravan();
            }
            return false;
        }

        public static bool IsAuthorityValid(CompReplicatorSovereignty comp)
        {
            if (comp == null || !comp.HasAuthority || comp.Controller == null)
                return false;
            Pawn block = comp.parent as Pawn;
            Pawn controller = comp.Controller;
            if (block == null || block.Dead || controller.Dead || controller.Faction == null)
                return false;
            if (comp.ControlFaction != controller.Faction)
                return false;
            if (!SharesPhysicalPresence(controller, block))
                return false;

            switch (comp.Authority)
            {
                case ReplicatorControlAuthority.Queen:
                    return IsExactQueen(controller) && comp.DomainKey == QueenDomainKey(controller);
                case ReplicatorControlAuthority.NeuralLattice:
                case ReplicatorControlAuthority.TemporaryAsuran:
                    return !string.IsNullOrEmpty(comp.DomainKey);
                default:
                    return false;
            }
        }

        public static bool IsInterferenceBlocking(CompReplicatorSovereignty comp)
        {
            Pawn block = comp?.parent as Pawn;
            Pawn controller = comp?.Controller;
            if (block == null)
                return true;
            if (ReplicatorEMP.IsSuppressed(block))
                return true;
            if (block.Spawned && block.Map != null && ReplicatorContainmentUtility.IsContained(block.Map, block.Position))
                return true;
            if (comp.Authority == ReplicatorControlAuthority.Queen && IsQueenSignalDisrupted(controller))
                return true;
            if (controller?.Spawned == true && controller.Map != null && ReplicatorContainmentUtility.IsContained(controller.Map, controller.Position))
                return true;
            return false;
        }

        public static bool SameDomain(Pawn a, Pawn b)
        {
            if (a == null || b == null || a.Faction != b.Faction)
                return false;
            CompReplicatorSovereignty ca = a.TryGetComp<CompReplicatorSovereignty>();
            CompReplicatorSovereignty cb = b.TryGetComp<CompReplicatorSovereignty>();
            bool aControlled = ca?.HasAuthority == true;
            bool bControlled = cb?.HasAuthority == true;
            if (!aControlled && !bControlled)
                return true;
            if (aControlled != bControlled)
                return false;
            return ca.Authority == cb.Authority && ca.Controller == cb.Controller &&
                   !string.IsNullOrEmpty(ca.DomainKey) && ca.DomainKey == cb.DomainKey;
        }

        public static bool IsOperationallyControlled(Pawn pawn)
            => pawn?.TryGetComp<CompReplicatorSovereignty>()?.Operational == true;

        public static bool TryAcquireForQueen(Pawn queen, Pawn block, float range, int maxControlled, out string rejection)
        {
            rejection = null;
            if (!IsExactQueen(queen) || queen.Faction != Faction.OfPlayer || !queen.Spawned || queen.Map == null)
            {
                rejection = "The exact Replicator Queen must be physically present and player-aligned.";
                return false;
            }
            if (IsQueenSignalDisrupted(queen))
            {
                rejection = "The Queen's nanite lattice is disrupted by EMP.";
                return false;
            }
            if (!IsBlockReplicator(block) || block.Dead || !block.Spawned || block.Map != queen.Map)
            {
                rejection = "Choose a living WNG block Replicator on the Queen's map.";
                return false;
            }
            float allowed = Math.Max(1f, range);
            if (queen.Position.DistanceToSquared(block.Position) > allowed * allowed)
            {
                rejection = "That Replicator is outside the Queen's sovereign command range.";
                return false;
            }

            CompReplicatorSovereignty comp = block.TryGetComp<CompReplicatorSovereignty>();
            if (comp.IsQueenControlledBy(queen))
                return true;

            int cap = Math.Max(1, maxControlled);
            int controlled = queen.Map.mapPawns.AllPawnsSpawned.Count(p =>
                p?.TryGetComp<CompReplicatorSovereignty>()?.IsQueenControlledBy(queen) == true);
            if (controlled >= cap)
            {
                rejection = "The Queen's current sovereign control limit has been reached.";
                return false;
            }

            return comp.TryAssignQueen(queen, out rejection);
        }

        public static int ReleaseQueenDomain(Pawn queen)
        {
            if (queen == null || Find.Maps == null)
                return 0;
            int released = 0;
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    CompReplicatorSovereignty comp = pawn?.TryGetComp<CompReplicatorSovereignty>();
                    if (comp?.IsQueenControlledBy(queen) == true && comp.ReleaseAuthority())
                        released++;
                }
            }
            return released;
        }

        public static Faction ResolveAutonomousSwarmFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            if (def == null || Find.FactionManager?.AllFactionsListForReading == null)
                return null;
            return Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => f != null && !f.defeated && f.def == def);
        }

        public static void EnsureQueenSovereigntyHediff(Pawn queen)
        {
            if (!IsExactQueen(queen) || queen.health?.hediffSet == null)
                return;
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_ReplicatorQueenSovereignty");
            if (def != null && !queen.health.hediffSet.HasHediff(def))
                queen.health.AddHediff(def);
        }
    }

    /// <summary>
    /// Exact-Queen command surface. This is a command transmitter, not a passive stat aura: each
    /// acquired block receives persistent authority metadata and actual faction ownership changes.
    /// </summary>
    public sealed class Hediff_ReplicatorQueenSovereignty : Hediff
    {
        private ReplicatorQueenSovereigntyExtension Extension => def?.GetModExtension<ReplicatorQueenSovereigntyExtension>();

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                yield return gizmo;

            if (!ReplicatorSovereigntyUtility.IsExactQueen(pawn) || pawn.Faction != Faction.OfPlayer || !pawn.Spawned || pawn.Map == null)
                yield break;

            float range = Math.Max(1f, Extension?.directControlRange ?? 40f);
            float swarmRadius = Math.Max(1f, Extension?.swarmControlRadius ?? 24f);
            int cap = Math.Max(1, Extension?.maxControlledBlocks ?? 12);
            bool disrupted = ReplicatorSovereigntyUtility.IsQueenSignalDisrupted(pawn) ||
                             ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position);

            TargetingParameters acquireParams = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetAnimals = false,
                canTargetHumans = false,
                canTargetMechs = true,
                validator = info => info.Thing is Pawn target &&
                    ReplicatorSovereigntyUtility.IsBlockReplicator(target) && target != pawn
            };

            Command_Target acquire = new Command_Target
            {
                defaultLabel = "Sovereign control",
                defaultDesc = "Take genuine persistent control of an exact block Replicator within command range. The block changes to the Queen's faction and keeps that controller identity through split/recombine transactions.",
                targetingParams = acquireParams,
                action = target =>
                {
                    Pawn block = target.Pawn;
                    if (!ReplicatorSovereigntyUtility.TryAcquireForQueen(pawn, block, range, cap, out string rejection))
                    {
                        if (!rejection.NullOrEmpty())
                            Messages.Message(rejection, pawn, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }
                    Messages.Message($"{block.LabelShort} is now under the Replicator Queen's sovereign control.", block, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            };
            if (disrupted)
                acquire.Disable("The Queen's sovereign signal is blocked by EMP or active Replicator containment.");
            yield return acquire;

            Command_Action swarm = new Command_Action
            {
                defaultLabel = "Seize nearby swarm",
                defaultDesc = "Take sovereign control of eligible nearby block Replicators up to the Queen's Def-tunable total control limit.",
                action = delegate
                {
                    List<Pawn> candidates = pawn.Map.mapPawns.AllPawnsSpawned
                        .Where(p => p != pawn && ReplicatorSovereigntyUtility.IsBlockReplicator(p))
                        .Where(p => p.Position.DistanceToSquared(pawn.Position) <= swarmRadius * swarmRadius)
                        .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                        .ThenBy(p => p.thingIDNumber)
                        .ToList();
                    int gained = 0;
                    foreach (Pawn target in candidates)
                    {
                        if (ReplicatorSovereigntyUtility.TryAcquireForQueen(pawn, target, swarmRadius, cap, out _))
                            gained++;
                    }
                    Messages.Message(
                        gained > 0 ? $"The Queen took sovereign control of {gained} block Replicator(s)." : "No eligible block Replicators could be brought under sovereign control.",
                        pawn,
                        gained > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.RejectInput,
                        historical: false);
                }
            };
            if (disrupted)
                swarm.Disable("The Queen's sovereign signal is blocked by EMP or active Replicator containment.");
            yield return swarm;

            yield return new Command_Action
            {
                defaultLabel = "Release sovereign swarm",
                defaultDesc = "Release all currently spawned block Replicators controlled by this exact Queen and restore their pre-control faction/domain.",
                action = delegate
                {
                    int released = ReplicatorSovereigntyUtility.ReleaseQueenDomain(pawn);
                    Messages.Message($"Released sovereign control of {released} block Replicator(s).", pawn, MessageTypeDefOf.NeutralEvent, historical: false);
                }
            };
        }
    }

    public sealed class GameComponent_ReplicatorSovereignty : GameComponent
    {
        private int nextMaintenanceTick;

        public GameComponent_ReplicatorSovereignty(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null)
                return;
            int now = Find.TickManager.TicksGame;
            if (now < nextMaintenanceTick)
                return;
            nextMaintenanceTick = now + 250;
            ReplicatorSovereigntyUtility.EnsureQueenSovereigntyHediff(GameComponent_ReplicatorQueenState.Current?.Queen);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextMaintenanceTick, "wngReplicatorSovereigntyMaintenanceTick", 0);
        }
    }

    public sealed class JobGiver_ReplicatorSovereignSuppressed : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            CompReplicatorSovereignty comp = pawn?.TryGetComp<CompReplicatorSovereignty>();
            if (comp?.HasAuthority != true || !comp.InterferenceBlocked)
                return null;
            JobDef def = DefDatabase<JobDef>.GetNamedSilentFail("WNG_ReplicatorSovereignSuppressed");
            return def == null ? null : JobMaker.MakeJob(def);
        }
    }

    public sealed class JobDriver_ReplicatorSovereignSuppressed : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_General.Wait(120);
        }
    }
}
