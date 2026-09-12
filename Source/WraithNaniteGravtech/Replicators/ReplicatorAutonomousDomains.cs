using System;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Assigns autonomous block Replicators to local swarm domains instead of treating an entire
    /// faction as one controller. Newly spawned autonomous blocks join the nearest compatible
    /// autonomous domain when one is close enough; otherwise they begin a new map-local domain.
    /// This keeps separate outbreaks independent while preserving exact domain identity through
    /// split/recombine inheritance.
    /// </summary>
    public sealed class MapComponent_ReplicatorAutonomousDomains : MapComponent
    {
        private const float JoinRadius = 45f;
        private int nextDomainSerial = 1;

        public MapComponent_ReplicatorAutonomousDomains(Map map) : base(map) { }

        public string ResolveDomain(Pawn pawn)
        {
            if (pawn == null || pawn.Map != map || pawn.Faction == null)
                return string.Empty;

            float radiusSq = JoinRadius * JoinRadius;
            Pawn nearby = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != pawn && !p.Dead && p.Spawned && p.Faction == pawn.Faction &&
                            p.Position.DistanceToSquared(pawn.Position) <= radiusSq)
                .Where(p =>
                {
                    CompReplicatorState state = p.TryGetComp<CompReplicatorState>();
                    return state != null && state.ControlKind == ReplicatorControlKind.Autonomous &&
                           !string.IsNullOrEmpty(state.ControlDomainId);
                })
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .ThenBy(p => p.thingIDNumber)
                .FirstOrDefault();

            string existing = nearby?.TryGetComp<CompReplicatorState>()?.ControlDomainId;
            if (!string.IsNullOrEmpty(existing))
                return existing;

            int serial = Math.Max(1, nextDomainSerial);
            nextDomainSerial = serial == int.MaxValue ? int.MaxValue : serial + 1;
            return $"auto:{map.Tile}:{serial}";
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextDomainSerial, "wngReplicatorNextAutonomousDomain", 1);
            if (nextDomainSerial < 1)
                nextDomainSerial = 1;
        }
    }
}
