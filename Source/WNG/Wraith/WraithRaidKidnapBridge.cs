using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Adopts completed vanilla kidnappings by Wraith factions into WNG's exact-pawn captivity
    /// lifecycle. Vanilla owns the physical down/carry/exit sequence; WNG takes over only after the
    /// exact pawn is present in the faction's kidnapped tracker. This prevents phantom captures.
    /// </summary>
    public sealed class GameComponent_WraithRaidKidnapBridge : GameComponent
    {
        private int nextScanTick;

        public GameComponent_WraithRaidKidnapBridge(Game game) { }

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
                if (!WraithCaptivityRegistry.IsWraithFaction(faction) || faction?.kidnapped == null)
                    continue;

                foreach (Pawn pawn in faction.kidnapped.KidnappedPawnsListForReading.ToList())
                {
                    if (!ShouldAdopt(pawn))
                        continue;

                    WraithCaptivityRecord record = registry.FindRecord(pawn)
                        ?? registry.RegisterCapturedPawn(pawn, faction, feedingStock: false);
                    if (record == null)
                        continue;

                    // Vanilla has already made this exact pawn a world pawn. WNG now owns the
                    // Wraith-specific rescue lifecycle, so remove it from vanilla ransom drift.
                    faction.kidnapped.RemoveKidnappedPawn(pawn);
                }
            }
        }

        private static bool ShouldAdopt(Pawn pawn)
        {
            if (!WraithCaptivityRegistry.IsValidBiologicalCaptive(pawn))
                return false;
            return pawn.Faction == Faction.OfPlayer || pawn.guest?.IsPrisoner == true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextScanTick, "wngWraithRaidKidnapNextScan", 0);
        }
    }
}
