using System;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_ReplicatorLatticeOverride : CompProperties
    {
        public CompProperties_ReplicatorLatticeOverride()
        {
            compClass = typeof(CompReplicatorLatticeOverride);
        }
    }

    /// <summary>
    /// Save-safe temporary control of a Replicator lattice. The override records the faction that
    /// owned the pawn before control was applied and restores it when the timer expires. It is not
    /// a permanent conversion path and it never erases the pawn's Replicator identity/state.
    /// </summary>
    public sealed class CompReplicatorLatticeOverride : ThingComp
    {
        private int overrideUntilTick;
        private Faction originalFaction;
        private Faction controllingFaction;

        public bool Active
        {
            get
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                return overrideUntilTick > now && controllingFaction != null;
            }
        }

        public int RemainingTicks
        {
            get
            {
                int now = Find.TickManager?.TicksGame ?? 0;
                return Math.Max(0, overrideUntilTick - now);
            }
        }

        public void ApplyTemporaryOverride(Faction controller, int ticks)
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || controller == null || ticks <= 0)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            long wantedLong = (long)now + ticks;
            int wanted = wantedLong >= int.MaxValue ? int.MaxValue : (int)wantedLong;

            if (!Active)
                originalFaction = pawn.Faction;

            controllingFaction = controller;
            overrideUntilTick = Math.Max(overrideUntilTick, wanted);

            if (pawn.Faction != controller)
                pawn.SetFaction(controller, null);
        }

        public void CopyTemporaryOverrideTo(Pawn child)
        {
            if (!Active || child == null)
                return;

            CompReplicatorLatticeOverride childComp = child.TryGetComp<CompReplicatorLatticeOverride>();
            if (childComp == null)
                return;

            childComp.originalFaction = originalFaction ?? child.Faction;
            childComp.controllingFaction = controllingFaction;
            childComp.overrideUntilTick = overrideUntilTick;
            if (controllingFaction != null && child.Faction != controllingFaction)
                child.SetFaction(controllingFaction, null);
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = parent as Pawn;
            if (pawn == null || overrideUntilTick <= 0)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < overrideUntilTick)
                return;

            RestoreOriginalFaction();
        }

        private void RestoreOriginalFaction()
        {
            Pawn pawn = parent as Pawn;
            Faction restore = originalFaction;

            overrideUntilTick = 0;
            controllingFaction = null;
            originalFaction = null;

            if (pawn != null && !pawn.Destroyed && restore != null && pawn.Faction != restore)
                pawn.SetFaction(restore, null);
        }

        public override string CompInspectStringExtra()
        {
            if (!Active)
                return null;

            return "Temporary lattice override: " + RemainingTicks.ToStringTicksToPeriod();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref overrideUntilTick, "wngReplicatorLatticeOverrideUntil", 0);
            Scribe_References.Look(ref originalFaction, "wngReplicatorLatticeOriginalFaction");
            Scribe_References.Look(ref controllingFaction, "wngReplicatorLatticeControllingFaction");

            if (Scribe.mode == LoadSaveMode.PostLoadInit && overrideUntilTick < 0)
                overrideUntilTick = 0;
        }
    }

    public static class ReplicatorLatticeOverrideUtility
    {
        public static bool IsTemporarilyOverridden(Pawn pawn)
        {
            return pawn?.TryGetComp<CompReplicatorLatticeOverride>()?.Active == true;
        }

        public static bool TryApply(Pawn pawn, Faction controller, int ticks)
        {
            CompReplicatorLatticeOverride comp = pawn?.TryGetComp<CompReplicatorLatticeOverride>();
            if (comp == null || controller == null || ticks <= 0)
                return false;

            comp.ApplyTemporaryOverride(controller, ticks);
            return true;
        }
    }
}
