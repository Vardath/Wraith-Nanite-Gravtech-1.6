using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WraithMatureHiveRetaliationState : IExposable
    {
        public int siteId = -1;
        public Faction faction;
        public int dueTick = -1;
        public int retryCount;

        public void ExposeData()
        {
            Scribe_Values.Look(ref siteId, "siteId", -1);
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref dueTick, "dueTick", -1);
            Scribe_Values.Look(ref retryCount, "retryCount", 0);
        }
    }

    /// <summary>
    /// Exact-lineage delayed response to the physical destruction of a Mature Hive.
    /// This is intentionally independent of WraithStrategicHungerRegistry: destroying a Hive
    /// is a military/political consequence, not an ordinary feeding request or hunger-pressure event.
    /// </summary>
    public sealed class WraithMatureHiveRetaliationRegistry : GameComponent
    {
        private const int TickInterval = 250;
        private const int InitialDelayMinTicks = 120000;
        private const int InitialDelayMaxTicks = 240000;
        private const int RetryDelayTicks = 60000;
        private const int MaxRetries = 3;

        private List<WraithMatureHiveRetaliationState> pending = new List<WraithMatureHiveRetaliationState>();
        private List<int> processedSiteIds = new List<int>();

        public WraithMatureHiveRetaliationRegistry(Game game)
        {
        }

        public void ScheduleHiveRetaliation(int siteId, Faction faction)
        {
            if (siteId < 0 || faction == null || faction.defeated || Faction.OfPlayer == null)
                return;
            if (!WraithLineageUtility.IsWraithLineage(faction) || !faction.HostileTo(Faction.OfPlayer))
                return;
            if (processedSiteIds.Contains(siteId) || pending.Any(x => x != null && x.siteId == siteId))
                return;

            // The site is consumed into the response pipeline once. Repeated SitePart ticks,
            // map reloads or save/reload therefore cannot schedule duplicate retaliation.
            processedSiteIds.Add(siteId);
            int now = Find.TickManager?.TicksGame ?? 0;
            pending.Add(new WraithMatureHiveRetaliationState
            {
                siteId = siteId,
                faction = faction,
                dueTick = now + Rand.RangeInclusive(InitialDelayMinTicks, InitialDelayMaxTicks),
                retryCount = 0
            });
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || pending == null || pending.Count == 0)
                return;

            int now = Find.TickManager.TicksGame;
            if (now % TickInterval != 0)
                return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                WraithMatureHiveRetaliationState state = pending[i];
                if (state == null)
                {
                    pending.RemoveAt(i);
                    continue;
                }
                if (now < state.dueTick)
                    continue;

                Faction faction = state.faction;
                if (faction == null || faction.defeated || Faction.OfPlayer == null || !faction.HostileTo(Faction.OfPlayer))
                {
                    // Peace or faction defeat cancels the response. This component never forces hostility.
                    pending.RemoveAt(i);
                    continue;
                }

                Map home = BestPlayerHome();
                if (home == null)
                {
                    if (!ScheduleRetryOrExpire(state, now))
                        pending.RemoveAt(i);
                    continue;
                }

                IncidentParms parms = new IncidentParms
                {
                    forced = true,
                    target = home,
                    faction = faction,
                    points = Mathf.Max(35f, StorytellerUtility.DefaultThreatPointsNow(home)),
                    raidStrategy = RaidStrategyDefOf.ImmediateAttack
                };

                if (IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (!ScheduleRetryOrExpire(state, now))
                    pending.RemoveAt(i);
            }
        }

        private static bool ScheduleRetryOrExpire(WraithMatureHiveRetaliationState state, int now)
        {
            state.retryCount++;
            if (state.retryCount > MaxRetries)
                return false;
            state.dueTick = now + RetryDelayTicks;
            return true;
        }

        private static Map BestPlayerHome()
        {
            return Find.Maps
                .Where(m => m != null && m.IsPlayerHome)
                .OrderByDescending(m => m.PlayerWealthForStoryteller)
                .FirstOrDefault();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "wngMatureHiveRetaliationPending", LookMode.Deep);
            Scribe_Collections.Look(ref processedSiteIds, "wngMatureHiveRetaliationProcessedSites", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (pending == null)
                    pending = new List<WraithMatureHiveRetaliationState>();
                if (processedSiteIds == null)
                    processedSiteIds = new List<int>();

                pending.RemoveAll(x => x == null || x.siteId < 0);
                processedSiteIds = processedSiteIds.Distinct().ToList();
            }
        }
    }
}
