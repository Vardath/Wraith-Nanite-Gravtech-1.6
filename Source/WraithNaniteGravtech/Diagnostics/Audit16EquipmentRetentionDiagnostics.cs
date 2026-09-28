using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Diagnostics
{
    public sealed class MapComponent_Audit16EquipmentRetention : MapComponent
    {
        private bool active;
        private int startedAt;
        private readonly Dictionary<int, string> requiredApparelByPawn = new Dictionary<int, string>();
        private readonly Dictionary<int, string> primaryWeaponByPawn = new Dictionary<int, string>();
        private readonly HashSet<int> observedPawnIds = new HashSet<int>();
        private readonly List<string> failures = new List<string>();

        public MapComponent_Audit16EquipmentRetention(Map map) : base(map) { }

        public void StartAudit()
        {
            active = false;
            failures.Clear();
            requiredApparelByPawn.Clear();
            primaryWeaponByPawn.Clear();
            observedPawnIds.Clear();

            if (map?.mapPawns?.AllPawnsSpawned == null)
            {
                Messages.Message("WNG Audit 16 could not start: no map pawn list.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn?.kindDef?.defName?.StartsWith("WNG_", StringComparison.Ordinal) != true)
                    continue;

                List<ThingDef> required = pawn.kindDef.apparelRequired;
                List<string> weaponTags = pawn.kindDef.weaponTags;
                if ((required == null || required.Count == 0) &&
                    (weaponTags == null || weaponTags.Count == 0))
                    continue;

                observedPawnIds.Add(pawn.thingIDNumber);

                ThingDef requiredDef = required?.FirstOrDefault(d => d != null);
                if (requiredDef != null)
                {
                    requiredApparelByPawn[pawn.thingIDNumber] = requiredDef.defName;
                    if (pawn.apparel?.WornApparel?.Any(a => a?.def == requiredDef) != true)
                        failures.Add(pawn.LabelShortCap + " starts Audit 16 without required apparel " + requiredDef.defName);
                }

                if (weaponTags != null && weaponTags.Count > 0)
                {
                    ThingWithComps primary = pawn.equipment?.Primary;
                    if (primary == null)
                    {
                        failures.Add(pawn.LabelShortCap + " starts Audit 16 without a primary signature weapon.");
                    }
                    else
                    {
                        primaryWeaponByPawn[pawn.thingIDNumber] = primary.def.defName;
                        List<string> actualTags = primary.def.weaponTags;
                        if (actualTags == null || !actualTags.Any(t => weaponTags.Contains(t)))
                            failures.Add(pawn.LabelShortCap + " primary weapon " + primary.def.defName +
                                         " does not match PawnKind weapon tags.");
                    }
                }
            }

            if (observedPawnIds.Count == 0)
            {
                Messages.Message(
                    "WNG Audit 16 needs at least one spawned WNG pawn with required apparel or weapon tags.",
                    MessageTypeDefOf.NeutralEvent,
                    false);
                Log.Message("[WNG AUDIT 16] NOT STARTED - no suitable spawned WNG pawns were present.");
                return;
            }

            startedAt = Find.TickManager?.TicksGame ?? 0;
            active = true;
            Log.Message("[WNG AUDIT 16] START - observing " + observedPawnIds.Count +
                        " spawned WNG pawn(s) for 1800 ticks.");
            Messages.Message("WNG Audit 16 started. Leave the observed WNG pawns spawned for about 30 in-game seconds.", MessageTypeDefOf.NeutralEvent, false);
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            if (!active || map == null || !map.IsHashIntervalTick(30))
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            foreach (int id in observedPawnIds.ToList())
            {
                Pawn pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p?.thingIDNumber == id);
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    failures.Add("Observed WNG pawn " + id + " disappeared/died during retention window.");
                    observedPawnIds.Remove(id);
                    continue;
                }

                if (requiredApparelByPawn.TryGetValue(id, out string apparelName))
                {
                    ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(apparelName);
                    if (apparelDef != null &&
                        pawn.apparel?.WornApparel?.Any(a => a?.def == apparelDef) != true)
                    {
                        failures.Add(pawn.LabelShortCap + " lost required apparel " + apparelName + " during retention window.");
                        requiredApparelByPawn.Remove(id);
                    }
                }

                if (primaryWeaponByPawn.TryGetValue(id, out string originalWeapon))
                {
                    ThingWithComps primary = pawn.equipment?.Primary;
                    if (primary == null)
                    {
                        failures.Add(pawn.LabelShortCap + " lost primary weapon " + originalWeapon + " during retention window.");
                        primaryWeaponByPawn.Remove(id);
                    }
                }
            }

            if (now - startedAt < 1800)
                return;

            active = false;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[WNG AUDIT 16] APPAREL / EQUIPMENT RETENTION");
            sb.AppendLine("Observed WNG pawns: " + observedPawnIds.Count);
            sb.AppendLine("Observation ticks: " + Math.Max(0, now - startedAt));

            if (failures.Count > 0)
            {
                sb.AppendLine("FAILURES:");
                foreach (string failure in failures.Distinct()) sb.AppendLine(" - " + failure);
                Log.Error(sb.ToString());
                Messages.Message("WNG Audit 16 FAILED: see Player.log.", MessageTypeDefOf.RejectInput, false);
            }
            else
            {
                sb.AppendLine("PASS: required apparel and signature weapons remained equipped through the spawn-retention window.");
                Log.Message(sb.ToString());
                Messages.Message("WNG Audit 16 PASS.", MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }

    public static class Audit16EquipmentRetentionDiagnostics
    {
        [DebugAction(
            "WNG",
            "Audit 16 - apparel / equipment retention",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            Map map = Find.CurrentMap;
            MapComponent_Audit16EquipmentRetention component =
                map?.GetComponent<MapComponent_Audit16EquipmentRetention>();
            if (component == null)
            {
                Log.Error("[WNG AUDIT 16] Runtime observation component is unavailable.");
                Messages.Message("WNG Audit 16 could not start.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            component.StartAudit();
        }
    }
}
