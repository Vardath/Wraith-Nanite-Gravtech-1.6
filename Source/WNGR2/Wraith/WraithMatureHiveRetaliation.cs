using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Save-persistent retaliation queue for neutralized mature Wraith Hives.
    /// Each mature-Hive site may schedule at most one response. The exact source lineage is stored
    /// by faction defName so save/load does not silently change the responsible Hive when it still
    /// exists. This system is deliberately independent of strategic feeding hunger.
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

        public bool ScheduleHiveRetaliation(int siteId, string factionDefName)
        {
            if (siteId < 0 || factionDefName.NullOrEmpty())
                return false;

            Normalize();
            if (sourceSiteIds.Contains(siteId))
                return false;

            int now = Find.TickManager?.TicksGame ?? 0;
            sourceSiteIds.Add(siteId);
            retaliationTicks.Add(SafeFutureTick(now, Rand.RangeInclusive(MinimumDelayTicks, MaximumDelayTicks)));
            sourceFactionDefNames.Add(factionDefName);
            return true;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.FactionManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now % CheckIntervalTicks != 0)
                return;

            Normalize();
            for (int i = sourceSiteIds.Count - 1; i >= 0; i--)
            {
                if (now < retaliationTicks[i])
                    continue;

                Map target = Find.Maps.FirstOrDefault(map => map != null && map.IsPlayerHome);
                IncidentDef retaliationDef = DefDatabase<IncidentDef>.GetNamedSilentFail("WNG_WraithMatureHiveRetaliation");
                Faction faction = ResolveSourceFaction(sourceFactionDefNames[i]) ?? ResolveFallbackWraithFaction();

                if (target == null || retaliationDef == null || faction == null)
                {
                    retaliationTicks[i] = SafeFutureTick(now, RetryDelayTicks);
                    continue;
                }

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, target);
                parms.faction = faction;
                if (retaliationDef.Worker.TryExecute(parms))
                    RemoveAt(i);
                else
                    retaliationTicks[i] = SafeFutureTick(now, RetryDelayTicks);
            }
        }

        private static Faction ResolveSourceFaction(string defName)
        {
            if (defName.NullOrEmpty() || Find.FactionManager == null)
                return null;

            return Find.FactionManager.AllFactions
                .FirstOrDefault(faction => faction != null
                    && !faction.defeated
                    && faction.def?.defName == defName
                    && WraithCaptureUtility.IsWraithCaptor(faction));
        }

        private static Faction ResolveFallbackWraithFaction()
        {
            if (Find.FactionManager == null)
                return null;

            return Find.FactionManager.AllFactions
                .FirstOrDefault(faction => faction != null
                    && !faction.defeated
                    && WraithCaptureUtility.IsWraithCaptor(faction));
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

            for (int i = 0; i < retaliationTicks.Count; i++)
            {
                if (retaliationTicks[i] <= 0)
                    retaliationTicks[i] = SafeFutureTick(now, RetryDelayTicks);
            }

            for (int i = 0; i < sourceSiteIds.Count; i++)
            {
                for (int j = sourceSiteIds.Count - 1; j > i; j--)
                {
                    if (sourceSiteIds[j] != sourceSiteIds[i])
                        continue;

                    retaliationTicks[i] = Math.Min(retaliationTicks[i], retaliationTicks[j]);
                    if (sourceFactionDefNames[i].NullOrEmpty() && !sourceFactionDefNames[j].NullOrEmpty())
                        sourceFactionDefNames[i] = sourceFactionDefNames[j];
                    RemoveAt(j);
                }
            }
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= sourceSiteIds.Count)
                return;
            sourceSiteIds.RemoveAt(index);
            if (index < retaliationTicks.Count)
                retaliationTicks.RemoveAt(index);
            if (index < sourceFactionDefNames.Count)
                sourceFactionDefNames.RemoveAt(index);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(0, delay);
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

    /// <summary>
    /// Executes a mature-Hive retaliation as an immediate Wraith raid, then best-effort stages a
    /// Wraith strike craft. Craft Defs are optional here so retaliation remains functional while
    /// the fresh public gravcraft family is rebuilt. A committed raid is never rolled back because
    /// an optional craft visual could not be staged.
    /// </summary>
    public sealed class IncidentWorker_WraithMatureHiveRetaliation : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            Faction faction = ResolveWraithFaction(parms);
            return map != null && map.IsPlayerHome && faction != null && !faction.defeated && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms?.target as Map;
            Faction faction = ResolveWraithFaction(parms);
            if (map == null || faction == null)
                return false;

            parms.faction = faction;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
                return false;

            float points = parms.points > 0f ? parms.points : StorytellerUtility.DefaultThreatPointsNow(map);
            string craftDefName = points >= 1800f ? "WNG_WraithCruiser" : "WNG_WraithStrikeCraft";
            string incomingDefName = points >= 1800f ? "WNG_WraithCruiserIncoming" : "WNG_WraithStrikeCraftIncoming";
            TryStageOptionalCraft(map, faction, craftDefName, incomingDefName);

            Messages.Message(
                (faction.Name ?? "Wraith forces") + " is retaliating for the neutralization of its mature Hive.",
                MessageTypeDefOf.ThreatBig,
                historical: true);
            return true;
        }

        private static Faction ResolveWraithFaction(IncidentParms parms)
        {
            Faction requested = parms?.faction;
            if (requested != null && !requested.defeated && WraithCaptureUtility.IsWraithCaptor(requested))
                return requested;

            return Find.FactionManager?.AllFactions
                .FirstOrDefault(faction => faction != null
                    && !faction.defeated
                    && WraithCaptureUtility.IsWraithCaptor(faction));
        }

        public static bool TryStageOptionalCraft(Map map, Faction faction, string craftDefName, string incomingDefName)
        {
            if (map == null || faction == null || craftDefName.NullOrEmpty() || incomingDefName.NullOrEmpty())
                return false;

            ThingDef craftDef = DefDatabase<ThingDef>.GetNamedSilentFail(craftDefName);
            ThingDef incomingDef = DefDatabase<ThingDef>.GetNamedSilentFail(incomingDefName);
            if (craftDef == null || incomingDef == null)
                return false;

            int radius = Math.Max(12, Math.Min(map.Size.x, map.Size.z) / 2 - 12);
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(map.Center, radius, true)
                         .Where(candidate => candidate.InBounds(map))
                         .OrderByDescending(candidate => candidate.DistanceToSquared(map.Center)))
            {
                if (!GenSpawn.CanSpawnAt(craftDef, cell, map))
                    continue;

                try
                {
                    Thing craft = ThingMaker.MakeThing(craftDef);
                    if (craft == null)
                        return false;
                    craft.SetFaction(faction);
                    CompRefuelable fuel = craft.TryGetComp<CompRefuelable>();
                    if (fuel != null)
                        fuel.Refuel(fuel.Props.fuelCapacity);
                    SkyfallerMaker.SpawnSkyfaller(incomingDef, craft, cell, map);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Optional retaliatory Wraith craft staging failed: " + ex.Message);
                    return false;
                }
            }

            return false;
        }
    }
}
