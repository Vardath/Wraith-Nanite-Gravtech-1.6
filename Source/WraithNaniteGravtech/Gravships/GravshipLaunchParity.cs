using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Restores Odyssey's hidden vanilla GravshipLaunch ritual when an existing/custom player
    /// ideology is missing it. Thing.GetGizmos obtains the normal Launch Gravship command from
    /// that ritual, and JobDriver_PilotConsole expects the same precept when a pawn pilots a
    /// console. No WNG launch command or parallel launch path is created.
    /// </summary>
    public sealed class GameComponent_WNGGravshipLaunchParity : GameComponent
    {
        private bool repairedThisSession;

        public GameComponent_WNGGravshipLaunchParity(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            EnsureVanillaLaunchRitual();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            EnsureVanillaLaunchRitual();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            EnsureVanillaLaunchRitual();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // One late retry covers unusual faction/ideology initialization order.
            if (!repairedThisSession)
                EnsureVanillaLaunchRitual();
        }

        private void EnsureVanillaLaunchRitual()
        {
            if (!ModsConfig.OdysseyActive || !ModsConfig.IdeologyActive)
            {
                repairedThisSession = true;
                return;
            }

            Faction player = Faction.OfPlayerSilentFail;
            if (player?.ideos == null)
                return;

            PreceptDef launchDef = PreceptDefOf.GravshipLaunch;
            if (launchDef?.ritualPatternBase == null)
                return;

            bool sawPlayerIdeology = false;
            foreach (Ideo ideo in player.ideos.AllIdeos)
            {
                if (ideo == null)
                    continue;

                sawPlayerIdeology = true;
                if (ideo.GetPrecept(launchDef) != null)
                    continue;

                Precept precept = PreceptMaker.MakePrecept(launchDef);
                ideo.AddPrecept(precept, init: true, fillWith: launchDef.ritualPatternBase);
                Log.Message(
                    "[WNG] Restored the missing vanilla Odyssey GravshipLaunch ritual for player ideology '" +
                    ideo.name + "'.");
            }

            repairedThisSession = sawPlayerIdeology;
        }
    }
}
