using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria;

namespace DZT.Common.GlobalProjectiles;

public class DZTDamageGlobalProjectile : GlobalProjectile
{
    public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
    {
        if (projectile.hostile && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
        {
            // 先将当前难度弹幕伤害补偿到传奇基准（3×），再应用 DZT 的 +40%
            float compensation = GetProjectileLegendaryCompensation();
            modifiers.FinalDamage *= compensation * 1.40f;
        }
    }

    private static float GetProjectileLegendaryCompensation()
    {
        float current = 1f;
        if (Main.masterMode)
            current = 3f;          // 大师/传奇弹幕均为 3×
        else if (Main.expertMode)
            current = 2f;
        else if (Main.GameMode == 3) // 旅行
            current = 0.5f;

        return 3f / current;
    }
}