using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityHiveSense : CompProperties_AbilityEffect
    {
        public float radius = 40f;

        public CompProperties_AbilityHiveSense()
        {
            compClass = typeof(CompAbilityEffect_HiveSense);
        }
    }

    /// <summary>
    /// Local Wraith mind-sense. This deliberately reports aggregate information rather than
    /// revealing exact cells: it is telepathic awareness, not a wallhack or map-fog bypass.
    /// Wraith-linked identity is the biological telepathy gene, so future hybrids can join the
    /// same grammar without special-casing a xenotype name.
    /// </summary>
    public sealed class CompAbilityEffect_HiveSense : CompAbilityEffect
    {
        public new CompProperties_AbilityHiveSense Props => (CompProperties_AbilityHiveSense)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            if (!WraithTelepathyUtility.IsTelepathicWraith(caster))
            {
                if (throwMessages && caster != null)
                    Messages.Message("Hive Sense requires active Wraith telepathy.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Map map = caster?.Map;
            if (caster == null || map == null)
                return;

            float radiusSquared = Props.radius * Props.radius;
            var sensed = map.mapPawns.AllPawnsSpawned
                .Where(p => p != null && p != caster && !p.Dead &&
                            WraithTelepathyUtility.IsTelepathicWraith(p) &&
                            (p.Position - caster.Position).LengthHorizontalSquared <= radiusSquared)
                .ToList();

            if (sensed.Count == 0)
            {
                Messages.Message(
                    "Hive Sense finds no other Wraith-linked mind within " + Mathf.RoundToInt(Props.radius) + " cells.",
                    caster,
                    MessageTypeDefOf.NeutralEvent,
                    false);
                return;
            }

            int hostile = sensed.Count(p => p.HostileTo(caster));
            int allied = sensed.Count(p => p.Faction != null && p.Faction == caster.Faction);
            int other = sensed.Count - hostile - allied;
            float nearest = sensed.Min(p => (p.Position - caster.Position).LengthHorizontal);

            Messages.Message(
                "Hive Sense detects " + sensed.Count + " Wraith-linked " + (sensed.Count == 1 ? "mind" : "minds") +
                " within " + Mathf.RoundToInt(Props.radius) + " cells: " + allied + " allied, " + hostile +
                " hostile, " + other + " unaligned. Nearest presence is about " + Mathf.RoundToInt(nearest) + " cells away.",
                caster,
                hostile > 0 ? MessageTypeDefOf.CautionInput : MessageTypeDefOf.NeutralEvent,
                false);
        }
    }

    public sealed class CompProperties_AbilityQueensCommand : CompProperties_AbilityEffect
    {
        public float radius = 10f;
        public HediffDef commandHediff;

        public CompProperties_AbilityQueensCommand()
        {
            compClass = typeof(CompAbilityEffect_QueensCommand);
        }
    }

    /// <summary>
    /// Queen-only allied Hive synchronization. It never changes faction/controller state and only
    /// affects living allied pawns carrying active Wraith telepathy, preserving exact pawn identity.
    /// </summary>
    public sealed class CompAbilityEffect_QueensCommand : CompAbilityEffect
    {
        public new CompProperties_AbilityQueensCommand Props => (CompProperties_AbilityQueensCommand)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Dead || caster.kindDef?.defName != "WNG_WraithQueen" ||
                !WraithTelepathyUtility.IsTelepathicWraith(caster))
            {
                if (throwMessages && caster != null)
                    Messages.Message("Queen's Command requires a living Wraith Queen with active telepathy.", caster, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Map map = caster?.Map;
            HediffDef hediffDef = Props.commandHediff;
            if (caster == null || map == null || hediffDef == null)
                return;

            float radiusSquared = Props.radius * Props.radius;
            int affected = 0;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.Faction != caster.Faction ||
                    !WraithTelepathyUtility.IsTelepathicWraith(pawn) ||
                    (pawn.Position - caster.Position).LengthHorizontalSquared > radiusSquared)
                    continue;

                Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef);
                if (existing != null)
                    pawn.health.RemoveHediff(existing);
                pawn.health?.AddHediff(hediffDef);
                affected++;
            }

            Messages.Message(
                "The Queen's command synchronizes " + affected + " nearby Wraith-linked " + (affected == 1 ? "mind" : "minds") + ".",
                caster,
                MessageTypeDefOf.PositiveEvent,
                false);
        }
    }

    internal static class WraithTelepathyUtility
    {
        private const string TelepathyGeneDefName = "WNG_WraithTelepathy";

        public static bool IsTelepathicWraith(Pawn pawn)
        {
            if (pawn?.genes == null || pawn.Dead)
                return false;

            GeneDef telepathy = DefDatabase<GeneDef>.GetNamedSilentFail(TelepathyGeneDefName);
            if (telepathy == null)
                return false;

            Gene gene = pawn.genes.GetGene(telepathy);
            return gene != null && gene.Active;
        }
    }
}
