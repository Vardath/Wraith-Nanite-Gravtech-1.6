using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Initializes the mutable Wraith lineages' starting relations exactly once.
    /// After that, ordinary RimWorld goodwill is authoritative and can change naturally.
    /// Sable Brood is intentionally excluded because its FactionDef permanentEnemy rule
    /// already defines its uncompromising doctrine.
    /// </summary>
    public sealed class WraithFactionPolitics : GameComponent
    {
        private const int RetryIntervalTicks = 600;
        private const int CinderVeiledGoodwill = -35;
        private const int CinderPaleGoodwill = -65;
        private const int VeiledPaleGoodwill = 25;

        private bool initialized;

        public WraithFactionPolitics(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (initialized || Find.TickManager == null || Find.FactionManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now % RetryIntervalTicks != 0)
                return;

            Faction cinder = Resolve("WNG_WraithCinderCourt");
            Faction veiled = Resolve("WNG_WraithVeiledHive");
            Faction pale = Resolve("WNG_WraithExiles");
            if (cinder == null || veiled == null || pale == null)
                return;

            bool a = TrySetStartingGoodwill(cinder, veiled, CinderVeiledGoodwill);
            bool b = TrySetStartingGoodwill(cinder, pale, CinderPaleGoodwill);
            bool c = TrySetStartingGoodwill(veiled, pale, VeiledPaleGoodwill);
            initialized = a && b && c;
        }

        private static Faction Resolve(string defName)
        {
            return Find.FactionManager.AllFactions
                .FirstOrDefault(f => f != null && !f.defeated && f.def?.defName == defName);
        }

        private static bool TrySetStartingGoodwill(Faction a, Faction b, int target)
        {
            if (a == null || b == null || a == b)
                return false;

            int boundedTarget = Math.Max(-100, Math.Min(100, target));
            int delta = boundedTarget - a.BaseGoodwillWith(b);
            if (delta == 0)
                return true;
            if (!a.CanChangeGoodwillFor(b, delta))
                return false;

            return a.TryAffectGoodwillWith(
                b,
                delta,
                canSendMessage: false,
                canSendHostilityLetter: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref initialized, "wngWraithLineageRelationsInitialized", false);
        }
    }
}
