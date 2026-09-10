using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace WraithNaniteGravtech
{
    /// <summary>
    /// Keeps the Neural Interface action attached to any pawn that currently has the gene.
    /// This also makes the ability recover if another mod or a gene rewrite temporarily drops it.
    /// </summary>
    public sealed class Gene_NeuralInterface : Gene
    {
        private int nextAbilityCheck;

        public override void PostAdd()
        {
            base.PostAdd();
            EnsureAbility();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!Active || pawn?.abilities == null)
                return;

            int now = Find.TickManager?.TicksGame ?? 0;
            if (now < nextAbilityCheck)
                return;

            nextAbilityCheck = now + 300;
            EnsureAbility();
        }

        private void EnsureAbility()
        {
            if (!Active || pawn?.abilities == null)
                return;

            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail("WNG_NaniteInterfaceAbility");
            if (def != null && pawn.abilities.GetAbility(def) == null)
                pawn.abilities.GainAbility(def);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref nextAbilityCheck, "wngNeuralInterfaceAbilityCheck", 0);
        }
    }

    public sealed class CompProperties_AbilityNaniteInterface : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityNaniteInterface()
        {
            compClass = typeof(CompAbilityEffect_NaniteInterface);
        }
    }

    /// <summary>
    /// Player-facing touch interface for the first-build human-form Replicator/Asuran branch.
    /// The action opens a native RimWorld menu rather than hiding several unrelated operations
    /// behind separate duplicate abilities.
    /// </summary>
    public sealed class CompAbilityEffect_NaniteInterface : CompAbilityEffect
    {
        private static readonly string[] NanitePackageGenes =
        {
            "WNG_NaniteBody",
            "WNG_NaniteReserve",
            "WNG_NaniteReconstruction",
            "WNG_EMPSensitiveNanites",
            "WNG_NeuralInterface",
            "WNG_AsuranCollectiveLink",
            "Deathless",
            "VacuumResistant"
        };

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn source = target.Pawn;
            if (!IsValidBiologicalPatternTarget(source))
            {
                if (throwMessages)
                    Messages.Message("The Neural Interface requires a living humanlike biological target.", parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (ReplicatorQueenUtility.IsQueen(source))
            {
                if (throwMessages)
                    Messages.Message("The Replicator Queen's sovereign identity cannot be duplicated through the ordinary Neural Interface.", parent.pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            Pawn source = target.Pawn;
            if (!StillValid(caster, source) || caster.Faction != Faction.OfPlayer)
                return;

            List<FloatMenuOption> options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Rewrite allegiance: recruit", () => Recruit(caster, source)),
                new FloatMenuOption("Set custody: imprison", () => Imprison(caster, source)),
                new FloatMenuOption("Set custody: enslave", () => Enslave(caster, source)),
                new FloatMenuOption("Acquire skill pattern", () => CopySkills(caster, source)),
                new FloatMenuOption("Assemble human-form copy", () => BuildCopy(caster, source))
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static bool IsValidBiologicalPatternTarget(Pawn pawn)
        {
            return pawn != null
                && !pawn.Dead
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike
                && pawn.RaceProps.IsFlesh
                && !pawn.RaceProps.IsMechanoid;
        }

        private static bool StillValid(Pawn caster, Pawn target)
        {
            return caster != null
                && target != null
                && caster != target
                && !caster.Dead
                && !target.Dead
                && caster.Map != null
                && target.Map == caster.Map
                && IsValidBiologicalPatternTarget(target)
                && !ReplicatorQueenUtility.IsQueen(target);
        }

        private static void Recruit(Pawn caster, Pawn target)
        {
            if (!StillValid(caster, target) || caster.Faction == null)
                return;

            if (target.Faction != caster.Faction)
                target.SetFaction(caster.Faction, null);

            Messages.Message(target.LabelShortCap + " has been rewritten into " + caster.Faction.Name + ".", target, MessageTypeDefOf.PositiveEvent, false);
        }

        private static void Imprison(Pawn caster, Pawn target)
        {
            if (!StillValid(caster, target) || caster.Faction == null || target.guest == null)
                return;

            target.guest.SetGuestStatus(caster.Faction, GuestStatus.Prisoner);
            Messages.Message(target.LabelShortCap + " is now held as a prisoner.", target, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void Enslave(Pawn caster, Pawn target)
        {
            if (!StillValid(caster, target) || caster.Faction == null || target.guest == null)
                return;

            if (!ModsConfig.IdeologyActive)
            {
                Messages.Message("Enslavement requires Ideology.", caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            target.guest.SetGuestStatus(caster.Faction, GuestStatus.Slave);
            Messages.Message(target.LabelShortCap + " is now enslaved.", target, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void CopySkills(Pawn caster, Pawn source)
        {
            if (!StillValid(caster, source) || caster.skills == null || source.skills == null)
                return;

            foreach (SkillRecord sourceSkill in source.skills.skills)
            {
                SkillRecord destination = caster.skills.GetSkill(sourceSkill.def);
                if (destination == null)
                    continue;

                if (sourceSkill.Level > destination.Level)
                    destination.Level = sourceSkill.Level;
                if ((int)sourceSkill.passion > (int)destination.passion)
                    destination.passion = sourceSkill.passion;
            }

            Messages.Message(caster.LabelShortCap + " acquired the strongest skill patterns present in " + source.LabelShortCap + ".", caster, MessageTypeDefOf.PositiveEvent, false);
        }

        private static Pawn GenerateCopyShell(Pawn caster, Pawn source)
        {
            if (source.def != ThingDefOf.Human && source.kindDef?.race == source.def)
            {
                try
                {
                    Pawn racialCopy = PawnGenerator.GeneratePawn(source.kindDef, caster.Faction);
                    if (racialCopy != null && racialCopy.def == source.def)
                        return racialCopy;
                    if (racialCopy != null && !racialCopy.Destroyed)
                        racialCopy.Destroy(DestroyMode.Vanish);
                }
                catch (Exception ex)
                {
                    Log.Warning("[WNG] Race-preserving Neural Interface copy generation failed for " + source.def?.defName + "; using the WNG human-form shell instead. " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            PawnKindDef fallback = DefDatabase<PawnKindDef>.GetNamedSilentFail(
                caster.Faction == Faction.OfPlayer ? "WNG_PlayerHumanFormReplicator" : "WNG_HumanFormReplicator");
            return fallback == null ? null : PawnGenerator.GeneratePawn(fallback, caster.Faction);
        }

        private static void BuildCopy(Pawn caster, Pawn source)
        {
            if (!StillValid(caster, source) || caster.Faction == null)
                return;

            Gene_Resource_NaniteReserve reserve = Gene_Resource_NaniteReserve.Get(caster);
            float cost = Gene_Resource_NaniteReserve.CopyPawnCost;
            if (reserve == null || !reserve.CanSpend(cost))
            {
                float available = reserve?.Value ?? 0f;
                Messages.Message("Insufficient nanite reserve. Requires " + cost.ToString("P0") + "; available " + available.ToString("P0") + ".", caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Pawn copy = GenerateCopyShell(caster, source);
            if (copy == null)
            {
                Messages.Message("No valid human-form copy shell could be generated.", caster, MessageTypeDefOf.RejectInput, false);
                return;
            }

            try
            {
                CopyIdentity(source, copy);

                if (!GenPlace.TryPlaceThing(copy, caster.Position, caster.Map, ThingPlaceMode.Near))
                {
                    if (!copy.Destroyed)
                        copy.Destroy(DestroyMode.Vanish);
                    Messages.Message("Human-form copy assembly failed because no valid placement cell was available. No nanite reserve was spent.", caster, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                if (!reserve.TrySpend(cost))
                {
                    if (!copy.Destroyed)
                        copy.Destroy(DestroyMode.Vanish);
                    Messages.Message("Human-form copy assembly aborted because the reserve changed before fabrication completed.", caster, MessageTypeDefOf.RejectInput, false);
                    return;
                }

                copy.Drawer?.renderer?.SetAllGraphicsDirty();
                Messages.Message("A human-form copy of " + source.LabelShortCap + " has been assembled, preserving the source identity pattern and layering the nanite package onto it.", copy, MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                if (!copy.Destroyed)
                    copy.Destroy(DestroyMode.Vanish);
                Log.Error("[WNG] Neural Interface copy transaction failed: " + ex);
                Messages.Message("Human-form copy assembly failed. No nanite reserve was spent.", caster, MessageTypeDefOf.RejectInput, false);
            }
        }

        private static void CopyIdentity(Pawn source, Pawn copy)
        {
            copy.gender = source.gender;
            copy.Name = source.Name;

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
                copy.story.skinColorOverride = source.story.skinColorOverride;

                if (source.story.traits != null && copy.story.traits != null)
                {
                    copy.story.traits.allTraits.Clear();
                    foreach (Trait sourceTrait in source.story.traits.allTraits)
                        copy.story.traits.GainTrait(new Trait(sourceTrait.def, sourceTrait.Degree, false));
                }
            }

            if (source.style != null && copy.style != null)
            {
                copy.style.beardDef = source.style.beardDef;
                copy.style.FaceTattoo = source.style.FaceTattoo;
                copy.style.BodyTattoo = source.style.BodyTattoo;
            }

            if (source.skills != null && copy.skills != null)
            {
                foreach (SkillRecord sourceSkill in source.skills.skills)
                {
                    SkillRecord destination = copy.skills.GetSkill(sourceSkill.def);
                    if (destination == null)
                        continue;

                    destination.Level = sourceSkill.Level;
                    destination.passion = sourceSkill.passion;
                    destination.xpSinceLastLevel = sourceSkill.xpSinceLastLevel;
                    destination.xpSinceMidnight = sourceSkill.xpSinceMidnight;
                }
            }

            CopyGenomeAndNaniteLayer(source, copy);
            copy.apparel?.DestroyAll();
        }

        private static void CopyGenomeAndNaniteLayer(Pawn source, Pawn copy)
        {
            if (!ModsConfig.BiotechActive || source.genes == null || copy.genes == null)
                return;

            foreach (Gene existing in copy.genes.GenesListForReading.ToList())
                copy.genes.RemoveGene(existing);

            XenotypeDef sourceXenotype = source.genes.Xenotype;
            if (sourceXenotype != null)
                copy.genes.SetXenotype(sourceXenotype);

            foreach (Gene sourceGene in source.genes.Endogenes)
                AddGeneIfMissing(copy, sourceGene?.def, false);

            foreach (Gene sourceGene in source.genes.Xenogenes)
                AddGeneIfMissing(copy, sourceGene?.def, true);

            copy.genes.xenotypeName = source.genes.xenotypeName;
            copy.genes.iconDef = source.genes.iconDef;

            foreach (string defName in NanitePackageGenes)
                AddGeneIfMissing(copy, DefDatabase<GeneDef>.GetNamedSilentFail(defName), true);
        }

        private static void AddGeneIfMissing(Pawn pawn, GeneDef def, bool xenogene)
        {
            if (pawn?.genes == null || def == null || pawn.genes.GetGene(def) != null)
                return;
            pawn.genes.AddGene(def, xenogene);
        }
    }
}
