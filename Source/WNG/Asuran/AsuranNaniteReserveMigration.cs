using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Compatibility-only implementation for the pre-2026-09-11 WNG Nanite Reserve gene.
    /// The Def is retained so old saves can deserialize their exact saved resource value. It is not
    /// generated or granted by current gameplay, exposes no resource gizmo, and is removed from each
    /// pawn immediately after a successful migration to the Food-backed Nanite Reserve.
    /// </summary>
    public sealed class Gene_Resource_NaniteReserve : Gene_Resource
    {
        public override float InitialResourceMax => 1f;
        public override float MinLevelForAlert => 0.10f;
        protected override Color BarColor => new Color(0.20f, 0.66f, 0.78f);
        protected override Color BarHighlightColor => new Color(0.43f, 0.88f, 0.98f);

        public override IEnumerable<Gizmo> GetGizmos()
        {
            yield break;
        }
    }

    /// <summary>
    /// One-shot save migration from WNG's former Gene_Resource Nanite Reserve to the current
    /// Need_Food-derived reserve. The obsolete WNG_NaniteReserve gene itself is the migration key:
    /// ordinary human pawns, generic food needs, and lookalike xenotypes are never candidates.
    ///
    /// Successful migration preserves the exact legacy resource percentage, adds only the current
    /// replacement physiology gene when needed, reconciles needs, copies the percentage into
    /// WNG_NaniteMatterReserve, and only then deletes the legacy gene. A failed/incomplete pawn keeps
    /// its legacy gene and the game-level migration remains pending so no data is silently discarded.
    /// </summary>
    public sealed class GameComponent_AsuranNaniteReserveMigration : GameComponent
    {
        private const int CurrentMigrationVersion = 1;
        private const string LegacyGeneDefName = "WNG_NaniteReserve";
        private const string PhysiologyGeneDefName = "WNG_AsuranNanitePhysiology";
        private const string ReserveNeedDefName = "WNG_NaniteMatterReserve";

        private int completedMigrationVersion;
        private int nextAttemptTick;

        public GameComponent_AsuranNaniteReserveMigration(Game game)
        {
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            completedMigrationVersion = CurrentMigrationVersion;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            if (completedMigrationVersion < CurrentMigrationVersion)
                nextAttemptTick = (Find.TickManager?.TicksGame ?? 0) + 1;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (completedMigrationVersion >= CurrentMigrationVersion)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextAttemptTick)
                return;
            nextAttemptTick = SafeFutureTick(now, 60);

            if (!TryMigrateAll(out int migratedCount))
                return;

            completedMigrationVersion = CurrentMigrationVersion;
            if (migratedCount > 0)
                Log.Message($"[WNG] Migrated legacy Nanite Reserve state for {migratedCount} pawn(s) without changing non-WNG pawns.");
        }

        private static bool TryMigrateAll(out int migratedCount)
        {
            migratedCount = 0;

            GeneDef legacyDef = DefDatabase<GeneDef>.GetNamedSilentFail(LegacyGeneDefName);
            GeneDef physiologyDef = DefDatabase<GeneDef>.GetNamedSilentFail(PhysiologyGeneDefName);
            NeedDef reserveDef = DefDatabase<NeedDef>.GetNamedSilentFail(ReserveNeedDefName);
            if (legacyDef == null || physiologyDef == null || reserveDef == null)
                return false;

            bool unresolvedLegacyPawn = false;
            List<Pawn> pawns = PawnsFinder.All_AliveOrDead
                .Where(p => p != null)
                .Distinct()
                .ToList();

            foreach (Pawn pawn in pawns)
            {
                if (pawn.genes == null)
                    continue;

                Gene_Resource_NaniteReserve legacy = pawn.genes.GetGene(legacyDef) as Gene_Resource_NaniteReserve;
                if (legacy == null)
                    continue;

                if (TryMigratePawn(pawn, legacy, physiologyDef, reserveDef))
                    migratedCount++;
                else
                    unresolvedLegacyPawn = true;
            }

            return !unresolvedLegacyPawn;
        }

        private static bool TryMigratePawn(Pawn pawn, Gene_Resource_NaniteReserve legacy, GeneDef physiologyDef, NeedDef reserveDef)
        {
            if (pawn?.genes == null || legacy == null || pawn.needs == null)
                return false;

            float legacyFraction;
            if (legacy.Max > 0.0001f)
                legacyFraction = Mathf.Clamp01(legacy.Value / legacy.Max);
            else
                legacyFraction = Mathf.Clamp01(legacy.Value);

            Gene physiology = pawn.genes.GetGene(physiologyDef);
            if (physiology == null)
                physiology = pawn.genes.AddGene(physiologyDef, xenogene: true);
            if (physiology == null)
                return false;

            pawn.needs.AddOrRemoveNeedsAsAppropriate();
            Need_Food reserve = pawn.needs.TryGetNeed(reserveDef) as Need_Food;
            if (reserve == null)
                return false;

            reserve.CurLevelPercentage = legacyFraction;

            // The compatibility marker is removed only after the replacement reserve exists and has
            // received the saved percentage. This makes the pawn-level transaction at-most-once.
            pawn.genes.RemoveGene(legacy);
            return true;
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long value = (long)Math.Max(0, now) + Math.Max(1, delay);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref completedMigrationVersion, "wngNaniteReserveMigrationVersion", 0);
            Scribe_Values.Look(ref nextAttemptTick, "wngNaniteReserveMigrationNextAttempt", 0);
        }
    }
}
