using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public enum ReplicatorAdaptation
    {
        None,
        Material,
        Armor,
        Ranged,
        Power,
        Shield,
        Grav
    }

    /// <summary>
    /// Small save-safe state packet that can pass through split/recombine transactions.
    /// It deliberately stores identity/state, not balance constants.
    /// </summary>
    public sealed class CompProperties_ReplicatorState : CompProperties
    {
        public CompProperties_ReplicatorState() => compClass = typeof(CompReplicatorState);
    }

    public sealed class CompReplicatorState : ThingComp
    {
        private string materialDefName;
        private ReplicatorAdaptation adaptation;

        public ThingDef MaterialDef => string.IsNullOrEmpty(materialDefName)
            ? null
            : DefDatabase<ThingDef>.GetNamedSilentFail(materialDefName);
        public ReplicatorAdaptation Adaptation => adaptation;

        public void CopyFrom(CompReplicatorState other)
        {
            if (other == null) return;
            materialDefName = other.materialDefName;
            adaptation = other.adaptation;
        }

        public void MergeFrom(CompReplicatorState other)
        {
            if (other == null) return;
            if (string.IsNullOrEmpty(materialDefName) && !string.IsNullOrEmpty(other.materialDefName))
                materialDefName = other.materialDefName;
            if (adaptation == ReplicatorAdaptation.None && other.adaptation != ReplicatorAdaptation.None)
                adaptation = other.adaptation;
        }

        public void RecordAssimilation(Thing target)
        {
            if (target?.def == null) return;

            ThingDef material = target.Stuff ?? (target.def.IsStuff ? target.def : null);
            if (material != null)
            {
                materialDefName = material.defName;
                adaptation = ReplicatorAdaptation.Material;
            }

            if (target.def.IsWeapon)
            {
                adaptation = target.def.IsRangedWeapon ? ReplicatorAdaptation.Ranged : ReplicatorAdaptation.Armor;
                return;
            }

            if (target.TryGetComp<CompPowerTrader>() != null || target.TryGetComp<CompPowerBattery>() != null)
            {
                adaptation = ReplicatorAdaptation.Power;
                return;
            }

            if (target.def.category == ThingCategory.Apparel || target.def.useHitPoints && target.def.BaseMaxHitPoints >= 500)
                adaptation = ReplicatorAdaptation.Armor;
        }

        public override string CompInspectStringExtra()
        {
            string material = MaterialDef?.LabelCap;
            if (adaptation == ReplicatorAdaptation.None && string.IsNullOrEmpty(material)) return null;
            if (string.IsNullOrEmpty(material)) return $"Learned adaptation: {adaptation}";
            return $"Replication material: {material}\nLearned adaptation: {adaptation}";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref materialDefName, "wngReplicatorMaterial");
            Scribe_Values.Look(ref adaptation, "wngReplicatorAdaptation", ReplicatorAdaptation.None);
        }
    }
}
