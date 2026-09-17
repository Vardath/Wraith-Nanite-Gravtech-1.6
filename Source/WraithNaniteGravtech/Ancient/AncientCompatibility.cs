using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public static class AncientCompatibilityUtility
    {
        public const string NaturalAffinityGeneDefName = "WNG_AncientAffinity";
        public const string ArtificialInterfaceHediffDefName = "WNG_AncientNeuralInterface";

        public static bool IsCompatible(Pawn pawn)
        {
            if (pawn == null || pawn.Dead)
                return false;
            if (AsuranCollectiveUtility.IsNaniteSynthetic(pawn))
                return true;
            return HasActiveGene(pawn, NaturalAffinityGeneDefName) || HasHediff(pawn, ArtificialInterfaceHediffDefName);
        }

        public static bool HasNaturalAffinity(Pawn pawn) => HasActiveGene(pawn, NaturalAffinityGeneDefName);

        public static bool IsEligibleForNaturalAffinity(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || pawn.genes == null)
                return false;
            if (IsCompatible(pawn) || AsuranCollectiveUtility.IsNaniteSynthetic(pawn))
                return false;
            return !HasActiveGene(pawn, "WNG_LifeForceMetabolism") && !HasActiveGene(pawn, "WNG_ReplicatorQueenLink");
        }

        private static bool HasActiveGene(Pawn pawn, string defName)
        {
            return pawn?.genes?.GenesListForReading?.Any(g => g?.def?.defName == defName && g.Active) == true;
        }

        private static bool HasHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            return def != null && pawn?.health?.hediffSet?.HasHediff(def) == true;
        }
    }

    // Rare natural ATA-like compatibility. Each humanlike pawn is assessed once per save.
    public sealed class AncientAffinityDistribution : GameComponent
    {
        public const float NaturalAffinityChance = 0.02f;
        private const int ScanIntervalTicks = 600;
        private List<int> assessedPawnIds = new List<int>();
        private HashSet<int> assessedLookup = new HashSet<int>();
        private int nextScanTick;

        public AncientAffinityDistribution(Game game) { }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager == null || Find.TickManager.TicksGame < nextScanTick)
                return;
            nextScanTick = Find.TickManager.TicksGame + ScanIntervalTicks;
            EnsureLookup();
            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
                foreach (Pawn pawn in map?.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
                    AssessPawn(pawn);
        }

        private void AssessPawn(Pawn pawn)
        {
            if (pawn == null || pawn.thingIDNumber <= 0 || pawn.RaceProps == null || !pawn.RaceProps.Humanlike || assessedLookup.Contains(pawn.thingIDNumber))
                return;
            assessedLookup.Add(pawn.thingIDNumber);
            assessedPawnIds.Add(pawn.thingIDNumber);
            if (!AncientCompatibilityUtility.IsEligibleForNaturalAffinity(pawn) || !Rand.Chance(NaturalAffinityChance))
                return;
            GeneDef affinity = DefDatabase<GeneDef>.GetNamedSilentFail(AncientCompatibilityUtility.NaturalAffinityGeneDefName);
            if (affinity != null && pawn.genes != null)
                pawn.genes.AddGene(affinity, false);
        }

        private void EnsureLookup()
        {
            if (assessedPawnIds == null)
                assessedPawnIds = new List<int>();
            assessedPawnIds = assessedPawnIds.Where(id => id > 0).Distinct().ToList();
            assessedLookup = new HashSet<int>(assessedPawnIds);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref assessedPawnIds, "wngAncientAffinityAssessedPawnIds", LookMode.Value);
            Scribe_Values.Look(ref nextScanTick, "wngAncientAffinityNextScanTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                EnsureLookup();
        }
    }
}
