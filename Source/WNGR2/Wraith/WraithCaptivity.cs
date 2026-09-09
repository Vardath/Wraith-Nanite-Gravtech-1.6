using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public enum WraithCaptiveTreatmentStage
    {
        FeedingStock = 0,
        Experimentation = 1,
        Conditioning = 2,
        Enthralled = 3
    }

    public sealed class WraithCaptivityRecord : IExposable
    {
        public Pawn pawn;
        public string captorFactionDefName;
        public int capturedTick;
        public int rescueFailures;
        public WraithCaptiveTreatmentStage stage;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref captorFactionDefName, "captorFactionDefName");
            Scribe_Values.Look(ref capturedTick, "capturedTick", -1);
            Scribe_Values.Look(ref rescueFailures, "rescueFailures", 0);
            Scribe_Values.Look(ref stage, "stage", WraithCaptiveTreatmentStage.FeedingStock);
        }
    }

    /// <summary>
    /// Save-persistent identity ledger for pawns captured by Wraith factions.
    /// This component never generates replacement pawns: rescue, escalation and later Dart/site
    /// systems operate on the exact Pawn reference that was originally captured.
    /// </summary>
    public sealed class WraithCaptivityRegistry : GameComponent
    {
        private List<WraithCaptivityRecord> records = new List<WraithCaptivityRecord>();

        private static readonly string[] StageHediffs =
        {
            "WNG_WraithFeedingStock",
            "WNG_WraithExperimentation",
            "WNG_WraithConditioning",
            "WNG_WraithEnthralled"
        };

        public WraithCaptivityRegistry(Game game) { }

        public static WraithCaptivityRegistry Current => Verse.Current.Game?.GetComponent<WraithCaptivityRegistry>();

        public IEnumerable<WraithCaptivityRecord> Records => records ?? Enumerable.Empty<WraithCaptivityRecord>();

        public WraithCaptivityRecord RegisterCapturedPawn(Pawn pawn, Faction captor)
        {
            if (pawn == null || pawn.Dead || captor?.def == null)
                return null;

            WraithCaptivityRecord existing = FindRecord(pawn);
            if (existing != null)
                return existing;

            WraithCaptivityRecord record = new WraithCaptivityRecord
            {
                pawn = pawn,
                captorFactionDefName = captor.def.defName,
                capturedTick = Find.TickManager?.TicksGame ?? -1,
                rescueFailures = 0,
                stage = WraithCaptiveTreatmentStage.FeedingStock
            };
            records.Add(record);
            ApplyStageHediff(record);
            return record;
        }

        public WraithCaptivityRecord FindRecord(Pawn pawn)
        {
            if (pawn == null || records == null)
                return null;
            return records.FirstOrDefault(r => r != null && r.pawn == pawn);
        }

        public bool RecordRescueFailure(Pawn pawn)
        {
            WraithCaptivityRecord record = FindRecord(pawn);
            if (record == null)
                return false;

            record.rescueFailures = Math.Max(0, record.rescueFailures) + 1;
            int next = Math.Min((int)WraithCaptiveTreatmentStage.Enthralled, (int)record.stage + 1);
            record.stage = (WraithCaptiveTreatmentStage)next;
            ApplyStageHediff(record);
            return true;
        }

        public bool ReleaseExactPawn(Pawn pawn)
        {
            WraithCaptivityRecord record = FindRecord(pawn);
            if (record == null)
                return false;

            // Enthrallment represents a durable treatment outcome and is intentionally not stripped
            // merely because the captive reached safety. Earlier captivity-status markers are removed.
            if (record.stage != WraithCaptiveTreatmentStage.Enthralled)
                RemoveStageHediffs(pawn);
            else
                RemoveStageHediffsExcept(pawn, "WNG_WraithEnthralled");

            return records.Remove(record);
        }

        private static void ApplyStageHediff(WraithCaptivityRecord record)
        {
            Pawn pawn = record?.pawn;
            if (pawn?.health?.hediffSet == null)
                return;

            int index = Math.Max(0, Math.Min(StageHediffs.Length - 1, (int)record.stage));
            string wanted = StageHediffs[index];
            RemoveStageHediffsExcept(pawn, wanted);

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(wanted);
            if (def != null && !pawn.health.hediffSet.HasHediff(def))
                pawn.health.AddHediff(def);
        }

        private static void RemoveStageHediffs(Pawn pawn)
        {
            RemoveStageHediffsExcept(pawn, null);
        }

        private static void RemoveStageHediffsExcept(Pawn pawn, string keepDefName)
        {
            if (pawn?.health?.hediffSet == null)
                return;

            foreach (string defName in StageHediffs)
            {
                if (defName == keepDefName)
                    continue;
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                if (def == null)
                    continue;
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing != null)
                    pawn.health.RemoveHediff(existing);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "wngWraithCaptivityRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                records = records?.Where(r => r != null && r.pawn != null).ToList() ?? new List<WraithCaptivityRecord>();
        }
    }
}
