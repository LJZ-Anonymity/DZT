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
                int maxDef = 0;
                float maxDmg = 0f;
                float maxLife = 0f;
                float maxDR = 0f;
                float maxSpeed = 0f;

                bool foundAnyPlayer = false;
                for (int i = 0; i < Main.maxPlayers; i++) // 遍历所有玩家，取各项药水加成的最大值
                {
                    Player p = Main.player[i];
                    if (p != null && p.active && !p.dead)
                    {
                        foundAnyPlayer = true;
                        CheckPotionBuffs(p, out int pDef, out float pDmg, out float pLife, out float pDR, out float pSpeed);

                        maxDef = Math.Max(maxDef, pDef);
                        maxDmg = Math.Max(maxDmg, pDmg);
                        maxLife = Math.Max(maxLife, pLife);
                        maxDR = Math.Max(maxDR, pDR);
                        maxSpeed = Math.Max(maxSpeed, pSpeed);
                    }
                }

                // 没有任何玩家存活，或者所有加成均为0，返回
                if (!foundAnyPlayer || (maxDef <= 0 && maxDmg <= 0f && maxLife <= 0f && maxDR <= 0f && maxSpeed <= 0f)) return;

                ApplyBonuses(npc, maxDef, maxDmg, maxLife, maxDR, maxSpeed);
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
        /// 检测玩家身上的药水 Buff，并累加对应的加成数值
        /// </summary>
        private static void CheckPotionBuffs(Player target, out int totalDef, out float totalDmg, out float totalLife, out float totalDR, out float totalSpeed)
        {
            totalDef = 0;
            totalDmg = 0f;
            totalLife = 0f;
            totalDR = 0f;
            totalSpeed = 0f;

            #region 防御类
            if (target.HasBuff(BuffID.Ironskin)) totalDef += 8; // 铁皮药水
            if (target.HasBuff(BuffID.CatBast)) totalDef += 8; // 韧皮雕像
            #endregion

            #region 伤害类
            if (target.HasBuff(BuffID.Wrath)) totalDmg += 0.10f; // 怒气药水
            if (target.HasBuff(BuffID.MagicPower)) totalDmg += 0.20f; // 魔能药水
            if (target.HasBuff(BuffID.Archery)) totalDmg += 0.10f; // 箭术药水
            #endregion

            #region 生命类
            if (target.HasBuff(BuffID.Lifeforce)) totalLife += 0.20f; // 生命药水
            #endregion

            #region 减伤类
            if (target.HasBuff(BuffID.Endurance)) totalDR += 0.10f; // 耐力药水
            #endregion

            #region 移动速度类
            if (target.HasBuff(BuffID.Swiftness)) totalSpeed += 0.25f; // 敏捷药水
            if (target.HasBuff(BuffID.SugarRush)) totalSpeed += 0.20f; // 糖果冲刺（蛋糕块）
            #endregion

            #region 食物类（取最高等级）
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
            #endregion

            #region 模组 Buff 兼容
            int tankBuff = GetTankBuffID(); // 泰坦药剂包
            if (tankBuff != -1 && target.HasBuff(tankBuff))
            {
                totalDef += 8;
                totalDmg += 0.10f;
                totalLife += 0.20f;
            }
            #endregion
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
        /// 若受击的玩家身上有暴怒药水，则本次伤害有概率翻倍
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
            for (int i = 1; i < BuffLoader.BuffCount; i++)
            {
                string name = Lang.GetBuffName(i);
                if (!string.IsNullOrEmpty(name) && name.Contains("坦克药剂包"))
                {
                    _tankBuffId = i;
                    return _tankBuffId;
                }
            }
            _tankBuffId = -1;
            return _tankBuffId;
        }
        #endregion
    }
}