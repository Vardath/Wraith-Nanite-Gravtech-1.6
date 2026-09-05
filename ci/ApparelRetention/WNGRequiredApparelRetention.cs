using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class MapComponent_WNGRequiredApparelRetention : MapComponent
    {
        private const int ScanIntervalTicks = 15;
        private const int SpawnRetentionTicks = 600;
        private readonly Dictionary<int, int> firstSeenTickByPawnId = new Dictionary<int, int>();
        private readonly HashSet<int> completedPawnIds = new HashSet<int>();

        public MapComponent_WNGRequiredApparelRetention(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now % ScanIntervalTicks != 0) return;

            IReadOnlyList<Pawn> pawns = map.mapPawns?.AllPawnsSpawned;
            if (pawns == null || pawns.Count == 0) return;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || completedPawnIds.Contains(pawn.thingIDNumber)) continue;

                if (!IsRelevantWngPawn(pawn))
                {
                    completedPawnIds.Add(pawn.thingIDNumber);
                    firstSeenTickByPawnId.Remove(pawn.thingIDNumber);
                    continue;
                }

                if (!firstSeenTickByPawnId.TryGetValue(pawn.thingIDNumber, out int firstSeenTick))
                {
                    firstSeenTick = now;
                    firstSeenTickByPawnId[pawn.thingIDNumber] = firstSeenTick;
                }

                ReconcilePawn(pawn);

                if (now - firstSeenTick >= SpawnRetentionTicks)
                {
                    completedPawnIds.Add(pawn.thingIDNumber);
                    firstSeenTickByPawnId.Remove(pawn.thingIDNumber);
                }
            }
        }

        private static bool IsRelevantWngPawn(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.apparel == null) return false;
            string kindName = pawn.kindDef?.defName;
            if (string.IsNullOrEmpty(kindName) || !kindName.StartsWith("WNG_", StringComparison.Ordinal)) return false;
            List<ThingDef> required = pawn.kindDef.apparelRequired;
            return required != null && required.Count > 0;
        }

        private static void ReconcilePawn(Pawn pawn)
        {
            List<ThingDef> required = pawn.kindDef.apparelRequired;
            for (int i = 0; i < required.Count; i++)
            {
                ThingDef requiredDef = required[i];
                if (requiredDef == null) continue;

                Apparel worn = FindWorn(pawn, requiredDef);
                if (worn == null)
                {
                    worn = FindJustDroppedRequiredApparel(pawn, requiredDef);
                    if (worn != null)
                    {
                        try { pawn.apparel.Wear(worn, true); }
                        catch (Exception ex)
                        {
                            Log.Warning("[WNG] Failed to restore required spawn apparel " + requiredDef.defName + " to " + pawn.LabelShortCap + ": " + ex.Message);
                            continue;
                        }
                    }
                }

                if (worn != null) MarkForcedWhenPossible(pawn, worn);
            }
        }

        private static Apparel FindWorn(Pawn pawn, ThingDef requiredDef)
        {
            List<Apparel> worn = pawn.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++) if (worn[i]?.def == requiredDef) return worn[i];
            return null;
        }

        private static Apparel FindJustDroppedRequiredApparel(Pawn pawn, ThingDef requiredDef)
        {
            IntVec3 center = pawn.Position;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 2.9f, true))
            {
                if (!cell.InBounds(pawn.Map)) continue;
                List<Thing> things = cell.GetThingList(pawn.Map);
                for (int i = 0; i < things.Count; i++)
                    if (things[i] is Apparel apparel && apparel.def == requiredDef && apparel.Wearer == null) return apparel;
            }
            return null;
        }

        private static void MarkForcedWhenPossible(Pawn pawn, Apparel apparel)
        {
            try { pawn.outfits?.forcedHandler?.SetForced(apparel, true); }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Failed to mark required spawn apparel as forced for " + pawn.LabelShortCap + ": " + ex.Message);
            }
        }
    }
}
