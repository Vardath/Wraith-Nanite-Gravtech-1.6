using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_TargetableWraithBootstrapHost : CompProperties_Targetable
    {
        public CompProperties_TargetableWraithBootstrapHost()
        {
            compClass = typeof(CompTargetable_WraithBootstrapHost);
        }
    }

    /// <summary>
    /// Targets the biological scaffold for a Wraith living-technology growth program. Living
    /// colonists, prisoners and slaves are supported, as are non-dessicated humanlike corpses.
    /// Mechanoids/animals are deliberately excluded; this is Wraith organic technology.
    /// </summary>
    public sealed class CompTargetable_WraithBootstrapHost : CompTargetable
    {
        protected override TargetingParameters GetTargetingParameters()
        {
            TargetingParameters parms = new TargetingParameters
            {
                canTargetLocations = false,
                canTargetPawns = true,
                canTargetHumans = true,
                canTargetAnimals = false,
                canTargetMechs = false,
                canTargetBuildings = false,
                canTargetItems = true,
                canTargetCorpses = true,
                canTargetEntities = false,
                validator = target => WraithBootstrapUtility.CanImplant(target.Thing)
            };
            return parms;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
                return false;

            AcceptanceReport report = WraithBootstrapUtility.CanImplantReport(target.Thing);
            if (!report.Accepted && showMessages && !report.Reason.NullOrEmpty())
                Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
            return report.Accepted;
        }
    }

    public sealed class CompProperties_WraithBootstrapImplantEffect : CompProperties
    {
        public HediffDef growthHediff;

        public CompProperties_WraithBootstrapImplantEffect()
        {
            compClass = typeof(CompTargetEffect_WraithBootstrapImplant);
        }
    }

    public sealed class CompTargetEffect_WraithBootstrapImplant : CompTargetEffect
    {
        private CompProperties_WraithBootstrapImplantEffect Props => (CompProperties_WraithBootstrapImplantEffect)props;

        public override void DoEffectOn(Pawn user, Thing target)
        {
            Pawn host = WraithBootstrapUtility.HostPawn(target);
            if (host == null || Props.growthHediff == null)
                return;

            AcceptanceReport report = WraithBootstrapUtility.CanImplantReport(target);
            if (!report.Accepted)
            {
                if (!report.Reason.NullOrEmpty())
                    Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Hediff growth = host.health.AddHediff(Props.growthHediff);
            if (growth == null)
                return;

            ConsumeOne(parent);
            string subject = target is Corpse ? host.LabelShort + "'s corpse" : host.LabelShort;
            Messages.Message($"{subject} has been implanted with {Props.growthHediff.label}.", target, MessageTypeDefOf.NeutralEvent, historical: false);
        }

        private static void ConsumeOne(Thing thing)
        {
            if (thing == null || thing.Destroyed)
                return;
            if (thing.stackCount <= 1)
            {
                thing.Destroy(DestroyMode.Vanish);
                return;
            }
            Thing one = thing.SplitOff(1);
            one?.Destroy(DestroyMode.Vanish);
        }
    }

    public sealed class HediffCompProperties_WraithBootstrapGrowth : HediffCompProperties
    {
        public int maturationTicks = 120000;
        public ThingDef outputDef;
        public int placementRadius = 6;

        public HediffCompProperties_WraithBootstrapGrowth()
        {
            compClass = typeof(HediffComp_WraithBootstrapGrowth);
        }
    }

    public sealed class HediffComp_WraithBootstrapGrowth : HediffComp
    {
        private int matureTick = -1;

        public HediffCompProperties_WraithBootstrapGrowth Props => (HediffCompProperties_WraithBootstrapGrowth)props;
        public int MatureTick => matureTick;
        public bool Mature => matureTick >= 0 && Find.TickManager.TicksGame >= matureTick;

        public override void CompPostMake()
        {
            base.CompPostMake();
            if (matureTick < 0)
                matureTick = Find.TickManager.TicksGame + Math.Max(1, Props.maturationTicks);
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                if (Mature)
                    return "ready to emerge";
                if (matureTick < 0)
                    return null;
                return Math.Max(0, matureTick - Find.TickManager.TicksGame).ToStringTicksToPeriod();
            }
        }

        public override string CompDescriptionExtra
        {
            get
            {
                if (Props.outputDef == null)
                    return null;
                return $"This Wraith growth program is using the host as a biological scaffold for {Props.outputDef.label}. If a living host dies, growth continues in the corpse. Once mature, it waits until enough nearby space exists to emerge.";
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref matureTick, "wngWraithBootstrapMatureTick", -1);
        }
    }

    public static class WraithBootstrapUtility
    {
        public static Pawn HostPawn(Thing target)
        {
            if (target is Pawn pawn)
                return pawn;
            return (target as Corpse)?.InnerPawn;
        }

        public static bool CanImplant(Thing target) => CanImplantReport(target).Accepted;

        public static AcceptanceReport CanImplantReport(Thing target)
        {
            Pawn host = HostPawn(target);
            if (host == null)
                return "Target must be a humanlike pawn or corpse.";
            if (host.RaceProps?.Humanlike != true || !host.RaceProps.IsFlesh)
                return "Wraith living technology requires a humanlike biological host.";
            if (host.IsQuestLodger())
                return "Quest lodgers cannot be used as Wraith growth hosts.";
            if (HasBootstrapGrowth(host))
                return "This host is already growing Wraith infrastructure.";

            if (target is Corpse corpse)
            {
                if (corpse.GetRotStage() == RotStage.Dessicated)
                    return "A dessicated corpse cannot support Wraith living growth.";
                return true;
            }

            if (host.Dead)
                return "Use the pawn's corpse as the growth host.";
            if (!host.IsColonistPlayerControlled && !host.IsPrisonerOfColony && !host.IsSlaveOfColony)
                return "Living hosts must be a player-controlled colonist, prisoner or slave.";
            return true;
        }

        public static bool HasBootstrapGrowth(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return false;
            return pawn.health.hediffSet.hediffs.Any(h => h.TryGetComp<HediffComp_WraithBootstrapGrowth>() != null);
        }

        public static HediffComp_WraithBootstrapGrowth GetMatureGrowth(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
                return null;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                HediffComp_WraithBootstrapGrowth comp = hediff.TryGetComp<HediffComp_WraithBootstrapGrowth>();
                if (comp?.Mature == true)
                    return comp;
            }
            return null;
        }

        public static bool TryFindEmergenceCell(Map map, IntVec3 center, ThingDef outputDef, int radius, Thing hostThing, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            if (map == null || outputDef == null)
                return false;

            Rot4 rot = outputDef.defaultPlacingRot;
            return CellFinder.TryFindRandomCellNear(center, map, Math.Max(1, radius), c =>
            {
                if (!c.InBounds(map) || c.Fogged(map))
                    return false;
                return GenConstruct.CanPlaceBlueprintAt(outputDef, c, rot, map, godMode: true, thingToIgnore: hostThing).Accepted;
            }, out result, 80);
        }

        public static bool TryEmerge(Pawn host, Thing hostThing, HediffComp_WraithBootstrapGrowth growth, Map map, IntVec3 center)
        {
            if (host == null || hostThing == null || growth?.Props.outputDef == null || map == null)
                return false;

            ThingDef outputDef = growth.Props.outputDef;
            if (!TryFindEmergenceCell(map, center, outputDef, growth.Props.placementRadius, hostThing, out IntVec3 cell))
                return false;

            Faction faction = Faction.OfPlayer;
            string hostLabel = host.LabelShort;

            if (hostThing is Corpse corpse)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }
            else
            {
                if (!host.Dead)
                    host.Kill(null);
                host.Corpse?.Destroy(DestroyMode.Vanish);
                if (host.Spawned)
                    host.Destroy(DestroyMode.Vanish);
            }

            Thing output = ThingMaker.MakeThing(outputDef);
            if (output == null)
            {
                Log.Error($"[WNG] Wraith bootstrap could not create output {outputDef.defName}.");
                return false;
            }
            if (outputDef.CanHaveFaction)
                output.SetFactionDirect(faction);
            GenSpawn.Spawn(output, cell, map, outputDef.defaultPlacingRot, WipeMode.Vanish);
            Messages.Message($"Wraith living technology has consumed {hostLabel} and matured into {outputDef.label}.", output, MessageTypeDefOf.PositiveEvent, historical: false);
            return true;
        }
    }

    public sealed class MapComponent_WraithBootstrapGrowth : MapComponent
    {
        private int nextScanTick;

        public MapComponent_WraithBootstrapGrowth(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            int now = Find.TickManager.TicksGame;
            if (now < nextScanTick)
                return;
            nextScanTick = now + 250;

            // Copy lists before emergence destroys a pawn/corpse.
            List<Pawn> pawns = map.mapPawns.AllPawnsSpawned.ToList();
            foreach (Pawn pawn in pawns)
            {
                HediffComp_WraithBootstrapGrowth growth = WraithBootstrapUtility.GetMatureGrowth(pawn);
                if (growth != null && pawn.Spawned)
                    WraithBootstrapUtility.TryEmerge(pawn, pawn, growth, map, pawn.Position);
            }

            List<Corpse> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().ToList();
            foreach (Corpse corpse in corpses)
            {
                if (!corpse.Spawned || corpse.Destroyed)
                    continue;
                Pawn inner = corpse.InnerPawn;
                HediffComp_WraithBootstrapGrowth growth = WraithBootstrapUtility.GetMatureGrowth(inner);
                if (growth != null)
                    WraithBootstrapUtility.TryEmerge(inner, corpse, growth, map, corpse.Position);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextScanTick, "wngWraithBootstrapNextScanTick", 0);
        }
    }

    public sealed class JobDriver_WraithBootstrapImplant : JobDriver
    {
        private const TargetIndex ImplantItem = TargetIndex.A;
        private const TargetIndex Host = TargetIndex.B;

        private Thing Implant => job.GetTarget(ImplantItem).Thing;
        private Thing HostThing => job.GetTarget(Host).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 1, 1, null, errorOnFailed))
                return false;
            return pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOnDestroyedOrNull(ImplantItem);
            this.FailOnDestroyedOrNull(Host);
            this.FailOn(() => Implant == null || Implant.TryGetComp<CompUsable>() == null);
            this.FailOn(() => !WraithBootstrapUtility.CanImplant(HostThing));

            yield return Toils_Goto.GotoThing(ImplantItem, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(ImplantItem);
            yield return Toils_Goto.GotoThing(Host, PathEndMode.Touch);

            int duration = Math.Max(60, Implant.TryGetComp<CompUsable>()?.Props.useDuration ?? 600);
            Toil implant = Toils_General.Wait(duration, Host);
            implant.WithProgressBarToilDelay(Host);
            implant.handlingFacing = true;
            yield return implant;

            yield return Toils_General.Do(() =>
            {
                if (Implant == null || HostThing == null || !WraithBootstrapUtility.CanImplant(HostThing))
                    return;
                Implant.TryGetComp<CompUsable>()?.UsedBy(pawn);
            });
        }
    }

    public sealed class CompProperties_WNGDeployBuilding : CompProperties
    {
        public ThingDef buildingDef;
        public string commandLabel = "Deploy";
        public string commandDescription;

        public CompProperties_WNGDeployBuilding()
        {
            compClass = typeof(CompWNGDeployBuilding);
        }
    }

    /// <summary>
    /// Direct-crafting bypass endpoint. It uses RimWorld's normal map targeter and construction
    /// placement validation; the expensive growth core was already fabricated at the bench, so
    /// deployment materializes the completed WNG structure rather than creating a second cost bill.
    /// </summary>
    public sealed class CompWNGDeployBuilding : ThingComp
    {
        private CompProperties_WNGDeployBuilding Props => (CompProperties_WNGDeployBuilding)props;

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction != Faction.OfPlayer && parent.Spawned)
                yield break;
            if (Props.buildingDef == null)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = Props.commandLabel,
                defaultDesc = Props.commandDescription ?? $"Deploy {Props.buildingDef.label}.",
                icon = parent.def.uiIcon,
                action = () => Find.Targeter.BeginTargeting(TargetingParameters.ForCell(), target => TryDeploy(target.Cell))
            };
        }

        private void TryDeploy(IntVec3 cell)
        {
            Map map = Find.CurrentMap;
            if (map == null || Props.buildingDef == null || !cell.InBounds(map))
                return;

            Rot4 rot = Props.buildingDef.defaultPlacingRot;
            AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(Props.buildingDef, cell, rot, map, godMode: true);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason.NullOrEmpty() ? "Cannot deploy the Wraith growth core there." : report.Reason, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Thing built = ThingMaker.MakeThing(Props.buildingDef);
            if (built == null)
                return;
            if (Props.buildingDef.CanHaveFaction)
                built.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(built, cell, map, rot, WipeMode.Vanish);
            ConsumeOne();
        }

        private void ConsumeOne()
        {
            if (parent.stackCount <= 1)
                parent.Destroy(DestroyMode.Vanish);
            else
                parent.SplitOff(1).Destroy(DestroyMode.Vanish);
        }
    }
}
