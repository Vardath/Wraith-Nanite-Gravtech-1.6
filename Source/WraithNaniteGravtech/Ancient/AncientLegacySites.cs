using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    internal static class AncientLegacySiteUtility
    {
        public static bool CanCreate(string sitePartDefName)
        {
            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail(sitePartDefName);
            if (part == null) return false;
            return !Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .Any(site => site?.parts != null && site.parts.Any(p => p?.def == part));
        }

        public static bool TryCreate(
            IncidentParms parms,
            string sitePartDefName,
            string customLabel,
            string letterLabel,
            string letterText,
            float minimumThreatPoints,
            int minTileDistance = 8,
            int maxTileDistance = 30)
        {
            Map map = parms?.target as Map;
            SitePartDef part = DefDatabase<SitePartDef>.GetNamedSilentFail(sitePartDefName);
            if (map == null || !map.IsPlayerHome || part == null || !CanCreate(sitePartDefName))
                return false;

            PlanetTile tile;
            if (!TileFinder.TryFindNewSiteTile(out tile, minTileDistance, maxTileDistance, allowCaravans: false))
                return false;

            float points = Math.Max(
                minimumThreatPoints,
                parms.points > 0f ? parms.points : StorytellerUtility.DefaultSiteThreatPointsNow());

            Site created = SiteMaker.MakeSite(part, tile, faction: null, ifHostileThenMustRemainHostile: false, threatPoints: points);
            if (created == null)
                return false;

            created.customLabel = customLabel;
            Find.WorldObjects.Add(created);
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, created);
            return true;
        }

        public static void BuildRoom(Map map, IntVec3 center, int radiusX, int radiusZ)
        {
            ThingDef wall = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_PrecursorWall");
            ThingDef door = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_PrecursorDoor");
            if (map == null || wall == null) return;

            for (int x = -radiusX; x <= radiusX; x++)
            {
                SpawnStructure(map, wall, center + new IntVec3(x, 0, radiusZ));
                ThingDef south = x == 0 && door != null ? door : wall;
                SpawnStructure(map, south, center + new IntVec3(x, 0, -radiusZ));
            }

            for (int z = -radiusZ + 1; z < radiusZ; z++)
            {
                SpawnStructure(map, wall, center + new IntVec3(-radiusX, 0, z));
                SpawnStructure(map, wall, center + new IntVec3(radiusX, 0, z));
            }
        }

        public static void PlaceThing(Map map, string defName, IntVec3 preferred)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (map == null || def == null) return;
            Thing thing = ThingMaker.MakeThing(def);
            if (!GenPlace.TryPlaceThing(thing, preferred, map, ThingPlaceMode.Near) && !thing.Destroyed)
                thing.Destroy(DestroyMode.Vanish);
        }

        public static void PlaceStack(Map map, string defName, IntVec3 preferred, int count)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (map == null || def == null) return;
            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = Math.Max(1, Math.Min(count, def.stackLimit));
            if (!GenPlace.TryPlaceThing(thing, preferred, map, ThingPlaceMode.Near) && !thing.Destroyed)
                thing.Destroy(DestroyMode.Vanish);
        }

        private static void SpawnStructure(Map map, ThingDef def, IntVec3 cell)
        {
            if (map == null || def == null || !cell.InBounds(map) || cell.GetFirstBuilding(map) != null)
                return;
            GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }
    }

    public sealed class IncidentWorker_PrecursorLaboratoryDiscovery : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            parms?.target is Map map && map.IsPlayerHome &&
            AncientLegacySiteUtility.CanCreate("WNG_PrecursorLaboratorySite");

        protected override bool TryExecuteWorker(IncidentParms parms) =>
            AncientLegacySiteUtility.TryCreate(
                parms,
                "WNG_PrecursorLaboratorySite",
                "Open Precursor Laboratory",
                "Precursor laboratory located",
                "Survey traces lead to an exposed Ancient-era laboratory. The site appears abandoned, but surviving evidence may clarify how the precursor systems in this region were used.",
                275f);
    }

    public sealed class IncidentWorker_PrecursorVaultDiscovery : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            parms?.target is Map map && map.IsPlayerHome &&
            AncientLegacySiteUtility.CanCreate("WNG_PrecursorVaultSite");

        protected override bool TryExecuteWorker(IncidentParms parms) =>
            AncientLegacySiteUtility.TryCreate(
                parms,
                "WNG_PrecursorVaultSite",
                "Sealed Precursor Vault",
                "Sealed precursor vault located",
                "A buried precursor vault has been isolated from surrounding ruins. Its intact shell suggests that at least some damaged high-energy containment evidence may remain inside.",
                325f);
    }

    public sealed class IncidentWorker_DeceptiveSurveyAnnexDiscovery : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            parms?.target is Map map && map.IsPlayerHome &&
            AncientLegacySiteUtility.CanCreate("WNG_DeceptiveSurveyAnnexSite");

        protected override bool TryExecuteWorker(IncidentParms parms) =>
            AncientLegacySiteUtility.TryCreate(
                parms,
                "WNG_DeceptiveSurveyAnnexSite",
                "Ancient Survey Annex",
                "Ancient survey annex located",
                "An intact survey annex has been located under shallow debris. Its architecture appears Ancient, and the surviving data core is quiet. The site may not be as straightforward as it looks.",
                350f);
    }

    public sealed class SitePartWorker_PrecursorLaboratory : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null) return;

            AncientLegacySiteUtility.BuildRoom(map, map.Center, 7, 5);
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorTable", map.Center);
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorFormChair", map.Center + new IntVec3(-2, 0, 0));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorFormChair", map.Center + new IntVec3(2, 0, 0));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorBedsideConsole", map.Center + new IntVec3(0, 0, 2));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorLumen", map.Center + new IntVec3(0, 0, -2));

            // Evidence, not a free functional Ancient weapon/power system.
            AncientLegacySiteUtility.PlaceThing(map, "WNG_RecoveredAncientDrone", map.Center + new IntVec3(-3, 0, 2));
            AncientLegacySiteUtility.PlaceStack(map, "Plasteel", map.Center + new IntVec3(3, 0, 2), Rand.RangeInclusive(14, 26));
            AncientLegacySiteUtility.PlaceStack(map, "ComponentIndustrial", map.Center + new IntVec3(3, 0, -2), Rand.RangeInclusive(1, 2));
        }
    }

    public sealed class SitePartWorker_PrecursorVault : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null) return;

            AncientLegacySiteUtility.BuildRoom(map, map.Center, 5, 5);
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorTableSmall", map.Center + new IntVec3(0, 0, 2));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorWallLumen", map.Center + new IntVec3(-3, 0, 3));

            // Degraded containment evidence remains inert; no free vacuum-energy module.
            AncientLegacySiteUtility.PlaceThing(map, "WNG_RecoveredVacuumEnergyModule", map.Center);
            AncientLegacySiteUtility.PlaceStack(map, "Gold", map.Center + new IntVec3(2, 0, 1), Rand.RangeInclusive(8, 16));
            AncientLegacySiteUtility.PlaceStack(map, "ComponentSpacer", map.Center + new IntVec3(-2, 0, 1), 1);
        }
    }

    public sealed class CompProperties_DeceptiveSurveyAnnexCore : CompProperties
    {
        public int graceTicks = 900;
        public float triggerRadius = 12f;
        public int minimumDefenders = 2;
        public int maximumDefenders = 4;

        public CompProperties_DeceptiveSurveyAnnexCore()
        {
            compClass = typeof(CompDeceptiveSurveyAnnexCore);
        }
    }

    public sealed class CompDeceptiveSurveyAnnexCore : ThingComp
    {
        private bool revealed;
        private int revealReadyTick;

        private CompProperties_DeceptiveSurveyAnnexCore Props =>
            (CompProperties_DeceptiveSurveyAnnexCore)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && revealReadyTick <= 0)
                revealReadyTick = SafeFutureTick(Find.TickManager?.TicksGame ?? 0, Props.graceTicks);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (revealed || parent?.Spawned != true || parent.Map == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (revealReadyTick <= 0)
                revealReadyTick = SafeFutureTick(now, Props.graceTicks);
            if (now < revealReadyTick)
                return;

            float radius = Math.Max(0f, Props.triggerRadius);
            float radiusSq = radius * radius;
            bool approached = parent.Map.mapPawns.FreeColonistsSpawned
                .Any(p => p != null && !p.Dead && p.Position.DistanceToSquared(parent.Position) <= radiusSq);
            if (!approached)
                return;

            // Commit the one-shot reveal before spawning defenders so save/reload or
            // presentation failure cannot duplicate the hidden cadre.
            revealed = true;
            RevealLattice();
        }

        private void RevealLattice()
        {
            Map map = parent.Map;
            if (map == null) return;

            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_PrecursorCollective");
            Faction faction = factionDef == null ? null : Find.FactionManager?.FirstFactionOfDef(factionDef);
            PawnKindDef baseKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_HumanFormReplicator");
            PawnKindDef soldier = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_PrecursorSoldier");
            if (faction == null || baseKind == null)
            {
                Log.Warning("[WNG] Deceptive survey annex revealed but its hostile Lattice faction/pawn kind was unavailable.");
                return;
            }

            int min = Math.Max(1, Props.minimumDefenders);
            int max = Math.Max(min, Props.maximumDefenders);
            int count = Rand.RangeInclusive(min, max);
            Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, parent.Position, 60000), map);

            for (int i = 0; i < count; i++)
            {
                PawnKindDef kind = soldier != null && i == count - 1 && count >= 3 ? soldier : baseKind;
                Pawn pawn = PawnGenerator.GeneratePawn(kind, faction);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(parent.Position, map, 9);
                GenSpawn.Spawn(pawn, cell, map);
                lord.AddPawn(pawn);
            }

            Find.LetterStack.ReceiveLetter(
                "Survey annex revealed",
                "The apparently Ancient survey core has unfolded into an Asuran lattice node. Concealed pattern hardware has awakened a finite defensive cadre: the annex was a Lattice trap, not an intact Ancient outpost.",
                LetterDefOf.ThreatSmall,
                parent);
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(0, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref revealed, "wngSurveyAnnexRevealed", false);
            Scribe_Values.Look(ref revealReadyTick, "wngSurveyAnnexRevealReadyTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                revealReadyTick = Math.Max(0, revealReadyTick);
        }
    }

    public sealed class SitePartWorker_DeceptiveSurveyAnnex : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            if (map == null) return;

            AncientLegacySiteUtility.BuildRoom(map, map.Center, 6, 4);
            AncientLegacySiteUtility.PlaceThing(map, "WNG_DeceptiveSurveyAnnexCore", map.Center);
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorTableSmall", map.Center + new IntVec3(0, 0, 2));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorFormChair", map.Center + new IntVec3(-2, 0, 2));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorFormChair", map.Center + new IntVec3(2, 0, 2));
            AncientLegacySiteUtility.PlaceThing(map, "WNG_PrecursorLumen", map.Center + new IntVec3(0, 0, -2));
        }
    }
}
