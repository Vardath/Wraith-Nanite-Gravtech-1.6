using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class GameComponent_WraithMaintenance : GameComponent
    {
        private static readonly Color WraithHairColor = new Color(0.97f, 0.98f, 0.99f);
        private int nextTick;
        private HairDef preferredLongStraightHair;

        public GameComponent_WraithMaintenance(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextTick) return;
            nextTick = now + 300;

            foreach (Map map in Find.Maps)
            {
                if (map?.mapPawns == null) continue;
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (pawn == null || pawn.Dead) continue;
                    if (WraithLifeForceUtility.IsWraith(pawn)) EnforceAppearance(pawn);
                    ReconcileGeneAbilities(pawn);
                }
            }
        }

        private void EnforceAppearance(Pawn pawn)
        {
            if (pawn.story == null) return;
            if (preferredLongStraightHair == null)
            {
                preferredLongStraightHair = DefDatabase<HairDef>.AllDefsListForReading
                    .Where(h => h != null && !string.IsNullOrEmpty(h.texPath))
                    .Select(h => new { Hair = h, Search = ((h.defName ?? "") + " " + (h.label ?? "") + " " + (h.texPath ?? "")).ToLowerInvariant() })
                    .Where(x => x.Search.Contains("long") || x.Search.Contains("shoulder"))
                    .Where(x => !x.Search.Contains("braid") && !x.Search.Contains("curl") && !x.Search.Contains("dread") && !x.Search.Contains("afro") && !x.Search.Contains("mohawk") && !x.Search.Contains("bun"))
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
            if (Mathf.Abs(current.r - WraithHairColor.r) > 0.002f || Mathf.Abs(current.g - WraithHairColor.g) > 0.002f || Mathf.Abs(current.b - WraithHairColor.b) > 0.002f)
            {
                pawn.story.HairColor = WraithHairColor;
                dirty = true;
            }

            if (dirty) pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static void ReconcileGeneAbilities(Pawn pawn)
        {
            if (pawn?.genes == null || pawn.abilities == null) return;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (gene?.def == null || !gene.Active || gene.def.abilities.NullOrEmpty() || !gene.def.defName.StartsWith("WNG_", StringComparison.Ordinal)) continue;
                foreach (AbilityDef ability in gene.def.abilities)
                    if (ability != null && pawn.abilities.GetAbility(ability) == null)
                        pawn.abilities.GainAbility(ability);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextTick, "wngWraithMaintenanceNextTick", 0);
        }
    }
}
