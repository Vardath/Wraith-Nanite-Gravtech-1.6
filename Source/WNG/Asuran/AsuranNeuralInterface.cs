using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    public sealed class CompProperties_AbilityNeuralInterface : CompProperties_AbilityEffect
    {
        public ResearchProjectDef researchPrerequisite;
        public PawnKindDef copyPawnKind;
        public XenotypeDef naniteXenotype;
        public float copyReserveCost = 0.60f;

        public CompProperties_AbilityNeuralInterface()
        {
            compClass = typeof(CompAbilityEffect_NeuralInterface);
        }
    }

    /// <summary>
    /// Touch-range human-form Replicator Neural Interface. The interface uses native faction/guest
    /// status mechanics for allegiance/prisoner/slave state and opens a paused operation menu for
    /// the exact biological pawn under the operator's hand. A concealed Asuran infiltrator is
    /// allowed as a scan target; direct interface contact permanently exposes its synthetic lattice.
    /// </summary>
    public sealed class CompAbilityEffect_NeuralInterface : CompAbilityEffect
    {
        private CompProperties_AbilityNeuralInterface InterfaceProps =>
            (CompProperties_AbilityNeuralInterface)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Pawn subject = target.Pawn;

            if (caster == null || caster.Dead || !caster.Spawned || caster.Map == null ||
                !caster.IsColonistPlayerControlled || !AsuranNaniteUtility.IsNaniteHumanoid(caster))
                return Reject("Only a player-controlled nanite humanoid can operate the Neural Interface.", throwMessages);

            if (InterfaceProps.researchPrerequisite != null && !InterfaceProps.researchPrerequisite.IsFinished)
                return Reject($"Research {InterfaceProps.researchPrerequisite.LabelCap} first.", throwMessages);

            if (!NeuralInterfaceUtility.IsValidSubject(caster, subject, requireAdjacent: false))
                return Reject("Neural Interface requires another living biological humanlike target.", throwMessages);

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            Pawn subject = target.Pawn;
            if (!NeuralInterfaceUtility.IsValidSubject(caster, subject, requireAdjacent: true))
            {
                Messages.Message("The Neural Interface target is no longer available at touch range.", caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (AsuranInfiltrationUtility.IsConcealed(subject))
            {
                AsuranInfiltrationUtility.Reveal(subject, "direct Neural Interface scan", caster);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Recruit: rewrite allegiance", () => NeuralInterfaceUtility.TryRecruit(caster, subject)),
                new FloatMenuOption("Imprison: impose prisoner status", () => NeuralInterfaceUtility.TryImprison(caster, subject)),
                new FloatMenuOption("Extract skills and passions (copy)", () => NeuralInterfaceUtility.TryCopySkills(caster, subject)),
                new FloatMenuOption($"Build human-form copy ({Math.Max(0f, InterfaceProps.copyReserveCost):P0} Nanite Reserve)",
                    () => NeuralInterfaceUtility.TryBuildCopy(caster, subject, InterfaceProps.copyPawnKind, InterfaceProps.naniteXenotype, InterfaceProps.copyReserveCost))
            };

            if (ModsConfig.IdeologyActive)
                options.Insert(2, new FloatMenuOption("Enslave: impose slave status", () => NeuralInterfaceUtility.TryEnslave(caster, subject)));

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private bool Reject(string reason, bool throwMessages)
        {
            if (throwMessages && parent?.pawn != null)
                Messages.Message(reason, parent.pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }
    }

    internal static class NeuralInterfaceUtility
    {
        public static bool IsValidSubject(Pawn caster, Pawn subject, bool requireAdjacent)
        {
            if (caster == null || subject == null || caster == subject || caster.Dead || subject.Dead)
                return false;
            if (!caster.Spawned || !subject.Spawned || caster.Map == null || caster.Map != subject.Map)
                return false;
            if (subject.RaceProps == null || !subject.RaceProps.Humanlike || !subject.RaceProps.IsFlesh || subject.RaceProps.IsMechanoid)
                return false;

            bool concealedInfiltrator = AsuranInfiltrationUtility.IsConcealed(subject);
            if ((!concealedInfiltrator && AsuranNaniteUtility.IsNaniteHumanoid(subject)) ||
                subject.TryGetComp<CompReplicatorState>() != null)
                return false;
            if (requireAdjacent && !caster.Position.AdjacentTo8WayOrInside(subject.Position))
                return false;
            return true;
        }

        public static void TryRecruit(Pawn caster, Pawn subject)
        {
            if (!Revalidate(caster, subject)) return;
            if (caster.Faction == null)
            {
                Reject(caster, "The operator has no faction to recruit into.");
                return;
            }

            subject.SetFaction(caster.Faction, caster);
            Messages.Message($"{subject.LabelShortCap}'s allegiance was rewritten.", subject, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void TryImprison(Pawn caster, Pawn subject)
        {
            if (!Revalidate(caster, subject)) return;
            if (caster.Faction == null || subject.guest == null)
            {
                Reject(caster, "The target cannot enter a valid prisoner state.");
                return;
            }
            if (!subject.Downed && !subject.IsPrisoner && !subject.IsSlave)
            {
                Reject(caster, "Imprisonment requires a downed, prisoner, or slave target so RimWorld's guest state remains valid.");
                return;
            }

            subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Prisoner);
            Messages.Message($"{subject.LabelShortCap} is now held as a prisoner.", subject, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void TryEnslave(Pawn caster, Pawn subject)
        {
            if (!ModsConfig.IdeologyActive)
            {
                Reject(caster, "Slavery systems are unavailable without Ideology.");
                return;
            }
            if (!Revalidate(caster, subject)) return;
            if (caster.Faction == null || subject.guest == null)
            {
                Reject(caster, "The target cannot enter a valid slave state.");
                return;
            }
            if (!subject.Downed && !subject.IsPrisoner && !subject.IsSlave)
            {
                Reject(caster, "Enslavement requires a downed, prisoner, or slave target so RimWorld's guest state remains valid.");
                return;
            }

            subject.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
            Messages.Message($"{subject.LabelShortCap} is now enslaved.", subject, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void TryCopySkills(Pawn caster, Pawn subject)
        {
            if (!Revalidate(caster, subject)) return;
            if (caster.skills == null || subject.skills == null)
            {
                Reject(caster, "The target does not expose a compatible skill pattern.");
                return;
            }

            foreach (SkillRecord source in subject.skills.skills)
            {
                if (source?.def == null) continue;
                SkillRecord destination = caster.skills.GetSkill(source.def);
                if (destination == null) continue;
                destination.Level = Math.Max(destination.Level, Math.Min(20, source.Level));
                if ((int)source.passion > (int)destination.passion)
                    destination.passion = source.passion;
                destination.xpSinceLastLevel = Math.Max(destination.xpSinceLastLevel, source.xpSinceLastLevel);
                destination.xpSinceMidnight = Math.Max(destination.xpSinceMidnight, source.xpSinceMidnight);
            }

            Messages.Message($"{caster.LabelShortCap} copied {subject.LabelShortCap}'s skill and passion pattern.", caster, MessageTypeDefOf.PositiveEvent, false);
        }

        public static void TryBuildCopy(Pawn caster, Pawn subject, PawnKindDef copyPawnKind, XenotypeDef naniteXenotype, float reserveCost)
        {
            if (!Revalidate(caster, subject)) return;
            float cost = Math.Max(0f, Math.Min(1f, reserveCost));
            if (!AsuranNaniteUtility.CanSpendFraction(caster, cost))
            {
                Reject(caster, $"Human-form reconstruction requires {cost:P0} Nanite Reserve.");
                return;
            }
            if (copyPawnKind == null || naniteXenotype == null || caster.Faction == null)
            {
                Reject(caster, "No valid human-form reconstruction template is configured.");
                return;
            }

            Pawn copy = null;
            try
            {
                copy = PawnGenerator.GeneratePawn(copyPawnKind, caster.Faction);
                if (copy == null)
                {
                    Reject(caster, "Human-form reconstruction failed before assembly. No reserve was spent.");
                    return;
                }

                ApplyExactPersonSnapshot(subject, copy);
                LayerNaniteIdentity(copy, naniteXenotype);

                IntVec3 cell = CellFinder.RandomClosewalkCellNear(caster.Position, caster.Map, 2,
                    c => c.InBounds(caster.Map) && c.Standable(caster.Map));
                if (!cell.IsValid)
                {
                    copy.Destroy(DestroyMode.Vanish);
                    Reject(caster, "No safe placement cell was available. No reserve was spent.");
                    return;
                }

                GenSpawn.Spawn(copy, cell, caster.Map);
                if (!AsuranNaniteUtility.TrySpendFraction(caster, cost))
                {
                    copy.Destroy(DestroyMode.Vanish);
                    Reject(caster, "Nanite Reserve changed before commit; reconstruction was rolled back.");
                    return;
                }

                Messages.Message($"A human-form copy of {subject.LabelShortCap} was reconstructed for {cost:P0} Nanite Reserve.", copy, MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                if (copy != null && !copy.Destroyed)
                    copy.Destroy(DestroyMode.Vanish);
                Log.Error($"[WNG] Neural Interface reconstruction failed: {ex}");
                Reject(caster, "Human-form reconstruction failed safely before a usable copy was committed.");
            }
        }

        private static void ApplyExactPersonSnapshot(Pawn source, Pawn copy)
        {
            copy.Name = source.Name;
            copy.gender = source.gender;

            if (source.ageTracker != null && copy.ageTracker != null)
            {
                copy.ageTracker.AgeBiologicalTicks = source.ageTracker.AgeBiologicalTicks;
                copy.ageTracker.AgeChronologicalTicks = source.ageTracker.AgeChronologicalTicks;
            }

            if (source.story != null && copy.story != null)
            {
                copy.story.Childhood = source.story.Childhood;
                copy.story.Adulthood = source.story.Adulthood;
                copy.story.Title = source.story.Title;
                copy.story.birthLastName = source.story.birthLastName;
                copy.story.bodyType = source.story.bodyType;
                copy.story.headType = source.story.headType;
                copy.story.hairDef = source.story.hairDef;
                copy.story.HairColor = source.story.HairColor;
                copy.story.SkinColorBase = source.story.SkinColorBase;
                copy.story.skinColorOverride = source.story.skinColorOverride;

                copy.story.traits.allTraits.Clear();
                foreach (Trait trait in source.story.traits.allTraits)
                {
                    if (trait?.def == null) continue;
                    copy.story.traits.GainTrait(new Trait(trait.def, trait.Degree, trait.ScenForced));
                }
            }

            if (source.skills != null && copy.skills != null)
            {
                foreach (SkillRecord src in source.skills.skills)
                {
                    if (src?.def == null) continue;
                    SkillRecord dst = copy.skills.GetSkill(src.def);
                    if (dst == null) continue;
                    dst.Level = Math.Min(20, Math.Max(0, src.Level));
                    dst.passion = src.passion;
                    dst.xpSinceLastLevel = Math.Max(0f, src.xpSinceLastLevel);
                    dst.xpSinceMidnight = Math.Max(0f, src.xpSinceMidnight);
                }
            }

            if (source.genes != null && copy.genes != null)
            {
                foreach (Gene sourceGene in source.genes.GenesListForReading.ToList())
                {
                    if (sourceGene?.def == null || copy.genes.GetGene(sourceGene.def) != null) continue;
                    bool sourceWasXenogene = source.genes.Xenogenes.Contains(sourceGene);
                    copy.genes.AddGene(sourceGene.def, sourceWasXenogene);
                }
            }

            copy.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static void LayerNaniteIdentity(Pawn copy, XenotypeDef naniteXenotype)
        {
            if (copy?.genes == null || naniteXenotype == null) return;
            foreach (GeneDef geneDef in naniteXenotype.AllGenes)
            {
                if (geneDef == null || copy.genes.GetGene(geneDef) != null) continue;
                copy.genes.AddGene(geneDef, true);
            }
        }

        private static bool Revalidate(Pawn caster, Pawn subject)
        {
            if (IsValidSubject(caster, subject, requireAdjacent: true)) return true;
            Reject(caster, "The Neural Interface operation was cancelled because the target state changed.");
            return false;
        }

        private static void Reject(Pawn caster, string text)
        {
            if (caster != null)
                Messages.Message(text, caster, MessageTypeDefOf.RejectInput, false);
        }
    }
}
