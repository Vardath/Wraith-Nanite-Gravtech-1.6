using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Controller identity is intentionally distinct from faction. Only AutonomousSwarm is actively
    /// assigned by the current WNGv1 slice; the other values reserve the accepted persistent authority
    /// identities so Queen / Neural Lattice / temporary intrusion can extend this state without a
    /// second competing domain component or save-key migration.
    /// </summary>
    public enum ReplicatorControlAuthority
    {
        Unassigned = 0,
        AutonomousSwarm = 1,
        ExactQueen = 2,
        SovereignNeuralLattice = 3,
        TemporaryAsuran = 4,
        CapturedQueenSovereign = 5
    }

    public sealed class CompProperties_ReplicatorDomain : CompProperties
    {
        public CompProperties_ReplicatorDomain()
        {
            compClass = typeof(CompReplicatorDomain);
        }
    }

    /// <summary>
    /// Persistent controller-domain identity. Temporary Asuran intrusion now reuses this same state
    /// owner through AssignExactState; exact Queen and Sovereign Neural Lattice remain separate later
    /// authority layers rather than parallel domain components.
    /// </summary>
    public sealed class CompReplicatorDomain : ThingComp
    {
        private ReplicatorControlAuthority authority = ReplicatorControlAuthority.Unassigned;
        private string domainId;
        private Pawn authorityPawn;

        public ReplicatorControlAuthority Authority => authority;
        public string DomainId => domainId;
        public Pawn AuthorityPawn => authorityPawn;

        public void EnsureAutonomousIdentity()
        {
            if (!string.IsNullOrEmpty(domainId))
                return;

            authority = ReplicatorControlAuthority.AutonomousSwarm;
            domainId = "auto:" + (parent?.thingIDNumber ?? 0);
            authorityPawn = null;
        }

        public void AssignAutonomousDomain(string exactDomainId)
        {
            if (string.IsNullOrWhiteSpace(exactDomainId))
                return;

            authority = ReplicatorControlAuthority.AutonomousSwarm;
            domainId = exactDomainId;
            authorityPawn = null;
        }

        public void AssignExactState(ReplicatorControlAuthority exactAuthority, string exactDomainId, Pawn exactAuthorityPawn)
        {
            if (string.IsNullOrWhiteSpace(exactDomainId))
                return;

            authority = exactAuthority;
            domainId = exactDomainId;
            authorityPawn = exactAuthorityPawn;
        }

        public void CopyFrom(CompReplicatorDomain source)
        {
            if (source == null)
                return;

            source.EnsureAutonomousIdentity();
            authority = source.authority;
            domainId = source.domainId;
            authorityPawn = source.authorityPawn;
        }

        public bool CompatibleWith(CompReplicatorDomain other)
        {
            if (other == null)
                return false;

            EnsureAutonomousIdentity();
            other.EnsureAutonomousIdentity();
            return !string.IsNullOrEmpty(domainId) && domainId == other.domainId;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureAutonomousIdentity();

            // Debug/directly spawned autonomous block forms can arrive factionless. A factionless
            // Replicator cannot run its hostile ecology, so bind only genuinely autonomous block
            // Replicators to the real permanent-enemy swarm faction on spawn.
            Pawn pawn = parent as Pawn;
            if (pawn != null &&
                pawn.Faction == null &&
                authority == ReplicatorControlAuthority.AutonomousSwarm &&
                ReplicatorAssimilationUtility.IsBlockReplicator(pawn))
            {
                FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
                Faction swarm = swarmDef == null ? null : Find.FactionManager?.FirstFactionOfDef(swarmDef);
                if (swarm != null)
                    pawn.SetFaction(swarm);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref authority, "wngReplicatorControlAuthority", ReplicatorControlAuthority.Unassigned);
            Scribe_Values.Look(ref domainId, "wngReplicatorDomainId");
            Scribe_References.Look(ref authorityPawn, "wngReplicatorAuthorityPawn");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && string.IsNullOrEmpty(domainId))
                EnsureAutonomousIdentity();
        }
    }

    public static class ReplicatorDomainUtility
    {
        public static CompReplicatorDomain Domain(Pawn pawn)
        {
            CompReplicatorDomain domain = pawn?.TryGetComp<CompReplicatorDomain>();
            domain?.EnsureAutonomousIdentity();
            return domain;
        }

        public static string DomainId(Pawn pawn)
        {
            return Domain(pawn)?.DomainId;
        }

        public static bool SameDomain(Pawn a, Pawn b)
        {
            CompReplicatorDomain da = Domain(a);
            CompReplicatorDomain db = Domain(b);
            return da != null && da.CompatibleWith(db) &&
                   ReplicatorSovereignControlUtility.SignalOperationalForCurrentDomain(a) &&
                   ReplicatorSovereignControlUtility.SignalOperationalForCurrentDomain(b);
        }

        public static void CopyDomain(Pawn source, Pawn target)
        {
            if (source == null || target == null)
                return;
            target.TryGetComp<CompReplicatorDomain>()?.CopyFrom(Domain(source));
        }

        public static bool AllSameDomain(IEnumerable<Pawn> pawns)
        {
            if (pawns == null)
                return false;

            string expected = null;
            bool any = false;
            foreach (Pawn pawn in pawns)
            {
                if (!ReplicatorSovereignControlUtility.SignalOperationalForCurrentDomain(pawn))
                    return false;
                string id = DomainId(pawn);
                if (string.IsNullOrEmpty(id))
                    return false;
                if (!any)
                {
                    expected = id;
                    any = true;
                }
                else if (id != expected)
                {
                    return false;
                }
            }
            return any;
        }
    }
}
