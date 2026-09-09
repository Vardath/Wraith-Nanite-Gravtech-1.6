using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    /// <summary>
    /// Map-local hostile block-Replicator lineage memory. Recurring swarms on the same map retain
    /// learned assimilation progress; once the map has remained clear of hostile adapted block
    /// Replicators for one full in-game day, the lineage memory is erased and a later outbreak is fresh.
    /// </summary>
    public sealed class MapComponent_ReplicatorLineageMemory : MapComponent
    {
        public const int ClearMapRetentionTicks = 60000;
        private const int PresenceCheckIntervalTicks = 250;

        private int rangedCredit;
        private int armorCredit;
        private int powerConstructionCredit;
        private int gravtechCredit;
        private int shieldCredit;

        private int rangedReadyTick;
        private int armorReadyTick;
        private int powerConstructionReadyTick;
        private int gravtechReadyTick;
        private int shieldReadyTick;

        private int clearSinceTick = -1;
        private int nextPresenceCheckTick;

        public MapComponent_ReplicatorLineageMemory(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextPresenceCheckTick)
                return;
            nextPresenceCheckTick = now + PresenceCheckIntervalTicks;

            bool anyHostileBlockReplicator = map?.mapPawns?.AllPawnsSpawned.Any(pawn =>
                pawn != null &&
                !pawn.Dead &&
                pawn.Faction != null &&
                pawn.HostileTo(Faction.OfPlayer) &&
                pawn.TryGetComp<CompReplicatorAdaptation>() != null) == true;

            if (anyHostileBlockReplicator)
            {
                clearSinceTick = -1;
                return;
            }

            if (!HasKnowledge)
            {
                clearSinceTick = -1;
                return;
            }

            if (clearSinceTick < 0)
            {
                clearSinceTick = now;
                return;
            }

            if (now - clearSinceTick >= ClearMapRetentionTicks)
                ClearKnowledge();
        }

        public void RegisterAssimilation(ReplicatorAdaptationDomain domain, int credit, int now, int delay)
        {
            int before = GetCredit(domain);
            int after = before + Math.Max(0, credit);
            SetCredit(domain, after);

            if (before < CompReplicatorAdaptation.RequiredCredit(domain) &&
                after >= CompReplicatorAdaptation.RequiredCredit(domain) &&
                GetReadyTick(domain) <= 0)
            {
                SetReadyTick(domain, now + Math.Max(1, delay));
            }

            clearSinceTick = -1;
        }

        public int GetCredit(ReplicatorAdaptationDomain domain)
        {
            switch (domain)
            {
                case ReplicatorAdaptationDomain.Ranged: return rangedCredit;
                case ReplicatorAdaptationDomain.Armor: return armorCredit;
                case ReplicatorAdaptationDomain.PowerConstruction: return powerConstructionCredit;
                case ReplicatorAdaptationDomain.Gravtech: return gravtechCredit;
                case ReplicatorAdaptationDomain.Shield: return shieldCredit;
                default: return 0;
            }
        }

        public int GetReadyTick(ReplicatorAdaptationDomain domain)
        {
            switch (domain)
            {
                case ReplicatorAdaptationDomain.Ranged: return rangedReadyTick;
                case ReplicatorAdaptationDomain.Armor: return armorReadyTick;
                case ReplicatorAdaptationDomain.PowerConstruction: return powerConstructionReadyTick;
                case ReplicatorAdaptationDomain.Gravtech: return gravtechReadyTick;
                case ReplicatorAdaptationDomain.Shield: return shieldReadyTick;
                default: return 0;
            }
        }

        private bool HasKnowledge =>
            rangedCredit > 0 || armorCredit > 0 || powerConstructionCredit > 0 || gravtechCredit > 0 || shieldCredit > 0;

        private void ClearKnowledge()
        {
            rangedCredit = 0;
            armorCredit = 0;
            powerConstructionCredit = 0;
            gravtechCredit = 0;
            shieldCredit = 0;

            rangedReadyTick = 0;
            armorReadyTick = 0;
            powerConstructionReadyTick = 0;
            gravtechReadyTick = 0;
            shieldReadyTick = 0;

            clearSinceTick = -1;
        }

        private void SetCredit(ReplicatorAdaptationDomain domain, int value)
        {
            switch (domain)
            {
                case ReplicatorAdaptationDomain.Ranged: rangedCredit = value; break;
                case ReplicatorAdaptationDomain.Armor: armorCredit = value; break;
                case ReplicatorAdaptationDomain.PowerConstruction: powerConstructionCredit = value; break;
                case ReplicatorAdaptationDomain.Gravtech: gravtechCredit = value; break;
                case ReplicatorAdaptationDomain.Shield: shieldCredit = value; break;
            }
        }

        private void SetReadyTick(ReplicatorAdaptationDomain domain, int value)
        {
            switch (domain)
            {
                case ReplicatorAdaptationDomain.Ranged: rangedReadyTick = value; break;
                case ReplicatorAdaptationDomain.Armor: armorReadyTick = value; break;
                case ReplicatorAdaptationDomain.PowerConstruction: powerConstructionReadyTick = value; break;
                case ReplicatorAdaptationDomain.Gravtech: gravtechReadyTick = value; break;
                case ReplicatorAdaptationDomain.Shield: shieldReadyTick = value; break;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref rangedCredit, "rangedCredit", 0);
            Scribe_Values.Look(ref armorCredit, "armorCredit", 0);
            Scribe_Values.Look(ref powerConstructionCredit, "powerConstructionCredit", 0);
            Scribe_Values.Look(ref gravtechCredit, "gravtechCredit", 0);
            Scribe_Values.Look(ref shieldCredit, "shieldCredit", 0);

            Scribe_Values.Look(ref rangedReadyTick, "rangedReadyTick", 0);
            Scribe_Values.Look(ref armorReadyTick, "armorReadyTick", 0);
            Scribe_Values.Look(ref powerConstructionReadyTick, "powerConstructionReadyTick", 0);
            Scribe_Values.Look(ref gravtechReadyTick, "gravtechReadyTick", 0);
            Scribe_Values.Look(ref shieldReadyTick, "shieldReadyTick", 0);

            Scribe_Values.Look(ref clearSinceTick, "clearSinceTick", -1);
            Scribe_Values.Look(ref nextPresenceCheckTick, "nextPresenceCheckTick", 0);
        }
    }
}
