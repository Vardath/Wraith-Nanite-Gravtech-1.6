using RimWorld;
using Verse;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorControl : CompProperties
    {
        public CompProperties_ReplicatorControl()
        {
            compClass = typeof(CompReplicatorControl);
        }
    }

    /// <summary>
    /// Player-built Replicators are harmless colony mechs while actively overseen. A living
    /// same-faction Replicator Queen passively coordinates all local base Replicators. A non-Queen
    /// Sovereign Neural Lattice bearer instead establishes a save-persistent target-specific
    /// binding. Temporary Asuran lattice intrusion remains a separate save-safe override.
    /// </summary>
    public sealed class CompReplicatorControl : ThingComp
    {
        private const int CheckIntervalTicks = 250;
        private const int SelfDefenseTicks = 900;
        private const int ThreatBroadcastCooldownTicks = 180;

        private static bool movementSoundsResolved;
        private static SoundDef movementStepElectric;
        private static SoundDef movementStepMetalA;
        private static SoundDef movementStepMetalB;

        private int uncontrolledTicks;
        private int nextMovementSoundTick;
        private int selfDefenseUntilTick;
        private int lastThreatBroadcastTick = -999999;
        private Pawn recentAggressor;
        private Pawn sovereignController;

        private Pawn Pawn => parent as Pawn;

        public Pawn ActiveSovereignController => HasActiveSovereignBinding() ? sovereignController : null;

        public bool BindToSovereign(Pawn controller)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || controller == null || controller.Dead || controller.Faction == null
                || !ReplicatorQueenUtility.HasSovereignDirectiveAuthority(controller))
                return false;

            // A real Queen does not create per-unit implant bindings. Her same-faction presence is
            // already the broader sovereign coordination domain.
            if (ReplicatorQueenUtility.IsQueen(controller))
            {
                sovereignController = null;
                pawn.SetFaction(controller.Faction, null);
                uncontrolledTicks = 0;
                return true;
            }

            sovereignController = controller;
            pawn.SetFaction(controller.Faction, null);
            uncontrolledTicks = 0;

            // Newly generated hierarchy children can be bound only after they have spawned. All
            // player-issued bindings use already-spawned targets and therefore validate here.
            return !pawn.Spawned || HasActiveSovereignBinding();
        }

        public void ClearSovereignBinding()
        {
            sovereignController = null;
        }

        /// <summary>
        /// Preserve a valid implant-level command domain across a hierarchy transaction. This
        /// deliberately validates the controller against the destination map/faction rather than
        /// the source Replicator, because a genuine death split runs after the source pawn is dead.
        /// </summary>
        public void CopySovereignBindingTo(Pawn child, Map expectedMap)
        {
            if (child == null || sovereignController == null || expectedMap == null)
                return;
            if (!ControllerCanOwnOnMap(sovereignController, expectedMap, child.Faction))
                return;

            CompReplicatorControl childControl = child.TryGetComp<CompReplicatorControl>();
            if (childControl != null)
                childControl.BindToSovereign(sovereignController);
        }

        public bool HasActiveSovereignBinding()
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map == null || sovereignController == null)
                return false;
            return ControllerCanOwnOnMap(sovereignController, pawn.Map, pawn.Faction);
        }

        private static bool ControllerCanOwnOnMap(Pawn controller, Map map, Faction targetFaction)
        {
            if (controller == null || controller.Dead || !controller.Spawned || controller.Map != map
                || controller.Faction == null || targetFaction == null || controller.Faction != targetFaction)
                return false;
            return ReplicatorQueenUtility.HasSovereignDirectiveAuthority(controller);
        }

        public override void CompTick()
        {
            base.CompTick();

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
                return;

            TickMovementAudio(pawn);

            if ((Find.TickManager.TicksGame + pawn.thingIDNumber) % CheckIntervalTicks != 0)
                return;

            if (sovereignController != null && !HasActiveSovereignBinding())
                sovereignController = null;

            if (pawn.Faction != Faction.OfPlayer)
            {
                uncontrolledTicks = 0;
                return;
            }

            if (pawn.IsColonyMechPlayerControlled
                || ReplicatorQueenUtility.HasSovereignForFaction(pawn.Map, pawn.Faction)
                || HasActiveSovereignBinding()
                || ReplicatorLatticeOverrideUtility.IsTemporarilyOverridden(pawn))
            {
                uncontrolledTicks = 0;
                return;
            }

            uncontrolledTicks += CheckIntervalTicks;
            if (uncontrolledTicks < WNG_Config.ReplicatorFeralDelayTicks)
                return;

            FactionDef swarmDef = DefDatabase<FactionDef>.GetNamedSilentFail("WNG_ReplicatorSwarm");
            Faction swarm = swarmDef == null ? null : Find.FactionManager.FirstFactionOfDef(swarmDef);
            if (swarm == null)
                return;

            uncontrolledTicks = 0;
            sovereignController = null;
            pawn.SetFaction(swarm, null);
            Messages.Message("WNG_ReplicatorGoneFeral".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.ThreatSmall, historical: true);
        }

        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            base.PostPreApplyDamage(ref dinfo, out absorbed);

            Pawn pawn = Pawn;
            Pawn aggressor = dinfo.Instigator as Pawn;
            if (pawn == null || aggressor == null || pawn.Map == null || pawn.Dead)
                return;
            if (pawn.Faction == Faction.OfPlayer || pawn.IsColonyMechPlayerControlled)
                return;
            if (aggressor == pawn || aggressor.Faction == pawn.Faction)
                return;

            int now = Find.TickManager.TicksGame;
            recentAggressor = aggressor;
            selfDefenseUntilTick = now + SelfDefenseTicks;

            if (now - lastThreatBroadcastTick >= ThreatBroadcastCooldownTicks)
            {
                pawn.Map.GetComponent<MapComponent_ReplicatorThreatResponse>()?.RegisterAttack(pawn, aggressor);
                lastThreatBroadcastTick = now;
            }
        }

        public bool TryGetSelfDefenseTarget(out Pawn aggressor)
        {
            aggressor = recentAggressor;
            Pawn pawn = Pawn;
            if (pawn == null || aggressor == null || Find.TickManager.TicksGame > selfDefenseUntilTick)
                return false;
            if (aggressor.Dead || !aggressor.Spawned || aggressor.Map != pawn.Map)
                return false;
            if (aggressor.Faction == pawn.Faction)
                return false;
            return pawn.CanReach(aggressor, Verse.AI.PathEndMode.Touch, Danger.Deadly);
        }

        private static void ResolveMovementSounds()
        {
            if (movementSoundsResolved)
                return;

            movementStepElectric = DefDatabase<SoundDef>.GetNamedSilentFail("WNG_ReplicatorStepElectric");
            movementStepMetalA = DefDatabase<SoundDef>.GetNamedSilentFail("WNG_ReplicatorStepMetalA");
            movementStepMetalB = DefDatabase<SoundDef>.GetNamedSilentFail("WNG_ReplicatorStepMetalB");
            movementSoundsResolved = true;
        }

        private void TickMovementAudio(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;
            if (pawn.pather == null || !pawn.pather.MovingNow)
            {
                if (nextMovementSoundTick < now)
                    nextMovementSoundTick = now + 4;
                return;
            }

            if (now < nextMovementSoundTick)
                return;

            ResolveMovementSounds();

            SoundDef sound;
            float roll = Rand.Value;
            if (roll < 0.25f)
                sound = movementStepElectric;
            else if (roll < 0.625f)
                sound = movementStepMetalA;
            else
                sound = movementStepMetalB;

            if (sound != null && pawn.Map != null)
                sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

            nextMovementSoundTick = now + Rand.RangeInclusive(7, 12);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref uncontrolledTicks, "wngReplicatorUncontrolledTicks", 0);
            Scribe_Values.Look(ref nextMovementSoundTick, "wngReplicatorNextMoveSoundTick", 0);
            Scribe_Values.Look(ref selfDefenseUntilTick, "wngReplicatorSelfDefenseUntilTick", 0);
            Scribe_Values.Look(ref lastThreatBroadcastTick, "wngReplicatorLastThreatBroadcastTick", -999999);
            Scribe_References.Look(ref recentAggressor, "wngReplicatorRecentAggressor");
            Scribe_References.Look(ref sovereignController, "wngReplicatorSovereignController");
        }
    }
}
