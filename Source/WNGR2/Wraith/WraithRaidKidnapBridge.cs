using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Converts completed vanilla ground-raid kidnappings by Wraith factions into WNG's exact-pawn
    /// captivity lifecycle. Vanilla remains responsible for the physical carry/escape sequence.
    /// Once WNG adopts the exact world pawn, it removes that pawn from vanilla's kidnapped tracker
    /// so ransom/recruitment logic cannot compete with Wraith feeding, treatment and rescue.
    /// </summary>
    public sealed class GameComponent_WraithRaidKidnapBridge : GameComponent
    {
        private int nextScanTick;

        public GameComponent_WraithRaidKidnapBridge(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextScanTick)
                return;
            nextScanTick = now + 250;

            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            if (registry == null || Find.FactionManager?.AllFactionsListForReading == null)
                return;

            foreach (Faction faction in Find.FactionManager.AllFactionsListForReading.ToList())
            {
                if (!WraithCaptureUtility.IsWraithCaptor(faction) || faction?.kidnapped == null)
                    continue;

                foreach (Pawn pawn in faction.kidnapped.KidnappedPawnsListForReading.ToList())
                {
                    if (!ShouldAdopt(pawn))
                        continue;

                    WraithCaptivityRecord record = registry.FindRecord(pawn)
                        ?? registry.RegisterCapturedPawn(pawn, faction);
                    if (record == null)
                        continue;

                    // The same exact pawn is already retained in WorldPawns by vanilla Kidnap().
                    // WNG now owns its off-map lifecycle, so disable vanilla ransom/recruitment drift.
                    faction.kidnapped.RemoveKidnappedPawn(pawn);
                }
            }
        }

        private static bool ShouldAdopt(Pawn pawn)
        {
            if (!WraithCaptureUtility.IsValidCaptiveIdentity(pawn))
                return false;

            bool playerPawn = pawn.Faction == Faction.OfPlayer;
            bool colonyPrisoner = pawn.guest?.IsPrisoner == true;
            return playerPawn || colonyPrisoner;
        }
    }
}
