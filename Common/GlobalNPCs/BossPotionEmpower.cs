using Terraria.ModLoader;
using DZT.Common.Systems;
using DZT.Common.Utils;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Common.GlobalNPCs
{
    public class BossPotionEmpower : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private const float RageDoubleDamageChance = 0.20f; // BOSS双倍伤害概率
        private readonly bool _hasLazyLoaded = false;
        private float _bossSpeedMultiplier = 1f; // Boss移速倍率
        private bool scalingApplied; // 是否完成难度缩放
        private bool hasApplied; // 是否已应用药水加成

        private static int _tankBuffId = -2;
        private static int _rangerBuffId = -2;
        private static int _combatBuffId = -2;
        private static int _magicBuffId = -2;
        private static int _summonerBuffId = -2;
        private static int _ultimateBuffId = -2;
        private static int _fishingBuffId = -2;

        public override void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (!npc.boss) return;
            scalingApplied = true;
        }

        public override void PostAI(NPC npc)
        {
            if (!npc.boss || !scalingApplied) return;
            if (!_hasLazyLoaded) LazyLoad();
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
        /// 懒加载
        /// </summary>
        private static void LazyLoad()
        {
            _combatBuffId = BuffUtils.GetModBuffID("战斗药剂包", "Combat Combination");
            _magicBuffId = BuffUtils.GetModBuffID("魔法药剂包", "Magic Combination");
            _rangerBuffId = BuffUtils.GetModBuffID("射手药剂包", "Ranger Combination");
            _summonerBuffId = BuffUtils.GetModBuffID("召唤师药剂包", "Summoner Combination");
            _tankBuffId = BuffUtils.GetModBuffID("坦克药剂包", "Tank Combination");
            _ultimateBuffId = BuffUtils.GetModBuffID("万能药剂包", "Ultimate Combination");
            _fishingBuffId = BuffUtils.GetModBuffID("钓鱼药剂包", "Fishing Combination");
        }

        /// <summary>
        /// 检测玩家身上的药水 Buff，并累加对应的加成数值
        /// </summary>
        private static void CheckPotionBuffs(Player target, out int totalDef, out float totalDmg, out float totalLife, out float totalDR, out float totalSpeed)
        {
            // 原版 Buff
            int vanillaDef = 0;
            float vanillaDmg = 0f;
            float vanillaLife = 0f;
            float vanillaDR = 0f;
            float vanillaSpeed = 0f;

            #region 原版 Buff
            if (target.HasBuff(BuffID.Ironskin)) vanillaDef += 8; // 铁皮药水
            if (target.HasBuff(BuffID.CatBast)) vanillaDef += 8; // 韧皮雕像

            if (target.HasBuff(BuffID.Wrath)) vanillaDmg += 0.10f; // 怒气药水
            if (target.HasBuff(BuffID.MagicPower)) vanillaDmg += 0.20f; // 魔能药水
            if (target.HasBuff(BuffID.Archery)) vanillaDmg += 0.10f; // 箭术药水

            if (target.HasBuff(BuffID.Lifeforce)) vanillaLife += 0.20f; // 生命药水
            if (target.HasBuff(BuffID.Endurance)) vanillaDR += 0.10f; // 耐力药水

            if (target.HasBuff(BuffID.Swiftness)) vanillaSpeed += 0.25f; // 敏捷药水
            if (target.HasBuff(BuffID.SugarRush)) vanillaSpeed += 0.20f; // 糖果冲刺

            // 食物类
            if (target.HasBuff(BuffID.WellFed3)) { vanillaDef += 4; vanillaDmg += 0.10f; vanillaSpeed += 0.40f; }
            else if (target.HasBuff(BuffID.WellFed2)) { vanillaDef += 3; vanillaDmg += 0.075f; vanillaSpeed += 0.30f; }
            else if (target.HasBuff(BuffID.WellFed)) { vanillaDef += 2; vanillaDmg += 0.05f; vanillaSpeed += 0.20f; }
            #endregion

            // 模组药剂等效（多个药剂包同类属性取最大值）
            int packDef = 0;
            float packDmg = 0f;
            float packLife = 0f;
            float packDR = 0f;
            float packSpeed = 0f;

            #region 模组兼容
            #region AlchemistNPC Lite
            if (_combatBuffId != -1 && target.HasBuff(_combatBuffId)) // 战斗药剂包
            {
                packDef = Math.Max(packDef, 8); // 铁皮8防
                packDmg = Math.Max(packDmg, 0.10f); // 怒气10%增伤
                packLife = Math.Max(packLife, 0.20f); // 生命力20%生命
                packDR = Math.Max(packDR, 0.10f); // 耐力10%减伤
            }

            if (_tankBuffId != -1 && target.HasBuff(_tankBuffId)) // 坦克药剂包
            {
                packDef = Math.Max(packDef, 8); // 铁皮8防
                packLife = Math.Max(packLife, 0.20f); // 生命力20%生命
                packDR = Math.Max(packDR, 0.10f); // 耐力10%减伤
            }

            if (_magicBuffId != -1 && target.HasBuff(_magicBuffId)) // 魔法药剂包
            {
                packDmg = Math.Max(packDmg, 0.30f); // 魔能20% + 怒气10%增伤
            }

            if (_rangerBuffId != -1 && target.HasBuff(_rangerBuffId)) // 射手药剂包
            {
                packDmg = Math.Max(packDmg, 0.20f); // 箭术10% + 怒气10%增伤
            }

            if (_summonerBuffId != -1 && target.HasBuff(_summonerBuffId)) // 召唤师药剂包
            {
                packDmg = Math.Max(packDmg, 0.10f); // 怒气10%增伤
            }

            if (_fishingBuffId != -1 && target.HasBuff(_fishingBuffId)) // 钓鱼药剂包
            {
                packDef = Math.Max(packDef, 8); // 铁皮8防
            }

            if (_ultimateBuffId != -1 && target.HasBuff(_ultimateBuffId)) // 万能药剂包
            {
                packDef = Math.Max(packDef, 8); // 铁皮8防
                packDmg = Math.Max(packDmg, 0.40f); // 魔能20% + 箭术10% + 怒气10%增伤
                packLife = Math.Max(packLife, 0.20f); // 生命力20%生命
                packDR = Math.Max(packDR, 0.10f); // 耐力10%减伤
            }
            #endregion
            #endregion

            // 最终结果 = 原版单体总和 + 药剂包最高值
            totalDef = vanillaDef + packDef;
            totalDmg = vanillaDmg + packDmg;
            totalLife = vanillaLife + packLife;
            totalDR = vanillaDR + packDR;
            totalSpeed = vanillaSpeed + packSpeed;
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
        /// 若受击的玩家有暴怒药水效果，则本次伤害有概率翻倍
        /// </summary>
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (!npc.boss || !scalingApplied) return;
            if (Main.rand.NextFloat() >= RageDoubleDamageChance) return;

            bool hasRage = target.HasBuff(BuffID.Rage); // 暴怒药水
            #region 含暴怒药水效果的模组药水
            bool hasCombatCombo = (_combatBuffId != -1 && target.HasBuff(_combatBuffId)); // 战斗药剂包
            bool hasMagicCombo = (_magicBuffId != -1 && target.HasBuff(_magicBuffId)); // 魔法药剂包
            bool hasRangerCombo = (_rangerBuffId != -1 && target.HasBuff(_rangerBuffId)); // 射手药剂包
            bool hasSummonerCombo = (_summonerBuffId != -1 && target.HasBuff(_summonerBuffId)); // 召唤师药剂包
            bool hasUltimateCombo = (_ultimateBuffId != -1 && target.HasBuff(_ultimateBuffId)); // 终极药剂包
            #endregion
            if (hasRage || hasCombatCombo || hasMagicCombo || hasRangerCombo || hasSummonerCombo || hasUltimateCombo)
            {
                modifiers.FinalDamage *= 2f; // 本次伤害翻倍
            }
        }
    }
}