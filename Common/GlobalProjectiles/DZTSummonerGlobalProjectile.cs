using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Common.GlobalProjectiles
{
    public class SummonerPotionPenalty : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        private const float MissChance = 0.10f; // 攻击落空概率

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return null;
            if (!IsBossAlive()) return null; // 没有Boss存活，不处理
            if (projectile.minion || projectile.sentry) // 仅处理属于玩家的召唤物本体和哨兵
            {
                if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
                {
                    Player owner = Main.player[projectile.owner];
                    if (owner.active && !owner.dead && owner.HasBuff(BuffID.Summoning))
                    {
                        if (Main.rand.NextFloat() < MissChance)
                            return false; // 拦截命中
                    }
                }
            }
            return null; // 让游戏按默认逻辑处理
        }

        /// <summary>
        /// 判断是否有Boss存活
        /// </summary>
        private static bool IsBossAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.boss)
                    return true;
            }
            return false;
        }
    }
}