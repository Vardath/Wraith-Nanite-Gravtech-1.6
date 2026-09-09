using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Low-frequency Wraith presentation/ability maintenance.  This is deliberately narrow: it
    /// enforces the established pale long-hair presentation and repairs missing gene-granted WNG
    /// abilities after gene duplication/overwrite.  It does not patch vanilla gene handling.
    /// </summary>
    public sealed class GameComponent_WraithMaintenance : GameComponent
    {
        private static readonly Color WraithHairColor = new Color(0.97f, 0.98f, 0.99f);
        private int nextTick;
        private HairDef preferredLongStraightHair;

        public GameComponent_WraithMaintenance(Game game)
        {
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextTick)
                return;
            nextTick = now + 300;

            foreach (Map map in Find.Maps)
            {
                if (map?.mapPawns == null)
                    continue;

                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || pawn.Dead)
                        continue;

                    if (IsWraith(pawn))
                        EnforceWraithAppearance(pawn);
                    ReconcileWngGeneAbilities(pawn);
                }
            }
        }

        private static bool IsWraith(Pawn pawn)
        {
            if (pawn?.genes == null)
                return false;
            if (pawn.genes.Xenotype?.defName == "WNG_Wraith")
                return true;

            return pawn.genes.GenesListForReading.Any(g =>
                g?.def?.defName == "WNG_LifeForce" && g.Active);
        }

        private void EnforceWraithAppearance(Pawn pawn)
        {
            if (pawn.story == null)
                return;

            if (preferredLongStraightHair == null)
            {
                preferredLongStraightHair = DefDatabase<HairDef>.AllDefsListForReading
                    .Where(h => h != null && !string.IsNullOrEmpty(h.texPath))
                    .Select(h => new
                    {
                        Hair = h,
                        Search = ((h.defName ?? string.Empty) + " " + (h.label ?? string.Empty) + " " + (h.texPath ?? string.Empty)).ToLowerInvariant()
                    })
                    .Where(x => x.Search.Contains("long") || x.Search.Contains("shoulder"))
                    .Where(x => !x.Search.Contains("braid")
                                && !x.Search.Contains("curl")
                                && !x.Search.Contains("dread")
                                && !x.Search.Contains("afro")
                                && !x.Search.Contains("mohawk")
                                && !x.Search.Contains("bun"))
                    .OrderByDescending(x => x.Search.Contains("straight") || x.Search.Contains("sleek"))
                    .Select(x => x.Hair)
                    .FirstOrDefault();
            }

            bool dirty = false;
            if (preferredLongStraightHair != null && pawn.story.hairDef != preferredLongStraightHair)
            {
                pawn.story.hairDef = preferredLongStraightHair;
                dirty = true;
            }

            Color current = pawn.story.HairColor;
            if (Mathf.Abs(current.r - WraithHairColor.r) > 0.002f
                || Mathf.Abs(current.g - WraithHairColor.g) > 0.002f
                || Mathf.Abs(current.b - WraithHairColor.b) > 0.002f)
            {
                pawn.story.HairColor = WraithHairColor;
                dirty = true;
            }

            if (dirty)
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static void ReconcileWngGeneAbilities(Pawn pawn)
        {
            if (pawn?.genes == null || pawn.abilities == null)
                return;

            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene?.def == null || !gene.Active || gene.def.abilities.NullOrEmpty())
                    continue;
                if (!gene.def.defName.StartsWith("WNG_", StringComparison.Ordinal))
                    continue;

                foreach (AbilityDef abilityDef in gene.def.abilities)
                {
                    if (abilityDef != null && pawn.abilities.GetAbility(abilityDef) == null)
                        pawn.abilities.GainAbility(abilityDef);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextTick, "wngWraithMaintenanceNextTick", 0);
        }
    }
}
