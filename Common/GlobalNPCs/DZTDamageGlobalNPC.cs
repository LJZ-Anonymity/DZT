using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria;

namespace DZT.Common.GlobalNPCs;

public class DZTDamageGlobalNPC : GlobalNPC
{
    public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
    {
        if (!npc.friendly && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
        {
            // 先将当前难度伤害补偿到传奇基准，再应用 DZT 的 +20%
            float compensation = GetContactLegendaryCompensation();
            modifiers.FinalDamage *= compensation * 1.20f;
        }
    }

    private static float GetContactLegendaryCompensation()
    {
        float current = 1f;
        if (Main.masterMode) // 大师模式：检查是否为 FTW 种子，是则按传奇难度 5.33x 计算，否则按大师 3x
        {
            current = Main.ActiveWorldFileData.ForTheWorthy ? 5.33f : 3f;
        }
        else if (Main.expertMode) // 专家模式
        {
            current = 2f;
        }
        else if (Main.GameMode == 3) // 旅行模式
        {
            current = 0.5f;
        }

        // 补偿到传奇基准 5.33x，再乘以 DZT 的 1.20
        return 5.33f / current;
    }
}