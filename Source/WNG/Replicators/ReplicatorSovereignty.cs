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
    /// Persistent authority metadata attached to every real WNG block Replicator. Queen authority,
    /// Sovereign Neural Lattice authority and future temporary Asuran intrusion share one physical
    /// transaction model without becoming the same identity/domain.
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


        // Temporary Asuran intrusion is an override rather than destructive reassignment. The exact
        // pre-intrusion controller/faction/domain is kept here so expiry can restore it.
        private bool hasSuspendedAuthority;
        private ReplicatorControlAuthority suspendedAuthority;
        private Pawn suspendedController;
        private Faction suspendedOriginalFaction;
        private Faction suspendedControlFaction;
        private string suspendedDomainKey;
        private int suspendedTemporaryUntil = -1;
        private Faction suspendedPawnFaction;

        private Pawn Pawn => parent as Pawn;
        private CompProperties_ReplicatorSovereignty Props => (CompProperties_ReplicatorSovereignty)props;

        public ReplicatorControlAuthority Authority => authority;
        public Pawn Controller => controller;
        public Faction OriginalFaction => originalFaction;
        public Faction ControlFaction => controlFaction;
        public string DomainKey => domainKey;
        public int TemporaryUntil => temporaryUntil;
        public bool HasSuspendedAuthority => hasSuspendedAuthority;
        public ReplicatorControlAuthority SuspendedAuthority => suspendedAuthority;
        public Pawn SuspendedController => suspendedController;
        public Faction SuspendedOriginalFaction => suspendedOriginalFaction;
        public Faction SuspendedControlFaction => suspendedControlFaction;
        public string SuspendedDomainKey => suspendedDomainKey;
        public int SuspendedTemporaryUntil => suspendedTemporaryUntil;
        public Faction SuspendedPawnFaction => suspendedPawnFaction;
        public bool HasAuthority => authority != ReplicatorControlAuthority.None;
        public bool IsQueenControlled => authority == ReplicatorControlAuthority.Queen && controller != null;
        public bool IsNeuralLatticeControlled => authority == ReplicatorControlAuthority.NeuralLattice && controller != null;
        public bool AuthorityValid => HasAuthority && ReplicatorSovereigntyUtility.IsAuthorityValid(this);
        public bool InterferenceBlocked => HasAuthority && ReplicatorSovereigntyUtility.IsInterferenceBlocking(this);
        public bool Operational => AuthorityValid && !InterferenceBlocked;

        public bool IsQueenControlledBy(Pawn exactQueen)
            => IsQueenControlled && controller == exactQueen && !string.IsNullOrEmpty(domainKey);

        public bool IsNeuralLatticeControlledBy(Pawn exactBearer)
            => IsNeuralLatticeControlled && controller == exactBearer && !string.IsNullOrEmpty(domainKey);

        public bool IsControlledBy(Pawn exactController)
            => HasAuthority && controller == exactController && !string.IsNullOrEmpty(domainKey);

        public bool TryAssignQueen(Pawn exactQueen, out string rejection)
        {
            rejection = null;
            Pawn pawn = Pawn;
            if (!ReplicatorSovereigntyUtility.IsExactQueen(exactQueen))
            {
                rejection = "Only the exact Replicator Queen has innate sovereign authority.";
                return false;
            }
            if (!ValidatePhysicalTarget(pawn, exactQueen, out rejection))
                return false;
            if (ReplicatorSovereigntyUtility.IsQueenSignalDisrupted(exactQueen))
            {
                rejection = "The Queen's nanite lattice is disrupted by EMP.";
                return false;
            }
            if (!ValidateAcquisitionInterference(pawn, exactQueen, out rejection))
                return false;
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

            AssignAuthority(
                exactQueen,
                exactQueen.Faction,
                ReplicatorControlAuthority.Queen,
                ReplicatorSovereigntyUtility.QueenDomainKey(exactQueen));
            return true;
        }

        public bool TryAssignNeuralLattice(Pawn exactBearer, out string rejection)
        {
            rejection = null;
            Pawn pawn = Pawn;
            if (ReplicatorSovereigntyUtility.IsExactQueen(exactBearer))
            {
                rejection = "The exact Replicator Queen uses her innate sovereign authority rather than an implant control domain.";
                return false;
            }
            if (!ReplicatorSovereigntyUtility.HasSovereignNeuralLattice(exactBearer))
            {
                rejection = "The controller does not have an active Sovereign Neural Lattice implant.";
                return false;
            }
            if (!ValidatePhysicalTarget(pawn, exactBearer, out rejection))
                return false;
            if (ReplicatorSovereigntyUtility.IsNeuralLatticeSignalDisrupted(exactBearer))
            {
                rejection = "The Sovereign Neural Lattice is disrupted by EMP.";
                return false;
            }
            if (!ValidateAcquisitionInterference(pawn, exactBearer, out rejection))
                return false;
            if (HasAuthority && !IsNeuralLatticeControlledBy(exactBearer))
            {
                rejection = "That Replicator already belongs to a different control domain.";
                return false;
            }
            if (IsNeuralLatticeControlledBy(exactBearer))
                return true;
            if (exactBearer.Faction == null)
            {
                rejection = "The implant bearer has no active faction authority.";
                return false;
            }

            AssignAuthority(
                exactBearer,
                exactBearer.Faction,
                ReplicatorControlAuthority.NeuralLattice,
                ReplicatorSovereigntyUtility.NeuralLatticeDomainKey(exactBearer));
            return true;
        }

        public bool TryAssignTemporaryAsuran(Pawn exactAsuran, int durationTicks, out string rejection)
        {
            rejection = null;
            Pawn pawn = Pawn;
            if (!ReplicatorSovereigntyUtility.IsTemporaryAsuranIntruder(exactAsuran))
            {
                rejection = "Only a valid Asuran field intruder can establish a temporary lattice override.";
                return false;
            }
            if (!ValidatePhysicalTarget(pawn, exactAsuran, out rejection))
                return false;
            if (ReplicatorSovereigntyUtility.IsAsuranSignalDisrupted(exactAsuran))
            {
                rejection = "The Asuran command lattice is disrupted by EMP.";
                return false;
            }
            if (!ValidateAcquisitionInterference(pawn, exactAsuran, out rejection))
                return false;
            if (exactAsuran.Faction == null)
            {
                rejection = "The Asuran intruder has no active faction authority.";
                return false;
            }

            string exactDomain = ReplicatorSovereigntyUtility.TemporaryAsuranDomainKey(exactAsuran);
            int now = Find.TickManager?.TicksGame ?? 0;
            long requestedUntil = (long)now + Math.Max(60, durationTicks);
            int until = requestedUntil >= int.MaxValue ? int.MaxValue : (int)requestedUntil;

            if (authority == ReplicatorControlAuthority.TemporaryAsuran)
            {
                if (controller != exactAsuran || domainKey != exactDomain)
                {
                    rejection = "That Replicator is already under another Asuran intrusion domain.";
                    return false;
                }
                temporaryUntil = Math.Max(temporaryUntil, until);
                return true;
            }

            if (HasAuthority && !AuthorityValid)
                ReleaseAuthority();

            CaptureSuspendedAuthority();
            originalFaction = pawn?.Faction;
            controlFaction = exactAsuran.Faction;
            controller = exactAsuran;
            authority = ReplicatorControlAuthority.TemporaryAsuran;
            domainKey = exactDomain;
            temporaryUntil = until;
            lastInterferenceBlocked = false;

            if (pawn != null && pawn.Faction != controlFaction)
                pawn.SetFaction(controlFaction, exactAsuran);
            pawn?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            return true;
        }

        private static bool ValidatePhysicalTarget(Pawn pawn, Pawn exactController, out string rejection)
        {
            rejection = null;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                rejection = "The target Replicator is not physically available.";
                return false;
            }
            if (!ReplicatorSovereigntyUtility.SharesPhysicalPresence(exactController, pawn))
            {
                rejection = "The controller and target Replicator must be physically present together.";
                return false;
            }
            return true;
        }

        private static bool ValidateAcquisitionInterference(Pawn pawn, Pawn exactController, out string rejection)
        {
            rejection = null;
            if (ReplicatorEMP.IsSuppressed(pawn))
            {
                rejection = "The target Replicator is disrupted by EMP.";
                return false;
            }
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
            {
                rejection = "An active Replicator containment field is blocking control acquisition.";
                return false;
            }
            if (exactController?.Spawned == true && exactController.Map != null &&
                ReplicatorContainmentUtility.IsContained(exactController.Map, exactController.Position))
            {
                rejection = "An active Replicator containment field is blocking the controller's signal.";
                return false;
            }
            return true;
        }

        private void CaptureSuspendedAuthority()
        {
            Pawn pawn = Pawn;
            hasSuspendedAuthority = true;
            suspendedAuthority = authority;
            suspendedController = controller;
            suspendedOriginalFaction = originalFaction;
            suspendedControlFaction = controlFaction;
            suspendedDomainKey = domainKey;
            suspendedTemporaryUntil = temporaryUntil;
            suspendedPawnFaction = pawn?.Faction;
        }

        private void ClearSuspendedAuthority()
        {
            hasSuspendedAuthority = false;
            suspendedAuthority = ReplicatorControlAuthority.None;
            suspendedController = null;
            suspendedOriginalFaction = null;
            suspendedControlFaction = null;
            suspendedDomainKey = null;
            suspendedTemporaryUntil = -1;
            suspendedPawnFaction = null;
        }

        private void AssignAuthority(
            Pawn exactController,
            Faction exactControlFaction,
            ReplicatorControlAuthority exactAuthority,
            string exactDomainKey)
        {
            Pawn pawn = Pawn;
            ClearSuspendedAuthority();
            originalFaction = pawn?.Faction;
            controlFaction = exactControlFaction;
            controller = exactController;
            authority = exactAuthority;
            domainKey = exactDomainKey;
            temporaryUntil = -1;
            lastInterferenceBlocked = false;

            if (pawn != null && pawn.Faction != controlFaction)
                pawn.SetFaction(controlFaction, exactController);
            pawn?.jobs?.EndCurrentJob(JobCondition.InterruptForced);
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
            hasSuspendedAuthority = other.hasSuspendedAuthority;
            suspendedAuthority = other.suspendedAuthority;
            suspendedController = other.suspendedController;
            suspendedOriginalFaction = other.suspendedOriginalFaction;
            suspendedControlFaction = other.suspendedControlFaction;
            suspendedDomainKey = other.suspendedDomainKey;
            suspendedTemporaryUntil = other.suspendedTemporaryUntil;
            suspendedPawnFaction = other.suspendedPawnFaction;
        }

        public bool ReleaseAuthority(bool restoreFaction = true)
        {
            Pawn pawn = Pawn;
            if (!HasAuthority)
                return false;

            if (authority == ReplicatorControlAuthority.TemporaryAsuran && hasSuspendedAuthority)
                return RestoreSuspendedAuthority(restoreFaction);

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

        private bool RestoreSuspendedAuthority(bool restoreFaction)
        {
            if (!hasSuspendedAuthority)
                return false;

            Pawn pawn = Pawn;
            ReplicatorControlAuthority restoreAuthority = suspendedAuthority;
            Pawn restoreController = suspendedController;
            Faction restoreOriginalFaction = suspendedOriginalFaction;
            Faction restoreControlFaction = suspendedControlFaction;
            string restoreDomainKey = suspendedDomainKey;
            int restoreTemporaryUntil = suspendedTemporaryUntil;
            Faction restorePawnFaction = suspendedPawnFaction;

            ClearMetadata();

            authority = restoreAuthority;
            controller = restoreController;
            originalFaction = restoreOriginalFaction;
            controlFaction = restoreControlFaction;
            domainKey = restoreDomainKey;
            temporaryUntil = restoreTemporaryUntil;
            lastInterferenceBlocked = false;

            if (restoreAuthority == ReplicatorControlAuthority.None)
            {
                controller = null;
                originalFaction = null;
                controlFaction = null;
                domainKey = null;
                temporaryUntil = -1;
            }

            if (pawn != null && !pawn.Destroyed)
            {
                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                if (restoreFaction)
                {
                    Faction restore = restorePawnFaction;
                    if (restoreAuthority != ReplicatorControlAuthority.None && restoreControlFaction != null && !restoreControlFaction.defeated)
                        restore = restoreControlFaction;
                    if (restore == null || restore.defeated)
                        restore = ReplicatorSovereigntyUtility.ResolveAutonomousSwarmFaction();
                    if (pawn.Faction != restore)
                        pawn.SetFaction(restore, restoreController);
                }
            }

            if (restoreAuthority != ReplicatorControlAuthority.None && !ReplicatorSovereigntyUtility.IsAuthorityValid(this))
                ReleaseAuthority(restoreFaction);
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
            ClearSuspendedAuthority();
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
            if (pawn == null || !HasAuthority || controller?.Faction != Faction.OfPlayer ||
                pawn.Faction != Faction.OfPlayer || controlFaction != Faction.OfPlayer)
                yield break;

            bool playerCommandDomain = authority == ReplicatorControlAuthority.Queen ||
                                       authority == ReplicatorControlAuthority.NeuralLattice ||
                                       authority == ReplicatorControlAuthority.TemporaryAsuran;
            if (!playerCommandDomain)
                yield break;

            string commandLabel = authority == ReplicatorControlAuthority.Queen
                ? "Sovereign"
                : authority == ReplicatorControlAuthority.NeuralLattice ? "Lattice" : "Intrusion";
            string authorityDescription = authority == ReplicatorControlAuthority.Queen
                ? "Queen-controlled"
                : authority == ReplicatorControlAuthority.NeuralLattice
                    ? "Neural-Lattice-controlled"
                    : "temporarily Asuran-intruded";
            bool blocked = !AuthorityValid || InterferenceBlocked;
            string blockedReason = !AuthorityValid
                ? "The recorded controller is no longer physically present as this Replicator's valid authority source."
                : "Control is blocked by EMP or an active containment field.";

            Command_Target move = new Command_Target
            {
                defaultLabel = commandLabel + " move",
                defaultDesc = $"Order this {authorityDescription} Replicator to move to a chosen cell without requiring a mechanitor overseer.",
                targetingParams = TargetingParameters.ForCell(),
                action = target => IssueMove(target.Cell)
            };
            if (blocked) move.Disable(blockedReason);
            yield return move;

            Command_Target attack = new Command_Target
            {
                defaultLabel = commandLabel + " attack",
                defaultDesc = $"Order this {authorityDescription} Replicator to attack an exact hostile target.",
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
                    defaultLabel = commandLabel + " repair",
                    defaultDesc = "Order this Repairer to repair an exact Replicator in the same controller domain.",
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
                    defaultLabel = commandLabel + " breach",
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
                    defaultLabel = commandLabel + " recombine",
                    defaultDesc = "Explicitly order nearby same-domain Replicators of this form to perform their normal upward hierarchy transaction. Stored matter, adaptations and controller authority are conserved.",
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
                defaultLabel = "Release controller authority",
                defaultDesc = "Return this block Replicator to its pre-control faction/domain.",
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
                Messages.Message("Choose a target hostile to the controller's current faction.", thing, MessageTypeDefOf.RejectInput, historical: false);
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
            return $"Replicator control: {authority} — {controllerLabel}\nController signal: {status}";
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
            Scribe_Values.Look(ref hasSuspendedAuthority, "wngReplicatorHasSuspendedAuthority", false);
            Scribe_Values.Look(ref suspendedAuthority, "wngReplicatorSuspendedAuthority", ReplicatorControlAuthority.None);
            Scribe_References.Look(ref suspendedController, "wngReplicatorSuspendedController");
            Scribe_References.Look(ref suspendedOriginalFaction, "wngReplicatorSuspendedOriginalFaction");
            Scribe_References.Look(ref suspendedControlFaction, "wngReplicatorSuspendedControlFaction");
            Scribe_Values.Look(ref suspendedDomainKey, "wngReplicatorSuspendedDomain");
            Scribe_Values.Look(ref suspendedTemporaryUntil, "wngReplicatorSuspendedTemporaryUntil", -1);
            Scribe_References.Look(ref suspendedPawnFaction, "wngReplicatorSuspendedPawnFaction");
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

        public static string NeuralLatticeDomainKey(Pawn bearer)
            => bearer == null ? null : "NeuralLattice:" + bearer.GetUniqueLoadID();

        public static string TemporaryAsuranDomainKey(Pawn asuran)
            => asuran == null ? null : "TemporaryAsuran:" + asuran.GetUniqueLoadID();

        public static bool IsAsuranIntrusionRole(Pawn pawn)
            => pawn != null && !pawn.Dead && !IsExactQueen(pawn) &&
               pawn.kindDef?.defName == "WNG_AsuranCommander" && AsuranNaniteUtility.IsNaniteHumanoid(pawn);

        public static bool IsTemporaryAsuranIntruder(Pawn pawn)
        {
            if (!IsAsuranIntrusionRole(pawn) || pawn.abilities == null)
                return false;
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_TemporaryAsuranIntrusion");
            return def != null && pawn.abilities.GetAbility(def, includeTemporary: true) != null;
        }

        public static bool IsAsuranSignalDisrupted(Pawn asuran)
        {
            if (!AsuranNaniteUtility.IsNaniteHumanoid(asuran) || asuran?.health?.hediffSet == null)
                return true;
            HediffDef disrupted = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_AsuranEMPDisrupted");
            return disrupted != null && asuran.health.hediffSet.HasHediff(disrupted);
        }

        public static Hediff_SovereignNeuralLattice GetSovereignNeuralLattice(Pawn pawn)
            => pawn?.health?.hediffSet?.hediffs?.OfType<Hediff_SovereignNeuralLattice>().FirstOrDefault();

        public static bool HasSovereignNeuralLattice(Pawn pawn)
            => pawn != null && !pawn.Dead && GetSovereignNeuralLattice(pawn) != null;

        public static bool IsQueenSignalDisrupted(Pawn queen)
        {
            if (queen?.health?.hediffSet == null)
                return true;
            HediffDef disrupted = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_AsuranEMPDisrupted");
            return disrupted != null && queen.health.hediffSet.HasHediff(disrupted);
        }

        public static bool IsNeuralLatticeSignalDisrupted(Pawn bearer)
            => GetSovereignNeuralLattice(bearer)?.SignalDisrupted != false;

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
                    return !IsExactQueen(controller) && HasSovereignNeuralLattice(controller) &&
                           comp.DomainKey == NeuralLatticeDomainKey(controller);
                case ReplicatorControlAuthority.TemporaryAsuran:
                    return IsTemporaryAsuranIntruder(controller) &&
                           comp.DomainKey == TemporaryAsuranDomainKey(controller) &&
                           comp.TemporaryUntil > (Find.TickManager?.TicksGame ?? 0);
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
            if (comp.Authority == ReplicatorControlAuthority.NeuralLattice && IsNeuralLatticeSignalDisrupted(controller))
                return true;
            if (comp.Authority == ReplicatorControlAuthority.TemporaryAsuran && IsAsuranSignalDisrupted(controller))
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

            bool sameActive = ca.Authority == cb.Authority && ca.Controller == cb.Controller &&
                              !string.IsNullOrEmpty(ca.DomainKey) && ca.DomainKey == cb.DomainKey;
            if (!sameActive)
                return false;
            if (ca.Authority != ReplicatorControlAuthority.TemporaryAsuran)
                return true;
            return SameTemporaryRestorationState(ca, cb);
        }

        private static bool SameTemporaryRestorationState(CompReplicatorSovereignty a, CompReplicatorSovereignty b)
        {
            if (a?.HasSuspendedAuthority != true || b?.HasSuspendedAuthority != true)
                return false;
            return a.SuspendedAuthority == b.SuspendedAuthority &&
                   a.SuspendedController == b.SuspendedController &&
                   a.SuspendedOriginalFaction == b.SuspendedOriginalFaction &&
                   a.SuspendedControlFaction == b.SuspendedControlFaction &&
                   a.SuspendedDomainKey == b.SuspendedDomainKey &&
                   a.SuspendedTemporaryUntil == b.SuspendedTemporaryUntil &&
                   a.SuspendedPawnFaction == b.SuspendedPawnFaction;
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
            if (!ValidateAcquisitionTarget(queen, block, range, out rejection))
                return false;

            CompReplicatorSovereignty comp = block.TryGetComp<CompReplicatorSovereignty>();
            if (comp.IsQueenControlledBy(queen))
                return true;

            int cap = Math.Max(1, maxControlled);
            int controlled = CountControllerDomainOnMap(queen, ReplicatorControlAuthority.Queen);
            if (controlled >= cap)
            {
                rejection = "The Queen's current sovereign control limit has been reached.";
                return false;
            }

            return comp.TryAssignQueen(queen, out rejection);
        }

        public static bool TryAcquireForNeuralLattice(Pawn bearer, Pawn block, float range, int maxControlled, out string rejection)
        {
            rejection = null;
            if (bearer == null || bearer.Dead || bearer.Faction != Faction.OfPlayer || !bearer.Spawned || bearer.Map == null)
            {
                rejection = "The Sovereign Neural Lattice bearer must be physically present and player-aligned.";
                return false;
            }
            if (IsExactQueen(bearer))
            {
                rejection = "The exact Replicator Queen uses her innate sovereign authority rather than an implant control domain.";
                return false;
            }
            if (!HasSovereignNeuralLattice(bearer))
            {
                rejection = "This pawn does not have an active Sovereign Neural Lattice implant.";
                return false;
            }
            if (IsNeuralLatticeSignalDisrupted(bearer))
            {
                rejection = "The Sovereign Neural Lattice is disrupted by EMP.";
                return false;
            }
            if (ReplicatorContainmentUtility.IsContained(bearer.Map, bearer.Position))
            {
                rejection = "An active Replicator containment field is blocking the implant's control signal.";
                return false;
            }
            if (!ValidateAcquisitionTarget(bearer, block, range, out rejection))
                return false;

            CompReplicatorSovereignty comp = block.TryGetComp<CompReplicatorSovereignty>();
            if (comp.IsNeuralLatticeControlledBy(bearer))
                return true;

            int cap = Math.Max(1, maxControlled);
            int controlled = CountControllerDomainOnMap(bearer, ReplicatorControlAuthority.NeuralLattice);
            if (controlled >= cap)
            {
                rejection = "This Sovereign Neural Lattice has reached its current block-control limit.";
                return false;
            }

            return comp.TryAssignNeuralLattice(bearer, out rejection);
        }

        public static bool CanAcquireForTemporaryAsuran(Pawn asuran, Pawn block, float range, int maxControlled, out string rejection)
        {
            rejection = null;
            if (!IsTemporaryAsuranIntruder(asuran) || !asuran.Spawned || asuran.Map == null || asuran.Faction == null)
            {
                rejection = "A valid Asuran commander must be physically present to intrude a Replicator lattice.";
                return false;
            }
            if (IsAsuranSignalDisrupted(asuran))
            {
                rejection = "The Asuran command lattice is disrupted by EMP.";
                return false;
            }
            if (ReplicatorContainmentUtility.IsContained(asuran.Map, asuran.Position))
            {
                rejection = "An active Replicator containment field is blocking the Asuran intrusion signal.";
                return false;
            }
            if (!ValidateAcquisitionTarget(asuran, block, range, out rejection))
                return false;

            CompReplicatorSovereignty comp = block.TryGetComp<CompReplicatorSovereignty>();
            if (comp == null)
            {
                rejection = "That pawn does not expose the WNG Replicator sovereignty lattice.";
                return false;
            }
            if (comp.Authority == ReplicatorControlAuthority.TemporaryAsuran)
            {
                if (comp.Controller == asuran && comp.DomainKey == TemporaryAsuranDomainKey(asuran))
                    return true;
                rejection = "That Replicator is already under another Asuran intrusion domain.";
                return false;
            }
            if (block.Faction == asuran.Faction || !asuran.HostileTo(block))
            {
                rejection = "Choose a hostile block Replicator outside the Asuran commander's current faction.";
                return false;
            }

            int cap = Math.Max(1, maxControlled);
            int controlled = CountControllerDomainOnMap(asuran, ReplicatorControlAuthority.TemporaryAsuran);
            if (controlled >= cap)
            {
                rejection = "This Asuran commander has reached the current temporary intrusion limit.";
                return false;
            }
            return true;
        }

        public static bool TryAcquireForTemporaryAsuran(
            Pawn asuran,
            Pawn block,
            float range,
            int maxControlled,
            int durationTicks,
            out string rejection)
        {
            if (!CanAcquireForTemporaryAsuran(asuran, block, range, maxControlled, out rejection))
                return false;
            CompReplicatorSovereignty comp = block.TryGetComp<CompReplicatorSovereignty>();
            return comp != null && comp.TryAssignTemporaryAsuran(asuran, Math.Max(60, durationTicks), out rejection);
        }

        private static bool ValidateAcquisitionTarget(Pawn controller, Pawn block, float range, out string rejection)
        {
            rejection = null;
            if (!IsBlockReplicator(block) || block.Dead || !block.Spawned || block.Map != controller.Map)
            {
                rejection = "Choose a living WNG block Replicator on the controller's map.";
                return false;
            }
            float allowed = Math.Max(1f, range);
            if (controller.Position.DistanceToSquared(block.Position) > allowed * allowed)
            {
                rejection = "That Replicator is outside the controller's command range.";
                return false;
            }
            return true;
        }

        private static int CountControllerDomainOnMap(Pawn controller, ReplicatorControlAuthority exactAuthority)
        {
            if (controller?.Map?.mapPawns?.AllPawnsSpawned == null)
                return 0;
            return controller.Map.mapPawns.AllPawnsSpawned.Count(p =>
            {
                CompReplicatorSovereignty comp = p?.TryGetComp<CompReplicatorSovereignty>();
                return comp?.Authority == exactAuthority && comp.Controller == controller;
            });
        }

        public static int ReleaseQueenDomain(Pawn queen)
            => ReleaseControllerDomain(queen, ReplicatorControlAuthority.Queen);

        public static int ReleaseControllerDomain(Pawn exactController, ReplicatorControlAuthority exactAuthority)
        {
            if (exactController == null)
                return 0;

            int released = 0;
            HashSet<int> handled = new HashSet<int>();
            if (Find.Maps != null)
            {
                foreach (Map map in Find.Maps)
                {
                    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
                    {
                        CompReplicatorSovereignty comp = pawn?.TryGetComp<CompReplicatorSovereignty>();
                        if (comp?.Authority == exactAuthority && comp.Controller == exactController &&
                            handled.Add(pawn.thingIDNumber) && comp.ReleaseAuthority())
                            released++;
                    }
                }
            }

            Caravan caravan = exactController.GetCaravan();
            if (caravan != null)
            {
                foreach (Pawn pawn in caravan.PawnsListForReading.ToList())
                {
                    CompReplicatorSovereignty comp = pawn?.TryGetComp<CompReplicatorSovereignty>();
                    if (comp?.Authority == exactAuthority && comp.Controller == exactController &&
                        handled.Add(pawn.thingIDNumber) && comp.ReleaseAuthority())
                        released++;
                }
            }
            return released;
        }

        public static void EnsureTemporaryAsuranIntrusionAbilities()
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_TemporaryAsuranIntrusion");
            if (def == null || Find.Maps == null)
                return;
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (!IsAsuranIntrusionRole(pawn) || pawn.abilities == null)
                        continue;
                    if (pawn.abilities.GetAbility(def, includeTemporary: true) == null)
                        pawn.abilities.GainAbility(def);
                }
            }
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
                defaultDesc = "Release all currently present block Replicators controlled by this exact Queen and restore their pre-control faction/domain.",
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
            ReplicatorSovereigntyUtility.EnsureTemporaryAsuranIntrusionAbilities();
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
