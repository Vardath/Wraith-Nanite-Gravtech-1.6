using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    [Flags]
    public enum ReplicatorAdaptationFlags
    {
        None = 0,
        Material = 1 << 0,
        Armor = 1 << 1,
        Ranged = 1 << 2,
        Power = 1 << 3,
        Shield = 1 << 4,
        Grav = 1 << 5,
        AntiShield = 1 << 6
    }

    public enum ReplicatorControlKind
    {
        Autonomous,
        Queen,
        SovereignLattice,
        TemporaryAsuran,
        CapturedQueen
    }

    public sealed class CompProperties_ReplicatorState : CompProperties
    {
        public CompProperties_ReplicatorState() => compClass = typeof(CompReplicatorState);
    }

    public sealed class CompReplicatorState : ThingComp
    {
        private int storedMatter;
        private int adaptationFlags;
        private ReplicatorControlKind controlKind;
        private string controlDomainId;
        private int empSuppressedUntilTick;

        public int StoredMatter => storedMatter;
        public ReplicatorAdaptationFlags Adaptations => (ReplicatorAdaptationFlags)adaptationFlags;
        public ReplicatorControlKind ControlKind => controlKind;
        public string ControlDomainId => controlDomainId ?? string.Empty;
        public bool EMPSuppressed => (Find.TickManager?.TicksGame ?? 0) < empSuppressedUntilTick;

        public void AddMatter(int amount) => storedMatter = Math.Max(0, storedMatter + amount);

        public void SetController(ReplicatorControlKind kind, string domainId)
        {
            controlKind = kind;
            controlDomainId = domainId ?? string.Empty;
        }

        public void Learn(ReplicatorAdaptationFlags flags) => adaptationFlags |= (int)flags;

        public void SuppressByEMP(int ticks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            empSuppressedUntilTick = Math.Max(empSuppressedUntilTick, now + Math.Max(0, ticks));
        }

        public bool SameControlDomain(CompReplicatorState other)
        {
            if (other == null || controlKind != other.controlKind)
                return false;
            return string.Equals(ControlDomainId, other.ControlDomainId, StringComparison.Ordinal);
        }

        public void InheritFrom(CompReplicatorState source, int matterOverride = -1)
        {
            if (source == null)
                return;
            storedMatter = matterOverride >= 0 ? matterOverride : source.storedMatter;
            adaptationFlags = source.adaptationFlags;
            controlKind = source.controlKind;
            controlDomainId = source.controlDomainId;
            empSuppressedUntilTick = source.empSuppressedUntilTick;
        }

        public void InheritMerged(IEnumerable<CompReplicatorState> sources)
        {
            List<CompReplicatorState> states = sources?.Where(s => s != null).ToList() ?? new List<CompReplicatorState>();
            if (states.Count == 0)
                return;
            storedMatter = states.Sum(s => Math.Max(0, s.storedMatter));
            adaptationFlags = states.Aggregate(0, (value, state) => value | state.adaptationFlags);
            controlKind = states[0].controlKind;
            controlDomainId = states[0].controlDomainId;
            empSuppressedUntilTick = states.Max(s => s.empSuppressedUntilTick);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref storedMatter, "storedMatter", 0);
            Scribe_Values.Look(ref adaptationFlags, "adaptationFlags", 0);
            Scribe_Values.Look(ref controlKind, "controlKind", ReplicatorControlKind.Autonomous);
            Scribe_Values.Look(ref controlDomainId, "controlDomainId", string.Empty);
            Scribe_Values.Look(ref empSuppressedUntilTick, "empSuppressedUntilTick", 0);
        }
    }

    internal static class ReplicatorHierarchyTransaction
    {
        private static readonly HashSet<int> intentionalConsumption = new HashSet<int>();
        public static void Begin(Pawn pawn) { if (pawn != null) intentionalConsumption.Add(pawn.thingIDNumber); }
        public static void End(Pawn pawn) { if (pawn != null) intentionalConsumption.Remove(pawn.thingIDNumber); }
        public static bool IsIntentional(Pawn pawn) => pawn != null && intentionalConsumption.Contains(pawn.thingIDNumber);
    }

    public sealed class CompProperties_ReplicatorHierarchy : CompProperties
    {
        public string upgradePawnKind;
        public int unitsRequired = 2;
        public float assemblyRadius = 7f;
        public int assemblyCheckTicks = 2500;
        public string splitChildPawnKind;
        public int splitCount = 0;
        public int splitRecombineDelayTicks = 2500;

        public CompProperties_ReplicatorHierarchy() => compClass = typeof(CompReplicatorHierarchy);
    }

    public sealed class CompReplicatorHierarchy : ThingComp
    {
        private int nextAssemblyTick;
        private int recombinationBlockedUntilTick;
        private bool deathSplitEmitted;
        private IntVec3 lastKnownPosition = IntVec3.Invalid;
        private static bool assemblyInProgress;

        private CompProperties_ReplicatorHierarchy Props => (CompProperties_ReplicatorHierarchy)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;
            if (!respawningAfterLoad && !string.IsNullOrEmpty(Props.upgradePawnKind))
                nextAssemblyTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(250, Props.assemblyCheckTicks);
        }

        public void BlockRecombinationForTicks(int ticks)
        {
            int until = (Find.TickManager?.TicksGame ?? 0) + Math.Max(0, ticks);
            recombinationBlockedUntilTick = Math.Max(recombinationBlockedUntilTick, until);
            nextAssemblyTick = Math.Max(nextAssemblyTick, until);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true)
                lastKnownPosition = pawn.Position;
            if (pawn == null || pawn.Dead || !pawn.Spawned || assemblyInProgress || string.IsNullOrEmpty(Props.upgradePawnKind) || Props.unitsRequired < 2)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextAssemblyTick || now < recombinationBlockedUntilTick)
                return;
            nextAssemblyTick = now + Math.Max(250, Props.assemblyCheckTicks);

            CompReplicatorState state = pawn.TryGetComp<CompReplicatorState>();
            if (state?.EMPSuppressed == true)
                return;

            float radius = Math.Max(1f, Props.assemblyRadius);
            if (state != null && (state.Adaptations & ReplicatorAdaptationFlags.Grav) != 0)
                radius *= 1.35f;
            float radiusSq = radius * radius;

            List<Pawn> candidates = pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned && p.def == pawn.def && p.Faction == pawn.Faction &&
                            p.Position.DistanceToSquared(pawn.Position) <= radiusSq &&
                            (p.TryGetComp<CompReplicatorHierarchy>()?.CanRecombineNow(now) ?? false) &&
                            (p.TryGetComp<CompReplicatorState>()?.EMPSuppressed != true) &&
                            state != null && state.SameControlDomain(p.TryGetComp<CompReplicatorState>()))
                .OrderBy(p => p.thingIDNumber)
                .ToList();

            if (candidates.Count < Props.unitsRequired || candidates[0] != pawn)
                return;

            PawnKindDef upgradeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.upgradePawnKind);
            if (upgradeKind?.race == null)
                return;

            List<Pawn> consumed = candidates.Take(Props.unitsRequired).ToList();
            Pawn upgraded = null;
            bool commitStarted = false;
            try
            {
                assemblyInProgress = true;
                upgraded = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind: upgradeKind,
                    faction: pawn.Faction,
                    context: PawnGenerationContext.NonPlayer,
                    tile: pawn.Map.Tile));
                upgraded.TryGetComp<CompReplicatorState>()?.InheritMerged(consumed.Select(p => p.TryGetComp<CompReplicatorState>()));
                GenSpawn.Spawn(upgraded, pawn.Position, pawn.Map);
                commitStarted = true;

                foreach (Pawn source in consumed)
                {
                    ReplicatorHierarchyTransaction.Begin(source);
                    try { source.Destroy(DestroyMode.Vanish); }
                    finally { ReplicatorHierarchyTransaction.End(source); }
                }
            }
            catch (Exception ex)
            {
                if (!commitStarted && upgraded != null && !upgraded.Destroyed)
                    upgraded.Destroy(DestroyMode.Vanish);
                Log.Error($"[WNG] Replicator hierarchy recombination failed for {pawn.def?.defName}: {ex}");
            }
            finally
            {
                assemblyInProgress = false;
            }
        }

        private bool CanRecombineNow(int now) => now >= recombinationBlockedUntilTick;

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeath = mode == DestroyMode.KillFinalize || (mode == DestroyMode.Vanish && pawn?.Dead == true);
            if (genuineDeath && !ReplicatorHierarchyTransaction.IsIntentional(pawn))
                TryEmitDeathSplit(previousMap, lastKnownPosition);
            base.PostDestroy(mode, previousMap);
        }

        private void TryEmitDeathSplit(Map map, IntVec3 origin)
        {
            if (deathSplitEmitted || map == null || Props.splitCount <= 0 || string.IsNullOrEmpty(Props.splitChildPawnKind))
                return;

            Pawn pawn = parent as Pawn;
            PawnKindDef childKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.splitChildPawnKind);
            if (pawn == null || childKind?.race == null || childKind.race == pawn.def)
                return;

            if (!origin.IsValid || !origin.InBounds(map))
                origin = lastKnownPosition;
            if (!origin.IsValid || !origin.InBounds(map))
                return;

            CompReplicatorState parentState = pawn.TryGetComp<CompReplicatorState>();
            int totalMatter = parentState?.StoredMatter ?? 0;
            int baseShare = Props.splitCount > 0 ? totalMatter / Props.splitCount : 0;
            int remainder = Props.splitCount > 0 ? totalMatter % Props.splitCount : 0;
            int spawned = 0;

            for (int i = 0; i < Props.splitCount; i++)
            {
                Pawn child = null;
                try
                {
                    child = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                        kind: childKind,
                        faction: pawn.Faction,
                        context: PawnGenerationContext.NonPlayer,
                        tile: map.Tile));
                    int share = baseShare + (i < remainder ? 1 : 0);
                    child.TryGetComp<CompReplicatorState>()?.InheritFrom(parentState, share);
                    child.TryGetComp<CompReplicatorHierarchy>()?.BlockRecombinationForTicks(Props.splitRecombineDelayTicks);
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(origin, map, 2);
                    GenSpawn.Spawn(child, cell, map);
                    spawned++;
                }
                catch (Exception ex)
                {
                    if (child != null && !child.Destroyed && !child.Spawned)
                        child.Destroy(DestroyMode.Vanish);
                    Log.Error($"[WNG] Replicator death split failed for {pawn.def?.defName}: {ex}");
                }
            }

            if (spawned == Props.splitCount)
                deathSplitEmitted = true;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAssemblyTick, "nextAssemblyTick", 0);
            Scribe_Values.Look(ref recombinationBlockedUntilTick, "recombinationBlockedUntilTick", 0);
            Scribe_Values.Look(ref deathSplitEmitted, "deathSplitEmitted", false);
        }
    }
}
