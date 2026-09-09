using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Replicators
{
    public sealed class CompProperties_ReplicatorToyGestation : CompProperties
    {
        public PawnKindDef outputPawnKind;

        public CompProperties_ReplicatorToyGestation()
        {
            compClass = typeof(CompReplicatorToyGestation);
        }
    }

    /// <summary>
    /// Controlled Child's Toy gestation. The timer is an absolute 90,000 game ticks and is
    /// serialized so saving, caravanning or reloading cannot restart or duplicate the birth.
    /// </summary>
    public sealed class CompReplicatorToyGestation : ThingComp
    {
        private int gestationStartedTick = -1;
        private bool gestationCompleted;
        private bool missingDefinitionReported;

        public CompProperties_ReplicatorToyGestation Props => (CompProperties_ReplicatorToyGestation)props;
        public bool GestationCompleted => gestationCompleted;

        private static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && gestationStartedTick < 0)
                gestationStartedTick = CurrentTick;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (gestationCompleted || parent == null || !parent.Spawned)
                return;

            if (gestationStartedTick < 0)
                gestationStartedTick = CurrentTick;

            CompReplicatorSuppression suppression = parent.TryGetComp<CompReplicatorSuppression>();
            if (suppression?.IsSuppressed == true)
                return;

            if (CurrentTick - gestationStartedTick < ReplicatorConstants.ToyGestationTicks)
                return;

            CompleteGestation();
        }

        private void CompleteGestation()
        {
            if (Props.outputPawnKind == null)
            {
                if (!missingDefinitionReported)
                {
                    missingDefinitionReported = true;
                    Log.Error("[WNG] Child's Toy gestation has no outputPawnKind; toy was not consumed.");
                }
                return;
            }

            Map map = parent.Map;
            if (map == null)
                return;

            Pawn pawn = null;
            try
            {
                pawn = PawnGenerator.GeneratePawn(Props.outputPawnKind, Faction.OfPlayer);
                GenSpawn.Spawn(pawn, parent.Position, map);
            }
            catch (Exception ex)
            {
                if (pawn != null && !pawn.Destroyed && !pawn.Spawned)
                {
                    try { pawn.Destroy(DestroyMode.Vanish); } catch { }
                }
                Log.Error($"[WNG] Child's Toy gestation failed: {ex}");
                return;
            }

            gestationCompleted = true;
            parent.Destroy(DestroyMode.Vanish);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref gestationStartedTick, "gestationStartedTick", -1);
            Scribe_Values.Look(ref gestationCompleted, "gestationCompleted", false);
            Scribe_Values.Look(ref missingDefinitionReported, "missingDefinitionReported", false);
        }
    }
}
