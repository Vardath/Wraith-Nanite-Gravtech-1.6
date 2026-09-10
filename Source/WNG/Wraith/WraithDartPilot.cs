using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithDartPilot : CompProperties
    {
        public string pilotPawnKindDefName = "WNG_WraithHunter";
        public int ejectionRadius = 4;

        public CompProperties_WraithDartPilot()
        {
            compClass = typeof(CompWraithDartPilot);
        }
    }

    /// <summary>
    /// Tracks the exact Wraith piloting a hostile Dart. The pilot uses the shuttle's native
    /// CompTransporter; WNG does not create a parallel crew container or custom boarding system.
    /// </summary>
    public sealed class CompWraithDartPilot : ThingComp
    {
        private Pawn pilot;

        private CompProperties_WraithDartPilot Props => (CompProperties_WraithDartPilot)props;
        private CompTransporter Transporter => parent?.TryGetComp<CompTransporter>();

        public Pawn Pilot => pilot;

        public bool HasOperationalHostilePilot
        {
            get
            {
                if (pilot == null || pilot.Dead || parent?.Faction == null || !WraithCaptivityRegistry.IsWraithFaction(parent.Faction))
                    return false;
                return Transporter?.innerContainer?.Contains(pilot) == true;
            }
        }

        public bool EnsureHostilePilot(Faction faction)
        {
            if (parent == null || faction == null || !WraithCaptivityRegistry.IsWraithFaction(faction))
                return false;

            CompTransporter transporter = Transporter;
            if (transporter == null)
                return false;

            if (pilot != null && !pilot.Dead)
            {
                if (transporter.innerContainer.Contains(pilot))
                    return true;

                if (pilot.Spawned)
                    pilot.DeSpawn();
                if (pilot.IsWorldPawn())
                    Find.WorldPawns.RemovePawn(pilot);
                if (transporter.innerContainer.TryAdd(pilot))
                {
                    transporter.Notify_ThingAdded(pilot);
                    return true;
                }
                return false;
            }

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.pilotPawnKindDefName);
            if (kind == null)
                return false;

            Pawn generated = PawnGenerator.GeneratePawn(kind, faction);
            if (generated == null)
                return false;

            if (!transporter.innerContainer.TryAdd(generated))
            {
                generated.Destroy(DestroyMode.Vanish);
                return false;
            }

            transporter.Notify_ThingAdded(generated);
            pilot = generated;
            return true;
        }

        public void EjectPilotForCaptureOrHack()
        {
            Pawn exactPilot = pilot;
            if (exactPilot == null)
                return;

            CompTransporter transporter = Transporter;
            Map map = parent?.Map;
            IntVec3 origin = parent?.Position ?? IntVec3.Invalid;

            if (transporter?.innerContainer?.Contains(exactPilot) == true)
            {
                transporter.innerContainer.Remove(exactPilot);
                transporter.Notify_ThingRemoved(exactPilot);
            }

            if (map != null && !exactPilot.Dead && !exactPilot.Spawned)
            {
                if (exactPilot.IsWorldPawn())
                    Find.WorldPawns.RemovePawn(exactPilot);

                IntVec3 center = origin.IsValid && origin.InBounds(map) ? origin : map.Center;
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, Math.Max(1, Props.ejectionRadius));
                GenSpawn.Spawn(exactPilot, cell, map);
            }

            pilot = null;
        }

        public override void Notify_Hacked(Pawn hacker)
        {
            // The hostile pilot is not a culling captive. A captured Dart cannot silently retain
            // its enemy pilot inside the player's newly acquired shuttle.
            EjectPilotForCaptureOrHack();
            base.Notify_Hacked(hacker);
        }

        public override string CompInspectStringExtra()
        {
            if (pilot == null || pilot.Dead)
                return parent?.Faction != null && WraithCaptivityRegistry.IsWraithFaction(parent.Faction)
                    ? "Wraith pilot: none"
                    : null;
            return "Wraith pilot: " + pilot.LabelShortCap;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref pilot, "wngWraithDartPilot");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pilot != null && pilot.Dead)
                pilot = null;
        }
    }
}
