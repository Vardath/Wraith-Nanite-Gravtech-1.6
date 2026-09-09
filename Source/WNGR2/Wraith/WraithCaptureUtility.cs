using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Shared eligibility and hand-off boundary for Wraith abductions.
    /// Registration records the exact pawn without moving it. Transport systems call
    /// TryCompleteAbduction only when the physical capture has actually succeeded.
    /// </summary>
    public static class WraithCaptureUtility
    {
        public static bool IsValidAbductionTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return false;
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh)
                return false;
            if (WraithLifeForceUtility.Get(pawn) != null)
                return false;

            bool colonyPawn = pawn.Faction == Faction.OfPlayer;
            bool prisoner = pawn.guest?.IsPrisoner == true;
            if (!colonyPawn && !prisoner)
                return false;

            string race = pawn.def?.defName ?? string.Empty;
            string kind = pawn.kindDef?.defName ?? string.Empty;
            string xenotype = pawn.genes?.Xenotype?.defName ?? string.Empty;
            return !LooksSynthetic(race) && !LooksSynthetic(kind) && !LooksSynthetic(xenotype);
        }

        public static bool TryRegisterAbduction(Pawn pawn, Faction captor)
        {
            if (!IsValidAbductionTarget(pawn) || !IsWraithCaptor(captor))
                return false;

            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            return registry != null && registry.RegisterCapturedPawn(pawn, captor) != null;
        }

        /// <summary>
        /// Commits a completed physical abduction. This is intentionally separate from registration:
        /// callers must only invoke it after the Dart/raid transport has genuinely secured the pawn.
        /// The exact pawn is despawned and retained in WorldPawns forever so rescue can recover the
        /// same object later. No replacement pawn is generated.
        /// </summary>
        public static bool TryCompleteAbduction(Pawn pawn, Faction captor)
        {
            if (!IsValidAbductionTarget(pawn) || !IsWraithCaptor(captor) || Find.WorldPawns == null)
                return false;

            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            if (registry == null)
                return false;

            WraithCaptivityRecord record = registry.RegisterCapturedPawn(pawn, captor);
            if (record == null)
                return false;

            try
            {
                if (pawn.Spawned)
                    pawn.DeSpawn();
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                return !pawn.Spawned && registry.FindRecord(pawn) == record;
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Failed to complete exact-pawn Wraith abduction for " + pawn + ": " + ex);
                return false;
            }
        }

        public static bool IsWraithCaptor(Faction faction)
        {
            string defName = faction?.def?.defName;
            return defName == "WNG_WraithBrood"
                || defName == "WNG_WraithCinderCourt"
                || defName == "WNG_WraithVeiledHive"
                || defName == "WNG_WraithExiles";
        }

        private static bool LooksSynthetic(string value)
        {
            if (value.NullOrEmpty())
                return false;
            return value.IndexOf("Replicator", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Asuran", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Nanite", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
