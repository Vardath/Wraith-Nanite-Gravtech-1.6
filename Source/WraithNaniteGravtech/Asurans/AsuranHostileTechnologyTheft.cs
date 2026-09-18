using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    internal static class AsuranHostileTechnologyTheftUtility
    {
        public const string HostileFactionDefName = "WNG_PrecursorCollective";
        public const string PatternJobDefName = "WNG_AsuranStealPattern";
        public const string ModuleJobDefName = "WNG_AsuranRecoverVacuumModule";
        public const string VacuumModuleDefName = "WNG_VacuumEnergyModule";
        public const string VacuumTapDefName = "WNG_VacuumEnergyTap";
        public const int PatternScanTicks = 1200;
        public const int MaxPatternUsersPerEncounter = 2;

        public static bool IsHostileAsuran(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   pawn.Spawned &&
                   pawn.Faction != null &&
                   pawn.Faction.def?.defName == HostileFactionDefName &&
                   pawn.Faction.HostileTo(Faction.OfPlayer) &&
                   AsuranCollectiveUtility.IsLinked(pawn);
        }

        public static bool IsTechnologyTheftJob(Pawn pawn)
        {
            string defName = pawn?.CurJobDef?.defName;
            return defName == PatternJobDefName || defName == ModuleJobDefName;
        }

        public static void NotifyPatternCompleted(Pawn actor, Thing target)
        {
            Current.Game?.GetComponent<GameComponent_AsuranHostileTechnologyTheft>()
                ?.CommitPatternTheft(actor, target);
        }

        public static void NotifyModuleSecured(Pawn actor)
        {
            Current.Game?.GetComponent<GameComponent_AsuranHostileTechnologyTheft>()
                ?.CommitModuleObjective(actor);
        }

        public static bool IsRecoverableVacuumTarget(Pawn actor, Thing target)
        {
            if (actor?.Map == null ||
                target == null ||
                target.Destroyed ||
                !target.Spawned ||
                target.Map != actor.Map ||
                actor.Map.areaManager?.Home == null ||
                !actor.Map.areaManager.Home[target.Position])
            {
                return false;
            }

            if (target.def?.defName == VacuumModuleDefName)
                return target.stackCount > 0;

            if (target.def?.defName != VacuumTapDefName || target.Faction != Faction.OfPlayer)
                return false;

            CompRefuelable fuel = target.TryGetComp<CompRefuelable>();
            return fuel != null && fuel.Fuel >= 0.999f;
        }
    }

    /// <summary>
    /// Persistent Lattice Collective intelligence. During one hostile presence on a player-home map,
    /// Asurans prioritise physical recovery of one full vacuum-energy module; otherwise they may
    /// complete one interruptible technology-pattern scan. Stolen pattern knowledge survives between
    /// encounters and can appear on at most two newly arriving hostile Asurans in a later presence.
    /// </summary>
    public sealed class GameComponent_AsuranHostileTechnologyTheft : GameComponent
    {
        private int nextTick;
        private List<string> stolenPatternIds = new List<string>();
        private List<int> seenHostilePawnIds = new List<int>();
        private List<int> patternEquippedPawnIds = new List<int>();
        private List<int> patternCompletedMapIds = new List<int>();
        private List<int> moduleCompletedMapIds = new List<int>();
        private List<int> patternWarningMapIds = new List<int>();
        private List<int> moduleWarningMapIds = new List<int>();
        private List<int> deploymentWarningMapIds = new List<int>();

        public GameComponent_AsuranHostileTechnologyTheft(Game game) { }

        public override void GameComponentTick()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextTick)
                return;
            nextTick = SafeFutureTick(now, 300);

            foreach (Map map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome)
                    continue;

                List<Pawn> hostileAsurans = map.mapPawns.AllPawnsSpawned
                    .Where(AsuranHostileTechnologyTheftUtility.IsHostileAsuran)
                    .OrderBy(p => p.thingIDNumber)
                    .ToList();

                if (hostileAsurans.Count == 0)
                {
                    ResetEncounter(map.uniqueID);
                    continue;
                }

                PrepareLaterRaidLoadouts(map, hostileAsurans);

                if (TryMaintainOrAssignModuleObjective(map, hostileAsurans))
                    continue;

                TryMaintainOrAssignPatternTheft(map, hostileAsurans);
            }

            TrimPersistentPawnLists();
        }

        private void PrepareLaterRaidLoadouts(Map map, List<Pawn> hostileAsurans)
        {
            int alreadyEquipped = hostileAsurans.Count(p => patternEquippedPawnIds.Contains(p.thingIDNumber));

            foreach (Pawn pawn in hostileAsurans)
            {
                bool firstSeen = !seenHostilePawnIds.Contains(pawn.thingIDNumber);
                if (firstSeen)
                    seenHostilePawnIds.Add(pawn.thingIDNumber);

                if (!firstSeen ||
                    stolenPatternIds.Count == 0 ||
                    alreadyEquipped >= AsuranHostileTechnologyTheftUtility.MaxPatternUsersPerEncounter)
                {
                    continue;
                }

                if (TryEquipStolenPattern(pawn, out AsuranTechnologyPatternSpec spec))
                {
                    patternEquippedPawnIds.Add(pawn.thingIDNumber);
                    alreadyEquipped++;
                    TryAnnounceDeployment(map, pawn, spec);
                }
            }
        }

        private bool TryMaintainOrAssignModuleObjective(Map map, List<Pawn> hostileAsurans)
        {
            if (moduleCompletedMapIds.Contains(map.uniqueID))
                return false;

            Pawn active = hostileAsurans.FirstOrDefault(
                p => p.CurJobDef?.defName == AsuranHostileTechnologyTheftUtility.ModuleJobDefName);
            if (active != null)
            {
                TryAnnounceModuleObjective(map, active.CurJob?.targetA.Thing);
                return true;
            }

            Pawn carrier = hostileAsurans.FirstOrDefault(
                p => p.carryTracker?.CarriedThing?.def?.defName == AsuranHostileTechnologyTheftUtility.VacuumModuleDefName);
            if (carrier != null)
            {
                CommitModuleObjective(carrier);
                return true;
            }

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(
                AsuranHostileTechnologyTheftUtility.ModuleJobDefName);
            if (jobDef == null)
                return false;

            foreach (Pawn pawn in hostileAsurans.Where(CanReceiveTheftOrder))
            {
                Thing target = FindBestRecoverableVacuumTarget(pawn);
                if (target == null)
                    continue;

                Job job = new Job(jobDef, target)
                {
                    expiryInterval = 3600,
                    count = 1
                };

                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                if (pawn.CurJobDef == jobDef && pawn.CurJob?.targetA.Thing == target)
                {
                    TryAnnounceModuleObjective(map, target);
                    return true;
                }
            }

            return false;
        }

        private void TryMaintainOrAssignPatternTheft(Map map, List<Pawn> hostileAsurans)
        {
            if (patternCompletedMapIds.Contains(map.uniqueID) ||
                stolenPatternIds.Count >= AsuranTechnologyPatternUtility.Specs.Count)
            {
                return;
            }

            Pawn active = hostileAsurans.FirstOrDefault(
                p => p.CurJobDef?.defName == AsuranHostileTechnologyTheftUtility.PatternJobDefName);
            if (active != null)
            {
                TryAnnouncePatternObjective(map, active.CurJob?.targetA.Thing);
                return;
            }

            JobDef jobDef = DefDatabase<JobDef>.GetNamedSilentFail(
                AsuranHostileTechnologyTheftUtility.PatternJobDefName);
            if (jobDef == null)
                return;

            foreach (Pawn pawn in hostileAsurans.Where(CanReceiveTheftOrder))
            {
                Thing target = FindBestPatternTarget(pawn);
                if (target == null)
                    continue;

                Job job = new Job(jobDef, target)
                {
                    expiryInterval = 3600,
                    count = 1
                };

                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                if (pawn.CurJobDef == jobDef && pawn.CurJob?.targetA.Thing == target)
                {
                    TryAnnouncePatternObjective(map, target);
                    return;
                }
            }
        }

        private static bool CanReceiveTheftOrder(Pawn pawn)
        {
            return pawn != null &&
                   !pawn.Dead &&
                   !pawn.Downed &&
                   pawn.Spawned &&
                   pawn.jobs != null &&
                   !AsuranHostileTechnologyTheftUtility.IsTechnologyTheftJob(pawn);
        }

        private Thing FindBestRecoverableVacuumTarget(Pawn pawn)
        {
            if (pawn?.Map == null)
                return null;

            Thing best = null;
            int bestDistance = int.MaxValue;

            ThingDef moduleDef = DefDatabase<ThingDef>.GetNamedSilentFail(
                AsuranHostileTechnologyTheftUtility.VacuumModuleDefName);
            if (moduleDef != null)
            {
                foreach (Thing thing in pawn.Map.listerThings.ThingsOfDef(moduleDef))
                {
                    if (!AsuranHostileTechnologyTheftUtility.IsRecoverableVacuumTarget(pawn, thing) ||
                        !pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly))
                    {
                        continue;
                    }

                    int distance = thing.Position.DistanceToSquared(pawn.Position);
                    if (distance < bestDistance)
                    {
                        best = thing;
                        bestDistance = distance;
                    }
                }
            }

            ThingDef tapDef = DefDatabase<ThingDef>.GetNamedSilentFail(
                AsuranHostileTechnologyTheftUtility.VacuumTapDefName);
            if (tapDef != null)
            {
                foreach (Thing tap in pawn.Map.listerThings.ThingsOfDef(tapDef))
                {
                    if (!AsuranHostileTechnologyTheftUtility.IsRecoverableVacuumTarget(pawn, tap) ||
                        !pawn.CanReach(tap, PathEndMode.Touch, Danger.Deadly))
                    {
                        continue;
                    }

                    int distance = tap.Position.DistanceToSquared(pawn.Position);
                    if (best == null || distance < bestDistance)
                    {
                        best = tap;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private Thing FindBestPatternTarget(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Map.areaManager?.Home == null)
                return null;

            Thing best = null;
            float bestValue = float.MinValue;
            int bestDistance = int.MaxValue;

            foreach (AsuranTechnologyPatternSpec spec in AsuranTechnologyPatternUtility.Specs)
            {
                if (stolenPatternIds.Contains(spec.PatternId))
                    continue;

                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(spec.SpecimenDefName);
                if (def == null)
                    continue;

                foreach (Thing thing in pawn.Map.listerThings.ThingsOfDef(def))
                {
                    if (thing == null ||
                        thing.Destroyed ||
                        !thing.Spawned ||
                        !pawn.Map.areaManager.Home[thing.Position] ||
                        !pawn.CanReach(thing, PathEndMode.Touch, Danger.Deadly))
                    {
                        continue;
                    }

                    float value = Math.Max(0f, thing.MarketValue);
                    int distance = thing.Position.DistanceToSquared(pawn.Position);
                    if (value > bestValue ||
                        (Math.Abs(value - bestValue) < 0.01f && distance < bestDistance))
                    {
                        best = thing;
                        bestValue = value;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        internal void CommitPatternTheft(Pawn actor, Thing target)
        {
            if (!AsuranHostileTechnologyTheftUtility.IsHostileAsuran(actor) ||
                actor.Map == null ||
                !actor.Map.IsPlayerHome ||
                target == null ||
                target.Destroyed ||
                !target.Spawned ||
                target.Map != actor.Map ||
                actor.Map.areaManager?.Home == null ||
                !actor.Map.areaManager.Home[target.Position])
            {
                return;
            }

            if (!AsuranTechnologyPatternUtility.TryPatternForSpecimen(
                    target.def, out AsuranTechnologyPatternSpec spec) ||
                stolenPatternIds.Contains(spec.PatternId))
            {
                return;
            }

            stolenPatternIds.Add(spec.PatternId);
            if (!patternCompletedMapIds.Contains(actor.Map.uniqueID))
                patternCompletedMapIds.Add(actor.Map.uniqueID);

            try
            {
                DefDatabase<SoundDef>.GetNamedSilentFail("WNG_NaniteCopyComplete")
                    ?.PlayOneShot(new TargetInfo(target.Position, actor.Map));
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Hostile Asuran pattern theft committed but completion audio failed: " + ex.Message);
            }

            try
            {
                Find.LetterStack.ReceiveLetter(
                    "Technology pattern stolen",
                    "The Lattice Collective completed a non-destructive scan of your " + spec.Label +
                    ". Future Asuran forces can reconstruct a bounded number of copies from the stolen pattern.",
                    LetterDefOf.ThreatBig,
                    target);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Hostile Asuran pattern theft committed but presentation failed: " + ex.Message);
            }
        }

        internal void CommitModuleObjective(Pawn actor)
        {
            if (!AsuranHostileTechnologyTheftUtility.IsHostileAsuran(actor) || actor.Map == null)
                return;

            Thing carried = actor.carryTracker?.CarriedThing;
            if (carried?.def?.defName != AsuranHostileTechnologyTheftUtility.VacuumModuleDefName)
                return;

            int mapId = actor.Map.uniqueID;
            if (!moduleCompletedMapIds.Contains(mapId))
                moduleCompletedMapIds.Add(mapId);

            try
            {
                Find.LetterStack.ReceiveLetter(
                    "Vacuum-energy module seized",
                    "A Lattice Collective operative has secured a finite vacuum-energy module. If the carrier escapes the map, the physical module leaves with that pawn; killing or downing the carrier can still recover it.",
                    LetterDefOf.ThreatBig,
                    actor);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Vacuum-module seizure committed but presentation failed: " + ex.Message);
            }
        }

        private bool TryEquipStolenPattern(Pawn pawn, out AsuranTechnologyPatternSpec deployedSpec)
        {
            deployedSpec = null;
            if (pawn == null || stolenPatternIds.Count == 0)
                return false;

            List<AsuranTechnologyPatternSpec> available = AsuranTechnologyPatternUtility.Specs
                .Where(spec => stolenPatternIds.Contains(spec.PatternId))
                .ToList();
            if (available.Count == 0)
                return false;

            int start = (int)(Math.Abs((long)pawn.thingIDNumber) % available.Count);
            for (int offset = 0; offset < available.Count; offset++)
            {
                AsuranTechnologyPatternSpec spec = available[(start + offset) % available.Count];
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(spec.SpecimenDefName);
                if (def == null)
                    continue;

                Thing item = ThingMaker.MakeThing(def);
                if (item == null)
                    continue;

                if (item is Apparel apparel && pawn.apparel != null)
                {
                    try
                    {
                        pawn.apparel.Wear(apparel, dropReplacedApparel: true);
                        deployedSpec = spec;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[WNG] Reconstructed stolen-pattern apparel could not be equipped: " + ex.Message);
                        if (!item.Destroyed && !item.Spawned)
                            item.Destroy(DestroyMode.Vanish);
                        continue;
                    }
                }

                if (item is ThingWithComps weapon && def.IsWeapon && pawn.equipment != null)
                {
                    if (TryInstallWeaponTransactionally(pawn, weapon))
                    {
                        deployedSpec = spec;
                        return true;
                    }

                    if (!item.Destroyed && !item.Spawned)
                        item.Destroy(DestroyMode.Vanish);
                    continue;
                }

                if (!item.Destroyed)
                    item.Destroy(DestroyMode.Vanish);
            }

            return false;
        }

        private static bool TryInstallWeaponTransactionally(Pawn pawn, ThingWithComps replacement)
        {
            ThingWithComps original = pawn?.equipment?.Primary;
            ThingWithComps droppedOriginal = null;

            try
            {
                if (original != null &&
                    !pawn.equipment.TryDropEquipment(original, out droppedOriginal, pawn.Position, forbid: false))
                {
                    return false;
                }

                pawn.equipment.AddEquipment(replacement);
                if (pawn.equipment.Primary != replacement)
                    throw new InvalidOperationException("replacement weapon did not become primary equipment");

                if (droppedOriginal != null && !droppedOriginal.Destroyed)
                    droppedOriginal.Destroy(DestroyMode.Vanish);

                return true;
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Stolen-pattern weapon installation rolled back: " + ex.Message);

                try
                {
                    if (pawn?.equipment?.Primary == replacement)
                    {
                        if (pawn.equipment.TryDropEquipment(
                                replacement,
                                out ThingWithComps failedReplacement,
                                pawn.Position,
                                forbid: false) &&
                            failedReplacement != null &&
                            !failedReplacement.Destroyed)
                        {
                            failedReplacement.Destroy(DestroyMode.Vanish);
                        }
                    }
                    else if (replacement != null && !replacement.Destroyed && !replacement.Spawned)
                    {
                        replacement.Destroy(DestroyMode.Vanish);
                    }
                }
                catch (Exception cleanupEx)
                {
                    Log.Warning("[WNG] Rejected stolen-pattern weapon cleanup failed: " + cleanupEx.Message);
                }

                RestoreOriginalWeapon(pawn, droppedOriginal ?? original);
                return false;
            }
        }

        private static void RestoreOriginalWeapon(Pawn pawn, ThingWithComps original)
        {
            if (pawn?.equipment == null ||
                original == null ||
                original.Destroyed ||
                pawn.equipment.Primary == original ||
                pawn.equipment.Primary != null)
            {
                return;
            }

            try
            {
                if (original.Spawned)
                    original.DeSpawn();

                pawn.equipment.AddEquipment(original);
                if (pawn.equipment.Primary == original)
                    return;
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Original Asuran weapon re-equip failed; preserving physical item: " + ex.Message);
            }

            if (!original.Destroyed && !original.Spawned && pawn.Map != null)
                GenPlace.TryPlaceThing(original, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }

        private void TryAnnouncePatternObjective(Map map, Thing target)
        {
            if (map == null ||
                target == null ||
                target.Destroyed ||
                patternWarningMapIds.Contains(map.uniqueID))
            {
                return;
            }

            AsuranTechnologyPatternUtility.TryPatternForSpecimen(
                target.def, out AsuranTechnologyPatternSpec spec);

            try
            {
                Find.LetterStack.ReceiveLetter(
                    "Asuran pattern theft detected",
                    "A Lattice Collective operative is scanning your " + (spec?.Label ?? target.LabelShort) +
                    ". The scan is interruptible: kill or down the operative, break the job, or remove the specimen before completion.",
                    LetterDefOf.ThreatBig,
                    target);
                patternWarningMapIds.Add(map.uniqueID);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Pattern-theft warning presentation failed: " + ex.Message);
            }
        }

        private void TryAnnounceModuleObjective(Map map, Thing target)
        {
            if (map == null ||
                target == null ||
                target.Destroyed ||
                moduleWarningMapIds.Contains(map.uniqueID))
            {
                return;
            }

            try
            {
                Find.LetterStack.ReceiveLetter(
                    "Asuran vacuum-module recovery",
                    "The Lattice Collective has prioritised a physical finite vacuum-energy module. The recovery job is interruptible; if a hostile operative secures the module it remains a real carried object that can still be recovered from the carrier.",
                    LetterDefOf.ThreatBig,
                    target);
                moduleWarningMapIds.Add(map.uniqueID);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Vacuum-module recovery warning presentation failed: " + ex.Message);
            }
        }

        private void TryAnnounceDeployment(Map map, Pawn pawn, AsuranTechnologyPatternSpec spec)
        {
            if (map == null ||
                pawn == null ||
                spec == null ||
                deploymentWarningMapIds.Contains(map.uniqueID))
            {
                return;
            }

            try
            {
                Find.LetterStack.ReceiveLetter(
                    "Stolen Asuran pattern deployed",
                    "The Lattice Collective has returned with a reconstructed " + spec.Label +
                    " derived from technology intelligence stolen during an earlier attack. No more than two hostile Asurans in this presence will receive reconstructed stolen-pattern equipment.",
                    LetterDefOf.ThreatBig,
                    pawn);
                deploymentWarningMapIds.Add(map.uniqueID);
            }
            catch (Exception ex)
            {
                Log.Warning("[WNG] Stolen-pattern deployment presentation failed: " + ex.Message);
            }
        }

        private void ResetEncounter(int mapId)
        {
            patternCompletedMapIds.Remove(mapId);
            moduleCompletedMapIds.Remove(mapId);
            patternWarningMapIds.Remove(mapId);
            moduleWarningMapIds.Remove(mapId);
            deploymentWarningMapIds.Remove(mapId);
        }

        private void TrimPersistentPawnLists()
        {
            if (seenHostilePawnIds.Count > 4096)
                seenHostilePawnIds.RemoveRange(0, seenHostilePawnIds.Count - 2048);
            if (patternEquippedPawnIds.Count > 4096)
                patternEquippedPawnIds.RemoveRange(0, patternEquippedPawnIds.Count - 2048);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextTick, "wngAsuranHostileTechTheftNextTick", 0);
            Scribe_Collections.Look(ref stolenPatternIds, "wngAsuranStolenTechnologyPatterns", LookMode.Value);
            Scribe_Collections.Look(ref seenHostilePawnIds, "wngAsuranTheftSeenHostiles", LookMode.Value);
            Scribe_Collections.Look(ref patternEquippedPawnIds, "wngAsuranPatternEquippedHostiles", LookMode.Value);
            Scribe_Collections.Look(ref patternCompletedMapIds, "wngAsuranPatternCompletedMaps", LookMode.Value);
            Scribe_Collections.Look(ref moduleCompletedMapIds, "wngAsuranModuleCompletedMaps", LookMode.Value);
            Scribe_Collections.Look(ref patternWarningMapIds, "wngAsuranPatternWarningMaps", LookMode.Value);
            Scribe_Collections.Look(ref moduleWarningMapIds, "wngAsuranModuleWarningMaps", LookMode.Value);
            Scribe_Collections.Look(ref deploymentWarningMapIds, "wngAsuranDeploymentWarningMaps", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                stolenPatternIds = (stolenPatternIds ?? new List<string>())
                    .Where(id => !id.NullOrEmpty() && AsuranTechnologyPatternUtility.TryPatternForId(id, out _))
                    .Distinct()
                    .ToList();
                seenHostilePawnIds = DistinctNonNegative(seenHostilePawnIds);
                patternEquippedPawnIds = DistinctNonNegative(patternEquippedPawnIds);
                patternCompletedMapIds = DistinctNonNegative(patternCompletedMapIds);
                moduleCompletedMapIds = DistinctNonNegative(moduleCompletedMapIds);
                patternWarningMapIds = DistinctNonNegative(patternWarningMapIds);
                moduleWarningMapIds = DistinctNonNegative(moduleWarningMapIds);
                deploymentWarningMapIds = DistinctNonNegative(deploymentWarningMapIds);
            }
        }

        private static List<int> DistinctNonNegative(List<int> values)
        {
            return (values ?? new List<int>()).Where(x => x >= 0).Distinct().ToList();
        }

        private static int SafeFutureTick(int now, int delay)
        {
            long result = (long)Math.Max(0, now) + Math.Max(1, delay);
            return result >= int.MaxValue ? int.MaxValue : (int)result;
        }
    }

    /// <summary>
    /// Real hostile scan job. Intelligence commits only after the vulnerable work interval completes.
    /// The target specimen is never consumed.
    /// </summary>
    public sealed class JobDriver_AsuranStealPattern : JobDriver
    {
        private Thing Target => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil begin = ToilMaker.MakeToil("WNG_BeginAsuranPatternScan");
            begin.defaultCompleteMode = ToilCompleteMode.Instant;
            begin.initAction = delegate
            {
                Pawn actor = GetActor();
                if (actor?.Map == null || Target == null || !Target.Spawned)
                    return;

                try
                {
                    DefDatabase<SoundDef>.GetNamedSilentFail("WNG_NaniteNeuralInterface")
                        ?.PlayOneShot(new TargetInfo(Target.Position, actor.Map));
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Hostile Asuran pattern scan started but audio failed: " + ex.Message);
                }
            };
            yield return begin;

            Toil work = Toils_General.Wait(
                AsuranHostileTechnologyTheftUtility.PatternScanTicks,
                TargetIndex.A);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return work;

            Toil complete = ToilMaker.MakeToil("WNG_CompleteAsuranPatternScan");
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            complete.initAction = delegate
            {
                AsuranHostileTechnologyTheftUtility.NotifyPatternCompleted(GetActor(), Target);
            };
            yield return complete;
        }
    }

    /// <summary>
    /// Physical module-recovery job. Loose modules are carried directly. A fully charged tap can
    /// surrender one complete physical module; partially depleted taps are intentionally not eligible
    /// because the current indivisible item Def cannot represent fractional remaining charge safely.
    /// </summary>
    public sealed class JobDriver_AsuranRecoverVacuumModule : JobDriver
    {
        private Thing Target => job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil work = Toils_General.Wait(240, TargetIndex.A);
            work.WithProgressBarToilDelay(TargetIndex.A);
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return work;

            Toil secure = ToilMaker.MakeToil("WNG_SecureVacuumModule");
            secure.defaultCompleteMode = ToilCompleteMode.Instant;
            secure.initAction = delegate
            {
                Pawn actor = GetActor();
                Thing target = Target;
                if (!AsuranHostileTechnologyTheftUtility.IsRecoverableVacuumTarget(actor, target))
                    return;

                Thing existingCarry = actor.carryTracker?.CarriedThing;
                if (existingCarry != null)
                    return;

                ThingDef moduleDef = DefDatabase<ThingDef>.GetNamedSilentFail(
                    AsuranHostileTechnologyTheftUtility.VacuumModuleDefName);
                if (moduleDef == null)
                    return;

                if (target.def == moduleDef)
                {
                    if (actor.carryTracker.TryStartCarry(target, 1) > 0)
                        AsuranHostileTechnologyTheftUtility.NotifyModuleSecured(actor);
                    return;
                }

                CompRefuelable fuel = target.TryGetComp<CompRefuelable>();
                if (fuel == null || fuel.Fuel < 0.999f)
                    return;

                Thing module = ThingMaker.MakeThing(moduleDef);
                float originalFuel = fuel.Fuel;
                fuel.ConsumeFuel(originalFuel);

                bool secured = false;
                try
                {
                    secured = actor.carryTracker.TryStartCarry(module, 1) > 0;
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Vacuum-module decoupling failed before physical custody: " + ex.Message);
                }

                if (!secured)
                {
                    if (module != null && !module.Destroyed)
                        module.Destroy(DestroyMode.Vanish);
                    fuel.Refuel(originalFuel);
                    return;
                }

                AsuranHostileTechnologyTheftUtility.NotifyModuleSecured(actor);
            };
            yield return secure;
        }
    }
}
