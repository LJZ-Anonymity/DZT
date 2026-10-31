using Terraria.ModLoader;
using DZT.Common.Systems;
using Terraria.ID;
using Terraria;

namespace DZT.Common.Players
{
    public class DZTBrainPlayer : ModPlayer
    {
        public override void PostUpdateEquips()
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (Player.dead) return; // 玩家死亡不反转

            bool reverseKeys = false;

            // 遍历所有活跃的 NPC，寻找二阶段的克苏鲁之脑
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.BrainofCthulhu && npc.life > 0)
                {
                    // 当 ai[0] < 0f 时，飞眼怪死光，进入二阶段狂暴冲刺状态
                    if (npc.ai[0] < 0f)
                    {
                        reverseKeys = true;
                        break;
                    }
                }
            }

            // 如果确认在打二阶段大脑，反转左右键
            if (reverseKeys)
            {
                bool leftPressed = Player.controlLeft;
                bool rightPressed = Player.controlRight;
                Player.controlLeft = rightPressed;
                Player.controlRight = leftPressed;
            }
        }
    }
}