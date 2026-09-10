using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WraithCaptivityRecord : IExposable
    {
        public Pawn pawn;
        public string captorFactionDefName;
        public int capturedTick = -1;
        public bool heldAsFeedingStock;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref captorFactionDefName, "captorFactionDefName");
            Scribe_Values.Look(ref capturedTick, "capturedTick", -1);
            Scribe_Values.Look(ref heldAsFeedingStock, "heldAsFeedingStock", false);
        }
    }

    /// <summary>
    /// Exact-pawn continuity ledger for Wraith abduction/captivity.
    ///
    /// The registry deliberately stores the real pawn reference rather than creating proxy victims.
    /// Dart abduction, Mature-Hive feeding stock and later rescue-site systems can all point to this
    /// same record.  It does not implement experimentation/thrall escalation; those branches remain
    /// separate until their current design is reconciled.
    /// </summary>
    public sealed class WraithCaptivityRegistry : GameComponent
    {
        private List<WraithCaptivityRecord> records = new List<WraithCaptivityRecord>();

        public WraithCaptivityRegistry(Game game) { }

        public static WraithCaptivityRegistry Current => Verse.Current.Game?.GetComponent<WraithCaptivityRegistry>();
        public IEnumerable<WraithCaptivityRecord> Records => records ?? Enumerable.Empty<WraithCaptivityRecord>();

        public static bool IsWraithFaction(Faction faction)
        {
            return faction?.def?.GetModExtension<WraithFactionHungerExtension>() != null;
        }

        public static bool IsValidBiologicalCaptive(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh)
                return false;
            if (WraithLifeForceUtility.IsWraith(pawn))
                return false;

            string identity = ((pawn.kindDef?.defName ?? string.Empty) + " "
                + (pawn.def?.defName ?? string.Empty) + " "
                + (pawn.genes?.Xenotype?.defName ?? string.Empty)).ToLowerInvariant();
            return !identity.Contains("replicator") && !identity.Contains("asuran") && !identity.Contains("nanite");
        }

        public WraithCaptivityRecord RegisterCapturedPawn(Pawn pawn, Faction captor, bool feedingStock = false)
        {
            if (!IsValidBiologicalCaptive(pawn) || !IsWraithFaction(captor))
                return null;

            WraithCaptivityRecord record = FindRecord(pawn);
            if (record == null)
            {
                record = new WraithCaptivityRecord
                {
                    pawn = pawn,
                    capturedTick = Find.TickManager?.TicksGame ?? 0
                };
                records.Add(record);
            }

            record.captorFactionDefName = captor.def.defName;
            record.heldAsFeedingStock = feedingStock;
            return record;
        }

        public WraithCaptivityRecord FindRecord(Pawn pawn)
        {
            if (pawn == null || records == null)
                return null;
            return records.FirstOrDefault(r => r != null && r.pawn == pawn);
        }

        public IEnumerable<WraithCaptivityRecord> CaptivesFor(Faction captor)
        {
            if (captor?.def == null || records == null)
                return Enumerable.Empty<WraithCaptivityRecord>();
            string defName = captor.def.defName;
            return records.Where(r => r != null && r.pawn != null && !r.pawn.Dead && r.captorFactionDefName == defName);
        }

        public bool SetFeedingStock(Pawn pawn, bool feedingStock)
        {
            WraithCaptivityRecord record = FindRecord(pawn);
            if (record == null)
                return false;
            record.heldAsFeedingStock = feedingStock;
            return true;
        }

        public bool ReleaseExactPawn(Pawn pawn)
        {
            WraithCaptivityRecord record = FindRecord(pawn);
            return record != null && records.Remove(record);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "wngWraithCaptivityRecords", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records = records?.Where(r => r != null && r.pawn != null).ToList()
                    ?? new List<WraithCaptivityRecord>();
            }
        }
    }
}
