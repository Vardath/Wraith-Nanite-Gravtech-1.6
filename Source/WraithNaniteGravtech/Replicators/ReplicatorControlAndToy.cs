using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorChildsToy : CompProperties
    {
        public int feralTicks = 60000;
        public int checkIntervalTicks = 250;

        public CompProperties_ReplicatorChildsToy()
        {
            compClass = typeof(CompReplicatorChildsToy);
        }
    }

    /// <summary>
    /// Player-safe Child's Toy state. A player-owned base Drone can be isolated into its own
    /// autonomous controller domain. If that exact pawn remains outside player control for a full
    /// day it becomes an ordinary hostile Drone. The hostile replacement is spawned before the
    /// source is consumed and inherits matter/adaptation state without duplication.
    /// </summary>
    public sealed class CompReplicatorChildsToy : ThingComp
    {
        private bool designatedToy;
        private int uncontrolledTicks;
        private int nextCheckTick;
        private CompProperties_ReplicatorChildsToy Props => (CompProperties_ReplicatorChildsToy)props;

        public bool DesignatedToy => designatedToy;

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            CompReplicatorState state = pawn?.TryGetComp<CompReplicatorState>();
            if (!designatedToy || pawn == null || pawn.Dead || !pawn.Spawned || state == null)
            {
                uncontrolledTicks = 0;
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now < nextCheckTick)
                return;
            int interval = Math.Max(60, Props.checkIntervalTicks);
            nextCheckTick = now + interval;

            if (pawn.Faction == Faction.OfPlayer)
            {
                uncontrolledTicks = 0;
                return;
            }

            if (state.EMPSuppressed)
                return;

            uncontrolledTicks = Math.Min(Math.Max(60, Props.feralTicks), uncontrolledTicks + interval);
            if (uncontrolledTicks >= Math.Max(60, Props.feralTicks))
                TryBecomeFeral(pawn, state);
        }

        private static Faction ResolveSwarmFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            return def == null ? null : Find.FactionManager?.AllFactions?.FirstOrDefault(f => f?.def == def);
        }

        private void TryBecomeFeral(Pawn source, CompReplicatorState sourceState)
        {
            Map map = source.Map;
            Faction swarm = ResolveSwarmFaction();
            PawnKindDef droneKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_ReplicatorDrone");
            if (map == null || swarm == null || droneKind == null)
                return;

            Pawn replacement = null;
            try
            {
                replacement = PawnGenerator.GeneratePawn(droneKind, swarm);
                CompReplicatorState replacementState = replacement?.TryGetComp<CompReplicatorState>();
                if (replacement == null || replacementState == null)
                {
                    if (replacement != null && !replacement.Destroyed)
                        replacement.Destroy(DestroyMode.Vanish);
                    return;
                }

                replacementState.InheritFrom(sourceState);
                replacementState.SetController(ReplicatorControlKind.Autonomous, string.Empty);
                GenSpawn.Spawn(replacement, source.Position, map);

                if (!replacement.Spawned)
                {
                    if (!replacement.Destroyed)
                        replacement.Destroy(DestroyMode.Vanish);
                    return;
                }

                Messages.Message("A Child's Toy Replicator left outside player control has gone feral.", replacement, MessageTypeDefOf.ThreatSmall, true);
                source.Destroy(DestroyMode.Vanish);
            }
            catch (Exception ex)
            {
                if (replacement != null && !replacement.Destroyed && !replacement.Spawned)
                    replacement.Destroy(DestroyMode.Vanish);
                Log.Error($"[WNG] Child's Toy feral transition failed: {ex}");
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn pawn = parent as Pawn;
            CompReplicatorState state = pawn?.TryGetComp<CompReplicatorState>();
            if (pawn == null || state == null || pawn.Faction != Faction.OfPlayer || pawn.def?.defName != "WNG_ReplicatorDrone")
                yield break;

            if (!designatedToy)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Designate Child's Toy",
                    defaultDesc = "Isolate this player-owned base Replicator into a unique Child's Toy control domain. It will never autonomously assimilate player property. If it later remains outside player control for one full day, it can go feral.",
                    action = delegate
                    {
                        designatedToy = true;
                        state.SetController(ReplicatorControlKind.Autonomous, "toy:" + pawn.thingIDNumber);
                        uncontrolledTicks = 0;
                    }
                };
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = "Release Child's Toy designation",
                    defaultDesc = "Return this pawn to the ordinary autonomous controller domain while it remains under player faction control.",
                    action = delegate
                    {
                        designatedToy = false;
                        state.SetController(ReplicatorControlKind.Autonomous, string.Empty);
                        uncontrolledTicks = 0;
                    }
                };
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!designatedToy)
                return null;

            if ((parent as Pawn)?.Faction == Faction.OfPlayer)
                return "Child's Toy control: stable isolated player domain.";

            float days = Math.Max(0, Math.Max(60, Props.feralTicks) - uncontrolledTicks) / 60000f;
            return $"Child's Toy control lost: {days:0.0} day(s) until feral reversion.";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref designatedToy, "wngChildsToyDesignated", false);
            Scribe_Values.Look(ref uncontrolledTicks, "wngChildsToyUncontrolledTicks", 0);
            Scribe_Values.Look(ref nextCheckTick, "wngChildsToyNextCheckTick", 0);
        }
    }
}
