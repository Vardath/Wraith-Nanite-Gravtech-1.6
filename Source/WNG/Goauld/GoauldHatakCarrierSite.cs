using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Generates a bounded landed System-Lord Ha'tak carrier as an actual Odyssey gravship layout.
    /// The native GravEngine, native GravshipHull and WNG Goa'uld substructure/facilities are real
    /// spawned Things/terrain. Death Gliders remain exact native shuttle Things physically parked
    /// on that substructure; no decorative carrier proxy or abstract hangar inventory is used.
    /// </summary>
    public sealed class SitePartWorker_GoauldHatakCarrier : SitePartWorker
    {
        private const int DeckRadius = 16;
        private const int DefenderCount = 8;
        private const int DeathGliderCount = 2;

        private static readonly string[] JaffaWarriorKindDefNames =
        {
            "JKB_JaffaWarrior01",
            "JKB_JaffaWarrior02",
            "JKB_JaffaWarrior03"
        };

        public override SitePartParams GenerateDefaultParams(float myThreatPoints, PlanetTile tile, Faction faction)
        {
            SitePartParams parms = base.GenerateDefaultParams(myThreatPoints, tile, faction);
            parms.threatPoints = Math.Max(0f, myThreatPoints);
            return parms;
        }

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);

            Site site = map?.Parent as Site;
            Faction faction = site?.Faction;
            if (map == null || site == null || faction == null ||
                !WNGOptionalIntegrations.GoauldJaffaIntegrationActive ||
                !WNGOptionalIntegrations.IsVerifiedRimGateSystemLordFaction(faction))
            {
                return;
            }

            if (!TryResolveDefs(out HatakDefs defs))
                return;

            IntVec3 center = map.Center;
            List<Thing> generated = new List<Thing>();
            List<Pawn> defenders = new List<Pawn>();
            List<Pawn> loadedCrew = new List<Pawn>();

            try
            {
                PrepareDeck(map, center, defs.deck);

                Building_GravEngine engine = SpawnNativeEngine(map, center, faction, generated);
                if (engine == null)
                {
                    Abort("native GravEngine", generated, defenders, loadedCrew);
                    return;
                }

                // Two field projectors keep the deliberately compact diamond deck inside native
                // Odyssey substructure-footprint support while leaving the engine itself exact.
                Thing fieldLeft = SpawnBuilding(defs.fieldProjector, center + new IntVec3(-5, 0, 7), Rot4.North, faction, map, generated);
                Thing fieldRight = SpawnBuilding(defs.fieldProjector, center + new IntVec3(5, 0, 7), Rot4.North, faction, map, generated);
                Thing peltac = SpawnBuilding(defs.peltac, center + new IntVec3(0, 0, -8), Rot4.North, faction, map, generated);
                Thing shield = SpawnBuilding(defs.shield, center + new IntVec3(0, 0, 4), Rot4.North, faction, map, generated);
                Thing powerCore = SpawnBuilding(defs.powerCore, center + new IntVec3(0, 0, -4), Rot4.North, faction, map, generated);
                Thing tankLeft = SpawnBuilding(defs.largeTank, center + new IntVec3(-7, 0, -4), Rot4.North, faction, map, generated);
                Thing tankRight = SpawnBuilding(defs.largeTank, center + new IntVec3(7, 0, -4), Rot4.North, faction, map, generated);
                Thing driveLeft = SpawnBuilding(defs.largeDrive, center + new IntVec3(-4, 0, -11), Rot4.North, faction, map, generated);
                Thing driveRight = SpawnBuilding(defs.largeDrive, center + new IntVec3(4, 0, -11), Rot4.North, faction, map, generated);
                Thing plasmaLeft = SpawnBuilding(defs.plasmaBattery, center + new IntVec3(-10, 0, 4), Rot4.North, faction, map, generated);
                Thing plasmaRight = SpawnBuilding(defs.plasmaBattery, center + new IntVec3(10, 0, 4), Rot4.North, faction, map, generated);
                Thing rings = SpawnBuilding(defs.transportRings, center + new IntVec3(0, 0, 10), Rot4.North, faction, map, generated);

                if (fieldLeft == null || fieldRight == null || peltac == null || shield == null || powerCore == null ||
                    tankLeft == null || tankRight == null || driveLeft == null || driveRight == null ||
                    plasmaLeft == null || plasmaRight == null || rings == null)
                {
                    Abort("essential Ha'tak facilities", generated, defenders, loadedCrew);
                    return;
                }

                FillGeneratedTank(tankLeft);
                FillGeneratedTank(tankRight);

                if (!SpawnFuelPipePaths(defs.fuelPipe, center, faction, map, generated) ||
                    !SpawnPowerNetwork(defs.powerConduit, center, faction, map, generated))
                {
                    Abort("Ha'tak fuel/power networks", generated, defenders, loadedCrew);
                    return;
                }

                if (!SpawnNativeHullPerimeter(map, center, faction, generated))
                {
                    Abort("native GravshipHull perimeter", generated, defenders, loadedCrew);
                    return;
                }

                List<PawnKindDef> crewKinds = ResolveVerifiedJaffaWarriorKinds();
                if (crewKinds.Count == 0)
                {
                    Abort("verified Jaffa PawnKinds", generated, defenders, loadedCrew);
                    return;
                }

                if (!SpawnDeckDefenders(DefenderCount, crewKinds, faction, map, center, defenders))
                {
                    Abort("bounded deck defenders", generated, defenders, loadedCrew);
                    return;
                }

                IntVec3[] gliderCells =
                {
                    center + new IntVec3(-8, 0, 0),
                    center + new IntVec3(8, 0, 0)
                };

                for (int i = 0; i < DeathGliderCount; i++)
                {
                    Thing glider = SpawnBuilding(defs.deathGlider, gliderCells[i], Rot4.North, faction, map, generated);
                    if (glider == null || !LoadExactJaffaCrew(glider, crewKinds, faction, loadedCrew))
                    {
                        Abort("crewed physical Death Gliders", generated, defenders, loadedCrew);
                        return;
                    }
                }

                // Allow all spawned native facilities to discover/link to the exact engine, then
                // immediately enforce technology-family isolation. Fuel path activation itself is
                // cached/ticked by the existing WNG fuel-facility comp.
                engine.TryGetComp<CompWNGGravEngineTheme>()?.EnforceFamilyLinks();

                Lord lord = LordMaker.MakeNewLord(faction, new LordJob_DefendBase(faction, engine.Position, 60000), map);
                foreach (Pawn pawn in defenders)
                    lord.AddPawn(pawn);

                map.GetComponent<MapComponent_GoauldHatakCarrierDefense>()?.ArmCarrierDefense();
            }
            catch (Exception ex)
            {
                Log.Error("[WNG] Ha'tak carrier generation failed: " + ex);
                Abort("unexpected generation exception", generated, defenders, loadedCrew);
            }
        }

        private static bool TryResolveDefs(out HatakDefs defs)
        {
            defs = new HatakDefs
            {
                deck = DefDatabase<TerrainDef>.GetNamedSilentFail("WNG_GoauldSubstructure"),
                peltac = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldPeltac"),
                largeTank = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldLargeNaquadriaTank"),
                largeDrive = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldLargeSublightDrive"),
                fieldProjector = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldFieldProjector"),
                shield = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldShieldGenerator"),
                powerCore = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldPowerCore"),
                fuelPipe = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldHiddenNaquadriaPipe"),
                powerConduit = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldHiddenPowerConduit"),
                plasmaBattery = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldHeavyPlasmaBattery"),
                transportRings = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldTransportRings"),
                deathGlider = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldDeathGlider")
            };

            if (defs.AllPresent && ThingDefOf.GravEngine != null && ThingDefOf.GravshipHull != null)
                return true;

            Log.Error("[WNG] Ha'tak carrier generation aborted because a required native/WNG Goa'uld Def is unavailable.");
            return false;
        }

        private static void PrepareDeck(Map map, IntVec3 center, TerrainDef deck)
        {
            for (int dx = -DeckRadius; dx <= DeckRadius; dx++)
            {
                for (int dz = -DeckRadius; dz <= DeckRadius; dz++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) > DeckRadius)
                        continue;

                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map))
                        continue;

                    foreach (Thing thing in cell.GetThingList(map).ToList())
                    {
                        if (thing is Pawn)
                            continue;
                        if (thing.def.category == ThingCategory.Building ||
                            thing.def.category == ThingCategory.Plant ||
                            thing.def.category == ThingCategory.Item)
                        {
                            if (!thing.Destroyed)
                                thing.Destroy(DestroyMode.Vanish);
                        }
                    }

                    map.terrainGrid.SetFoundation(cell, deck);
                }
            }
        }

        private static Building_GravEngine SpawnNativeEngine(Map map, IntVec3 cell, Faction faction, List<Thing> generated)
        {
            if (map.listerThings.ThingsOfDef(ThingDefOf.GravEngine).Any())
            {
                Log.Error("[WNG] Ha'tak carrier site map already contains a native GravEngine; refusing to create a second engine.");
                return null;
            }

            Building_GravEngine engine = ThingMaker.MakeThing(ThingDefOf.GravEngine) as Building_GravEngine;
            if (engine == null)
                return null;

            engine.SetFactionDirect(faction);
            GenSpawn.Spawn(engine, cell, map, Rot4.North, WipeMode.Vanish);
            engine.TryGetComp<CompWNGGravEngineTheme>()?.SetTheme(WNGGravshipTheme.Goauld);
            engine.RenamableLabel = faction.Name + " Ha'tak";
            generated.Add(engine);
            return engine;
        }

        private static Thing SpawnBuilding(ThingDef def, IntVec3 cell, Rot4 rotation, Faction faction, Map map, List<Thing> generated)
        {
            if (def == null || !cell.InBounds(map))
                return null;

            Thing thing = ThingMaker.MakeThing(def);
            if (thing == null)
                return null;

            try
            {
                thing.SetFactionDirect(faction);
                Thing spawned = GenSpawn.Spawn(thing, cell, map, rotation, WipeMode.Vanish);
                if (spawned?.Spawned != true)
                {
                    if (!thing.Destroyed)
                        thing.Destroy(DestroyMode.Vanish);
                    return null;
                }

                generated.Add(spawned);
                return spawned;
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Ha'tak site placement failed for " + def.defName + " at " + cell + ": " + ex.Message);
                if (!thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
                return null;
            }
        }

        private static void FillGeneratedTank(Thing tank)
        {
            CompRefuelable refuelable = tank?.TryGetComp<CompRefuelable>();
            if (refuelable != null)
                refuelable.Refuel(refuelable.Props.fuelCapacity * 0.8f);
        }

        private static bool SpawnFuelPipePaths(ThingDef pipeDef, IntVec3 center, Faction faction, Map map, List<Thing> generated)
        {
            IntVec3[][] paths =
            {
                new[]
                {
                    center + new IntVec3(-5, 0, -4), center + new IntVec3(-4, 0, -4),
                    center + new IntVec3(-3, 0, -4), center + new IntVec3(-2, 0, -4),
                    center + new IntVec3(-2, 0, -3), center + new IntVec3(-2, 0, -2)
                },
                new[]
                {
                    center + new IntVec3(5, 0, -4), center + new IntVec3(4, 0, -4),
                    center + new IntVec3(3, 0, -4), center + new IntVec3(2, 0, -4),
                    center + new IntVec3(2, 0, -3), center + new IntVec3(2, 0, -2)
                }
            };

            foreach (IntVec3[] path in paths)
            {
                foreach (IntVec3 cell in path)
                {
                    if (SpawnBuilding(pipeDef, cell, Rot4.North, faction, map, generated) == null)
                        return false;
                }
            }

            return true;
        }

        private static bool SpawnPowerNetwork(ThingDef conduitDef, IntVec3 center, Faction faction, Map map, List<Thing> generated)
        {
            HashSet<IntVec3> cells = new HashSet<IntVec3>();

            // Main trunk from the power core's east side to the northern systems.
            for (int z = -4; z <= 10; z++)
                cells.Add(center + new IntVec3(3, 0, z));
            cells.Add(center + new IntVec3(2, 0, -4));
            cells.Add(center + new IntVec3(2, 0, 4));
            cells.Add(center + new IntVec3(2, 0, 10));

            // Right plasma battery branch.
            for (int x = 4; x <= 8; x++)
                cells.Add(center + new IntVec3(x, 0, 4));

            // Left plasma battery branch kept south of the central shield/engine footprints.
            for (int x = -9; x <= 3; x++)
                cells.Add(center + new IntVec3(x, 0, 2));
            cells.Add(center + new IntVec3(-9, 0, 3));
            cells.Add(center + new IntVec3(-9, 0, 4));

            foreach (IntVec3 cell in cells)
            {
                if (SpawnBuilding(conduitDef, cell, Rot4.North, faction, map, generated) == null)
                    return false;
            }

            return true;
        }

        private static bool SpawnNativeHullPerimeter(Map map, IntVec3 center, Faction faction, List<Thing> generated)
        {
            for (int dx = -DeckRadius; dx <= DeckRadius; dx++)
            {
                for (int dz = -DeckRadius; dz <= DeckRadius; dz++)
                {
                    if (Math.Abs(dx) + Math.Abs(dz) != DeckRadius)
                        continue;

                    // South boarding breach and smaller north service opening keep the landed site
                    // pathable without inventing a non-native Goa'uld door in this slice.
                    if (dz <= -13 && Math.Abs(dx) <= 3)
                        continue;
                    if (dz >= 14 && Math.Abs(dx) <= 2)
                        continue;

                    IntVec3 cell = center + new IntVec3(dx, 0, dz);
                    if (!cell.InBounds(map))
                        return false;

                    Thing hull = ThingMaker.MakeThing(ThingDefOf.GravshipHull, ThingDefOf.Steel);
                    if (hull == null)
                        return false;

                    try
                    {
                        hull.SetFactionDirect(faction);
                        Thing spawned = GenSpawn.Spawn(hull, cell, map, Rot4.North, WipeMode.Vanish);
                        spawned.TryGetComp<CompWNGGravshipPartTheme>()?.SetTheme(WNGGravshipTheme.Goauld);
                        generated.Add(spawned);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[WNG] Native Ha'tak hull placement failed at " + cell + ": " + ex.Message);
                        if (!hull.Destroyed)
                            hull.Destroy(DestroyMode.Vanish);
                        return false;
                    }
                }
            }

            return true;
        }

        private static List<PawnKindDef> ResolveVerifiedJaffaWarriorKinds()
        {
            List<PawnKindDef> result = new List<PawnKindDef>();
            if (!WNGOptionalIntegrations.RequiredRimGateBiotechActive)
                return result;

            foreach (string defName in JaffaWarriorKindDefNames)
            {
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
                if (kind?.modContentPack == null)
                    continue;
                if (!string.Equals(kind.modContentPack.PackageIdPlayerFacing,
                        WNGOptionalIntegrations.RimGateBiotechPackageId,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                result.Add(kind);
            }

            return result;
        }

        private static bool SpawnDeckDefenders(int count, List<PawnKindDef> kinds, Faction faction, Map map, IntVec3 center, List<Pawn> output)
        {
            for (int i = 0; i < count; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kinds.RandomElement(), faction, map.Tile);
                if (pawn == null)
                    return false;

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, 12,
                    c => c.InBounds(map) && c.Standable(map) && Math.Abs(c.x - center.x) + Math.Abs(c.z - center.z) <= 14);
                if (!cell.IsValid)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                    return false;
                }

                try
                {
                    GenSpawn.Spawn(pawn, cell, map);
                    output.Add(pawn);
                }
                catch
                {
                    if (!pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                    return false;
                }
            }

            return true;
        }

        private static bool LoadExactJaffaCrew(Thing glider, List<PawnKindDef> kinds, Faction faction, List<Pawn> loadedCrew)
        {
            CompTransporter transporter = glider?.TryGetComp<CompTransporter>();
            if (transporter == null)
                return false;

            for (int i = 0; i < 2; i++)
            {
                Pawn pawn = PawnGenerator.GeneratePawn(kinds.RandomElement(), faction);
                if (pawn == null || !transporter.innerContainer.TryAdd(pawn))
                {
                    if (pawn != null && !pawn.Destroyed)
                        pawn.Destroy(DestroyMode.Vanish);
                    return false;
                }

                transporter.Notify_ThingAdded(pawn);
                loadedCrew.Add(pawn);
            }

            return true;
        }

        private static void Abort(string stage, List<Thing> generated, List<Pawn> defenders, List<Pawn> loadedCrew)
        {
            Log.Error("[WNG] Ha'tak carrier generation failed at " + stage + "; removing incomplete generated carrier Things/pawns.");

            foreach (Pawn pawn in defenders)
                if (pawn != null && !pawn.Destroyed)
                    pawn.Destroy(DestroyMode.Vanish);

            foreach (Pawn pawn in loadedCrew)
            {
                if (pawn == null || pawn.Destroyed)
                    continue;
                ThingOwner holder = pawn.ParentHolder as ThingOwner;
                holder?.Remove(pawn);
                pawn.Destroy(DestroyMode.Vanish);
            }

            for (int i = generated.Count - 1; i >= 0; i--)
            {
                Thing thing = generated[i];
                if (thing != null && !thing.Destroyed)
                    thing.Destroy(DestroyMode.Vanish);
            }
        }

        private struct HatakDefs
        {
            public TerrainDef deck;
            public ThingDef peltac;
            public ThingDef largeTank;
            public ThingDef largeDrive;
            public ThingDef fieldProjector;
            public ThingDef shield;
            public ThingDef powerCore;
            public ThingDef fuelPipe;
            public ThingDef powerConduit;
            public ThingDef plasmaBattery;
            public ThingDef transportRings;
            public ThingDef deathGlider;

            public bool AllPresent => deck != null && peltac != null && largeTank != null && largeDrive != null &&
                fieldProjector != null && shield != null && powerCore != null && fuelPipe != null &&
                powerConduit != null && plasmaBattery != null && transportRings != null && deathGlider != null;
        }
    }

    /// <summary>
    /// One bounded automatic launch from physically parked carrier Gliders after a player map visit.
    /// It never creates replacement craft; it calls the mission comp on the exact generated Gliders.
    /// </summary>
    public sealed class MapComponent_GoauldHatakCarrierDefense : MapComponent
    {
        private const int LaunchDelayTicks = 900;
        private bool armed;
        private bool launched;
        private int launchTick;

        public MapComponent_GoauldHatakCarrierDefense(Map map) : base(map)
        {
        }

        public void ArmCarrierDefense()
        {
            if (launched)
                return;
            armed = true;
            launchTick = (Find.TickManager?.TicksGame ?? 0) + LaunchDelayTicks;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!armed || launched || Find.TickManager.TicksGame < launchTick)
                return;

            Site site = map.Parent as Site;
            if (site?.parts == null || !site.parts.Any(p => p != null && p.def?.defName == "WNG_GoauldHatakCarrier"))
            {
                armed = false;
                return;
            }

            ThingDef gliderDef = DefDatabase<ThingDef>.GetNamedSilentFail("WNG_GoauldDeathGlider");
            if (gliderDef == null)
            {
                launched = true;
                return;
            }

            launched = true;
            foreach (Thing glider in map.listerThings.ThingsOfDef(gliderDef)
                         .Where(t => t?.Spawned == true && t.Faction == site.Faction)
                         .OrderBy(t => t.thingIDNumber)
                         .Take(2)
                         .ToList())
            {
                glider.TryGetComp<CompGoauldDeathGliderMission>()
                    ?.TryBeginCombatSortie(departWhenComplete: false, showFailureMessage: false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref armed, "wngHatakCarrierDefenseArmed", false);
            Scribe_Values.Look(ref launched, "wngHatakCarrierDefenseLaunched", false);
            Scribe_Values.Look(ref launchTick, "wngHatakCarrierDefenseLaunchTick", 0);
        }
    }

    /// <summary>
    /// Discovers at most one bounded landed Ha'tak carrier site at a time. It binds only to exact
    /// verified System-Lord world factions and never manufactures a parallel Goa'uld/Jaffa faction.
    /// </summary>
    public sealed class IncidentWorker_GoauldHatakCarrierDiscovery : IncidentWorker
    {
        private const int MaxActiveSites = 1;
        private const int MinSiteDistance = 12;
        private const int MaxSiteDistance = 28;

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms) || !WNGOptionalIntegrations.GoauldJaffaIntegrationActive ||
                Find.WorldObjects == null || Find.FactionManager == null || Faction.OfPlayer == null)
                return false;

            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_GoauldHatakCarrier");
            return siteDef != null && ActiveSites(siteDef).Count < MaxActiveSites && EligibleFactions(siteDef).Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            SitePartDef siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail("WNG_GoauldHatakCarrier");
            if (siteDef == null || ActiveSites(siteDef).Count >= MaxActiveSites)
                return false;

            List<Faction> factions = EligibleFactions(siteDef);
            if (factions.Count == 0)
                return false;
            Faction faction = factions.RandomElement();

            if (!TileFinder.TryFindNewSiteTile(
                    out PlanetTile tile,
                    MinSiteDistance,
                    MaxSiteDistance,
                    allowCaravans: false,
                    tileFinderMode: TileFinderMode.Near))
                return false;

            Site site = SiteMaker.MakeSite(siteDef, tile, faction, ifHostileThenMustRemainHostile: true, threatPoints: 0f);
            if (site?.parts == null || !site.parts.Any(p => p != null && p.def == siteDef))
            {
                site?.Destroy();
                return false;
            }

            site.customLabel = faction.Name + " landed Ha'tak";
            Find.WorldObjects.Add(site);
            Find.LetterStack.ReceiveLetter(
                "Landed Ha'tak carrier located",
                "Long-range tracking has located a landed Ha'tak belonging to " + faction.Name + ". The site contains a real Goa'uld Odyssey gravship structure, Jaffa defenders and physically parked Death Gliders that can launch from the carrier deck.",
                LetterDefOf.ThreatBig,
                site);
            return true;
        }

        private static List<Site> ActiveSites(SitePartDef def)
        {
            return Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .Where(s => s != null && !s.Destroyed && s.parts != null && s.parts.Any(p => p != null && p.def == def))
                .ToList();
        }

        private static List<Faction> EligibleFactions(SitePartDef def)
        {
            HashSet<Faction> represented = new HashSet<Faction>(ActiveSites(def).Select(s => s.Faction).Where(f => f != null));
            HashSet<FactionDef> verifiedDefs = new HashSet<FactionDef>(WNGOptionalIntegrations.ResolveRimGateSystemLordFactionDefs());

            return Find.FactionManager.AllFactionsListForReading
                .Where(f => f != null && f != Faction.OfPlayer && !f.defeated &&
                            verifiedDefs.Contains(f.def) && f.HostileTo(Faction.OfPlayer) && !represented.Contains(f))
                .ToList();
        }
    }
}
