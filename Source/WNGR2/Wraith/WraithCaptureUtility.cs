using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Shared eligibility and hand-off boundary for Wraith abductions.
    /// Transport systems are responsible for physically moving the pawn; this utility only decides
    /// whether that exact pawn is valid prey and commits its identity to the captivity registry.
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
