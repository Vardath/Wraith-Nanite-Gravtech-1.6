using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_GoauldDeathGliderMission : CompProperties
    {
        public int attackPasses = 2;
        public int shotsPerPass = 4;
        public float acquisitionRadius = 45f;
        public float sortieFuelCost = 30f;
        public int requiredCrew = 2;
        public ThingDef projectile;

        public CompProperties_GoauldDeathGliderMission()
        {
            compClass = typeof(CompGoauldDeathGliderMission);
        }
    }

    /// <summary>
    /// Combat-sortie layer for the exact native Death Glider shuttle Thing.
    /// Native CompTransporter owns the exact crew and cargo; WNG only requires a real two-person
    /// combat crew and carries that same shuttle through physical attack-pass skyfallers.
    ///
    /// A Glider parked on connected Odyssey gravship substructure is carried with that gravship by
    /// Odyssey itself. WNG therefore does not create a proxy hangar inventory or decorative bay.
    /// </summary>
    public sealed class CompGoauldDeathGliderMission : ThingComp
    {
        private bool sortieActive;
        private bool departAfterSortie;
        private int completedPasses;
        private int shotsFired;
        private IntVec3 returnCell = IntVec3.Invalid;

        private CompProperties_GoauldDeathGliderMission Props => (CompProperties_GoauldDeathGliderMission)props;
        private CompTransporter Transporter => parent?.TryGetComp<CompTransporter>();
        private CompRefuelable Fuel => parent?.TryGetComp<CompRefuelable>();

        public bool SortieActive => sortieActive;
        public int CompletedPasses => completedPasses;

        public int OperationalCrewCount
        {
            get
            {
                CompTransporter transporter = Transporter;
                if (transporter?.innerContainer == null)
                    return 0;

                return transporter.innerContainer
                    .OfType<Pawn>()
                    .Count(p => p != null && !p.Dead && !p.Downed && p.RaceProps?.Humanlike == true);
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent?.Faction != Faction.OfPlayer)
                yield break;

            Command_Action sortie = new Command_Action
            {
                defaultLabel = "Death Glider combat sortie",
                defaultDesc = "Launch this exact Death Glider through a paired staff-cannon attack run and return it to its original landing cell. The native transporter must contain a real two-person combat crew.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip", reportFailure: false),
                action = () => TryBeginCombatSortie(departWhenComplete: false, showFailureMessage: true)
            };

            string failure = SortieFailureReason();
            if (!failure.NullOrEmpty())
                sortie.Disable(failure);

            yield return sortie;
        }

        public bool TryBeginCombatSortie(bool departWhenComplete, bool showFailureMessage)
        {
            string failure = SortieFailureReason();
            if (!failure.NullOrEmpty())
            {
                if (showFailureMessage)
                    Messages.Message(failure, parent, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            Map map = parent.Map;
            returnCell = parent.Position;
            completedPasses = 0;
            shotsFired = 0;
            sortieActive = true;
            departAfterSortie = departWhenComplete;

            IntVec3 firstPassCell = WNGShuttleFlightUtility.FindAttackPassCell(parent, map, returnCell, oppositeSide: false);
            if (!WNGShuttleFlightUtility.TryBeginPhysicalPasses(parent, map, firstPassCell))
            {
                sortieActive = false;
                departAfterSortie = false;
                returnCell = IntVec3.Invalid;
                if (showFailureMessage)
                    Messages.Message("The Death Glider could not begin its combat sortie.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            float fuelCost = Math.Max(0f, Props.sortieFuelCost);
            if (fuelCost > 0f)
                Fuel.ConsumeFuel(fuelCost);
            return true;
        }

        private string SortieFailureReason()
        {
            if (sortieActive)
                return "This Death Glider is already committed to a combat sortie.";
            if (parent?.Spawned != true || parent.Map == null)
                return "The Death Glider must be landed on a map.";

            CompTransporter transporter = Transporter;
            if (transporter == null)
                return "Native shuttle transporter unavailable.";
            if (transporter.AnythingLeftToLoad)
                return "Finish loading the assigned Death Glider crew and cargo first.";
            if (transporter.OverMassCapacity)
                return "The Death Glider is over mass capacity.";

            int requiredCrew = Math.Max(1, Props.requiredCrew);
            if (OperationalCrewCount < requiredCrew)
                return $"Load {requiredCrew} conscious humanlike crew into the Death Glider.";

            CompRefuelable fuel = Fuel;
            float fuelCost = Math.Max(0f, Props.sortieFuelCost);
            if (fuel == null || fuel.Fuel < fuelCost)
                return $"At least {fuelCost:0.#} fuel is required for a combat sortie.";
            if (Props.projectile == null)
                return "Death Glider staff-cannon projectile is unavailable.";

            return null;
        }

        public bool ExecutePhysicalPass(Map map, IntVec3 passCell)
        {
            if (!sortieActive || map == null || parent == null || parent.Destroyed)
                return false;

            FireStaffCannonPass(map, passCell);
            completedPasses++;
            return completedPasses < Math.Max(1, Props.attackPasses);
        }

        public IntVec3 FindReturnLandingCell(Map map, IntVec3 fallback)
        {
            if (map != null && returnCell.IsValid && returnCell.InBounds(map) &&
                (parent?.def == null || GenSpawn.CanSpawnAt(parent.def, returnCell, map)))
            {
                return returnCell;
            }

            return WNGShuttleFlightUtility.FindLandingCell(parent, map, fallback);
        }

        public void NotifyPhysicallyLanded()
        {
            bool shouldDepart = departAfterSortie;
            sortieActive = false;
            departAfterSortie = false;
            returnCell = IntVec3.Invalid;

            if (shouldDepart)
                TryBeginNativeHostileEscape();
        }

        private bool TryBeginNativeHostileEscape()
        {
            if (parent?.Spawned != true || parent.Map == null)
                return false;
            if (parent.Faction == null || Faction.OfPlayer == null || !parent.Faction.HostileTo(Faction.OfPlayer))
                return false;
            if (OperationalCrewCount < Math.Max(1, Props.requiredCrew))
                return false;

            CompShuttle shuttle = parent.TryGetComp<CompShuttle>();
            TransportShip transportShip = shuttle?.shipParent;
            if (transportShip == null || transportShip.Disposed)
                return false;

            CompLaunchable launchable = parent.TryGetComp<CompLaunchable>();
            CompRefuelable refuelable = Fuel;
            float minimumFuel = Math.Max(0f, launchable?.Props?.minFuelCost ?? 0f);
            if (refuelable != null && refuelable.Fuel < minimumFuel)
                return false;

            PlanetTile destination = FindNativeEscapeDestination(parent.Tile);
            if (!destination.Valid)
                return false;

            ShipJob_FlyAway flyAway = (ShipJob_FlyAway)ShipJobMaker.MakeShipJob(ShipJobDefOf.FlyAway);
            flyAway.destinationTile = destination;
            flyAway.arrivalAction = new WNGHostileShuttleEscapeArrivalAction();
            flyAway.dropMode = TransportShipDropMode.None;
            transportShip.ForceJob(flyAway);

            if (parent.Spawned)
                return false;

            if (refuelable != null && minimumFuel > 0f)
                refuelable.ConsumeFuel(minimumFuel);
            return true;
        }

        private static PlanetTile FindNativeEscapeDestination(PlanetTile origin)
        {
            if (!origin.Valid || Find.WorldGrid == null)
                return PlanetTile.Invalid;

            List<PlanetTile> neighbors = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(origin, neighbors);
            if (neighbors.Count > 0)
                return neighbors.RandomElement();

            return origin;
        }

        private void FireStaffCannonPass(Map map, IntVec3 passCell)
        {
            if (map?.listerThings?.AllThings == null || parent?.Faction == null || Props.projectile == null)
                return;

            float radius = Math.Max(1f, Props.acquisitionRadius);
            float radiusSq = radius * radius;
            int shotCount = Math.Max(1, Props.shotsPerPass);

            List<Thing> targets = map.listerThings.AllThings
                .Where(t => IsValidCombatTarget(t, map))
                .Where(t => t.Position.DistanceToSquared(passCell) <= radiusSq)
                .OrderBy(t => t is Pawn ? 0 : 1)
                .ThenBy(t => t.Position.DistanceToSquared(passCell))
                .ThenBy(t => t.thingIDNumber)
                .Take(2)
                .ToList();

            if (targets.Count == 0)
                return;

            for (int i = 0; i < shotCount; i++)
            {
                Thing target = targets[i % targets.Count];
                Thing made = ThingMaker.MakeThing(Props.projectile);
                if (!(made is Projectile projectile))
                {
                    made?.Destroy();
                    continue;
                }

                GenSpawn.Spawn(projectile, passCell, map);
                projectile.Launch(parent, passCell.ToVector3Shifted(), target, target, ProjectileHitFlags.IntendedTarget);
                shotsFired++;
            }
        }

        private bool IsValidCombatTarget(Thing thing, Map map)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != map || thing.Faction == null ||
                parent?.Faction == null || thing.Faction == parent.Faction)
            {
                return false;
            }

            if (!(thing is Pawn) && !(thing is Building))
                return false;

            return parent.Faction.HostileTo(thing.Faction);
        }

        public override void Notify_Hacked(Pawn hacker)
        {
            sortieActive = false;
            departAfterSortie = false;
            returnCell = IntVec3.Invalid;
            base.Notify_Hacked(hacker);
        }

        public override string CompInspectStringExtra()
        {
            int requiredCrew = Math.Max(1, Props.requiredCrew);
            string state = sortieActive
                ? $"in flight ({completedPasses}/{Math.Max(1, Props.attackPasses)} passes, {shotsFired} bolts fired)"
                : "landed";

            return $"Death Glider combat crew: {OperationalCrewCount}/{requiredCrew}" +
                   $"\nCombat status: {state}" +
                   "\nCarrier role: park on connected Ha'tak gravship substructure to carry the exact fighter with the mothership.";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref sortieActive, "wngDeathGliderSortieActive", false);
            Scribe_Values.Look(ref departAfterSortie, "wngDeathGliderDepartAfterSortie", false);
            Scribe_Values.Look(ref completedPasses, "wngDeathGliderCompletedPasses", 0);
            Scribe_Values.Look(ref shotsFired, "wngDeathGliderShotsFired", 0);
            Scribe_Values.Look(ref returnCell, "wngDeathGliderReturnCell", IntVec3.Invalid);
        }
    }
}
