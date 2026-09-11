using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
                action = TryBeginCombatSortie
            };

            CompTransporter transporter = Transporter;
            CompRefuelable fuel = Fuel;

            if (sortieActive)
                sortie.Disable("This Death Glider is already committed to a combat sortie.");
            else if (parent?.Spawned != true || parent.Map == null)
                sortie.Disable("The Death Glider must be landed on a map.");
            else if (transporter == null)
                sortie.Disable("Native shuttle transporter unavailable.");
            else if (transporter.AnythingLeftToLoad)
                sortie.Disable("Finish loading the assigned Death Glider crew and cargo first.");
            else if (transporter.OverMassCapacity)
                sortie.Disable("The Death Glider is over mass capacity.");
            else if (OperationalCrewCount < Math.Max(1, Props.requiredCrew))
                sortie.Disable($"Load {Math.Max(1, Props.requiredCrew)} conscious humanlike crew into the Death Glider.");
            else if (fuel == null || fuel.Fuel < Math.Max(0f, Props.sortieFuelCost))
                sortie.Disable($"At least {Math.Max(0f, Props.sortieFuelCost):0.#} fuel is required for a combat sortie.");
            else if (Props.projectile == null)
                sortie.Disable("Death Glider staff-cannon projectile is unavailable.");

            yield return sortie;
        }

        private void TryBeginCombatSortie()
        {
            if (sortieActive || parent?.Spawned != true || parent.Map == null)
                return;

            CompTransporter transporter = Transporter;
            CompRefuelable fuel = Fuel;
            int requiredCrew = Math.Max(1, Props.requiredCrew);
            float fuelCost = Math.Max(0f, Props.sortieFuelCost);

            if (transporter == null || transporter.AnythingLeftToLoad || transporter.OverMassCapacity ||
                OperationalCrewCount < requiredCrew || fuel == null || fuel.Fuel < fuelCost || Props.projectile == null)
            {
                Messages.Message("Death Glider sortie requirements are not met.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Map map = parent.Map;
            returnCell = parent.Position;
            completedPasses = 0;
            shotsFired = 0;
            sortieActive = true;

            IntVec3 firstPassCell = WNGShuttleFlightUtility.FindAttackPassCell(parent, map, returnCell, oppositeSide: false);
            if (!WNGShuttleFlightUtility.TryBeginPhysicalPasses(parent, map, firstPassCell))
            {
                sortieActive = false;
                returnCell = IntVec3.Invalid;
                Messages.Message("The Death Glider could not begin its combat sortie.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (fuelCost > 0f)
                fuel.ConsumeFuel(fuelCost);
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
            sortieActive = false;
            returnCell = IntVec3.Invalid;
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
            Scribe_Values.Look(ref completedPasses, "wngDeathGliderCompletedPasses", 0);
            Scribe_Values.Look(ref shotsFired, "wngDeathGliderShotsFired", 0);
            Scribe_Values.Look(ref returnCell, "wngDeathGliderReturnCell", IntVec3.Invalid);
        }
    }
}
