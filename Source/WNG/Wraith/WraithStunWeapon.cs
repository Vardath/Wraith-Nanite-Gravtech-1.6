using RimWorld;
using Verse;
using Verse.Sound;

namespace WraithNaniteGravtech
{
    public sealed class WraithStunnerProjectileExtension : DefModExtension
    {
        public int stunTicks = 1800;
    }

    /// <summary>
    /// Wraith capture projectile. It deliberately subclasses Projectile rather than Bullet so a
    /// successful hit never enters Bullet.TakeDamage. The only pawn impact effect is RimWorld's
    /// native StunHandler, which also preserves the game's own stun-resistance/adaptation rules.
    /// </summary>
    public sealed class Projectile_WraithStunner : Projectile
    {
        public override bool AnimalsFleeImpact => true;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 position = Position;
            Thing instigator = launcher;
            WraithStunnerProjectileExtension ext = def.GetModExtension<WraithStunnerProjectileExtension>();
            int stunTicks = ext?.stunTicks ?? 1800;

            // Projectile.Impact owns normal impact cleanup/effecters but does not apply Bullet's
            // health damage. A shield interception therefore still destroys the shot correctly.
            base.Impact(hitThing, blockedByShield);

            Pawn pawn = hitThing as Pawn;
            if (blockedByShield || pawn == null || pawn.Dead)
                return;

            pawn.stances?.stunner?.StunFor(stunTicks, instigator, addBattleLog: true);
            SoundDefOf.Psycast_Skip_Pulse.PlayOneShot(new TargetInfo(position, map));
        }
    }
}
