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
            modifiers.SourceDamage *= compensation * 1.20f;
        }
    }

    private static float GetContactLegendaryCompensation()
    {
        float current = 1f;
        if (Main.masterMode)
            current = Main.ActiveWorldFileData.ForTheWorthy ? 5.33f : 3f;
        else if (Main.expertMode)
            current = 2f;
        else if (Main.GameMode == 3)
            current = 0.5f;

        return 5.33f / current;
    }
}