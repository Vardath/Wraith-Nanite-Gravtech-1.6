using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    internal static class ReplicatorHierarchyTransaction
    {
        private static readonly HashSet<int> consumedForUpgrade = new HashSet<int>();
        public static void BeginConsume(Pawn pawn) { if (pawn != null) consumedForUpgrade.Add(pawn.thingIDNumber); }
        public static void EndConsume(Pawn pawn) { if (pawn != null) consumedForUpgrade.Remove(pawn.thingIDNumber); }
        public static bool IsUpgradeConsumption(Pawn pawn) => pawn != null && consumedForUpgrade.Contains(pawn.thingIDNumber);
    }

    public sealed class CompProperties_ReplicatorHierarchy : CompProperties
    {
        public string upgradePawnKind;
        public int unitsRequired;
        public float assemblyRadius;
        public int assemblyCheckTicks;
        public string splitChildPawnKind;
        public int splitCount;
        public int splitRecombineDelayTicks;
        public CompProperties_ReplicatorHierarchy() => compClass = typeof(CompReplicatorHierarchy);
    }

    public sealed class CompReplicatorHierarchy : ThingComp
    {
        private int nextAssemblyTick;
        private int recombineBlockedUntil;
        private bool deathSplitHandled;
        private IntVec3 lastPosition = IntVec3.Invalid;
        private static bool assembling;
        private CompProperties_ReplicatorHierarchy Props => (CompProperties_ReplicatorHierarchy)props;

        public bool CanUpgradeNow => CanUpgrade;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true) lastPosition = pawn.Position;
            if (!respawningAfterLoad && CanUpgrade)
                nextAssemblyTick = (Find.TickManager?.TicksGame ?? 0) + Math.Max(1, Props.assemblyCheckTicks);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn?.Spawned == true) lastPosition = pawn.Position;
            if (pawn == null || pawn.Dead || !pawn.Spawned || !CanUpgrade || assembling || ReplicatorEMP.IsSuppressed(pawn)) return;

            // Player/sovereign Replicators never silently reorganize themselves. Their exact Queen
            // can invoke TrySovereignRecombine explicitly; autonomous recombination remains hostile-
            // swarm behavior.
            if (pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled) return;
            if (ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position)) return;

            int now = Find.TickManager.TicksGame;
            if (now < nextAssemblyTick || now < recombineBlockedUntil) return;
            nextAssemblyTick = now + Math.Max(1, Props.assemblyCheckTicks);

            List<Pawn> candidates = FindCandidates(pawn, now);
            if (candidates.Count < Props.unitsRequired || candidates[0] != pawn) return;
            PawnKindDef upgradeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.upgradePawnKind);
            if (upgradeKind == null) return;

            AssembleSources(pawn, candidates.Take(Props.unitsRequired).ToList(), upgradeKind, out _);
        }

        public bool TrySovereignRecombine(Pawn controller, out string rejection)
        {
            rejection = null;
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null)
            {
                rejection = "The Replicator is not physically available for recombination.";
                return false;
            }
            if (!CanUpgrade)
            {
                rejection = "This Replicator form has no higher hierarchy form.";
                return false;
            }

            CompReplicatorSovereignty sovereignty = pawn.TryGetComp<CompReplicatorSovereignty>();
            if (sovereignty?.Operational != true || !sovereignty.IsQueenControlledBy(controller))
            {
                rejection = "Only an operational block in this exact Queen's sovereign domain can be explicitly recombined.";
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < recombineBlockedUntil)
            {
                rejection = "This Replicator is still inside its post-breakup recombination lockout.";
                return false;
            }
            if (ReplicatorEMP.IsSuppressed(pawn) || ReplicatorContainmentUtility.IsContained(pawn.Map, pawn.Position))
            {
                rejection = "EMP or active containment is blocking recombination.";
                return false;
            }
            if (assembling)
            {
                rejection = "Another Replicator hierarchy transaction is already resolving.";
                return false;
            }

            List<Pawn> sameDomain = FindCandidates(pawn, now);
            if (sameDomain.Count < Props.unitsRequired)
            {
                rejection = $"Requires {Props.unitsRequired} same-form Replicators in the same sovereign domain within assembly range.";
                return false;
            }

            List<Pawn> sources = new List<Pawn> { pawn };
            sources.AddRange(sameDomain
                .Where(p => p != pawn)
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .Take(Math.Max(0, Props.unitsRequired - 1)));
            if (sources.Count < Props.unitsRequired)
            {
                rejection = "Not enough eligible same-domain Replicators are available.";
                return false;
            }

            PawnKindDef upgradeKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.upgradePawnKind);
            if (upgradeKind == null)
            {
                rejection = "The configured higher Replicator form is unavailable.";
                return false;
            }

            return AssembleSources(pawn, sources, upgradeKind, out rejection);
        }

        private List<Pawn> FindCandidates(Pawn pawn, int now)
        {
            if (pawn?.Map?.mapPawns?.AllPawnsSpawned == null)
                return new List<Pawn>();

            float radius = Math.Max(0.1f, Props.assemblyRadius);
            float radiusSq = radius * radius;
            return pawn.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && !p.Dead && p.Spawned && p.def == pawn.def && p.Faction == pawn.Faction
                    && p.Position.DistanceToSquared(pawn.Position) <= radiusSq
                    && !ReplicatorEMP.IsSuppressed(p)
                    && !ReplicatorContainmentUtility.IsContained(pawn.Map, p.Position)
                    && ReplicatorSovereigntyUtility.SameDomain(pawn, p)
                    && (p.TryGetComp<CompReplicatorHierarchy>()?.CanParticipate(now) ?? true))
                .OrderBy(p => p.thingIDNumber)
                .ToList();
        }

        private bool AssembleSources(Pawn anchor, List<Pawn> sources, PawnKindDef upgradeKind, out string rejection)
        {
            rejection = null;
            if (anchor == null || anchor.Map == null || sources.NullOrEmpty() || upgradeKind == null)
            {
                rejection = "The hierarchy transaction is missing its physical source state.";
                return false;
            }
            if (sources.Any(s => s == null || s.Dead || !s.Spawned || s.Map != anchor.Map || !ReplicatorSovereigntyUtility.SameDomain(anchor, s)))
            {
                rejection = "The source Replicators no longer share one valid physical control domain.";
                return false;
            }

            Pawn upgraded = null;
            try
            {
                assembling = true;
                upgraded = PawnGenerator.GeneratePawn(upgradeKind, anchor.Faction);
                CompReplicatorState upgradedState = upgraded.TryGetComp<CompReplicatorState>();
                CompReplicatorAssimilation upgradedMatter = upgraded.TryGetComp<CompReplicatorAssimilation>();
                CompReplicatorSovereignty upgradedAuthority = upgraded.TryGetComp<CompReplicatorSovereignty>();
                float carriedMatter = 0f;

                foreach (Pawn source in sources)
                {
                    upgradedState?.MergeFrom(source.TryGetComp<CompReplicatorState>());
                    carriedMatter += source.TryGetComp<CompReplicatorAssimilation>()?.StoredMatter ?? 0f;
                }
                upgradedMatter?.SetStoredMatter(carriedMatter);
                upgradedAuthority?.CopyAuthorityFrom(sources[0].TryGetComp<CompReplicatorSovereignty>());
                GenSpawn.Spawn(upgraded, anchor.Position, anchor.Map);

                foreach (Pawn source in sources)
                {
                    if (source == null || source.Destroyed) continue;
                    ReplicatorHierarchyTransaction.BeginConsume(source);
                    try { source.Destroy(DestroyMode.Vanish); }
                    finally { ReplicatorHierarchyTransaction.EndConsume(source); }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[WNG] Replicator recombination failed: {ex}");
                rejection = "The Replicator hierarchy transaction failed; source bodies were not intentionally discarded as a successful recombination.";
                if (upgraded != null && !upgraded.Destroyed)
                {
                    ReplicatorHierarchyTransaction.BeginConsume(upgraded);
                    try { upgraded.Destroy(DestroyMode.Vanish); }
                    finally { ReplicatorHierarchyTransaction.EndConsume(upgraded); }
                }
                return false;
            }
            finally { assembling = false; }
        }

        private bool CanUpgrade => Props.unitsRequired >= 2 && !string.IsNullOrEmpty(Props.upgradePawnKind);
        private bool CanParticipate(int now) => now >= recombineBlockedUntil;

        public void DelayRecombination(int ticks)
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            recombineBlockedUntil = Math.Max(recombineBlockedUntil, now + Math.Max(0, ticks));
            nextAssemblyTick = Math.Max(nextAssemblyTick, recombineBlockedUntil);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            Pawn pawn = parent as Pawn;
            bool genuineDeath = mode == DestroyMode.KillFinalize || pawn?.Dead == true;
            if (genuineDeath && !ReplicatorHierarchyTransaction.IsUpgradeConsumption(pawn))
                Split(previousMap, pawn);
            base.PostDestroy(mode, previousMap);
        }

        private void Split(Map map, Pawn source)
        {
            if (deathSplitHandled || map == null || source == null || Props.splitCount <= 0 || string.IsNullOrEmpty(Props.splitChildPawnKind)) return;
            PawnKindDef childKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.splitChildPawnKind);
            if (childKind == null) return;
            IntVec3 origin = lastPosition;
            if (!origin.IsValid || !origin.InBounds(map)) return;

            deathSplitHandled = true;
            float sourceMatter = source.TryGetComp<CompReplicatorAssimilation>()?.StoredMatter ?? 0f;
            float matterPerChild = Props.splitCount > 0 ? sourceMatter / Props.splitCount : 0f;
            CompReplicatorSovereignty sourceAuthority = source.TryGetComp<CompReplicatorSovereignty>();
            for (int i = 0; i < Props.splitCount; i++)
            {
                try
                {
                    Pawn child = PawnGenerator.GeneratePawn(childKind, source.Faction);
                    child.TryGetComp<CompReplicatorState>()?.CopyFrom(source.TryGetComp<CompReplicatorState>());
                    child.TryGetComp<CompReplicatorAssimilation>()?.SetStoredMatter(matterPerChild);
                    child.TryGetComp<CompReplicatorSovereignty>()?.CopyAuthorityFrom(sourceAuthority);
                    child.TryGetComp<CompReplicatorHierarchy>()?.DelayRecombination(Props.splitRecombineDelayTicks);
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(origin, map, 2);
                    GenSpawn.Spawn(child, cell, map);
                }
                catch (Exception ex)
                {
                    Log.Error($"[WNG] Replicator death split failed: {ex}");
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            Pawn pawn = parent as Pawn;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (pawn == null || !pawn.Spawned || recombineBlockedUntil <= now) return null;
            int remaining = recombineBlockedUntil - now;
            return $"Recombination lockout: {remaining / (float)GenDate.TicksPerHour:0.0} in-game hour(s)";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextAssemblyTick, "wngReplicatorNextAssembly", 0);
            Scribe_Values.Look(ref recombineBlockedUntil, "wngReplicatorRecombineBlockedUntil", 0);
            Scribe_Values.Look(ref deathSplitHandled, "wngReplicatorDeathSplitHandled", false);
        }
    }
}
