using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Detects a genuinely neutralized hostile Mature Hive and hands that exact site/lineage to the
    /// separate retaliation queue. It does not inspect or alter strategic hunger.
    /// </summary>
    public sealed class GameComponent_WraithMatureHiveNeutralization : GameComponent
    {
        private int nextCheckTick;

        public GameComponent_WraithMatureHiveNeutralization(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextCheckTick)
                return;
            nextCheckTick = now + 600;

            SitePartDef matureHive = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_WraithMatureHive");
            if (matureHive == null || Find.WorldObjects?.AllWorldObjects == null)
                return;

            foreach (Site site in Find.WorldObjects.AllWorldObjects.OfType<Site>())
            {
                if (site == null || site.Destroyed || !site.HasMap || site.parts == null
                    || !site.parts.Any(p => p != null && p.def == matureHive))
                    continue;

                Faction faction = site.Faction;
                Map map = site.Map;
                if (faction == null || map == null || Faction.OfPlayer == null
                    || !faction.HostileTo(Faction.OfPlayer) || !WraithCaptivityRegistry.IsWraithFaction(faction))
                    continue;

                if (GenHostility.AnyHostileActiveThreatToPlayer(map, countDormantPawnsAsHostile: true))
                    continue;

                WraithMatureHiveRetaliationRegistry.Current?.Schedule(site.ID, faction);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextCheckTick, "wngMatureHiveNeutralizationNextCheck", 0);
        }
    }
}
