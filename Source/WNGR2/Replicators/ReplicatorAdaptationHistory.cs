using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Save-wide memory of technologies previously encountered by Replicator lineages. This does
    /// not grant unlocks. A new map can inherit only a bounded, sub-threshold heuristic seed, so
    /// every specialist body plan and the shield countermeasure still has to be learned locally.
    /// </summary>
    public sealed class ReplicatorAdaptationHistory : GameComponent
    {
        private int rangedEvidence;
        private int armorEvidence;
        private int powerEvidence;
        private int gravEvidence;
        private int shieldEvidence;
        private int shieldEngagementEvidence;

        public ReplicatorAdaptationHistory(Game game)
        {
        }

        public int ShieldEngagementSeed => Math.Min(6, shieldEngagementEvidence / 4);

        public void RecordTechnology(ReplicatorAdaptationType type, int amount)
        {
            int gain = Math.Max(0, amount);
            if (gain == 0)
                return;

            switch (type)
            {
                case ReplicatorAdaptationType.Ranged:
                    rangedEvidence = SafeAdd(rangedEvidence, gain);
                    break;
                case ReplicatorAdaptationType.Armor:
                    armorEvidence = SafeAdd(armorEvidence, gain);
                    break;
                case ReplicatorAdaptationType.Power:
                    powerEvidence = SafeAdd(powerEvidence, gain);
                    break;
                case ReplicatorAdaptationType.Grav:
                    gravEvidence = SafeAdd(gravEvidence, gain);
                    break;
                case ReplicatorAdaptationType.Shield:
                    shieldEvidence = SafeAdd(shieldEvidence, gain);
                    break;
            }
        }

        public void RecordShieldEngagement()
        {
            shieldEngagementEvidence = SafeAdd(shieldEngagementEvidence, 1);
        }

        public int SeedPoints(ReplicatorAdaptationType type)
        {
            int threshold = MapComponent_ReplicatorAdaptation.ThresholdFor(type);
            if (threshold == int.MaxValue || threshold <= 1)
                return 0;

            int evidence = EvidenceFor(type);
            if (evidence <= 0)
                return 0;

            // Persistent memory accelerates rediscovery but can never satisfy a local threshold.
            int seeded = evidence / 4;
            return Math.Min(threshold - 1, Math.Max(0, seeded));
        }

        private int EvidenceFor(ReplicatorAdaptationType type)
        {
            switch (type)
            {
                case ReplicatorAdaptationType.Ranged: return rangedEvidence;
                case ReplicatorAdaptationType.Armor: return armorEvidence;
                case ReplicatorAdaptationType.Power: return powerEvidence;
                case ReplicatorAdaptationType.Grav: return gravEvidence;
                case ReplicatorAdaptationType.Shield: return shieldEvidence;
                default: return 0;
            }
        }

        private static int SafeAdd(int current, int amount)
        {
            long result = (long)Math.Max(0, current) + Math.Max(0, amount);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref rangedEvidence, "wngReplicatorHistoryRanged", 0);
            Scribe_Values.Look(ref armorEvidence, "wngReplicatorHistoryArmor", 0);
            Scribe_Values.Look(ref powerEvidence, "wngReplicatorHistoryPower", 0);
            Scribe_Values.Look(ref gravEvidence, "wngReplicatorHistoryGrav", 0);
            Scribe_Values.Look(ref shieldEvidence, "wngReplicatorHistoryShield", 0);
            Scribe_Values.Look(ref shieldEngagementEvidence, "wngReplicatorHistoryShieldEngagements", 0);
        }
    }
}
