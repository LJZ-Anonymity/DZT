using Terraria.ModLoader;
using Terraria;

namespace DZT.Common.GlobalProjectiles
{
    public class HostileProjectileDamageEdit : GlobalProjectile
    {
        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            if (projectile.hostile)
            {
                modifiers.FinalDamage *= 1.40f; // 弹幕伤害提高 40%
            }
        }
    }
}