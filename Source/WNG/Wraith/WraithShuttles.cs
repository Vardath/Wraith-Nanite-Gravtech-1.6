using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_WraithDartCulling : CompProperties
    {
        public int maxCaptives = 3;
        public int stunTicks = 1200;
        public int releaseRadius = 4;

        public CompProperties_WraithDartCulling()
        {
            compClass = typeof(CompWraithDartCulling);
        }
    }

    /// <summary>
    /// Stargate-specific culling layer attached to a native RimWorld/Odyssey shuttle.
    /// Native CompShuttle/CompTransporter continue to own loading, boarding and launch behavior.
    /// This component only owns Wraith culling-beam capture continuity, hacking outcome and
    /// deterministic captive resolution.
    /// </summary>
    public sealed class CompWraithDartCulling : ThingComp
    {
        private List<Pawn> bufferedCaptives = new List<Pawn>();
        private IntVec3 lastMapPosition = IntVec3.Invalid;
        private bool nativeEscapeCommitted;

        private CompProperties_WraithDartCulling Props => (CompProperties_WraithDartCulling)props;
        private CompTransporter Transporter => parent?.GetComp<CompTransporter>();

        public int BufferedCount => bufferedCaptives?.Count(p => p != null) ?? 0;
        public int CapacityRemaining => Math.Max(0, Math.Max(1, Props.maxCaptives) - BufferedCount);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            bufferedCaptives ??= new List<Pawn>();
            if (parent?.Spawned == true)
                lastMapPosition = parent.Position;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent?.Spawned == true)
                lastMapPosition = parent.Position;
        }

        public bool TryAbsorbExact(Pawn target)
        {
            if (target == null || target.Dead || !target.Spawned || target.Map != parent?.Map)
                return false;
            if (CapacityRemaining <= 0 || Transporter == null || !WraithCaptivityRegistry.IsValidBiologicalCaptive(target))
                return false;
            if (!WraithCaptivityRegistry.IsWraithFaction(parent.Faction))
                return false;

            float targetMass = target.GetStatValue(StatDefOf.Mass);
            if (Transporter.MassUsage + targetMass > Transporter.MassCapacity)
                return false;

            Map map = target.Map;
            IntVec3 position = target.Position;
            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            if (registry?.RegisterCapturedPawn(target, parent.Faction, false) == null)
                return false;

            if (target.stances?.stunner != null)
                target.stances.stunner.StunFor(Math.Max(1, Props.stunTicks), parent);

            target.DeSpawn();
            if (!Transporter.innerContainer.TryAdd(target))
            {
                registry.ReleaseExactPawn(target);
                if (!target.Spawned)
                    GenSpawn.Spawn(target, position, map);
                return false;
            }

            if (!bufferedCaptives.Contains(target))
                bufferedCaptives.Add(target);
            return true;
        }

        /// <summary>
        /// Called by the future raid/flight controller only when the Wraith have physically
        /// boarded and are committing the same native shuttle to escape. This does not launch or
        /// destroy the shuttle; native shuttle code remains responsible for that operation.
        /// </summary>
        public void CommitNativeEscapeWithCaptives()
        {
            CompTransporter transporter = Transporter;
            if (transporter == null)
                return;

            foreach (Pawn captive in bufferedCaptives.Where(p => p != null).ToList())
            {
                if (!transporter.innerContainer.Contains(captive))
                    continue;
                transporter.innerContainer.Remove(captive);
                if (!captive.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(captive, PawnDiscardDecideMode.KeepForever);
            }

            bufferedCaptives.Clear();
            nativeEscapeCommitted = true;
        }

        public override void Notify_Hacked(Pawn hacker)
        {
            base.Notify_Hacked(hacker);
            nativeEscapeCommitted = false;
            ReleaseBufferedCaptives(parent?.Map, parent?.Position ?? lastMapPosition);
            if (Faction.OfPlayer != null && parent?.Faction != Faction.OfPlayer)
                parent.SetFaction(Faction.OfPlayer, hacker);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (!nativeEscapeCommitted)
                ReleaseBufferedCaptives(previousMap, lastMapPosition);
            base.PostDestroy(mode, previousMap);
        }

        private void ReleaseBufferedCaptives(Map map, IntVec3 origin)
        {
            if (bufferedCaptives == null || bufferedCaptives.Count == 0)
                return;

            CompTransporter transporter = Transporter;
            WraithCaptivityRegistry registry = WraithCaptivityRegistry.Current;
            List<Pawn> captives = bufferedCaptives.Where(p => p != null).Distinct().ToList();
            foreach (Pawn captive in captives)
            {
                if (transporter?.innerContainer?.Contains(captive) == true)
                    transporter.innerContainer.Remove(captive);
                registry?.ReleaseExactPawn(captive);

                if (map == null || captive.Dead)
                    continue;
                if (captive.IsWorldPawn())
                    Find.WorldPawns.RemovePawn(captive);
                if (!captive.Spawned)
                {
                    IntVec3 center = origin.IsValid && origin.InBounds(map) ? origin : CellFinderLoose.RandomCellWith(c => c.Standable(map), map);
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(center, map, Math.Max(1, Props.releaseRadius));
                    GenSpawn.Spawn(captive, cell, map);
                }
            }

            bufferedCaptives.Clear();
        }

        public override string CompInspectStringExtra()
        {
            if (BufferedCount <= 0) return null;
            return "Culling buffer: " + BufferedCount + "/" + Math.Max(1, Props.maxCaptives) + " exact captive(s)";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref bufferedCaptives, "wngDartBufferedCaptives", LookMode.Reference);
            Scribe_Values.Look(ref lastMapPosition, "wngDartLastMapPosition", IntVec3.Invalid);
            Scribe_Values.Look(ref nativeEscapeCommitted, "wngDartNativeEscapeCommitted", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                bufferedCaptives = bufferedCaptives?.Where(p => p != null).Distinct().ToList() ?? new List<Pawn>();
        }
    }
}
