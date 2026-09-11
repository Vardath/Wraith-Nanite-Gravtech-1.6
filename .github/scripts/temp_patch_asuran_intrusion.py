from pathlib import Path

path = Path("Source/WNG/Replicators/ReplicatorSovereignty.cs")
text = path.read_text()

if "wngReplicatorSuspendedAuthority" in text:
    print("Temporary Asuran intrusion sovereignty patch already applied.")
    raise SystemExit(0)


def require_once(anchor: str, name: str) -> int:
    count = text.count(anchor)
    if count != 1:
        raise RuntimeError(f"{name}: expected exactly one anchor, found {count}")
    return text.index(anchor)


def insert_after(anchor: str, addition: str, name: str) -> None:
    global text
    pos = require_once(anchor, name) + len(anchor)
    text = text[:pos] + addition + text[pos:]


def insert_before(anchor: str, addition: str, name: str) -> None:
    global text
    pos = require_once(anchor, name)
    text = text[:pos] + addition + text[pos:]


def replace_once(old: str, new: str, name: str) -> None:
    global text
    require_once(old, name)
    text = text.replace(old, new, 1)


def method_bounds(signature: str) -> tuple[int, int]:
    start = require_once(signature, signature)
    brace = text.index("{", start)
    depth = 0
    in_string = False
    verbatim = False
    escape = False
    i = brace
    while i < len(text):
        ch = text[i]
        if in_string:
            if verbatim:
                if ch == '"':
                    if i + 1 < len(text) and text[i + 1] == '"':
                        i += 1
                    else:
                        in_string = False
                        verbatim = False
            else:
                if escape:
                    escape = False
                elif ch == "\\":
                    escape = True
                elif ch == '"':
                    in_string = False
        else:
            if ch == '@' and i + 1 < len(text) and text[i + 1] == '"':
                in_string = True
                verbatim = True
                i += 1
            elif ch == '"':
                in_string = True
                verbatim = False
            elif ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    end = i + 1
                    if end < len(text) and text[end] == "\r":
                        end += 1
                    if end < len(text) and text[end] == "\n":
                        end += 1
                    return start, end
        i += 1
    raise RuntimeError(f"Unclosed method for {signature}")


def replace_method(signature: str, new_method: str) -> None:
    global text
    start, end = method_bounds(signature)
    text = text[:start] + new_method + text[end:]


insert_after(
    "        private bool lastInterferenceBlocked;\n",
    """

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
""",
    "suspended authority fields",
)

insert_after(
    "        public string DomainKey => domainKey;\n",
    """        public int TemporaryUntil => temporaryUntil;
        public bool HasSuspendedAuthority => hasSuspendedAuthority;
        public ReplicatorControlAuthority SuspendedAuthority => suspendedAuthority;
        public Pawn SuspendedController => suspendedController;
        public Faction SuspendedOriginalFaction => suspendedOriginalFaction;
        public Faction SuspendedControlFaction => suspendedControlFaction;
        public string SuspendedDomainKey => suspendedDomainKey;
        public int SuspendedTemporaryUntil => suspendedTemporaryUntil;
        public Faction SuspendedPawnFaction => suspendedPawnFaction;
""",
    "suspended authority properties",
)

insert_before(
    "        private static bool ValidatePhysicalTarget(Pawn pawn, Pawn exactController, out string rejection)\n",
    """        public bool TryAssignTemporaryAsuran(Pawn exactAsuran, int durationTicks, out string rejection)
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

""",
    "temporary assignment method",
)

insert_before(
    "        private void AssignAuthority(\n",
    """        private void CaptureSuspendedAuthority()
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

""",
    "snapshot helpers",
)

replace_method(
    "        private void AssignAuthority(\n",
    """        private void AssignAuthority(
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
""",
)

replace_method(
    "        public void CopyAuthorityFrom(CompReplicatorSovereignty other)\n",
    """        public void CopyAuthorityFrom(CompReplicatorSovereignty other)
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
""",
)

replace_method(
    "        public bool ReleaseAuthority(bool restoreFaction = true)\n",
    """        public bool ReleaseAuthority(bool restoreFaction = true)
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
""",
)

replace_method(
    "        private void ClearMetadata()\n",
    """        private void ClearMetadata()
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
""",
)

replace_once(
    """            bool playerCommandDomain = authority == ReplicatorControlAuthority.Queen ||
                                       authority == ReplicatorControlAuthority.NeuralLattice;
            if (!playerCommandDomain)
                yield break;

            string commandLabel = authority == ReplicatorControlAuthority.Queen ? "Sovereign" : "Lattice";
            string authorityDescription = authority == ReplicatorControlAuthority.Queen
                ? "Queen-controlled"
                : "Neural-Lattice-controlled";
""",
    """            bool playerCommandDomain = authority == ReplicatorControlAuthority.Queen ||
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
""",
    "player temporary command surface",
)

insert_after(
    "            Scribe_Values.Look(ref lastInterferenceBlocked, \"wngReplicatorAuthorityWasBlocked\", false);\n",
    """            Scribe_Values.Look(ref hasSuspendedAuthority, "wngReplicatorHasSuspendedAuthority", false);
            Scribe_Values.Look(ref suspendedAuthority, "wngReplicatorSuspendedAuthority", ReplicatorControlAuthority.None);
            Scribe_References.Look(ref suspendedController, "wngReplicatorSuspendedController");
            Scribe_References.Look(ref suspendedOriginalFaction, "wngReplicatorSuspendedOriginalFaction");
            Scribe_References.Look(ref suspendedControlFaction, "wngReplicatorSuspendedControlFaction");
            Scribe_Values.Look(ref suspendedDomainKey, "wngReplicatorSuspendedDomain");
            Scribe_Values.Look(ref suspendedTemporaryUntil, "wngReplicatorSuspendedTemporaryUntil", -1);
            Scribe_References.Look(ref suspendedPawnFaction, "wngReplicatorSuspendedPawnFaction");
""",
    "suspended state serialization",
)

insert_after(
    """        public static string NeuralLatticeDomainKey(Pawn bearer)
            => bearer == null ? null : "NeuralLattice:" + bearer.GetUniqueLoadID();
""",
    """
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
""",
    "temporary Asuran identity utilities",
)

replace_once(
    """                case ReplicatorControlAuthority.TemporaryAsuran:
                    return !string.IsNullOrEmpty(comp.DomainKey);
""",
    """                case ReplicatorControlAuthority.TemporaryAsuran:
                    return IsTemporaryAsuranIntruder(controller) &&
                           comp.DomainKey == TemporaryAsuranDomainKey(controller) &&
                           comp.TemporaryUntil > (Find.TickManager?.TicksGame ?? 0);
""",
    "temporary authority validity",
)

insert_after(
    """            if (comp.Authority == ReplicatorControlAuthority.NeuralLattice && IsNeuralLatticeSignalDisrupted(controller))
                return true;
""",
    """            if (comp.Authority == ReplicatorControlAuthority.TemporaryAsuran && IsAsuranSignalDisrupted(controller))
                return true;
""",
    "temporary EMP interference",
)

replace_method(
    "        public static bool SameDomain(Pawn a, Pawn b)\n",
    """        public static bool SameDomain(Pawn a, Pawn b)
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
""",
)

insert_before(
    "        private static bool ValidateAcquisitionTarget(Pawn controller, Pawn block, float range, out string rejection)\n",
    """        public static bool CanAcquireForTemporaryAsuran(Pawn asuran, Pawn block, float range, int maxControlled, out string rejection)
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

""",
    "temporary acquisition utilities",
)

insert_before(
    "        public static Faction ResolveAutonomousSwarmFaction()\n",
    """        public static void EnsureTemporaryAsuranIntrusionAbilities()
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

""",
    "existing-save commander ability maintenance",
)

insert_after(
    "            ReplicatorSovereigntyUtility.EnsureQueenSovereigntyHediff(GameComponent_ReplicatorQueenState.Current?.Queen);\n",
    "            ReplicatorSovereigntyUtility.EnsureTemporaryAsuranIntrusionAbilities();\n",
    "sovereignty maintenance hook",
)

path.write_text(text)
print("Patched ReplicatorSovereignty.cs for temporary Asuran intrusion with exact suspended-domain restoration.")
