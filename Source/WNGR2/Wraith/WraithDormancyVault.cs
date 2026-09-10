using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithDormancyVault : CompProperties
    {
        public int dormantCount = 4;
        public float triggerRadius = 18f;
        public int awakenIntervalTicks = 180;
        public int awakenPerWave = 2;

        public CompProperties_WraithDormancyVault()
        {
            compClass = typeof(CompWraithDormancyVault);
        }
    }

    /// <summary>
    /// Finite sealed Wraith combat reserve for hostile mature Hives. The saved reserve only moves
    /// downward. These pawns are never demographic founders and are never replenished by cloning.
    /// </summary>
    public sealed class CompWraithDormancyVault : ThingComp
    {
        private int remainingDormants = -1;
        private int nextWakeTick = -1;
        private bool activated;
        private bool playerWarned;

        private CompProperties_WraithDormancyVault VaultProps => (CompProperties_WraithDormancyVault)props;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad && remainingDormants < 0)
                remainingDormants = Math.Max(0, VaultProps.dormantCount);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || parent.Map == null || parent.Faction == null || remainingDormants <= 0)
                return;
            if (!parent.IsHashIntervalTick(60))
                return;
            if (!parent.Faction.HostileTo(Faction.OfPlayer))
                return;

            if (!activated && TriggerConditionMet())
            {
                activated = true;
                nextWakeTick = Find.TickManager.TicksGame;
            }

            if (!activated || Find.TickManager.TicksGame < nextWakeTick)
                return;

            ReleaseWave();
            nextWakeTick = Find.TickManager.TicksGame + Math.Max(1, VaultProps.awakenIntervalTicks);
        }

        private bool TriggerConditionMet()
        {
            if (parent.HitPoints < parent.MaxHitPoints)
                return true;

            float radiusSquared = VaultProps.triggerRadius * VaultProps.triggerRadius;
            IReadOnlyList<Pawn> colonists = parent.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn pawn = colonists[i];
                if (pawn == null || pawn.Dead)
                    continue;
                if (pawn.Position.DistanceToSquared(parent.Position) <= radiusSquared)
                    return true;
            }

            return false;
        }

        private void ReleaseWave()
        {
            PawnKindDef hunter = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithHunter");
            PawnKindDef warrior = DefDatabase<PawnKindDef>.GetNamedSilentFail("WNG_WraithWarrior");
            if (hunter == null || warrior == null || parent.Map == null || parent.Faction == null)
                return;

            int waveSize = Math.Min(Math.Max(1, VaultProps.awakenPerWave), remainingDormants);
            List<Pawn> released = new List<Pawn>(waveSize);

            for (int i = 0; i < waveSize; i++)
            {
                PawnKindDef kind = ((remainingDormants + i) & 1) == 0 ? warrior : hunter;
                Pawn pawn = PawnGenerator.GeneratePawn(kind, parent.Faction);
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(parent.Position, parent.Map, 5);
                GenSpawn.Spawn(pawn, cell, parent.Map);

                if (!pawn.Spawned || pawn.Map != parent.Map)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                    continue;
                }

                remainingDormants--;
                released.Add(pawn);
            }

            if (released.Count == 0)
                return;

            Lord lord = LordMaker.MakeNewLord(
                parent.Faction,
                new LordJob_DefendBase(parent.Faction, parent.Position, 60000),
                parent.Map);
            for (int i = 0; i < released.Count; i++)
                lord.AddPawn(released[i]);

            if (!playerWarned)
            {
                playerWarned = true;
                Find.LetterStack.ReceiveLetter(
                    "Dormant Wraith awakening",
                    "A sealed Wraith reserve has begun waking in response to your intrusion.",
                    LetterDefOf.ThreatBig,
                    parent);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (remainingDormants < 0)
                return null;

            string state = activated && remainingDormants > 0 ? "awakening" : remainingDormants > 0 ? "sealed" : "empty";
            return "Dormant Wraith reserve: " + remainingDormants + " (" + state + ")";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref remainingDormants, "wngDormancyVaultRemaining", -1);
            Scribe_Values.Look(ref nextWakeTick, "wngDormancyVaultNextWake", -1);
            Scribe_Values.Look(ref activated, "wngDormancyVaultActivated", false);
            Scribe_Values.Look(ref playerWarned, "wngDormancyVaultPlayerWarned", false);
        }
    }
}
