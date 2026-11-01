using DZT.Common.Systems;
using Terraria.ModLoader;
using DZT.Common.Utils;
using Terraria.ID;
using Terraria;

namespace DZT.Common.GlobalProjectiles
{
    public class SummonerPotionPenalty : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        private readonly bool _hasLazyLoaded = false;
        private const float MissChance = 0.10f; // 攻击落空概率

        private static int _summonerComboId = -2; // 召唤师药剂包

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return null;
            if (!IsBossAlive()) return null; // 没有Boss存活，不处理
            if (!_hasLazyLoaded) LazyLoad();
            if (projectile.minion || projectile.sentry) // 仅处理属于玩家的召唤物本体和哨兵
            {
                if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
                {
                    Player owner = Main.player[projectile.owner];
                    if (owner.active && !owner.dead)
                    {
                        bool hasSummoningBuff = owner.HasBuff(BuffID.Summoning);
                        bool hasSummonerCombo = (_summonerComboId != -1 && owner.HasBuff(_summonerComboId));

                        if (hasSummoningBuff || hasSummonerCombo)
                        {
                            if (Main.rand.NextFloat() < MissChance)
                                return false; // 拦截命中
                        }
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

        /// <summary>
        /// 懒加载
        /// </summary>
        private static void LazyLoad()
        {
            _summonerComboId = BuffUtils.GetModBuffID("召唤师药剂包", "Summoner Combination");
        }
    }
}