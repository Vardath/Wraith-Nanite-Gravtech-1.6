using Verse;

namespace WraithNaniteGravtech.Replicators
{
    public sealed class CompProperties_ReplicatorState : CompProperties
    {
        public CompProperties_ReplicatorState()
        {
            compClass = typeof(CompReplicatorState);
        }
    }

    /// <summary>
    /// Small save-safe state carrier shared by modular Replicator forms.
    /// The hierarchy copies this state when a body splits or recombines so a body change
    /// never silently erases its material identity, learned specialization or temporary lattice state.
    /// </summary>
    public sealed class CompReplicatorState : ThingComp
    {
        private string materialSignature;
        private string specialization;
        private int latticeOverrideUntilTick;

        public string MaterialSignature => materialSignature;
        public string Specialization => specialization;
        public int LatticeOverrideUntilTick => latticeOverrideUntilTick;

        public void SetMaterialSignature(string value) => materialSignature = value;
        public void SetSpecialization(string value) => specialization = value;

        public void SetLatticeOverrideUntil(int absoluteTick)
        {
            if (absoluteTick > latticeOverrideUntilTick)
                latticeOverrideUntilTick = absoluteTick;
        }

        public bool LatticeOverrideActive
        {
            get
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                return latticeOverrideUntilTick > now;
            }
        }

        public void CopyFrom(CompReplicatorState source)
        {
            if (source == null)
                return;

            materialSignature = source.materialSignature;
            specialization = source.specialization;
            latticeOverrideUntilTick = source.latticeOverrideUntilTick;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref materialSignature, "materialSignature");
            Scribe_Values.Look(ref specialization, "specialization");
            Scribe_Values.Look(ref latticeOverrideUntilTick, "latticeOverrideUntilTick", 0);
        }
    }
}
