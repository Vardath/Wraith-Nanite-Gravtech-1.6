using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class WraithFactionHungerExtension : DefModExtension
    {
        public float initialHunger = 0.15f;
        public float hungerPerDay = 0.08f;
        public float requestThreshold = 0.65f;
        public float raidThreshold = 0.80f;
        public float feedingRelief = 0.55f;
        public float requestChanceBase = 0.10f;
        public float requestChanceScale = 0.35f;
        public float raidChanceBase = 0.025f;
        public float raidChanceScale = 0.10f;
        public float raidDoctrineFactor = 1f;
        public int feedingAgeYears = 8;
        public bool canRequestFeeding = true;
        public bool refusalForcesRaid = true;
        public int requestRetryTicks = 60000;
        public int postRequestCooldownTicks = 180000;
        public int forcedRaidRetryTicks = 2500;
    }

    public sealed class WraithFactionHungerRecord : IExposable
    {
        public string factionDefName;
        public float hunger;
        public int nextRequestTick = -1;
        public int forcedRaidTick = -1;

        public WraithFactionHungerRecord() { }
        public WraithFactionHungerRecord(string factionDefName, float hunger)
        {
            this.factionDefName = factionDefName;
            this.hunger = hunger;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Values.Look(ref hunger, "hunger", 0f);
            Scribe_Values.Look(ref nextRequestTick, "nextRequestTick", -1);
            Scribe_Values.Look(ref forcedRaidTick, "forcedRaidTick", -1);
        }
    }

    /// <summary>
    /// Strategic lineage hunger. This system is intentionally independent from pawn-level Drain Life.
    /// Ordinary Wraith feeding never calls this component and never opens its request UI.
    /// </summary>
    public sealed class WraithFactionHunger : GameComponent
    {
        private const int HungerTickInterval = 2500;
        private const int PressureCheckInterval = 15000;

        private List<WraithFactionHungerRecord> records = new List<WraithFactionHungerRecord>();
        private int lastHungerTick = -1;
        private bool requestWindowOpen;

        public WraithFactionHunger(Game game) { }

        public float GetStrategicHunger(Faction faction)
        {
            if (faction?.def == null) return 0f;
            WraithFactionHungerExtension ext = faction.def.GetModExtension<WraithFactionHungerExtension>();
            return ext == null ? 0f : GetRecord(faction.def, ext).hunger;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.FactionManager == null || Faction.OfPlayer == null) return;

            int now = Find.TickManager.TicksGame;
            if (lastHungerTick < 0) lastHungerTick = now;

            if (now - lastHungerTick >= HungerTickInterval)
            {
                int elapsed = Math.Max(0, now - lastHungerTick);
                foreach (Faction faction in WraithFactions())
                {
                    WraithFactionHungerExtension ext = faction.def.GetModExtension<WraithFactionHungerExtension>();
                    WraithFactionHungerRecord record = GetRecord(faction.def, ext);
                    record.hunger = Clamp01(record.hunger + Math.Max(0f, ext.hungerPerDay) * (elapsed / 60000f));
                }
                lastHungerTick = now;
            }

            TryForcedRaids(now);
            if (now % PressureCheckInterval != 0) return;

            Map home = Find.Maps.Where(m => m != null && m.IsPlayerHome).OrderByDescending(m => m.PlayerWealthForStoryteller).FirstOrDefault();
            if (home == null) return;
            TryHungerRaidPressure(home);
            TryOpenFeedingRequest(home, now);
        }

        private IEnumerable<Faction> WraithFactions()
        {
            return Find.FactionManager.AllFactionsListForReading.Where(f =>
                f != null && !f.defeated && f.def?.GetModExtension<WraithFactionHungerExtension>() != null);
        }

        private WraithFactionHungerRecord GetRecord(FactionDef def, WraithFactionHungerExtension ext)
        {
            WraithFactionHungerRecord record = records.FirstOrDefault(r => r?.factionDefName == def.defName);
            if (record != null) return record;
            record = new WraithFactionHungerRecord(def.defName, Clamp01(ext?.initialHunger ?? 0f));
            records.Add(record);
            return record;
        }

        private void TryOpenFeedingRequest(Map home, int now)
        {
            if (requestWindowOpen || Find.WindowStack == null) return;
            List<Pawn> subjects = EligibleFeedingSubjects(home).ToList();
            if (subjects.Count == 0) return;

            foreach (Faction faction in WraithFactions()
                .Where(f => !f.HostileTo(Faction.OfPlayer))
                .OrderByDescending(GetStrategicHunger))
            {
                WraithFactionHungerExtension ext = faction.def.GetModExtension<WraithFactionHungerExtension>();
                if (ext == null || !ext.canRequestFeeding) continue;
                WraithFactionHungerRecord record = GetRecord(faction.def, ext);
                if (record.hunger < ext.requestThreshold || now < Math.Max(0, record.nextRequestTick)) continue;

                float chance = Math.Max(0f, ext.requestChanceBase) + Math.Max(0f, ext.requestChanceScale) * InverseLerp(ext.requestThreshold, 1f, record.hunger);
                if (!Rand.Chance(Math.Min(1f, chance)))
                {
                    record.nextRequestTick = SafeFutureTick(now, ext.requestRetryTicks);
                    continue;
                }

                OpenFeedingRequest(faction, home, subjects.Count, record, ext, now);
                return;
            }
        }

        private void OpenFeedingRequest(Faction faction, Map home, int candidateCount, WraithFactionHungerRecord record, WraithFactionHungerExtension ext, int now)
        {
            int years = Math.Max(0, ext.feedingAgeYears);
            string text = faction.Name + " is suffering a genuine feeding shortage. Their strategic hunger has reached "
                + Math.Round(record.hunger * 100f) + "% and they are requesting controlled access to one biological prisoner.\n\n"
                + candidateCount + " eligible prisoner" + (candidateCount == 1 ? " is" : "s are") + " available. "
                + "The selected subject will gain " + years + " biological years and Life Drained.\n\n"
                + "Refusing or declining this genuine request increases the chance of an attack. Ordinary Wraith Drain Life does not create this request.";

            record.nextRequestTick = SafeFutureTick(now, ext.postRequestCooldownTicks);
            requestWindowOpen = true;
            try
            {
                Find.WindowStack.Add(new Dialog_MessageBox(
                    text,
                    "Allow feeding",
                    () => { requestWindowOpen = false; OpenFeedingSubjectMenu(faction, home, record, ext); },
                    "Refuse",
                    () => { requestWindowOpen = false; RefuseRequest(faction, record, ext); },
                    faction.Name + " — feeding request",
                    buttonADestructive: false,
                    acceptAction: () => { requestWindowOpen = false; OpenFeedingSubjectMenu(faction, home, record, ext); },
                    cancelAction: () => { requestWindowOpen = false; RefuseRequest(faction, record, ext); }));
            }
            catch (Exception ex)
            {
                requestWindowOpen = false;
                record.nextRequestTick = SafeFutureTick(now, ext.requestRetryTicks);
                Log.Error("[WNG] Failed to open strategic Wraith feeding request: " + ex);
            }
        }

        private void OpenFeedingSubjectMenu(Faction faction, Map home, WraithFactionHungerRecord record, WraithFactionHungerExtension ext)
        {
            List<Pawn> subjects = EligibleFeedingSubjects(home).OrderBy(p => p.LabelShort).ToList();
            if (subjects.Count == 0)
            {
                RefuseRequest(faction, record, ext);
                return;
            }

            int years = Math.Max(0, ext.feedingAgeYears);
            List<FloatMenuOption> options = subjects.Select(p => new FloatMenuOption(
                p.LabelShortCap + " — controlled feeding: +" + years + " biological years",
                () => AcceptRequest(faction, p, years, record, ext))).ToList();
            options.Add(new FloatMenuOption("Refuse the feeding request", () => RefuseRequest(faction, record, ext)));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void AcceptRequest(Faction faction, Pawn subject, int years, WraithFactionHungerRecord record, WraithFactionHungerExtension ext)
        {
            if (faction == null || subject == null || subject.Dead || !subject.Spawned || !IsEligibleFeedingSubject(subject))
            {
                RefuseRequest(faction, record, ext);
                return;
            }

            AdjustBiologicalAge(subject, years);
            RefreshLifeDrained(subject);
            record.hunger = Clamp01(record.hunger - Math.Max(0f, ext.feedingRelief));
            Find.LetterStack.ReceiveLetter(
                "Wraith feeding agreement fulfilled",
                faction.Name + " completed the requested controlled feeding on " + subject.LabelShortCap + ". The faction's strategic hunger has fallen.",
                LetterDefOf.NeutralEvent,
                subject);
        }

        private void RefuseRequest(Faction faction, WraithFactionHungerRecord record, WraithFactionHungerExtension ext)
        {
            if (faction == null || record == null || ext == null) return;
            if (ext.refusalForcesRaid)
            {
                EnsureHostileToPlayer(faction);
                record.forcedRaidTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, 1);
            }
            Messages.Message(faction.Name + " treats the refused feeding request as a challenge and attack pressure rises.", MessageTypeDefOf.ThreatBig, true);
        }

        private void TryForcedRaids(int now)
        {
            foreach (Faction faction in WraithFactions())
            {
                WraithFactionHungerExtension ext = faction.def.GetModExtension<WraithFactionHungerExtension>();
                WraithFactionHungerRecord record = GetRecord(faction.def, ext);
                if (record.forcedRaidTick < 0 || now < record.forcedRaidTick) continue;
                Map home = Find.Maps.Where(m => m != null && m.IsPlayerHome).OrderByDescending(m => m.PlayerWealthForStoryteller).FirstOrDefault();
                if (home == null)
                {
                    record.forcedRaidTick = SafeFutureTick(now, ext.forcedRaidRetryTicks);
                    continue;
                }
                EnsureHostileToPlayer(faction);
                if (TryLaunchRaid(faction, home, Math.Max(0.90f, record.hunger)))
                {
                    record.forcedRaidTick = -1;
                    record.hunger = Math.Max(0.35f, record.hunger - 0.30f);
                }
                else record.forcedRaidTick = SafeFutureTick(now, ext.forcedRaidRetryTicks);
            }
        }

        private void TryHungerRaidPressure(Map home)
        {
            foreach (Faction faction in WraithFactions())
            {
                if (!faction.HostileTo(Faction.OfPlayer)) continue;
                WraithFactionHungerExtension ext = faction.def.GetModExtension<WraithFactionHungerExtension>();
                WraithFactionHungerRecord record = GetRecord(faction.def, ext);
                if (record.hunger < ext.raidThreshold) continue;
                float chance = Math.Max(0f, ext.raidDoctrineFactor) * (Math.Max(0f, ext.raidChanceBase) + Math.Max(0f, ext.raidChanceScale) * InverseLerp(ext.raidThreshold, 1f, record.hunger));
                if (!Rand.Chance(Math.Min(0.30f, chance))) continue;
                if (TryLaunchRaid(faction, home, record.hunger))
                {
                    record.hunger = Math.Max(0.40f, record.hunger - 0.22f);
                    return;
                }
            }
        }

        private static bool TryLaunchRaid(Faction faction, Map home, float hunger)
        {
            if (faction == null || home == null || !faction.HostileTo(Faction.OfPlayer)) return false;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, home);
            parms.faction = faction;
            parms.points *= 0.85f + 0.45f * Clamp01(hunger);
            return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        }

        private static IEnumerable<Pawn> EligibleFeedingSubjects(Map map)
        {
            return map?.mapPawns?.AllPawnsSpawned?.Where(IsEligibleFeedingSubject) ?? Enumerable.Empty<Pawn>();
        }

        private static bool IsEligibleFeedingSubject(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.guest?.IsPrisoner != true || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh) return false;
            if (WraithLifeForceUtility.IsWraith(pawn)) return false;
            string identity = ((pawn.kindDef?.defName ?? "") + " " + (pawn.def?.defName ?? "") + " " + (pawn.genes?.Xenotype?.defName ?? "")).ToLowerInvariant();
            return !identity.Contains("replicator") && !identity.Contains("asuran") && !identity.Contains("nanite");
        }

        private static void AdjustBiologicalAge(Pawn pawn, int years)
        {
            if (pawn?.ageTracker == null || years <= 0) return;
            const long ticksPerYear = 3600000L;
            long delta = (long)years * ticksPerYear;
            pawn.ageTracker.AgeBiologicalTicks = pawn.ageTracker.AgeBiologicalTicks > long.MaxValue - delta ? long.MaxValue : pawn.ageTracker.AgeBiologicalTicks + delta;
        }

        private static void RefreshLifeDrained(Pawn pawn)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_LifeDrained");
            if (def == null || pawn?.health?.hediffSet == null) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null) pawn.health.RemoveHediff(existing);
            pawn.health.AddHediff(def);
        }

        private static void EnsureHostileToPlayer(Faction faction)
        {
            if (faction == null || Faction.OfPlayer == null || faction.HostileTo(Faction.OfPlayer)) return;
            int delta = -80 - faction.BaseGoodwillWith(Faction.OfPlayer);
            if (delta < 0 && faction.CanChangeGoodwillFor(Faction.OfPlayer, delta))
                faction.TryAffectGoodwillWith(Faction.OfPlayer, delta, false, false);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(0, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
        private static float InverseLerp(float a, float b, float value) => Math.Abs(b - a) < 0.0001f ? 0f : Clamp01((value - a) / (b - a));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "wngWraithFactionHungerRecords", LookMode.Deep);
            Scribe_Values.Look(ref lastHungerTick, "wngWraithFactionHungerLastTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records = records?.Where(r => r != null && !r.factionDefName.NullOrEmpty()).ToList() ?? new List<WraithFactionHungerRecord>();
                foreach (WraithFactionHungerRecord record in records) record.hunger = Clamp01(record.hunger);
                requestWindowOpen = false;
            }
        }
    }
}
