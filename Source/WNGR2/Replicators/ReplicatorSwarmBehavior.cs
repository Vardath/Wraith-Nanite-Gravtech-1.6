using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Shared low-cost swarm posture. It keeps autonomous Replicators matter-first and only changes
    /// recombination/target scoring modestly as an infestation grows. This is deliberately not an
    /// ordinary raid AI replacement.
    /// </summary>
    public sealed class MapComponent_ReplicatorSwarmBehavior : MapComponent
    {
        public MapComponent_ReplicatorSwarmBehavior(Map map) : base(map)
        {
        }

        public float AssemblyFactorFor(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map || pawn.Faction == null)
                return 1f;

            int count = ReplicatorUtility.CountBlockReplicatorsForFaction(map, pawn.Faction);
            if (count >= 24)
                return 0.88f;
            if (count >= 10)
                return 0.94f;
            return 1f;
        }

        public float TargetScoreOffset(Pawn pawn, Thing target)
        {
            if (pawn == null || target == null)
                return 0f;

            // Loose matter is the safest/fastest growth path, so an autonomous swarm prefers it.
            if (target.def?.category == ThingCategory.Item)
                return -25f;

            // Large mature swarms become a little more willing to consume infrastructure, without
            // turning every unit into an anti-colony combat pawn.
            if (target.def?.category == ThingCategory.Building && pawn.Faction != null)
            {
                int count = ReplicatorUtility.CountBlockReplicatorsForFaction(map, pawn.Faction);
                if (count >= 18)
                    return -8f;
            }

            return 0f;
        }
    }

    /// <summary>
    /// Short-lived shared retaliation memory. A struck Replicator can broadcast its aggressor to
    /// same-faction units, but the memory expires quickly and does not convert the swarm into a
    /// permanent raid/lord attack state.
    /// </summary>
    public sealed class MapComponent_ReplicatorThreatResponse : MapComponent
    {
        private const int SharedThreatTicks = 900;
        private Pawn aggressor;
        private Faction threatenedFaction;
        private int expiresAtTick;

        public MapComponent_ReplicatorThreatResponse(Map map) : base(map)
        {
        }

        public void RegisterAttack(Pawn attacked, Pawn attacker)
        {
            if (attacked == null || attacker == null || attacked.Faction == null || attacked.Map != map || attacker.Map != map)
                return;
            if (attacker == attacked || attacker.Faction == attacked.Faction)
                return;

            aggressor = attacker;
            threatenedFaction = attacked.Faction;
            int now = Find.TickManager?.TicksGame ?? 0;
            long expiry = (long)now + SharedThreatTicks;
            expiresAtTick = expiry >= int.MaxValue ? int.MaxValue : (int)expiry;
        }

        public bool TryGetRetaliationTarget(Pawn responder, out Pawn target)
        {
            target = aggressor;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (responder == null || responder.Dead || !responder.Spawned || responder.Map != map)
                return false;
            if (target == null || target.Dead || !target.Spawned || target.Map != map || now > expiresAtTick)
                return false;
            if (responder.Faction == null || responder.Faction != threatenedFaction || target.Faction == responder.Faction)
                return false;
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref aggressor, "wngReplicatorSharedAggressor");
            Scribe_References.Look(ref threatenedFaction, "wngReplicatorThreatenedFaction");
            Scribe_Values.Look(ref expiresAtTick, "wngReplicatorThreatExpires", 0);
        }
    }
}
