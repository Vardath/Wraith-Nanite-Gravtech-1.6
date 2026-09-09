using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    public sealed class CompProperties_ReplicatorMatterDormancy : CompProperties
    {
        public PawnKindDef assemblyPawnKind;
        public FactionDef assemblyFaction;
        public int requiredStack = ReplicatorConstants.DangerousMatterMinimumStack;

        public CompProperties_ReplicatorMatterDormancy()
        {
            compClass = typeof(CompReplicatorMatterDormancy);
        }
    }

    /// <summary>
    /// Dangerous Replicator Matter is inert below the canonical threshold. A qualifying stack
    /// arms a 30,000-tick dormancy timer and may then assemble one configured Replicator body.
    /// The matter is only consumed after the pawn has been generated and spawned successfully.
    /// </summary>
    public sealed class CompReplicatorMatterDormancy : ThingComp
    {
        private int armedAtTick;
        private int assembliesCompleted;
        private bool missingDefinitionReported;

        public CompProperties_ReplicatorMatterDormancy Props => (CompProperties_ReplicatorMatterDormancy)props;

        public bool IsDangerous => parent != null && parent.stackCount >= RequiredStack;
        public int AssembliesCompleted => assembliesCompleted;

        private int RequiredStack => Math.Max(ReplicatorConstants.DangerousMatterMinimumStack, Props.requiredStack);
        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && IsDangerous && armedAtTick <= 0)
                armedAtTick = CurrentTick;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (parent == null || !parent.Spawned)
                return;

            CompReplicatorSuppression suppression = parent.TryGetComp<CompReplicatorSuppression>();
            if (suppression?.IsSuppressed == true)
                return;

            if (!IsDangerous)
            {
                armedAtTick = 0;
                return;
            }

            if (armedAtTick <= 0)
            {
                armedAtTick = CurrentTick;
                return;
            }

            if (CurrentTick - armedAtTick < ReplicatorConstants.DormancyTicks)
                return;

            TryAssemble();
        }

        private void TryAssemble()
        {
            if (Props.assemblyPawnKind == null || Props.assemblyFaction == null)
            {
                ReportMissingConfigurationOnce();
                return;
            }

            Map map = parent.Map;
            if (map == null)
                return;

            Faction faction = Find.FactionManager?.AllFactionsListForReading
                ?.FirstOrDefault(candidate => candidate.def == Props.assemblyFaction);
            if (faction == null)
            {
                ReportMissingConfigurationOnce();
                return;
            }

            if (faction != Faction.OfPlayer && WNGMod.Settings != null)
            {
                int existing = map.mapPawns.AllPawnsSpawned.Count(pawn =>
                    pawn.Faction == faction && pawn.TryGetComp<CompReplicatorState>() != null);
                if (existing >= WNGMod.Settings.maxHostileReplicators)
                    return;
            }

            Pawn spawnedPawn = null;
            try
            {
                spawnedPawn = PawnGenerator.GeneratePawn(Props.assemblyPawnKind, faction);
                GenSpawn.Spawn(spawnedPawn, parent.Position, map);
            }
            catch (Exception ex)
            {
                if (spawnedPawn != null && !spawnedPawn.Destroyed && !spawnedPawn.Spawned)
                {
                    try { spawnedPawn.Destroy(DestroyMode.Vanish); } catch { }
                }
                Log.Error($"[WNG] Replicator Matter self-assembly failed: {ex}");
                return;
            }

            assembliesCompleted++;
            int consumed = RequiredStack;
            if (parent.stackCount <= consumed)
            {
                parent.Destroy(DestroyMode.Vanish);
                return;
            }

            parent.stackCount -= consumed;
            armedAtTick = parent.stackCount >= RequiredStack ? CurrentTick : 0;
        }

        private void ReportMissingConfigurationOnce()
        {
            if (missingDefinitionReported)
                return;

            missingDefinitionReported = true;
            Log.Error("[WNG] Replicator Matter dormancy requires both assemblyPawnKind and assemblyFaction; matter was not consumed.");
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref armedAtTick, "armedAtTick", 0);
            Scribe_Values.Look(ref assembliesCompleted, "assembliesCompleted", 0);
            Scribe_Values.Look(ref missingDefinitionReported, "missingDefinitionReported", false);
        }
    }
}
