using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Common.Players
{
    /// <summary>
    /// BOSS出现时，玩家荆棘药水效果减半
    /// </summary>
    public class DZTThornsNerfPlayer : ModPlayer
    {
        public override void PostUpdateEquips()
        {
            if (Player.HasBuff(BuffID.Thorns))
            {
                if (IsAnyBossAlive())
                {
                    Player.thorns *= 0.5f;
                }
            }
        }

        private static bool IsAnyBossAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.boss)
                {
                    return true;
                }
            }
            return false;
        }
    }
}