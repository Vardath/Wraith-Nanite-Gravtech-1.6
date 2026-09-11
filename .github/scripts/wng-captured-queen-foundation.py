from pathlib import Path

path = Path('Source/WNG/Replicators/ReplicatorSovereignty.cs')
text = path.read_text(encoding='utf-8')

if 'public bool TryAssignCapturedQueen(' not in text:
    needle = '        public bool TryAssignNeuralLattice(Pawn exactBearer, out string rejection)\n'
    insert = '''        public bool TryAssignCapturedQueen(Pawn exactQueen, Faction captorFaction, out string rejection)
        {
            rejection = null;
            Pawn pawn = Pawn;
            if (!ReplicatorSovereigntyUtility.IsExactQueenRetainedByAsuranLattice(exactQueen, captorFaction))
            {
                rejection = "The exact Replicator Queen is not currently retained by that Asuran Lattice faction.";
                return false;
            }
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || !ReplicatorSovereigntyUtility.IsBlockReplicator(pawn))
            {
                rejection = "The target Replicator is not physically available.";
                return false;
            }
            if (ReplicatorEMP.IsSuppressed(pawn))
            {
                rejection = "The target Replicator is disrupted by EMP.";
                return false;
            }
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
            {
                rejection = "An active Replicator containment field is blocking captured-Queen control acquisition.";
                return false;
            }
            if (HasAuthority)
            {
                if (IsQueenControlledBy(exactQueen) && controlFaction == captorFaction)
                    return true;
                rejection = "That Replicator already belongs to another controller domain.";
                return false;
            }

            AssignAuthority(
                exactQueen,
                captorFaction,
                ReplicatorControlAuthority.Queen,
                ReplicatorSovereigntyUtility.QueenDomainKey(exactQueen));
            return true;
        }

'''
    if needle not in text:
        raise SystemExit('TryAssignNeuralLattice insertion point not found')
    text = text.replace(needle, insert + needle, 1)

if 'public static bool IsExactQueenRetainedByAsuranLattice' not in text:
    needle = '        public static string QueenDomainKey(Pawn queen)\n'
    insert = '''        public static bool IsExactQueenRetainedByAsuranLattice(Pawn queen, Faction expectedCaptor)
        {
            GameComponent_ReplicatorQueenState state = GameComponent_ReplicatorQueenState.Current;
            if (!IsExactQueen(queen) || state?.Status != ReplicatorQueenStatus.CapturedByAsurans ||
                state.Queen != queen || expectedCaptor == null || expectedCaptor.defeated ||
                expectedCaptor.def?.defName != "WNG_AsuranLattice" || expectedCaptor.kidnapped == null)
                return false;

            return expectedCaptor.kidnapped.KidnappedPawnsListForReading.Contains(queen);
        }

        public static bool TryGetCapturedQueenAsuranFaction(Pawn queen, out Faction captor)
        {
            captor = null;
            if (!IsExactQueen(queen) || Find.FactionManager?.AllFactionsListForReading == null)
                return false;

            captor = Find.FactionManager.AllFactionsListForReading
                .FirstOrDefault(f => IsExactQueenRetainedByAsuranLattice(queen, f));
            return captor != null;
        }

'''
    if needle not in text:
        raise SystemExit('QueenDomainKey insertion point not found')
    text = text.replace(needle, insert + needle, 1)

old = '''            if (block == null || block.Dead || controller.Dead || controller.Faction == null)
                return false;
            if (comp.ControlFaction != controller.Faction)
                return false;
            if (!SharesPhysicalPresence(controller, block))
                return false;

            switch (comp.Authority)
            {
                case ReplicatorControlAuthority.Queen:
                    return IsExactQueen(controller) && comp.DomainKey == QueenDomainKey(controller);'''
new = '''            if (block == null || block.Dead || controller.Dead)
                return false;

            bool capturedQueenRemote = comp.Authority == ReplicatorControlAuthority.Queen &&
                                       IsExactQueenRetainedByAsuranLattice(controller, comp.ControlFaction);
            if (!capturedQueenRemote)
            {
                if (controller.Faction == null || comp.ControlFaction != controller.Faction)
                    return false;
                if (!SharesPhysicalPresence(controller, block))
                    return false;
            }
            else if (block.Faction != comp.ControlFaction)
            {
                return false;
            }

            switch (comp.Authority)
            {
                case ReplicatorControlAuthority.Queen:
                    return IsExactQueen(controller) && comp.DomainKey == QueenDomainKey(controller) &&
                           (capturedQueenRemote || SharesPhysicalPresence(controller, block));'''
if old in text:
    text = text.replace(old, new, 1)
elif 'bool capturedQueenRemote = comp.Authority == ReplicatorControlAuthority.Queen' not in text:
    raise SystemExit('IsAuthorityValid replacement point not found')

if 'public static bool TryAcquireForCapturedQueen(' not in text:
    needle = '        public static bool TryAcquireForNeuralLattice(Pawn bearer, Pawn block, float range, int maxControlled, out string rejection)\n'
    insert = '''        public static bool TryAcquireForCapturedQueen(Pawn queen, Faction captorFaction, Pawn block, out string rejection)
        {
            rejection = null;
            if (!IsExactQueenRetainedByAsuranLattice(queen, captorFaction))
            {
                rejection = "The exact Replicator Queen is not retained by the specified Asuran Lattice faction.";
                return false;
            }
            if (!IsBlockReplicator(block) || block.Dead || !block.Spawned || block.Map == null)
            {
                rejection = "Choose a living spawned WNG block Replicator.";
                return false;
            }

            CompReplicatorSovereignty comp = block.TryGetComp<CompReplicatorSovereignty>();
            return comp != null && comp.TryAssignCapturedQueen(queen, captorFaction, out rejection);
        }

'''
    if needle not in text:
        raise SystemExit('TryAcquireForNeuralLattice insertion point not found')
    text = text.replace(needle, insert + needle, 1)

path.write_text(text, encoding='utf-8')

assimilation = Path('Source/WNG/Replicators/ReplicatorAssimilation.cs').read_text(encoding='utf-8')
expected = 'child.TryGetComp<CompReplicatorSovereignty>()?.CopyAuthorityFrom(parentPawn.TryGetComp<CompReplicatorSovereignty>());'
if expected not in assimilation:
    raise SystemExit('Assimilation sovereignty inheritance fix is missing from branch')
