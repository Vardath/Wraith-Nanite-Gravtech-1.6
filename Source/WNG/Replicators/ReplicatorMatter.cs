using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorSalvage : CompProperties
    {
        public string matterDefName = "WNG_ReplicatorMatter";
        public int minMatter = 1;
        public int maxMatter = 3;
        public string fragmentDefName;
        public float fragmentChance;
        public CompProperties_ReplicatorSalvage() => compClass = typeof(CompReplicatorSalvage);
    }

    public sealed class CompReplicatorSalvage : ThingComp
    {
        private IntVec3 lastPosition = IntVec3.Invalid;
        private bool emitted;
        private CompProperties_ReplicatorSalvage Props => (CompProperties_ReplicatorSalvage)props;

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Spawned == true) lastPosition = parent.Position;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (parent?.Spawned == true) lastPosition = parent.Position;
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeath = mode == DestroyMode.KillFinalize || pawn?.Dead == true;
            if (!emitted && genuineDeath && !ReplicatorHierarchyTransaction.IsUpgradeConsumption(pawn))
            {
                emitted = true;
                Emit(previousMap);
            }
            base.PostDestroy(mode, previousMap);
        }

        private void Emit(Map map)
        {
            if (map == null || !lastPosition.IsValid || !lastPosition.InBounds(map)) return;
            ThingDef matterDef = DefDatabase<ThingDef>.GetNamedSilentFail(Props.matterDefName);
            if (matterDef != null)
            {
                Thing matter = ThingMaker.MakeThing(matterDef);
                matter.stackCount = Rand.RangeInclusive(Math.Max(1, Props.minMatter), Math.Max(Props.minMatter, Props.maxMatter));
                GenPlace.TryPlaceThing(matter, lastPosition, map, ThingPlaceMode.Near);
            }
            if (!string.IsNullOrEmpty(Props.fragmentDefName) && Props.fragmentChance > 0f && Rand.Chance(Props.fragmentChance))
            {
                ThingDef fragmentDef = DefDatabase<ThingDef>.GetNamedSilentFail(Props.fragmentDefName);
                if (fragmentDef != null) GenPlace.TryPlaceThing(ThingMaker.MakeThing(fragmentDef), lastPosition, map, ThingPlaceMode.Near);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref emitted, "wngReplicatorSalvageEmitted", false);
        }
    }

    public sealed class CompProperties_ReplicatorMatterReassembly : CompProperties
    {
        public string factionDefName = "WNG_ReplicatorSwarm";
        public string pawnKindDefName = "WNG_ReplicatorDrone";
        public int minimumStack = 10;
        public int consumePerPawn = 10;
        public int dormantTicks = 60000;
        public int retryTicks = 2500;
        public float chancePerCheck = 0.05f;
        public float chanceGrowthPerDay = 0.10f;
        public float maxChancePerCheck = 0.85f;
        public int maxPawnsPerWake = 2;
        public int maxHostileReplicatorsPerMap = 120;
        public CompProperties_ReplicatorMatterReassembly() => compClass = typeof(CompReplicatorMatterReassembly);
    }

    public sealed class CompReplicatorMatterReassembly : ThingComp
    {
        private int nextAttemptTick;
        private int exposureStartTick = -1;
        private CompProperties_ReplicatorMatterReassembly Props => (CompProperties_ReplicatorMatterReassembly)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && Find.TickManager != null)
                ResetDangerClock(Find.TickManager.TicksGame);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (parent == null || parent.Destroyed || !parent.Spawned || parent.stackCount < Math.Max(1, Props.minimumStack)) return;
            int now = Find.TickManager.TicksGame;

            // Powered containment freezes and resets the reformation danger clock. Once containment
            // fails or the blocks are removed, they must spend a full configured dormancy interval
            // exposed again before the first chance to reform.
            if (ReplicatorContainmentUtility.IsContained(parent.Map, parent.Position))
            {
                ResetDangerClock(now);
                return;
            }

            if (exposureStartTick < 0)
                ResetDangerClock(now);
            if (now < nextAttemptTick) return;

            int dormancy = Math.Max(250, Props.dormantTicks);
            int eligibleSince = exposureStartTick + dormancy;
            if (now < eligibleSince)
            {
                nextAttemptTick = eligibleSince;
                return;
            }

            nextAttemptTick = now + Math.Max(250, Props.retryTicks);
            float exposedEligibleDays = Math.Max(0f, now - eligibleSince) / (float)GenDate.TicksPerDay;
            float chance = Math.Max(0f, Props.chancePerCheck)
                + exposedEligibleDays * Math.Max(0f, Props.chanceGrowthPerDay);
            chance = Math.Min(Math.Max(0f, Props.maxChancePerCheck), chance);
            chance = Math.Min(1f, chance);
            if (!Rand.Chance(chance)) return;

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.pawnKindDefName);
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(Props.factionDefName);
            Faction faction = factionDef == null ? null : Find.FactionManager.FirstFactionOfDef(factionDef);
            if (kind == null || faction == null) return;

            int cap = Math.Max(1, Props.maxHostileReplicatorsPerMap);
            int existing = parent.Map.mapPawns.AllPawnsSpawned.Count(p => p != null && !p.Dead && p.Faction == faction && p.TryGetComp<CompReplicatorState>() != null);
            int room = Math.Max(0, cap - existing);
            if (room <= 0) return;

            int cost = Math.Max(1, Props.consumePerPawn);
            int count = Math.Min(Math.Min(Math.Max(1, Props.maxPawnsPerWake), parent.stackCount / cost), room);
            if (count <= 0) return;
            Map map = parent.Map;
            IntVec3 origin = parent.Position;
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(origin, map, 2);
                GenSpawn.Spawn(pawn, cell, map);
                parent.stackCount -= cost;
                if (parent.stackCount <= 0)
                {
                    parent.Destroy(DestroyMode.Vanish);
                    break;
                }
            }

            // Any blocks left after a successful reformation begin a fresh one-day dormancy cycle.
            if (parent != null && !parent.Destroyed && parent.stackCount >= Math.Max(1, Props.minimumStack))
                ResetDangerClock(now);
        }

        private void ResetDangerClock(int now)
        {
            exposureStartTick = now;
            nextAttemptTick = now + Math.Max(250, Props.dormantTicks);
        }

        public override string CompInspectStringExtra()
        {
            if (parent == null || parent.stackCount < Math.Max(1, Props.minimumStack)) return null;
            if (parent.Spawned && ReplicatorContainmentUtility.IsContained(parent.Map, parent.Position))
                return "Contained Replicator blocks: powered suppression has reset the reformation clock.";

            int now = Find.TickManager?.TicksGame ?? 0;
            int dormancy = Math.Max(250, Props.dormantTicks);
            int eligibleTick = exposureStartTick < 0 ? now + dormancy : exposureStartTick + dormancy;
            if (now < eligibleTick)
                return $"Dormant Replicator blocks: reformation risk begins in {(eligibleTick - now) / (float)GenDate.TicksPerDay:0.0} day(s).";

            float days = Math.Max(0f, now - eligibleTick) / (float)GenDate.TicksPerDay;
            float chance = Math.Min(1f, Math.Min(Math.Max(0f, Props.maxChancePerCheck), Math.Max(0f, Props.chancePerCheck) + days * Math.Max(0f, Props.chanceGrowthPerDay)));
            return $"Active Replicator blocks: reformation chance is rising ({chance:P0} per check).";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAttemptTick, "wngReplicatorMatterNextAttempt", 0);
            Scribe_Values.Look(ref exposureStartTick, "wngReplicatorMatterExposureStart", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && exposureStartTick < 0 && Find.TickManager != null)
                ResetDangerClock(Find.TickManager.TicksGame);
        }
    }
}
