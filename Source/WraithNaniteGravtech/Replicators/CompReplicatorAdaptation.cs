using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    public enum ReplicatorAdaptationDomain
    {
        Ranged,
        Armor,
        PowerConstruction,
        Gravtech,
        Shield
    }

    public sealed class CompProperties_ReplicatorAdaptation : CompProperties
    {
        // Tuning defaults, not canonical thresholds. The lineage thresholds themselves are fixed below.
        public int adaptationDelayTicks = 2500;
        public int precursorCredit = 2;

        public CompProperties_ReplicatorAdaptation()
        {
            compClass = typeof(CompReplicatorAdaptation);
        }
    }

    /// <summary>
    /// Save-safe learned adaptation for block Replicators. Observation alone never calls this API:
    /// progress is awarded only by a completed assimilation transaction. A threshold also starts a
    /// delay, preventing instant upgrades. Ancient/Asuran/Precursor-grade targets contribute more
    /// lineage credit than mundane machinery, while the exact 3/4/4/5/8 thresholds remain fixed.
    /// </summary>
    public sealed class CompReplicatorAdaptation : ThingComp
    {
        public const int RangedAssimilationsRequired = 3;
        public const int ArmorAssimilationsRequired = 4;
        public const int PowerConstructionAssimilationsRequired = 4;
        public const int GravtechAssimilationsRequired = 5;
        public const int ShieldAssimilationsRequired = 8;

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

        private int successfulAssimilations;
        private int lastSuccessfulAssimilationTick = -1;
        private int specializedDomain = -1;

        public CompProperties_ReplicatorAdaptation Props => (CompProperties_ReplicatorAdaptation)props;
        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        public int SuccessfulAssimilations => successfulAssimilations;
        public int LastSuccessfulAssimilationTick => lastSuccessfulAssimilationTick;
        public bool HasSpecialization => specializedDomain >= 0 || !string.IsNullOrEmpty(parent.TryGetComp<CompReplicatorState>()?.Specialization);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);

            if (parent is Pawn pawn && pawn.Spawned && pawn.Faction != null && pawn.HostileTo(Faction.OfPlayer))
            {
                MapComponent_ReplicatorLineageMemory memory = pawn.Map?.GetComponent<MapComponent_ReplicatorLineageMemory>();
                MergeFromLineage(memory);
            }
        }

        public bool NotifySuccessfulAssimilation(
            ReplicatorAdaptationDomain domain,
            string materialSignature,
            bool ancientAsuranOrPrecursorGrade)
        {
            CompReplicatorSuppression suppression = parent.TryGetComp<CompReplicatorSuppression>();
            if (suppression?.IsSuppressed == true)
                return false;

            int credit = ancientAsuranOrPrecursorGrade ? Math.Max(2, Props.precursorCredit) : 1;
            int now = CurrentTick;
            int delay = Math.Max(1, Props.adaptationDelayTicks);

            AddCredit(domain, credit, now, delay);
            successfulAssimilations++;
            lastSuccessfulAssimilationTick = now;

            CompReplicatorState state = parent.TryGetComp<CompReplicatorState>();
            if (state != null && !string.IsNullOrEmpty(materialSignature))
                state.SetMaterialSignature(materialSignature);

            if (parent is Pawn pawn && pawn.Spawned && pawn.Faction != null && pawn.HostileTo(Faction.OfPlayer))
            {
                pawn.Map?.GetComponent<MapComponent_ReplicatorLineageMemory>()
                    ?.RegisterAssimilation(domain, credit, now, delay);
            }

            return true;
        }

        public bool IsUnlocked(ReplicatorAdaptationDomain domain)
        {
            return GetCredit(domain) >= RequiredCredit(domain) &&
                   GetReadyTick(domain) > 0 &&
                   CurrentTick >= GetReadyTick(domain);
        }

        /// <summary>
        /// Assigns exactly one learned specialist role to this body. The caller decides when a
        /// specialist body should be created; this method only enforces lineage unlock and one-role-only.
        /// </summary>
        public bool TryAssignSpecialization(ReplicatorAdaptationDomain domain)
        {
            if (HasSpecialization || !IsUnlocked(domain))
                return false;

            specializedDomain = (int)domain;
            parent.TryGetComp<CompReplicatorState>()?.SetSpecialization(DomainKey(domain));
            return true;
        }

        public void CopyFrom(CompReplicatorAdaptation source)
        {
            if (source == null)
                return;

            rangedCredit = source.rangedCredit;
            armorCredit = source.armorCredit;
            powerConstructionCredit = source.powerConstructionCredit;
            gravtechCredit = source.gravtechCredit;
            shieldCredit = source.shieldCredit;

            rangedReadyTick = source.rangedReadyTick;
            armorReadyTick = source.armorReadyTick;
            powerConstructionReadyTick = source.powerConstructionReadyTick;
            gravtechReadyTick = source.gravtechReadyTick;
            shieldReadyTick = source.shieldReadyTick;

            successfulAssimilations = source.successfulAssimilations;
            lastSuccessfulAssimilationTick = source.lastSuccessfulAssimilationTick;
            specializedDomain = source.specializedDomain;
        }

        internal void MergeFromLineage(MapComponent_ReplicatorLineageMemory memory)
        {
            if (memory == null)
                return;

            foreach (ReplicatorAdaptationDomain domain in Enum.GetValues(typeof(ReplicatorAdaptationDomain)))
            {
                int lineageCredit = memory.GetCredit(domain);
                int lineageReadyTick = memory.GetReadyTick(domain);
                if (lineageCredit > GetCredit(domain))
                    SetCredit(domain, lineageCredit);
                if (lineageReadyTick > 0 && (GetReadyTick(domain) <= 0 || lineageReadyTick < GetReadyTick(domain)))
                    SetReadyTick(domain, lineageReadyTick);
            }
        }

        private void AddCredit(ReplicatorAdaptationDomain domain, int credit, int now, int delay)
        {
            int before = GetCredit(domain);
            int after = before + Math.Max(0, credit);
            SetCredit(domain, after);

            if (before < RequiredCredit(domain) && after >= RequiredCredit(domain) && GetReadyTick(domain) <= 0)
                SetReadyTick(domain, now + delay);
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

        public static int RequiredCredit(ReplicatorAdaptationDomain domain)
        {
            switch (domain)
            {
                case ReplicatorAdaptationDomain.Ranged: return RangedAssimilationsRequired;
                case ReplicatorAdaptationDomain.Armor: return ArmorAssimilationsRequired;
                case ReplicatorAdaptationDomain.PowerConstruction: return PowerConstructionAssimilationsRequired;
                case ReplicatorAdaptationDomain.Gravtech: return GravtechAssimilationsRequired;
                case ReplicatorAdaptationDomain.Shield: return ShieldAssimilationsRequired;
                default: return int.MaxValue;
            }
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

        private static string DomainKey(ReplicatorAdaptationDomain domain)
        {
            switch (domain)
            {
                case ReplicatorAdaptationDomain.Ranged: return "ranged";
                case ReplicatorAdaptationDomain.Armor: return "armor";
                case ReplicatorAdaptationDomain.PowerConstruction: return "power-construction";
                case ReplicatorAdaptationDomain.Gravtech: return "gravtech";
                case ReplicatorAdaptationDomain.Shield: return "shield";
                default: return "unknown";
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
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

            Scribe_Values.Look(ref successfulAssimilations, "successfulAssimilations", 0);
            Scribe_Values.Look(ref lastSuccessfulAssimilationTick, "lastSuccessfulAssimilationTick", -1);
            Scribe_Values.Look(ref specializedDomain, "specializedDomain", -1);
        }
    }
}
