using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// A real RimWorld Skyfaller wrapper carrying the exact WNG shuttle Thing through an attack pass.
    /// It never creates a proxy craft. The same inner Thing, with its comps/transporter contents/state,
    /// is handed to the next pass or spawned as the final landed shuttle.
    /// </summary>
    public sealed class WNGAttackPassSkyfaller : Skyfaller
    {
        protected override void Impact()
        {
            Map map = Map;
            IntVec3 passCell = Position;
            Thing craft = innerContainer?.FirstOrDefault();

            if (craft == null || map == null)
            {
                base.Impact();
                return;
            }

            innerContainer.Remove(craft);

            bool continueFlight = false;
            if (craft.TryGetComp<CompWraithDartRaidMission>() is CompWraithDartRaidMission dartMission)
                continueFlight = dartMission.ExecutePhysicalPass(map, passCell);
            else if (craft.TryGetComp<CompPuddleJumperRaidMission>() is CompPuddleJumperRaidMission jumperMission)
                continueFlight = jumperMission.ExecutePhysicalPass(map, passCell);

            if (continueFlight)
            {
                IntVec3 nextCell = WNGShuttleFlightUtility.FindNextPassCell(craft, map, passCell);
                if (!WNGShuttleFlightUtility.SpawnAttackPass(craft, map, nextCell))
                    LandExactCraft(craft, map, passCell);
            }
            else
            {
                LandExactCraft(craft, map, passCell);
            }

            Destroy(DestroyMode.Vanish);
        }

        private static void LandExactCraft(Thing craft, Map map, IntVec3 near)
        {
            IntVec3 landingCell = WNGShuttleFlightUtility.FindLandingCell(craft, map, near);
            GenSpawn.Spawn(craft, landingCell, map, WipeMode.Vanish);
            craft.TryGetComp<CompWraithDartRaidMission>()?.NotifyPhysicallyLanded();
            craft.TryGetComp<CompPuddleJumperRaidMission>()?.NotifyPhysicallyLanded();
        }
    }

    public static class WNGShuttleFlightUtility
    {
        private const string AttackPassSkyfallerDefName = "WNG_ShuttleAttackPassIncoming";

        public static bool TryBeginPhysicalPasses(Thing craft, Map map, IntVec3 firstPassCell)
        {
            if (craft == null || map == null || !firstPassCell.IsValid || !firstPassCell.InBounds(map))
                return false;

            bool wasSpawned = craft.Spawned;
            IntVec3 oldCell = wasSpawned ? craft.Position : IntVec3.Invalid;
            if (wasSpawned)
                craft.DeSpawn();

            if (SpawnAttackPass(craft, map, firstPassCell))
                return true;

            if (!craft.Spawned && oldCell.IsValid && oldCell.InBounds(map))
                GenSpawn.Spawn(craft, FindLandingCell(craft, map, oldCell), map, WipeMode.Vanish);
            return false;
        }

        public static bool SpawnAttackPass(Thing craft, Map map, IntVec3 passCell)
        {
            ThingDef skyfallerDef = DefDatabase<ThingDef>.GetNamedSilentFail(AttackPassSkyfallerDefName);
            if (skyfallerDef == null || craft == null || map == null)
                return false;

            if (!passCell.IsValid || !passCell.InBounds(map))
                passCell = FindLandingCell(craft, map, map.Center);

            Skyfaller skyfaller = SkyfallerMaker.MakeSkyfaller(skyfallerDef, craft);
            if (skyfaller == null)
                return false;

            GenSpawn.Spawn(skyfaller, passCell, map, WipeMode.Vanish);
            return true;
        }

        public static IntVec3 FindAttackPassCell(Thing craft, Map map, IntVec3 origin, bool oppositeSide)
        {
            if (map == null)
                return origin;

            IntVec3 preferred;
            if (oppositeSide)
            {
                preferred = new IntVec3(map.Size.x - 1 - origin.x, 0, map.Size.z - 1 - origin.z);
            }
            else
            {
                int x = origin.x < map.Size.x / 2 ? Math.Min(map.Size.x - 2, Math.Max(1, map.Size.x * 3 / 4)) : Math.Max(1, map.Size.x / 4);
                int z = origin.z < map.Size.z / 2 ? Math.Min(map.Size.z - 2, Math.Max(1, map.Size.z * 3 / 4)) : Math.Max(1, map.Size.z / 4);
                preferred = new IntVec3(x, 0, z);
            }

            return FindLandingCell(craft, map, preferred);
        }

        public static IntVec3 FindNextPassCell(Thing craft, Map map, IntVec3 previous)
        {
            return FindAttackPassCell(craft, map, previous, oppositeSide: true);
        }

        public static IntVec3 FindLandingCell(Thing craft, Map map, IntVec3 near)
        {
            if (map == null)
                return near;

            Predicate<IntVec3> validator = cell => cell.InBounds(map) &&
                !cell.Fogged(map) &&
                (craft?.def == null || GenSpawn.CanSpawnAt(craft.def, cell, map));

            if (near.IsValid && near.InBounds(map) && validator(near))
                return near;

            IntVec3 center = near.IsValid && near.InBounds(map) ? near : map.Center;
            if (CellFinder.TryFindRandomCellNear(center, map, 12, validator, out IntVec3 result))
                return result;

            return CellFinder.RandomCell(map);
        }
    }
}
