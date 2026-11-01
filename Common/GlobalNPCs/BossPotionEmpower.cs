using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Common.GlobalNPCs
{
    public class BossPotionEmpower : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const float RageDoubleDamageChance = 0.20f; // BOSS双倍伤害概率
        private float _bossSpeedMultiplier = 1f; // Boss移速倍率
        private bool scalingApplied; // 是否完成难度缩放
        private bool hasApplied; // 是否已应用药水加成

        public override void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (!npc.boss) return;
            scalingApplied = true;
        }

        public override void PostAI(NPC npc)
        {
            if (!npc.boss || !scalingApplied) return;

            // 如果尚未应用加成，则进行一次性的检测与属性强化
            if (!hasApplied)
            {
                Player target = GetValidTarget(npc);
                if (target == null) return;

                CheckPotionBuffs(target, out int totalDef, out float totalDmg, out float totalLife, out float totalDR, out float totalSpeed);

                // 所有加成均为0，返回
                if (totalDef <= 0 && totalDmg <= 0f && totalLife <= 0f && totalDR <= 0f && totalSpeed <= 0f) return;

                ApplyBonuses(npc, totalDef, totalDmg, totalLife, totalDR, totalSpeed);
                hasApplied = true;
            }

            // 每帧持续应用移速加成
            if (hasApplied && _bossSpeedMultiplier != 1f)
            {
                npc.velocity /= _bossSpeedMultiplier; // 抵消上一帧的乘法
                npc.velocity *= _bossSpeedMultiplier;
            }
        }

        /// <summary>
        /// 安全获取 Boss 的目标玩家。优先使用 Boss 当前锁定目标，若无效则寻找最近的玩家。
        /// </summary>
        private static Player GetValidTarget(NPC npc)
        {
            if (npc.HasValidTarget)
            {
                return Main.player[npc.target];
            }

            int closest = Player.FindClosest(npc.position, npc.width, npc.height);
            if (closest >= 0 && closest < Main.maxPlayers)
            {
                Player p = Main.player[closest];
                if (p != null && p.active && !p.dead)
                    return p;
            }

            return null;
        }

        /// <summary>
        /// 检测玩家身上的药水 Buff，并累加对应的加成数值
        /// </summary>
        /// <param name="target">目标玩家</param>
        /// <param name="totalDef">防御</param>
        /// <param name="totalDmg">伤害</param>
        /// <param name="totalLife">生命</param>
        /// <param name="totalDR">减伤</param>
        /// <param name="totalSpeed">移速</param>
        private static void CheckPotionBuffs(Player target, out int totalDef, out float totalDmg, out float totalLife, out float totalDR, out float totalSpeed)
        {
            totalDef = 0;
            totalDmg = 0f;
            totalLife = 0f;
            totalDR = 0f;
            totalSpeed = 0f;

            // 防御类
            if (target.HasBuff(BuffID.Ironskin)) totalDef += 8; // 铁皮药水
            if (target.HasBuff(BuffID.CatBast)) totalDef += 8; // 韧皮雕像

            // 伤害类
            if (target.HasBuff(BuffID.Wrath)) totalDmg += 0.10f; // 怒气药水
            if (target.HasBuff(BuffID.MagicPower)) totalDmg += 0.20f; // 魔能药水

            // 生命类
            if (target.HasBuff(BuffID.Lifeforce)) totalLife += 0.20f; // 生命药水

            // 减伤类
            if (target.HasBuff(BuffID.Endurance)) totalDR += 0.10f; // 耐力药水

            // 移动速度类
            if (target.HasBuff(BuffID.Swiftness)) totalSpeed += 0.25f; // 敏捷药水
            if (target.HasBuff(BuffID.SugarRush)) totalSpeed += 0.20f; // 糖果冲刺（蛋糕块）

            // 食物类（取最高等级）
            if (target.HasBuff(BuffID.WellFed3)) // 酒足饭饱
            {
                totalDef += 4;
                totalDmg += 0.10f;
                totalSpeed += 0.40f;
            }
            else if (target.HasBuff(BuffID.WellFed2)) // 很满意
            {
                totalDef += 3;
                totalDmg += 0.075f;
                totalSpeed += 0.30f;
            }
            else if (target.HasBuff(BuffID.WellFed)) // 吃得好
            {
                totalDef += 2;
                totalDmg += 0.05f;
                totalSpeed += 0.20f;
            }

            // --- 模组 Buff 兼容 ---
            int tankBuff = GetTankBuffID(); // 获取模组中的泰坦药剂包
            if (tankBuff != -1 && target.HasBuff(tankBuff))
            {
                totalDef += 8;
                totalDmg += 0.10f;
                totalLife += 0.20f;
            }
        }

        /// <summary>
        /// 将计算好的加成应用给 Boss
        /// </summary>
        private void ApplyBonuses(NPC npc, int totalDef, float totalDmg, float totalLife, float totalDR, float totalSpeed)
        {
            hasApplied = true; // 标记已应用
            if (totalDef > 0) npc.defense += (int)Math.Ceiling(totalDef * 0.20f);
            if (totalDmg > 0f) npc.damage = (int)(npc.defDamage * (1f + totalDmg * 0.20f));
            if (totalLife > 0f)
            {
                float bonus = 1f + totalLife * 0.20f;
                npc.lifeMax = (int)(npc.lifeMax * bonus);
                npc.life = npc.lifeMax;
            }
            if (totalDR > 0f) npc.takenDamageMultiplier *= 1f - totalDR * 0.20f;
            if (totalSpeed > 0f) _bossSpeedMultiplier = 1f + totalSpeed * 0.20f;
        }

        /// <summary>
        /// 若玩家身上有暴怒药水，则本次伤害有概率翻倍
        /// </summary>
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (!npc.boss || !scalingApplied) return;
            if (target.HasBuff(BuffID.Rage) && Main.rand.NextFloat() < RageDoubleDamageChance)
            {
                modifiers.FinalDamage *= 2f; // 本次伤害翻倍
            }
        }

        #region 兼容 AchemistNPC Lite 模组
        private static int _tankBuffId = 0;
        /// <summary>
        /// 动态获取“坦克药剂包”的 Buff ID。
        /// </summary>
        private static int GetTankBuffID()
        {
            if (_tankBuffId != 0) return _tankBuffId;
            for (int i = 1; i < BuffLoader.BuffCount; i++) // 遍历游戏内注册的所有 Buff
            {
                string name = Lang.GetBuffName(i); // 获取该 Buff 的本地化显示名称
                if (!string.IsNullOrEmpty(name) && name.Contains("坦克药剂包")) // 模糊匹配名字中包含“坦克药剂包”的 Buff
                {
                    _tankBuffId = i;
                    //Main.NewText($"成功动态定位模组Buff: ID {i} - {name}", 0, 255, 0);
                    return _tankBuffId;
                }
            }

            // 如果没找到，标记为 -1，下次不再重复遍历
            _tankBuffId = -1;
            return _tankBuffId;
        }
        #endregion
    }
}