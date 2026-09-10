using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    [Flags]
    public enum ReplicatorAdaptation
    {
        None = 0,
        Material = 1 << 0,
        Armor = 1 << 1,
        Ranged = 1 << 2,
        Power = 1 << 3,
        Shield = 1 << 4,
        Grav = 1 << 5,
        AntiShield = 1 << 6
    }

    internal enum LegacyReplicatorAdaptation
    {
        None = 0,
        Material = 1,
        Armor = 2,
        Ranged = 3,
        Power = 4,
        Shield = 5,
        Grav = 6
    }

    public sealed class CompProperties_ReplicatorState : CompProperties
    {
        public int antiShieldEvidenceRequired = 3;
        public CompProperties_ReplicatorState() => compClass = typeof(CompReplicatorState);
    }

    public sealed class CompReplicatorState : ThingComp
    {
        private string materialDefName;
        private int adaptationMask;
        private int shieldEvidence;
        private CompProperties_ReplicatorState Props => (CompProperties_ReplicatorState)props;

        public ThingDef MaterialDef => string.IsNullOrEmpty(materialDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(materialDefName);
        public ReplicatorAdaptation Adaptations => (ReplicatorAdaptation)adaptationMask;
        public int ShieldEvidence => shieldEvidence;
        public int AntiShieldEvidenceRequired => Math.Max(1, Props.antiShieldEvidenceRequired);

        public bool HasAdaptation(ReplicatorAdaptation adaptation)
            => adaptation != ReplicatorAdaptation.None && (Adaptations & adaptation) == adaptation;

        public bool AddAdaptation(ReplicatorAdaptation adaptation)
        {
            if (adaptation == ReplicatorAdaptation.None) return false;
            int before = adaptationMask;
            adaptationMask |= (int)adaptation;
            return before != adaptationMask;
        }

        public void CopyFrom(CompReplicatorState other)
        {
            if (other == null) return;
            materialDefName = other.materialDefName;
            adaptationMask = other.adaptationMask;
            shieldEvidence = other.shieldEvidence;
        }

        public void MergeFrom(CompReplicatorState other)
        {
            if (other == null) return;
            if (string.IsNullOrEmpty(materialDefName) && !string.IsNullOrEmpty(other.materialDefName)) materialDefName = other.materialDefName;
            adaptationMask |= other.adaptationMask;
            shieldEvidence = Math.Min(int.MaxValue, shieldEvidence + Math.Max(0, other.shieldEvidence));
            UnlockAntiShieldIfReady();
        }

        public void RecordAssimilation(Thing target)
        {
            if (target?.def == null) return;
            ThingDef material = target.Stuff ?? (target.def.IsStuff ? target.def : null);
            if (material != null)
            {
                materialDefName = material.defName;
                AddAdaptation(ReplicatorAdaptation.Material);
            }
            if (target.def.IsWeapon && target.def.IsRangedWeapon) AddAdaptation(ReplicatorAdaptation.Ranged);
            if (target.TryGetComp<CompPowerTrader>() != null || target.TryGetComp<CompPowerBattery>() != null) AddAdaptation(ReplicatorAdaptation.Power);
            if (target.def.apparel != null || (target.def.useHitPoints && target.def.BaseMaxHitPoints >= 500)) AddAdaptation(ReplicatorAdaptation.Armor);

            string identity = ((target.def.defName ?? string.Empty) + " " + (target.def.label ?? string.Empty)).ToLowerInvariant();
            if (identity.Contains("shield") || identity.Contains("barrier"))
            {
                AddAdaptation(ReplicatorAdaptation.Shield);
                if (shieldEvidence < int.MaxValue) shieldEvidence++;
                UnlockAntiShieldIfReady();
            }
            if (identity.Contains("grav") || identity.Contains("gravity")) AddAdaptation(ReplicatorAdaptation.Grav);
        }

        private void UnlockAntiShieldIfReady()
        {
            if (shieldEvidence >= AntiShieldEvidenceRequired) AddAdaptation(ReplicatorAdaptation.AntiShield);
        }

        private IEnumerable<ReplicatorAdaptation> LearnedAdaptations()
        {
            ReplicatorAdaptation[] values = { ReplicatorAdaptation.Material, ReplicatorAdaptation.Armor, ReplicatorAdaptation.Ranged, ReplicatorAdaptation.Power, ReplicatorAdaptation.Shield, ReplicatorAdaptation.Grav, ReplicatorAdaptation.AntiShield };
            return values.Where(HasAdaptation);
        }

        public override string CompInspectStringExtra()
        {
            string material = MaterialDef?.LabelCap;
            List<string> lines = new List<string>();
            if (!string.IsNullOrEmpty(material)) lines.Add($"Replication material: {material}");
            List<string> learned = LearnedAdaptations().Select(a => a == ReplicatorAdaptation.AntiShield ? "Anti-shield" : a.ToString()).ToList();
            if (learned.Count > 0) lines.Add($"Learned adaptations: {string.Join(", ", learned)}");
            if (HasAdaptation(ReplicatorAdaptation.Shield) && !HasAdaptation(ReplicatorAdaptation.AntiShield)) lines.Add($"Shield countermeasure evidence: {Math.Min(shieldEvidence, AntiShieldEvidenceRequired)}/{AntiShieldEvidenceRequired}");
            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref materialDefName, "wngReplicatorMaterial");
            Scribe_Values.Look(ref adaptationMask, "wngReplicatorAdaptationMask", 0);
            Scribe_Values.Look(ref shieldEvidence, "wngReplicatorShieldEvidence", 0);
            LegacyReplicatorAdaptation legacy = LegacyReplicatorAdaptation.None;
            Scribe_Values.Look(ref legacy, "wngReplicatorAdaptation", LegacyReplicatorAdaptation.None);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (adaptationMask == 0 && legacy != LegacyReplicatorAdaptation.None) adaptationMask = (int)ConvertLegacy(legacy);
                UnlockAntiShieldIfReady();
            }
        }

        private static ReplicatorAdaptation ConvertLegacy(LegacyReplicatorAdaptation legacy)
        {
            switch (legacy)
            {
                case LegacyReplicatorAdaptation.Material: return ReplicatorAdaptation.Material;
                case LegacyReplicatorAdaptation.Armor: return ReplicatorAdaptation.Armor;
                case LegacyReplicatorAdaptation.Ranged: return ReplicatorAdaptation.Ranged;
                case LegacyReplicatorAdaptation.Power: return ReplicatorAdaptation.Power;
                case LegacyReplicatorAdaptation.Shield: return ReplicatorAdaptation.Shield;
                case LegacyReplicatorAdaptation.Grav: return ReplicatorAdaptation.Grav;
                default: return ReplicatorAdaptation.None;
            }
        }
    }
}
