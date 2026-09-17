using RimWorld;
using Verse;

namespace WraithNaniteGravtech.Anomaly
{
    public static class WhispersImplantUtility
    {
        private const string MistAbilityDefName = "WNG_ReleaseWhispersMist";
        public static AbilityDef MistAbilityDef => DefDatabase<AbilityDef>.GetNamedSilentFail(MistAbilityDefName);
    }

    public sealed class Hediff_WhispersMistGland : Hediff_Implant
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            AbilityDef ability = WhispersImplantUtility.MistAbilityDef;
            if (ability != null && pawn?.abilities != null && pawn.abilities.GetAbility(ability) == null)
                pawn.abilities.GainAbility(ability);
        }

        public override void PostRemoved()
        {
            Pawn bearer = pawn;
            AbilityDef ability = WhispersImplantUtility.MistAbilityDef;
            if (ability != null && bearer?.abilities != null && bearer.abilities.GetAbility(ability) != null)
                bearer.abilities.RemoveAbility(ability);
            base.PostRemoved();
        }
    }

    public sealed class CompProperties_AbilityWhispersMist : CompProperties_AbilityEffect
    {
        public float radius = 4.2f;
        public int gasPerCell = 44;
        public CompProperties_AbilityWhispersMist() { compClass = typeof(CompAbilityEffect_WhispersMist); }
    }

    public sealed class CompAbilityEffect_WhispersMist : CompAbilityEffect
    {
        public new CompProperties_AbilityWhispersMist Props => (CompProperties_AbilityWhispersMist)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            HediffDef gland = DefDatabase<HediffDef>.GetNamedSilentFail("WNG_WhispersMistGland");
            bool valid = caster != null && !caster.Dead && caster.Spawned && caster.Map?.gasGrid != null &&
                         gland != null && caster.health?.hediffSet?.HasHediff(gland) == true;
            if (!valid && throwMessages && caster != null)
                Messages.Message("A functioning implanted Whispers mist gland is required.", caster, MessageTypeDefOf.RejectInput, false);
            return valid && base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            Map map = caster?.Map;
            if (caster == null || map?.gasGrid == null)
                return;

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(caster.Position, Props.radius, true))
            {
                if (cell.InBounds(map))
                    map.gasGrid.AddGas(cell, GasType.BlindSmoke, Props.gasPerCell);
            }
        }
    }
}
