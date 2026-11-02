using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria;

namespace DZT.Common.GlobalProjectiles;

public class DZTDamageGlobalProjectile : GlobalProjectile
{
    public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
    {
        if (projectile.hostile && !projectile.friendly &&
            ModContent.GetInstance<DZTWorldSystem>().DZTActive)
        {
            float compensation = GetProjectileLegendaryCompensation();
            modifiers.SourceDamage *= compensation * 1.40f;
        }
    }

    private static float GetProjectileLegendaryCompensation()
    {
        float current = 1f;
        if (Main.masterMode)
            current = 3f;
        else if (Main.expertMode)
            current = 2f;
        else if (Main.GameMode == 3)
            current = 0.5f;

        return 3f / current;
    }
}