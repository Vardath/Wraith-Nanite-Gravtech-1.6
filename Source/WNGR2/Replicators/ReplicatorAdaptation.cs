using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public enum ReplicatorAdaptationType
    {
        Primitive = 0,
        Ranged = 1,
        Armor = 2,
        Power = 3,
        Grav = 4,
        Shield = 5
    }

    public sealed class MapComponent_ReplicatorAdaptation : MapComponent
    {
        private const int CheckIntervalTicks = 600;
        private const int FreshLineageResetTicks = 60000;
        private const int ShieldEngagementSampleIntervalTicks = 300;
        private const int ShieldCountermeasureThreshold = 18;

        private int rangedPoints;
        private int armorPoints;
        private int powerPoints;
        private int gravPoints;
        private int shieldPoints;
        private int shieldEngagementSamples;
        private int nextShieldEngagementSampleTick;
        private int extinctionTicks;
        private bool historicalSeedApplied;
        private bool announcedRanged;
        private bool announcedArmor;
        private bool announcedPower;
        private bool announcedGrav;
        private bool announcedShield;
        private bool announcedShieldCountermeasure;

        public MapComponent_ReplicatorAdaptation(Map map) : base(map) { }

        public int PointsFor(ReplicatorAdaptationType type)
        {
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: return rangedPoints;
                case ReplicatorAdaptationType.Armor: return armorPoints;
                case ReplicatorAdaptationType.Power: return powerPoints;
                case ReplicatorAdaptationType.Grav: return gravPoints;
                case ReplicatorAdaptationType.Shield: return shieldPoints;
                default: return 0;
            }
        }

        public static int ThresholdFor(ReplicatorAdaptationType type)
        {
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: return 3;
                case ReplicatorAdaptationType.Armor: return 4;
                case ReplicatorAdaptationType.Power: return 4;
                case ReplicatorAdaptationType.Grav: return 5;
                case ReplicatorAdaptationType.Shield: return 8;
                default: return int.MaxValue;
            }
        }

        public bool Learned(ReplicatorAdaptationType type) => PointsFor(type) >= ThresholdFor(type);
        public bool ShieldCountermeasureLearned => Learned(ReplicatorAdaptationType.Shield) && shieldEngagementSamples >= ShieldCountermeasureThreshold;
        public int ShieldEngagementSamples => shieldEngagementSamples;

        public void RecordSuccessfulAssimilation(Pawn assimilator, Thing target)
        {
            if (assimilator == null || target == null || assimilator.Faction == Faction.OfPlayer)
                return;
            if (ReplicatorEMPSuppressionUtility.IsSuppressed(assimilator) ||
                ReplicatorContainmentUtility.BlocksAssimilation(assimilator, target) ||
                ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(assimilator))
                return;

            SeedFromPersistentHistoryIfNeeded();
            ReplicatorAdaptationType type = ReplicatorAdaptationUtility.ClassifyTechnology(target);
            if (type == ReplicatorAdaptationType.Primitive)
                return;

            int gain = ReplicatorAdaptationUtility.IsAdvancedPrecursorTechnology(target) ? 2 : 1;
            bool wasLearned = Learned(type);
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: rangedPoints += gain; break;
                case ReplicatorAdaptationType.Armor: armorPoints += gain; break;
                case ReplicatorAdaptationType.Power: powerPoints += gain; break;
                case ReplicatorAdaptationType.Grav: gravPoints += gain; break;
                case ReplicatorAdaptationType.Shield: shieldPoints += gain; break;
            }

            Current.Game?.GetComponent<ReplicatorAdaptationHistory>()?.RecordTechnology(type, gain);
            extinctionTicks = 0;
            if (!wasLearned && Learned(type))
                AnnounceLearned(type, assimilator);
        }

        public void RecordShieldEngagement(Pawn attacker, Thing shieldSource, float blockedDamage)
        {
            if (attacker == null || attacker.Dead || !attacker.Spawned || attacker.Map != map || attacker.Faction == null ||
                attacker.Faction == Faction.OfPlayer || !ReplicatorQueenUtility.IsBlockReplicator(attacker) || blockedDamage <= 0f)
                return;

            SeedFromPersistentHistoryIfNeeded();
            if (!Learned(ReplicatorAdaptationType.Shield) || ShieldCountermeasureLearned)
                return;
            if (shieldSource?.Faction != null && !attacker.Faction.HostileTo(shieldSource.Faction))
                return;
            if (ReplicatorEMPSuppressionUtility.IsSuppressed(attacker) ||
                ReplicatorContainmentUtility.IsContained(map, attacker.Position) ||
                ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(attacker))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextShieldEngagementSampleTick)
                return;

            nextShieldEngagementSampleTick = now + ShieldEngagementSampleIntervalTicks;
            shieldEngagementSamples = Math.Min(ShieldCountermeasureThreshold, shieldEngagementSamples + 1);
            Current.Game?.GetComponent<ReplicatorAdaptationHistory>()?.RecordShieldEngagement();
            extinctionTicks = 0;
            if (ShieldCountermeasureLearned)
                AnnounceShieldCountermeasure(attacker);
        }

        public ReplicatorAdaptationType SelectSpecialization(ReplicatorAdaptationType inherited = ReplicatorAdaptationType.Primitive)
        {
            if (inherited != ReplicatorAdaptationType.Primitive && Rand.Chance(0.68f))
                return inherited;

            List<Tuple<ReplicatorAdaptationType, float>> learned = new List<Tuple<ReplicatorAdaptationType, float>>();
            if (Learned(ReplicatorAdaptationType.Ranged)) learned.Add(Tuple.Create(ReplicatorAdaptationType.Ranged, 1.0f));
            if (Learned(ReplicatorAdaptationType.Armor)) learned.Add(Tuple.Create(ReplicatorAdaptationType.Armor, 0.9f));
            if (Learned(ReplicatorAdaptationType.Power)) learned.Add(Tuple.Create(ReplicatorAdaptationType.Power, 0.9f));
            if (Learned(ReplicatorAdaptationType.Grav)) learned.Add(Tuple.Create(ReplicatorAdaptationType.Grav, 0.65f));
            if (Learned(ReplicatorAdaptationType.Shield)) learned.Add(Tuple.Create(ReplicatorAdaptationType.Shield, 0.22f));
            if (learned.Count == 0 || !Rand.Chance(0.46f))
                return ReplicatorAdaptationType.Primitive;

            float total = learned.Sum(x => x.Item2);
            float roll = Rand.Value * total;
            foreach (Tuple<ReplicatorAdaptationType, float> entry in learned)
            {
                roll -= entry.Item2;
                if (roll <= 0f)
                    return entry.Item1;
            }
            return learned[learned.Count - 1].Item1;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!map.IsHashIntervalTick(CheckIntervalTicks))
                return;

            bool hostileBlocksRemain = map.mapPawns.AllPawnsSpawned.Any(p =>
                p != null && !p.Dead && p.Faction != Faction.OfPlayer && ReplicatorQueenUtility.IsBlockReplicator(p));
            if (hostileBlocksRemain)
            {
                SeedFromPersistentHistoryIfNeeded();
                extinctionTicks = 0;
                return;
            }

            extinctionTicks += CheckIntervalTicks;
            if (extinctionTicks >= FreshLineageResetTicks)
                ResetLineage();
        }

        private void SeedFromPersistentHistoryIfNeeded()
        {
            if (historicalSeedApplied)
                return;
            ReplicatorAdaptationHistory history = Current.Game?.GetComponent<ReplicatorAdaptationHistory>();
            if (history != null)
            {
                rangedPoints = Math.Max(rangedPoints, history.SeedPoints(ReplicatorAdaptationType.Ranged));
                armorPoints = Math.Max(armorPoints, history.SeedPoints(ReplicatorAdaptationType.Armor));
                powerPoints = Math.Max(powerPoints, history.SeedPoints(ReplicatorAdaptationType.Power));
                gravPoints = Math.Max(gravPoints, history.SeedPoints(ReplicatorAdaptationType.Grav));
                shieldPoints = Math.Max(shieldPoints, history.SeedPoints(ReplicatorAdaptationType.Shield));
                shieldEngagementSamples = Math.Max(shieldEngagementSamples, history.ShieldEngagementSeed);
            }
            historicalSeedApplied = true;
        }

        private void AnnounceLearned(ReplicatorAdaptationType type, Pawn source)
        {
            if (WasAnnounced(type))
                return;
            SetAnnounced(type, true);
            string role = ReplicatorAdaptationUtility.RoleLabel(type);
            Find.LetterStack.ReceiveLetter(
                "Replicator adaptation: " + role,
                "The local Replicator lineage has finished assimilating enough relevant technology to reproduce a new " + role + " specialist body plan. Destroying the remaining swarm and keeping the map clear will eventually erase this local lineage memory.",
                LetterDefOf.ThreatSmall,
                source);
        }

        private void AnnounceShieldCountermeasure(Pawn source)
        {
            if (announcedShieldCountermeasure)
                return;
            announcedShieldCountermeasure = true;
            Find.LetterStack.ReceiveLetter(
                "Replicator adaptation: shield disruption",
                "After prolonged contact with active defensive fields, the local Replicator lineage has phase-tuned a dedicated anti-shield response. Rare shield-specialist bodies can now grow short-range disruption emitters. EMP suppression, containment and lattice override can still interrupt them.",
                LetterDefOf.ThreatBig,
                source);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                CompReplicatorAdaptation comp = pawn?.TryGetComp<CompReplicatorAdaptation>();
                if (pawn != null && !pawn.Dead && pawn.Faction != Faction.OfPlayer &&
                    ReplicatorQueenUtility.IsBlockReplicator(pawn) && comp?.Specialization == ReplicatorAdaptationType.Shield)
                    comp.RefreshRoleEquipment();
            }
        }

        private bool WasAnnounced(ReplicatorAdaptationType type)
        {
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: return announcedRanged;
                case ReplicatorAdaptationType.Armor: return announcedArmor;
                case ReplicatorAdaptationType.Power: return announcedPower;
                case ReplicatorAdaptationType.Grav: return announcedGrav;
                case ReplicatorAdaptationType.Shield: return announcedShield;
                default: return false;
            }
        }

        private void SetAnnounced(ReplicatorAdaptationType type, bool value)
        {
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: announcedRanged = value; break;
                case ReplicatorAdaptationType.Armor: announcedArmor = value; break;
                case ReplicatorAdaptationType.Power: announcedPower = value; break;
                case ReplicatorAdaptationType.Grav: announcedGrav = value; break;
                case ReplicatorAdaptationType.Shield: announcedShield = value; break;
            }
        }

        private void ResetLineage()
        {
            historicalSeedApplied = false;
            rangedPoints = armorPoints = powerPoints = gravPoints = shieldPoints = 0;
            shieldEngagementSamples = 0;
            nextShieldEngagementSampleTick = 0;
            extinctionTicks = FreshLineageResetTicks;
            announcedRanged = announcedArmor = announcedPower = announcedGrav = announcedShield = false;
            announcedShieldCountermeasure = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref rangedPoints, "wngReplicatorAdaptRanged", 0);
            Scribe_Values.Look(ref armorPoints, "wngReplicatorAdaptArmor", 0);
            Scribe_Values.Look(ref powerPoints, "wngReplicatorAdaptPower", 0);
            Scribe_Values.Look(ref gravPoints, "wngReplicatorAdaptGrav", 0);
            Scribe_Values.Look(ref shieldPoints, "wngReplicatorAdaptShield", 0);
            Scribe_Values.Look(ref shieldEngagementSamples, "wngReplicatorShieldEngagementSamples", 0);
            Scribe_Values.Look(ref nextShieldEngagementSampleTick, "wngReplicatorShieldNextSampleTick", 0);
            Scribe_Values.Look(ref extinctionTicks, "wngReplicatorAdaptExtinctionTicks", 0);
            Scribe_Values.Look(ref historicalSeedApplied, "wngReplicatorHistorySeedApplied", false);
            Scribe_Values.Look(ref announcedRanged, "wngReplicatorAdaptAnnouncedRanged", false);
            Scribe_Values.Look(ref announcedArmor, "wngReplicatorAdaptAnnouncedArmor", false);
            Scribe_Values.Look(ref announcedPower, "wngReplicatorAdaptAnnouncedPower", false);
            Scribe_Values.Look(ref announcedGrav, "wngReplicatorAdaptAnnouncedGrav", false);
            Scribe_Values.Look(ref announcedShield, "wngReplicatorAdaptAnnouncedShield", false);
            Scribe_Values.Look(ref announcedShieldCountermeasure, "wngReplicatorAdaptAnnouncedShieldCountermeasure", false);
        }
    }

    public static class ReplicatorAdaptationUtility
    {
        public static ReplicatorAdaptationType ClassifyTechnology(Thing target)
        {
            if (target?.def == null)
                return ReplicatorAdaptationType.Primitive;
            ThingDef def = target.def;
            string name = def.defName ?? string.Empty;

            if (name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                def.comps?.Any(c => c is CompProperties_ProjectileInterceptor) == true)
                return ReplicatorAdaptationType.Shield;
            if (def.building?.turretGunDef != null ||
                name.IndexOf("Turret", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Cannon", StringComparison.OrdinalIgnoreCase) >= 0)
                return ReplicatorAdaptationType.Ranged;
            if (name.IndexOf("Grav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Thruster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Shuttle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("PuddleJumper", StringComparison.OrdinalIgnoreCase) >= 0)
                return ReplicatorAdaptationType.Grav;
            if (target.TryGetComp<CompPowerTrader>() != null || target.TryGetComp<CompPowerBattery>() != null ||
                name.IndexOf("Generator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Power", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Battery", StringComparison.OrdinalIgnoreCase) >= 0)
                return ReplicatorAdaptationType.Power;
            if (def.IsApparel && (name.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Armour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Vest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Carapace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  name.IndexOf("Plate", StringComparison.OrdinalIgnoreCase) >= 0))
                return ReplicatorAdaptationType.Armor;
            return ReplicatorAdaptationType.Primitive;
        }

        public static bool IsAdvancedPrecursorTechnology(Thing target)
        {
            string name = target?.def?.defName ?? string.Empty;
            return name.StartsWith("WNG_Precursor", StringComparison.Ordinal) ||
                   name.StartsWith("WNG_Ancient", StringComparison.Ordinal) ||
                   name.StartsWith("WNG_Asuran", StringComparison.Ordinal) ||
                   name.StartsWith("WNG_Vacuum", StringComparison.Ordinal) ||
                   name.StartsWith("WNG_Puddle", StringComparison.Ordinal);
        }

        public static string RoleLabel(ReplicatorAdaptationType type)
        {
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: return "ranged";
                case ReplicatorAdaptationType.Armor: return "heavy-armor";
                case ReplicatorAdaptationType.Power: return "power-construction";
                case ReplicatorAdaptationType.Grav: return "grav-mobile";
                case ReplicatorAdaptationType.Shield: return "shielded";
                default: return "primitive";
            }
        }
    }

    public sealed class CompProperties_ReplicatorAdaptation : CompProperties
    {
        public CompProperties_ReplicatorAdaptation() { compClass = typeof(CompReplicatorAdaptation); }
    }

    public sealed class CompReplicatorAdaptation : ThingComp
    {
        private const float ShieldMax = 90f;
        private const int ShieldRechargeDelayTicks = 900;
        private const int ShieldEmpDisableTicks = 1800;
        private const int ShieldRechargeIntervalTicks = 120;

        private int specialization;
        private float shieldEnergy;
        private int shieldDisabledUntil;
        private int lastShieldHitTick;
        private int nextShieldRechargeTick;
        private Graphic overlayGraphic;
        private int overlayFor = -1;

        public ReplicatorAdaptationType Specialization => (ReplicatorAdaptationType)specialization;
        public float AssemblyIntervalFactor => Specialization == ReplicatorAdaptationType.Power ? 0.68f : 1f;
        public int AssimilationTicks => Specialization == ReplicatorAdaptationType.Power ? 180 : 300;
        public bool HasShieldCountermeasureWeapon
        {
            get
            {
                Pawn pawn = parent as Pawn;
                return pawn?.equipment != null && Specialization == ReplicatorAdaptationType.Shield &&
                       pawn.equipment.AllEquipmentListForReading.Any(e => e?.def?.defName == "WNG_ReplicatorShieldDisruptor");
            }
        }

        public void SetSpecialization(ReplicatorAdaptationType type)
        {
            specialization = (int)type;
            overlayGraphic = null;
            overlayFor = -1;
            if (type == ReplicatorAdaptationType.Shield && shieldEnergy <= 0f)
                shieldEnergy = ShieldMax;
            ApplyRoleHediff();
            EnsureRoleEquipment();
        }

        public void InheritFrom(Pawn parentPawn, Map map)
        {
            ReplicatorAdaptationType inherited = parentPawn?.TryGetComp<CompReplicatorAdaptation>()?.Specialization ?? ReplicatorAdaptationType.Primitive;
            MapComponent_ReplicatorAdaptation history = map?.GetComponent<MapComponent_ReplicatorAdaptation>();
            SetSpecialization(history?.SelectSpecialization(inherited) ?? inherited);
        }

        public void InheritExact(ReplicatorAdaptationType type) => SetSpecialization(type);
        public void RefreshRoleEquipment() => EnsureRoleEquipment();

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            ApplyRoleHediff();
            EnsureRoleEquipment();
            if (!respawningAfterLoad && Specialization == ReplicatorAdaptationType.Shield && shieldEnergy <= 0f)
                shieldEnergy = ShieldMax;
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || Specialization != ReplicatorAdaptationType.Shield)
                return;
            if (pawn.IsHashIntervalTick(600))
                EnsureRoleEquipment();
            int now = Find.TickManager.TicksGame;
            if (now < shieldDisabledUntil || now < nextShieldRechargeTick || now - lastShieldHitTick < ShieldRechargeDelayTicks)
                return;
            nextShieldRechargeTick = now + ShieldRechargeIntervalTicks;
            shieldEnergy = Math.Min(ShieldMax, shieldEnergy + 3f);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (Specialization != ReplicatorAdaptationType.Shield)
                return;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (dinfo.Def == DamageDefOf.EMP)
            {
                shieldEnergy = 0f;
                shieldDisabledUntil = now + ShieldEmpDisableTicks;
                lastShieldHitTick = now;
                return;
            }
            if (now < shieldDisabledUntil || shieldEnergy <= 0f || dinfo.Amount <= 0f)
                return;
            lastShieldHitTick = now;
            float incoming = dinfo.Amount;
            if (shieldEnergy + 0.001f >= incoming)
            {
                shieldEnergy -= incoming;
                absorbed = true;
                return;
            }
            float remainder = Math.Max(0f, incoming - shieldEnergy);
            shieldEnergy = 0f;
            dinfo.SetAmount(remainder);
        }

        private void ApplyRoleHediff()
        {
            Pawn pawn = parent as Pawn;
            if (pawn?.health == null)
                return;
            string[] defs = { "WNG_ReplicatorAdaptRanged", "WNG_ReplicatorAdaptArmor", "WNG_ReplicatorAdaptPower", "WNG_ReplicatorAdaptGrav", "WNG_ReplicatorAdaptShield" };
            string wanted = Specialization == ReplicatorAdaptationType.Primitive ? null : "WNG_ReplicatorAdapt" + Specialization;
            foreach (string name in defs)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null && name != wanted)
                    pawn.health.RemoveHediff(existing);
            }
            if (wanted != null)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(wanted);
                if (def != null && !pawn.health.hediffSet.HasHediff(def))
                    pawn.health.AddHediff(def);
            }
        }

        private void EnsureRoleEquipment()
        {
            Pawn pawn = parent as Pawn;
            if (pawn?.equipment == null)
                return;
            string weaponDefName = null;
            if (Specialization == ReplicatorAdaptationType.Ranged)
                weaponDefName = "WNG_ReplicatorPulseCaster";
            else if (Specialization == ReplicatorAdaptationType.Shield && pawn.Faction != Faction.OfPlayer && pawn.Map != null &&
                     pawn.Map.GetComponent<MapComponent_ReplicatorAdaptation>()?.ShieldCountermeasureLearned == true)
                weaponDefName = "WNG_ReplicatorShieldDisruptor";
            if (weaponDefName == null)
                return;
            ThingDef weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDefName);
            if (weaponDef == null || pawn.equipment.AllEquipmentListForReading.Any(e => e.def == weaponDef))
                return;
            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon != null)
                pawn.equipment.AddEquipment(weapon);
        }

        public override void PostDraw()
        {
            base.PostDraw();
            Pawn pawn = parent as Pawn;
            if (pawn == null || Specialization == ReplicatorAdaptationType.Primitive)
                return;
            if (overlayGraphic == null || overlayFor != specialization)
            {
                string path = "Things/Pawn/Replicator/Adaptation/WNG_ReplicatorAdapt_" + Specialization;
                float size = Mathf.Clamp(0.42f + pawn.BodySize * 0.18f, 0.48f, 0.92f);
                overlayGraphic = GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, new Vector2(size, size), Color.white);
                overlayFor = specialization;
            }
            Vector3 pos = pawn.DrawPos;
            pos.y += 0.008f;
            pos.z += 0.05f;
            overlayGraphic?.Draw(pos, Rot4.North, pawn);
        }

        public override string CompInspectStringExtra()
        {
            if (Specialization == ReplicatorAdaptationType.Primitive)
                return "Replicator adaptation: primitive";
            string result = "Replicator adaptation: " + ReplicatorAdaptationUtility.RoleLabel(Specialization);
            if (Specialization == ReplicatorAdaptationType.Shield)
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                result += now < shieldDisabledUntil ? "\nPersonal field: EMP-disabled" : $"\nPersonal field: {Math.Ceiling(shieldEnergy)} / {ShieldMax}";
                if (HasShieldCountermeasureWeapon)
                    result += "\nCountermeasure: phase disruption emitter";
            }
            return result;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref specialization, "wngReplicatorAdaptationType", 0);
            Scribe_Values.Look(ref shieldEnergy, "wngReplicatorAdaptiveShieldEnergy", 0f);
            Scribe_Values.Look(ref shieldDisabledUntil, "wngReplicatorAdaptiveShieldDisabledUntil", 0);
            Scribe_Values.Look(ref lastShieldHitTick, "wngReplicatorAdaptiveShieldLastHit", 0);
            Scribe_Values.Look(ref nextShieldRechargeTick, "wngReplicatorAdaptiveShieldNextRecharge", 0);
        }
    }
}
