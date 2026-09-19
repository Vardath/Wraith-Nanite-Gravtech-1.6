using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Low-frequency continuity repair for abilities declared by active WNG genes.
    ///
    /// This deliberately does not infer caste/PawnKind abilities and never removes abilities.
    /// Its sole job is to restore a declared WNG gene ability if vanilla gene duplication,
    /// replacement or save/load continuity has left that exact ability missing.
    /// </summary>
    public sealed class GameComponent_WNGGeneAbilityReconciler : GameComponent
    {
        private const int ReconcileIntervalTicks = 300;
        private int nextReconcileTick;

        public GameComponent_WNGGeneAbilityReconciler(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Find.TickManager == null)
                return;

            int now = Find.TickManager.TicksGame;
            if (now < nextReconcileTick)
                return;

            nextReconcileTick =
                now > int.MaxValue - ReconcileIntervalTicks
                    ? int.MaxValue
                    : now + ReconcileIntervalTicks;

            foreach (Map map in Find.Maps)
            {
                if (map?.mapPawns == null)
                    continue;

                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    ReconcilePawn(pawn);
            }
        }

        private static void ReconcilePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.genes == null || pawn.abilities == null)
                return;

            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                GeneDef geneDef = gene?.def;
                if (geneDef == null ||
                    !gene.Active ||
                    geneDef.defName.NullOrEmpty() ||
                    !geneDef.defName.StartsWith("WNG_", StringComparison.Ordinal) ||
                    geneDef.abilities.NullOrEmpty())
                {
                    continue;
                }

                foreach (AbilityDef abilityDef in geneDef.abilities)
                {
                    if (abilityDef == null || pawn.abilities.GetAbility(abilityDef) != null)
                        continue;

                    try
                    {
                        pawn.abilities.GainAbility(abilityDef);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(
                            "[WNG] Could not restore gene-granted ability " +
                            abilityDef.defName +
                            " for " +
                            pawn.LabelShortCap +
                            ": " +
                            ex.Message);
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(
                ref nextReconcileTick,
                "wngGeneAbilityNextReconcileTick",
                0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                nextReconcileTick = Math.Max(0, nextReconcileTick);
        }
    }
}
