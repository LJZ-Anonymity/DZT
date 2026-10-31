using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Bosses
{
    internal class WallofFleshAIOverride : GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.WallofFlesh
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                AddDefenseFromHungries(npc); // 统计存活饿鬼数量并增加防御
                RemovePotionBuffs(); // 移除玩家身上的重力药水
            }
            return true;
        }

        /// <summary>
        /// 每只存活的饿鬼为血肉墙提供 1 点防御
        /// </summary>
        private static void AddDefenseFromHungries(NPC wof)
        {
            int hungryCount = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active)
                {
                    if (n.type == NPCID.TheHungry || n.type == NPCID.TheHungryII)
                    {
                        hungryCount++;
                    }
                }
            }
            wof.defense = wof.defDefense + hungryCount;
        }

        /// <summary>
        /// 移除玩家身上的重力药水效果
        /// </summary>
        private static void RemovePotionBuffs()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < Main.player.Length; i++)
                {
                    Player p = Main.player[i];
                    if (p != null && p.active && p.HasBuff(BuffID.Gravitation))
                    {
                        p.ClearBuff(BuffID.Gravitation);
                    }
                }
            }
        }
    }
}