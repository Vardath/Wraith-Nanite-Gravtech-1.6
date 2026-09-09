using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public enum ReplicatorSpecialistRole
    {
        None,
        Repair,
        Burrower,
        Artillery
    }

    public sealed class CompProperties_ReplicatorSpecialistBody : CompProperties
    {
        public ReplicatorSpecialistRole role = ReplicatorSpecialistRole.None;
        public float buildingAssimilationFactor = 1f;
        public float itemAssimilationFactor = 1f;
        public float buildingTargetScoreOffset = 0f;
        public string integratedWeaponDefName;

        public CompProperties_ReplicatorSpecialistBody()
        {
            compClass = typeof(CompReplicatorSpecialistBody);
        }
    }

    /// <summary>
    /// Small, explicit behavior differences for specialist block bodies. The comp does not create a
    /// separate population ladder; specialists remain ordinary Replicator matter arrangements.
    /// </summary>
    public sealed class CompReplicatorSpecialistBody : ThingComp
    {
        private CompProperties_ReplicatorSpecialistBody Props => (CompProperties_ReplicatorSpecialistBody)props;
        public ReplicatorSpecialistRole Role => Props.role;

        public float AssimilationFactorFor(Thing target)
        {
            if (target?.def?.category == ThingCategory.Building)
                return Math.Max(0.1f, Props.buildingAssimilationFactor);
            if (target?.def?.category == ThingCategory.Item)
                return Math.Max(0.1f, Props.itemAssimilationFactor);
            return 1f;
        }

        public float TargetScoreOffset(Thing target)
        {
            if (target == null)
                return 0f;

            if (Role == ReplicatorSpecialistRole.Repair)
                return 12f;

            if (target.def?.category == ThingCategory.Building)
                return Props.buildingTargetScoreOffset;

            return 0f;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureIntegratedWeapon();
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn != null && pawn.Spawned && pawn.IsHashIntervalTick(1200))
                EnsureIntegratedWeapon();
        }

        private void EnsureIntegratedWeapon()
        {
            if (string.IsNullOrEmpty(Props.integratedWeaponDefName))
                return;

            Pawn pawn = parent as Pawn;
            if (pawn?.equipment == null)
                return;

            ThingDef weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail(Props.integratedWeaponDefName);
            if (weaponDef == null)
                return;

            for (int i = 0; i < pawn.equipment.AllEquipmentListForReading.Count; i++)
            {
                if (pawn.equipment.AllEquipmentListForReading[i]?.def == weaponDef)
                    return;
            }

            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon != null)
                pawn.equipment.AddEquipment(weapon);
        }

        public override string CompInspectStringExtra()
        {
            return Role == ReplicatorSpecialistRole.None
                ? null
                : "Replicator body role: " + Role.ToString().ToLowerInvariant();
        }
    }
}
