using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_GoauldTransportRings : CompProperties
    {
        public int cooldownTicks = 300;
        public int arrivalRadius = 4;
        public bool requirePower = true;

        public CompProperties_GoauldTransportRings()
        {
            compClass = typeof(CompGoauldTransportRings);
        }
    }

    /// <summary>
    /// Goa'uld transport-ring endpoint layered on RimWorld's native CompTransporter. The native
    /// transporter owns loading assignment and exact Thing storage. WNG only selects another ring
    /// endpoint and rematerializes those exact Things on the destination map.
    /// </summary>
    public sealed class CompGoauldTransportRings : ThingComp
    {
        private int nextReadyTick;

        private CompProperties_GoauldTransportRings Props => (CompProperties_GoauldTransportRings)props;
        private CompTransporter Transporter => parent.TryGetComp<CompTransporter>();
        private CompPowerTrader Power => parent.TryGetComp<CompPowerTrader>();

        public bool Powered => !Props.requirePower || (Power != null && Power.PowerOn);
        public bool Ready => Find.TickManager.TicksGame >= nextReadyTick;

        public bool ReceiveReadyFor(CompGoauldTransportRings sender)
        {
            if (sender == null || sender == this || parent?.Spawned != true || !Powered || !Ready)
                return false;
            if (parent.Faction != sender.parent.Faction)
                return false;

            CompTransporter receiverTransporter = Transporter;
            if (receiverTransporter == null)
                return false;

            // Never materialize into a ring platform which is itself holding an outbound load.
            return !receiverTransporter.innerContainer.Any && !receiverTransporter.AnythingLeftToLoad;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
                yield return gizmo;

            if (parent?.Faction != Faction.OfPlayer)
                yield break;

            CompTransporter transporter = Transporter;
            Command_Action activate = new Command_Action
            {
                defaultLabel = "Activate transport rings",
                defaultDesc = "Select another powered Goa'uld ring platform and transport the exact loaded people, animals, mechs and cargo to it. No Stargate dialing is required.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip", reportFailure: false),
                action = OpenDestinationMenu
            };

            if (transporter == null)
                activate.Disable("Transporter component unavailable.");
            else if (!Powered)
                activate.Disable("Transport rings require power.");
            else if (!Ready)
                activate.Disable($"Transport rings are recharging for {(nextReadyTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()}.");
            else if (!transporter.innerContainer.Any)
                activate.Disable("Load people, animals, mechs or cargo into the transport rings first.");
            else if (transporter.AnythingLeftToLoad)
                activate.Disable("Finish loading the assigned transport-ring cargo first.");
            else if (transporter.OverMassCapacity)
                activate.Disable("The loaded transport mass exceeds ring capacity.");
            else if (!FindDestinations().Any())
                activate.Disable("No other compatible powered transport rings are available on currently loaded maps.");

            yield return activate;
        }

        private void OpenDestinationMenu()
        {
            List<CompGoauldTransportRings> destinations = FindDestinations();
            if (destinations.Count == 0)
            {
                Messages.Message("No compatible powered transport-ring destination is available on a currently loaded map.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (CompGoauldTransportRings destination in destinations)
            {
                CompGoauldTransportRings captured = destination;
                options.Add(new FloatMenuOption(DestinationLabel(captured), () => TryTransportTo(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private List<CompGoauldTransportRings> FindDestinations()
        {
            List<CompGoauldTransportRings> result = new List<CompGoauldTransportRings>();
            foreach (Map map in Find.Maps)
            {
                if (map == null)
                    continue;

                List<Thing> allThings = map.listerThings?.AllThings;
                if (allThings == null)
                    continue;

                for (int i = 0; i < allThings.Count; i++)
                {
                    CompGoauldTransportRings rings = allThings[i]?.TryGetComp<CompGoauldTransportRings>();
                    if (rings != null && rings.ReceiveReadyFor(this))
                        result.Add(rings);
                }
            }

            return result
                .OrderBy(r => MapLabel(r.parent.Map), StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.parent.Position.x)
                .ThenBy(r => r.parent.Position.z)
                .ToList();
        }

        private static string DestinationLabel(CompGoauldTransportRings rings)
        {
            Map map = rings.parent.Map;
            string sameMap = map == Find.CurrentMap ? " (current map)" : string.Empty;
            return $"{MapLabel(map)}{sameMap} — rings at {rings.parent.Position.x}, {rings.parent.Position.z}";
        }

        private static string MapLabel(Map map)
        {
            if (map == null)
                return "Unknown map";
            string label = map.info?.parent?.Label;
            return label.NullOrEmpty() ? $"Map {map.uniqueID}" : label;
        }

        private void TryTransportTo(CompGoauldTransportRings destination)
        {
            CompTransporter transporter = Transporter;
            if (transporter == null || !parent.Spawned || !Powered || !Ready)
                return;
            if (transporter.AnythingLeftToLoad || !transporter.innerContainer.Any || transporter.OverMassCapacity)
                return;
            if (destination == null || !destination.ReceiveReadyFor(this))
            {
                Messages.Message("That transport-ring destination is no longer available.", parent, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Map destinationMap = destination.parent.Map;
            IntVec3 destinationCenter = destination.parent.Position;
            Room destinationRoom = destinationCenter.GetRoom(destinationMap);
            int radius = Math.Max(1, Props.arrivalRadius);

            Predicate<IntVec3> validator = cell =>
                cell.InBounds(destinationMap) &&
                cell.DistanceToSquared(destinationCenter) <= radius * radius &&
                (destinationRoom == null || cell.GetRoom(destinationMap) == destinationRoom) &&
                cell.Walkable(destinationMap);

            List<Thing> contents = transporter.innerContainer.ToList();
            int movedThings = 0;
            int movedStackCount = 0;
            bool placementFailed = false;

            SoundDefOf.Psycast_Skip_Entry.PlayOneShot(new TargetInfo(parent.Position, parent.Map));

            for (int i = contents.Count - 1; i >= 0; i--)
            {
                Thing thing = contents[i];
                if (thing == null || !transporter.innerContainer.Contains(thing))
                    continue;

                int originalCount = thing.stackCount;
                if (!transporter.innerContainer.TryDrop(
                        thing,
                        destinationCenter,
                        destinationMap,
                        ThingPlaceMode.Near,
                        originalCount,
                        out Thing resultingThing,
                        null,
                        validator))
                {
                    placementFailed = true;
                    break;
                }

                transporter.Notify_ThingRemoved(thing);
                movedThings++;
                movedStackCount += originalCount;

                if (resultingThing is Pawn pawn)
                    pawn.Notify_Teleported();
            }

            if (movedThings > 0)
            {
                int now = Find.TickManager.TicksGame;
                nextReadyTick = now + Math.Max(1, Props.cooldownTicks);
                destination.nextReadyTick = now + Math.Max(1, destination.Props.cooldownTicks);
                SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(destinationCenter, destinationMap));
            }

            if (!transporter.innerContainer.Any)
            {
                // Native reset of group ID / loading assignment. With an empty ThingOwner this
                // cannot unload anything back onto the source map.
                transporter.CancelLoad();
            }

            if (placementFailed)
            {
                Messages.Message(
                    $"Transport rings moved {movedThings} loaded entr{(movedThings == 1 ? "y" : "ies")} ({movedStackCount} total units), but the receiving ring area became obstructed. Remaining exact cargo has stayed loaded at the sender.",
                    parent,
                    MessageTypeDefOf.CautionInput,
                    historical: false);
            }
            else if (movedThings > 0)
            {
                Messages.Message(
                    $"Transport complete to {DestinationLabel(destination)}: {movedThings} loaded entr{(movedThings == 1 ? "y" : "ies")} rematerialized.",
                    destination.parent,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (!Powered)
                return "Transport rings: unpowered";
            if (!Ready)
                return $"Transport rings: recharging ({(nextReadyTick - Find.TickManager.TicksGame).ToStringTicksToPeriod()})";
            return "Transport rings: ready";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextReadyTick, "wngRingNextReadyTick", 0);
        }
    }
}
