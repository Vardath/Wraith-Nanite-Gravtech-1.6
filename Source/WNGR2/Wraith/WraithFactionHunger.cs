using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Save-persistent strategic hunger for the four Wraith lineages.
    ///
    /// This is deliberately separate from pawn-level Drain Life.  Ordinary Wraith feeding never
    /// opens diplomacy UI.  A request can only appear when a mutable Wraith faction has accumulated
    /// genuine strategic hunger and the colony has an eligible biological prisoner.  Refusing an
    /// actual request makes that faction attack; hostile/high-hunger lineages also gain independent
    /// raid pressure as hunger rises.
    /// </summary>
    public sealed class WraithFactionHunger : GameComponent
    {
        private const int HungerTickInterval = 2500;
        private const int PressureCheckInterval = 15000;
        private const int RequestRetryTicks = 60000;
        private const int PostRequestCooldownTicks = 180000;
        private const int ForcedRaidRetryTicks = 2500;
        private const float HungerPerDay = 0.08f;
        private const float RequestThreshold = 0.65f;
        private const float RaidThreshold = 0.80f;
        private const float FeedingRelief = 0.55f;

        private float sableHunger = 0.20f;
        private float cinderHunger = 0.18f;
        private float veiledHunger = 0.16f;
        private float paleHunger = 0.12f;

        private int lastHungerTick = -1;
        private int nextCinderRequestTick = -1;
        private int nextVeiledRequestTick = -1;
        private int nextPaleRequestTick = -1;
        private int forcedCinderRaidTick = -1;
        private int forcedVeiledRaidTick = -1;
        private int forcedPaleRaidTick = -1;
        private bool requestWindowOpen;

        public WraithFactionHunger(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Faction.OfPlayer == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (lastHungerTick < 0)
                lastHungerTick = now;

            if (now - lastHungerTick >= HungerTickInterval)
            {
                int elapsed = Math.Max(0, now - lastHungerTick);
                float gain = HungerPerDay * (elapsed / 60000f);
                sableHunger = Clamp01(sableHunger + gain);
                cinderHunger = Clamp01(cinderHunger + gain);
                veiledHunger = Clamp01(veiledHunger + gain);
                paleHunger = Clamp01(paleHunger + gain);
                lastHungerTick = now;
            }

            TryForcedRaids(now);

            if (now % PressureCheckInterval != 0)
                return;

            Map home = Find.Maps
                .Where(m => m != null && m.IsPlayerHome)
                .OrderByDescending(m => m.PlayerWealthForStoryteller)
                .FirstOrDefault();
            if (home == null)
                return;

            TryHungerRaidPressure(home, now);
            TryOpenFeedingRequest(home, now);
        }

        private void TryOpenFeedingRequest(Map home, int now)
        {
            if (requestWindowOpen || Find.WindowStack == null)
                return;

            List<Pawn> candidates = EligibleFeedingSubjects(home).ToList();
            if (candidates.Count == 0)
                return;

            foreach (string defName in MutableLineagesOrderedByHunger())
            {
                Faction faction = ResolveFaction(defName);
                if (faction == null || faction.HostileTo(Faction.OfPlayer))
                    continue;

                float hunger = GetHunger(defName);
                if (hunger < RequestThreshold || now < GetNextRequestTick(defName))
                    continue;

                float chance = 0.10f + 0.35f * InverseLerp(RequestThreshold, 1f, hunger);
                if (!Rand.Chance(chance))
                {
                    SetNextRequestTick(defName, SafeFutureTick(now, RequestRetryTicks));
                    continue;
                }

                OpenFeedingRequest(faction, home, hunger, candidates.Count, now);
                return;
            }
        }

        private void OpenFeedingRequest(Faction faction, Map home, float hunger, int candidateCount, int now)
        {
            string defName = faction.def.defName;
            int years = FeedingAgeYears(defName);
            string text = faction.Name + " is suffering a genuine feeding shortage. Their hunger has reached "
                + Math.Round(hunger * 100f) + "% and they are requesting controlled access to one biological prisoner.\n\n"
                + candidateCount + " eligible prisoner" + (candidateCount == 1 ? " is" : "s are") + " available. "
                + "A controlled feeding by this lineage will add " + years + " biological years and apply Life Drained.\n\n"
                + "If you refuse this feeding request, the Hive will attack. This request is strategic faction hunger; using Drain Life normally does not open this window.";

            SetNextRequestTick(defName, SafeFutureTick(now, PostRequestCooldownTicks));
            requestWindowOpen = true;
            try
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    text,
                    "Allow feeding",
                    () =>
                    {
                        requestWindowOpen = false;
                        OpenFeedingSubjectMenu(faction, home);
                    },
                    "Refuse",
                    () =>
                    {
                        requestWindowOpen = false;
                        RefuseRequest(faction);
                    },
                    faction.Name + " — feeding request",
                    buttonADestructive: false,
                    acceptAction: () =>
                    {
                        requestWindowOpen = false;
                        OpenFeedingSubjectMenu(faction, home);
                    },
                    cancelAction: () =>
                    {
                        requestWindowOpen = false;
                        RefuseRequest(faction);
                    }));
            }
            catch (Exception ex)
            {
                requestWindowOpen = false;
                SetNextRequestTick(defName, SafeFutureTick(now, RequestRetryTicks));
                Log.Error("[WNG] Failed to open Wraith feeding request: " + ex);
            }
        }

        private void OpenFeedingSubjectMenu(Faction faction, Map home)
        {
            List<Pawn> subjects = EligibleFeedingSubjects(home).OrderBy(p => p.LabelShort).ToList();
            if (subjects.Count == 0)
            {
                RefuseRequest(faction);
                return;
            }

            int years = FeedingAgeYears(faction.def.defName);
            List<FloatMenuOption> options = subjects
                .Select(pawn => new FloatMenuOption(
                    pawn.LabelShortCap + " — controlled feeding: +" + years + " biological years",
                    () => AcceptRequest(faction, pawn, years)))
                .ToList();
            options.Add(new FloatMenuOption("Refuse the feeding request", () => RefuseRequest(faction)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AcceptRequest(Faction faction, Pawn subject, int years)
        {
            if (faction == null || subject == null || subject.Dead || !subject.Spawned || !IsEligibleFeedingSubject(subject))
            {
                RefuseRequest(faction);
                return;
            }

            AdjustBiologicalAge(subject, years);
            RefreshLifeDrained(subject);
            string defName = faction.def.defName;
            SetHunger(defName, Math.Max(0.05f, GetHunger(defName) - FeedingRelief));

            Find.LetterStack.ReceiveLetter(
                "Wraith feeding agreement fulfilled",
                faction.Name + " completed the requested controlled feeding on " + subject.LabelShortCap + ". "
                + subject.LabelShortCap + " aged by " + years + " biological years and carries Life Drained. "
                + "The faction's strategic hunger has fallen; ordinary individual Drain Life remains unrelated to this request system.",
                LetterDefOf.NeutralEvent,
                subject);
        }

        private void RefuseRequest(Faction faction)
        {
            if (faction == null || faction.def == null)
                return;

            EnsureHostileToPlayer(faction);
            ScheduleForcedRaid(faction.def.defName, Find.TickManager?.TicksGame ?? 0);
            Messages.Message(
                faction.Name + " treats the refused feeding request as a challenge and is preparing an attack.",
                MessageTypeDefOf.ThreatBig,
                historical: true);
        }

        private void TryForcedRaids(int now)
        {
            TryForcedRaid("WNG_WraithCinderCourt", ref forcedCinderRaidTick, now);
            TryForcedRaid("WNG_WraithVeiledHive", ref forcedVeiledRaidTick, now);
            TryForcedRaid("WNG_WraithExiles", ref forcedPaleRaidTick, now);
        }

        private void TryForcedRaid(string defName, ref int dueTick, int now)
        {
            if (dueTick < 0 || now < dueTick)
                return;

            Map home = Find.Maps
                .Where(m => m != null && m.IsPlayerHome)
                .OrderByDescending(m => m.PlayerWealthForStoryteller)
                .FirstOrDefault();
            Faction faction = ResolveFaction(defName);
            if (home == null || faction == null)
            {
                dueTick = SafeFutureTick(now, ForcedRaidRetryTicks);
                return;
            }

            EnsureHostileToPlayer(faction);
            if (TryLaunchRaid(faction, home, Math.Max(0.90f, GetHunger(defName))))
            {
                dueTick = -1;
                SetHunger(defName, Math.Max(0.35f, GetHunger(defName) - 0.30f));
            }
            else
            {
                dueTick = SafeFutureTick(now, ForcedRaidRetryTicks);
            }
        }

        private void TryHungerRaidPressure(Map home, int now)
        {
            foreach (string defName in AllLineages())
            {
                Faction faction = ResolveFaction(defName);
                if (faction == null || !faction.HostileTo(Faction.OfPlayer))
                    continue;

                float hunger = GetHunger(defName);
                if (hunger < RaidThreshold)
                    continue;

                float doctrineFactor = defName == "WNG_WraithBrood" ? 1.35f : defName == "WNG_WraithCinderCourt" ? 1.15f : 0.75f;
                float chance = doctrineFactor * (0.025f + 0.10f * InverseLerp(RaidThreshold, 1f, hunger));
                if (!Rand.Chance(Math.Min(0.22f, chance)))
                    continue;

                if (TryLaunchRaid(faction, home, hunger))
                {
                    SetHunger(defName, Math.Max(0.40f, hunger - 0.22f));
                    return;
                }
            }
        }

        private static bool TryLaunchRaid(Faction faction, Map home, float hunger)
        {
            if (faction == null || home == null || !faction.HostileTo(Faction.OfPlayer))
                return false;

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, home);
            parms.faction = faction;
            parms.points *= 0.85f + 0.45f * Clamp01(hunger);
            return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        }

        private static IEnumerable<Pawn> EligibleFeedingSubjects(Map map)
        {
            if (map == null)
                return Enumerable.Empty<Pawn>();
            return map.mapPawns.AllPawnsSpawned.Where(IsEligibleFeedingSubject);
        }

        private static bool IsEligibleFeedingSubject(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.guest?.IsPrisoner != true)
                return false;
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh)
                return false;
            if (WraithLifeForceUtility.Get(pawn) != null)
                return false;

            string kind = pawn.kindDef?.defName ?? string.Empty;
            string race = pawn.def?.defName ?? string.Empty;
            string xenotype = pawn.genes?.Xenotype?.defName ?? string.Empty;
            return !LooksSynthetic(kind) && !LooksSynthetic(race) && !LooksSynthetic(xenotype);
        }

        private static bool LooksSynthetic(string value)
        {
            if (value.NullOrEmpty())
                return false;
            return value.IndexOf("Replicator", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Asuran", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("Nanite", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AdjustBiologicalAge(Pawn pawn, int years)
        {
            if (pawn?.ageTracker == null || years <= 0)
                return;
            const long ticksPerYear = 3600000L;
            long current = pawn.ageTracker.AgeBiologicalTicks;
            long delta = (long)years * ticksPerYear;
            pawn.ageTracker.AgeBiologicalTicks = current > long.MaxValue - delta ? long.MaxValue : current + delta;
        }

        private static void RefreshLifeDrained(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeDrained");
            if (def == null || pawn?.health?.hediffSet == null)
                return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
                pawn.health.RemoveHediff(existing);
            pawn.health.AddHediff(def);
        }

        private static void EnsureHostileToPlayer(Faction faction)
        {
            if (faction == null || Faction.OfPlayer == null || faction.HostileTo(Faction.OfPlayer))
                return;

            int current = faction.BaseGoodwillWith(Faction.OfPlayer);
            int target = -80;
            int delta = target - current;
            if (delta < 0 && faction.CanChangeGoodwillFor(Faction.OfPlayer, delta))
            {
                faction.TryAffectGoodwillWith(
                    Faction.OfPlayer,
                    delta,
                    canSendMessage: false,
                    canSendHostilityLetter: false);
            }
        }

        private void ScheduleForcedRaid(string defName, int now)
        {
            int due = SafeFutureTick(now, 1);
            switch (defName)
            {
                case "WNG_WraithCinderCourt": forcedCinderRaidTick = due; break;
                case "WNG_WraithVeiledHive": forcedVeiledRaidTick = due; break;
                case "WNG_WraithExiles": forcedPaleRaidTick = due; break;
            }
        }

        private static Faction ResolveFaction(string defName)
        {
            return Find.FactionManager?.AllFactions.FirstOrDefault(f => f != null && !f.defeated && f.def?.defName == defName);
        }

        private static IEnumerable<string> AllLineages()
        {
            yield return "WNG_WraithBrood";
            yield return "WNG_WraithCinderCourt";
            yield return "WNG_WraithVeiledHive";
            yield return "WNG_WraithExiles";
        }

        private IEnumerable<string> MutableLineagesOrderedByHunger()
        {
            return new[] { "WNG_WraithCinderCourt", "WNG_WraithVeiledHive", "WNG_WraithExiles" }
                .OrderByDescending(GetHunger);
        }

        private static int FeedingAgeYears(string defName)
        {
            switch (defName)
            {
                case "WNG_WraithCinderCourt": return 12;
                case "WNG_WraithVeiledHive": return 8;
                case "WNG_WraithExiles": return 5;
                default: return 8;
            }
        }

        private float GetHunger(string defName)
        {
            switch (defName)
            {
                case "WNG_WraithBrood": return sableHunger;
                case "WNG_WraithCinderCourt": return cinderHunger;
                case "WNG_WraithVeiledHive": return veiledHunger;
                case "WNG_WraithExiles": return paleHunger;
                default: return 0f;
            }
        }

        private void SetHunger(string defName, float value)
        {
            value = Clamp01(value);
            switch (defName)
            {
                case "WNG_WraithBrood": sableHunger = value; break;
                case "WNG_WraithCinderCourt": cinderHunger = value; break;
                case "WNG_WraithVeiledHive": veiledHunger = value; break;
                case "WNG_WraithExiles": paleHunger = value; break;
            }
        }

        private int GetNextRequestTick(string defName)
        {
            switch (defName)
            {
                case "WNG_WraithCinderCourt": return nextCinderRequestTick < 0 ? 0 : nextCinderRequestTick;
                case "WNG_WraithVeiledHive": return nextVeiledRequestTick < 0 ? 0 : nextVeiledRequestTick;
                case "WNG_WraithExiles": return nextPaleRequestTick < 0 ? 0 : nextPaleRequestTick;
                default: return int.MaxValue;
            }
        }

        private void SetNextRequestTick(string defName, int tick)
        {
            switch (defName)
            {
                case "WNG_WraithCinderCourt": nextCinderRequestTick = tick; break;
                case "WNG_WraithVeiledHive": nextVeiledRequestTick = tick; break;
                case "WNG_WraithExiles": nextPaleRequestTick = tick; break;
            }
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long next = (long)Math.Max(0, now) + Math.Max(0, delay);
            return next >= int.MaxValue ? int.MaxValue : (int)next;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static float InverseLerp(float a, float b, float value)
        {
            if (Math.Abs(b - a) < 0.0001f)
                return 0f;
            return Clamp01((value - a) / (b - a));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sableHunger, "wngSableFactionHunger", 0.20f);
            Scribe_Values.Look(ref cinderHunger, "wngCinderFactionHunger", 0.18f);
            Scribe_Values.Look(ref veiledHunger, "wngVeiledFactionHunger", 0.16f);
            Scribe_Values.Look(ref paleHunger, "wngPaleFactionHunger", 0.12f);
            Scribe_Values.Look(ref lastHungerTick, "wngFactionHungerLastTick", -1);
            Scribe_Values.Look(ref nextCinderRequestTick, "wngCinderFeedingRequestTick", -1);
            Scribe_Values.Look(ref nextVeiledRequestTick, "wngVeiledFeedingRequestTick", -1);
            Scribe_Values.Look(ref nextPaleRequestTick, "wngPaleFeedingRequestTick", -1);
            Scribe_Values.Look(ref forcedCinderRaidTick, "wngForcedCinderRaidTick", -1);
            Scribe_Values.Look(ref forcedVeiledRaidTick, "wngForcedVeiledRaidTick", -1);
            Scribe_Values.Look(ref forcedPaleRaidTick, "wngForcedPaleRaidTick", -1);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                sableHunger = Clamp01(sableHunger);
                cinderHunger = Clamp01(cinderHunger);
                veiledHunger = Clamp01(veiledHunger);
                paleHunger = Clamp01(paleHunger);
                requestWindowOpen = false;
            }
        }
    }
}
