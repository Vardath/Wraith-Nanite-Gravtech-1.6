using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public static class ReplicatorCoordinationUtility
    {
        public static bool IsController(Pawn pawn)
        {
            return pawn?.def?.defName == "WNG_ReplicatorController" || pawn?.kindDef?.defName == "WNG_ReplicatorController";
        }
    }

    /// <summary>
    /// Cached local controller influence. Controllers improve nearby same-faction organization but
    /// never become a hard dependency: without one, ordinary Replicators retain a factor of 1.0 and
    /// continue to assimilate/recombine autonomously.
    /// </summary>
    public sealed class MapComponent_ReplicatorCoordination : MapComponent
    {
        private const int RefreshTicks = 180;
        private const float ControllerRadius = 18f;
        private readonly List<Pawn> controllers = new List<Pawn>();
        private int nextRefreshTick;

        public MapComponent_ReplicatorCoordination(Map map) : base(map)
        {
        }

        public float AssemblyFactorFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map || pawn.Faction == null)
                return 1f;
            if (ReplicatorEMPSuppressionUtility.IsSuppressed(pawn)
                || ReplicatorContainmentUtility.IsContained(map, pawn.Position)
                || ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(pawn))
                return 1f;

            RefreshIfNeeded();
            float radiusSq = ControllerRadius * ControllerRadius;
            for (int i = 0; i < controllers.Count; i++)
            {
                Pawn controller = controllers[i];
                if (controller == null || controller.Dead || !controller.Spawned || controller.Faction != pawn.Faction)
                    continue;
                if (ReplicatorEMPSuppressionUtility.IsSuppressed(controller)
                    || ReplicatorContainmentUtility.IsContained(map, controller.Position)
                    || ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(controller))
                    continue;
                if (controller.Position.DistanceToSquared(pawn.Position) <= radiusSq)
                    return 0.82f;
            }
            return 1f;
        }

        public bool HasActiveControllerFor(Pawn pawn)
        {
            return AssemblyFactorFor(pawn) < 1f;
        }

        private void RefreshIfNeeded()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextRefreshTick)
                return;

            nextRefreshTick = now + RefreshTicks;
            controllers.Clear();
            if (map?.mapPawns == null)
                return;

            controllers.AddRange(map.mapPawns.AllPawnsSpawned.Where(p =>
                p != null && !p.Dead && p.Spawned && ReplicatorCoordinationUtility.IsController(p)));
        }
    }
}
