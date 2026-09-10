using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Save-persistent retaliation queue for neutralized Mature Hives. Each source site can enqueue
    /// at most one retaliation and the exact source lineage is retained by faction Def name. This
    /// system is intentionally independent of strategic faction hunger and ordinary feeding.
    /// </summary>
    public sealed class WraithMatureHiveRetaliationRegistry : GameComponent
    {
        private const int CheckIntervalTicks = 600;
        private const int RetryDelayTicks = 60000;
        private const int MinimumDelayTicks = 120000;
        private const int MaximumDelayTicks = 240000;

        private List<int> sourceSiteIds = new List<int>();
        private List<int> retaliationTicks = new List<int>();
        private List<string> sourceFactionDefNames = new List<string>();

        public WraithMatureHiveRetaliationRegistry(Game game) { }

        public static WraithMatureHiveRetaliationRegistry Current => Verse.Current.Game?.GetComponent<WraithMatureHiveRetaliationRegistry>();

        public bool Schedule(int siteId, Faction sourceFaction)
        {
            if (siteId < 0 || sourceFaction?.def == null || !WraithCaptivityRegistry.IsWraithFaction(sourceFaction))
                return false;

            Normalize();
            if (sourceSiteIds.Contains(siteId))
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            sourceSiteIds.Add(siteId);
            retaliationTicks.Add(SafeFutureTick(now, Rand.RangeInclusive(MinimumDelayTicks, MaximumDelayTicks)));
            sourceFactionDefNames.Add(sourceFaction.def.defName);
            return true;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.FactionManager == null || Faction.OfPlayer == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now % CheckIntervalTicks != 0)
                return;

            Normalize();
            for (int i = sourceSiteIds.Count - 1; i >= 0; i--)
            {
                if (now < retaliationTicks[i])
                    continue;

                Map target = Find.Maps.Where(m => m != null && m.IsPlayerHome)
                    .OrderByDescending(m => m.PlayerWealthForStoryteller)
                    .FirstOrDefault();
                Faction faction = ResolveFaction(sourceFactionDefNames[i]);
                IncidentDef incident = DefDatabase<IncidentDef>.GetNamedSilentFail("WNG_WraithMatureHiveRetaliation");

                if (target == null || faction == null || incident == null)
                {
                    retaliationTicks[i] = SafeFutureTick(now, RetryDelayTicks);
                    continue;
                }

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, target);
                parms.faction = faction;
                if (incident.Worker.TryExecute(parms))
                    RemoveAt(i);
                else
                    retaliationTicks[i] = SafeFutureTick(now, RetryDelayTicks);
            }
        }

        private static Faction ResolveFaction(string defName)
        {
            if (defName.NullOrEmpty() || Find.FactionManager == null)
                return null;
            return Find.FactionManager.AllFactions.FirstOrDefault(f =>
                f != null && !f.defeated && f.def?.defName == defName && WraithCaptivityRegistry.IsWraithFaction(f));
        }

        private void Normalize()
        {
            sourceSiteIds = sourceSiteIds ?? new List<int>();
            retaliationTicks = retaliationTicks ?? new List<int>();
            sourceFactionDefNames = sourceFactionDefNames ?? new List<string>();
            int now = Find.TickManager?.TicksGame ?? 0;

            while (retaliationTicks.Count < sourceSiteIds.Count)
                retaliationTicks.Add(SafeFutureTick(now, RetryDelayTicks));
            while (retaliationTicks.Count > sourceSiteIds.Count)
                retaliationTicks.RemoveAt(retaliationTicks.Count - 1);
            while (sourceFactionDefNames.Count < sourceSiteIds.Count)
                sourceFactionDefNames.Add(string.Empty);
            while (sourceFactionDefNames.Count > sourceSiteIds.Count)
                sourceFactionDefNames.RemoveAt(sourceFactionDefNames.Count - 1);

            for (int i = sourceSiteIds.Count - 1; i >= 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (sourceSiteIds[j] != sourceSiteIds[i])
                        continue;
                    retaliationTicks[j] = Math.Min(retaliationTicks[j], retaliationTicks[i]);
                    if (sourceFactionDefNames[j].NullOrEmpty())
                        sourceFactionDefNames[j] = sourceFactionDefNames[i];
                    RemoveAt(i);
                    break;
                }
            }
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= sourceSiteIds.Count)
                return;
            sourceSiteIds.RemoveAt(index);
            if (index < retaliationTicks.Count) retaliationTicks.RemoveAt(index);
            if (index < sourceFactionDefNames.Count) sourceFactionDefNames.RemoveAt(index);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref sourceSiteIds, "wngMatureHiveRetaliationSourceSites", LookMode.Value);
            Scribe_Collections.Look(ref retaliationTicks, "wngMatureHiveRetaliationTicks", LookMode.Value);
            Scribe_Collections.Look(ref sourceFactionDefNames, "wngMatureHiveRetaliationFactionDefs", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Normalize();
        }
    }

    public sealed class IncidentWorker_WraithMatureHiveRetaliation : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            Faction faction = parms?.faction;
            return base.CanFireNowSub(parms)
                && map != null
                && map.IsPlayerHome
                && faction != null
                && !faction.defeated
                && WraithCaptivityRegistry.IsWraithFaction(faction);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            Faction faction = parms?.faction;
            if (map == null || faction == null || !WraithCaptivityRegistry.IsWraithFaction(faction))
                return false;

            parms.faction = faction;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
                return false;

            Messages.Message(
                faction.Name + " is retaliating for the neutralization of one of its mature Hives.",
                MessageTypeDefOf.ThreatBig,
                historical: true);
            return true;
        }
    }
}
