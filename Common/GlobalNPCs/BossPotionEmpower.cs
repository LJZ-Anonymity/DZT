using Terraria.ModLoader;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Common.GlobalNPCs
{
    public class BossPotionEmpower : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private float _bossSpeedMultiplier = 1f; // 存储Boss移速倍率，用于每帧应用
        private bool scalingApplied; // 是否完成难度缩放
        private bool hasApplied;     // 是否已应用药水加成

        public override void ApplyDifficultyAndPlayerScaling(NPC npc, int numPlayers, float balance, float bossAdjustment)
        {
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

                // 如果所有加成均为0，直接返回
                if (totalDef <= 0 && totalDmg <= 0f && totalLife <= 0f && totalDR <= 0f && totalSpeed <= 0f) return;

                ApplyBonuses(npc, totalDef, totalDmg, totalLife, totalDR, totalSpeed);
                hasApplied = true;
            }

            // 每帧持续应用移速加成
            if (hasApplied && _bossSpeedMultiplier != 1f)
            {
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

            // 生命类
            if (target.HasBuff(BuffID.Lifeforce)) totalLife += 0.20f; // 生命药水

            // 减伤类
            if (target.HasBuff(BuffID.Endurance)) totalDR += 0.10f; // 耐力药水

            // 移动速度类
            if (target.HasBuff(BuffID.Swiftness)) totalSpeed += 0.25f; // 敏捷药水 +25%
            if (target.HasBuff(BuffID.SugarRush)) totalSpeed += 0.20f; // 糖果冲刺（蛋糕块） +20%

            // --- 模组 Buff 兼容 ---
            int tankBuff = GetTankBuffID(); // 获取模组中的坦克药剂包
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
            // 标记已应用
            hasApplied = true;

            if (totalDef > 0)
            {
                npc.defense += (int)Math.Ceiling(totalDef * 0.20f);
            }

            if (totalDmg > 0f)
            {
                npc.damage = (int)(npc.defDamage * (1f + totalDmg * 0.20f));
            }

            if (totalLife > 0f)
            {
                float bonus = 1f + totalLife * 0.20f;
                npc.lifeMax = (int)(npc.lifeMax * bonus);
                npc.life = npc.lifeMax; // 强制同步满血
            }

            if (totalDR > 0f)
            {
                float scaledDR = totalDR * 0.20f;
                npc.takenDamageMultiplier *= (1f - scaledDR);
            }

            if (totalSpeed > 0f)
            {
                _bossSpeedMultiplier = 1f + totalSpeed * 0.20f;
            }

            npc.netUpdate = true;
            Main.NewText($"成功强化Boss！生命:{1f + totalLife * 0.20f}% 防御:{totalDef} 伤害:{totalDmg * 100}% 减伤:{totalDR * 100}% 移速:{totalSpeed * 100}%");
        }

        /// <summary>
        /// 调试用：打印玩家当前所有激活的 Buff（ID / 名称 / 剩余时间）。
        /// 单人模式下直接传 Main.LocalPlayer 即可。
        /// </summary>
        public static void DumpPlayerBuffs(Player player)
        {
            // 使用 LocalPlayer 作为兜底（单人档专用）
            player ??= Main.LocalPlayer;

            Main.NewText($"===== [{player.name}] Buff 列表明细 =====", 255, 255, 0); // 黄色提示

            int activeCount = 0;

            // Player.buffType 和 buffTime 是平行数组，长度固定（原版为 44，tModLoader 可能扩展）
            for (int i = 0; i < player.buffType.Length; i++)
            {
                int buffId = player.buffType[i];

                // buffType 为 0 表示该槽位为空，跳过
                if (buffId <= 0) continue;

                // 获取剩余时间（tick），并转为秒
                int timeLeft = player.buffTime[i];
                float secondsLeft = timeLeft / 60f;

                // 使用 Lang.GetBuffName 获取本地化的 Buff 名称[reference:0]
                string buffName = Lang.GetBuffName(buffId);

                // 输出：槽位索引 | Buff ID | 名称 | 剩余秒数
                Main.NewText(
                    $"  槽位{i:D2} | ID:{buffId:D3} | {buffName} | {secondsLeft:F1}s",
                    180, 220, 255 // 淡蓝色
                );

                activeCount++;
            }

            if (activeCount == 0)
            {
                Main.NewText("  （该玩家当前没有任何激活的 Buff）", 255, 100, 100);
            }
            else
            {
                Main.NewText($"===== 共 {activeCount} 个激活 Buff =====", 255, 255, 0);
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

            // 遍历游戏内注册的所有 Buff（数量由 BuffLoader.BuffCount 决定，源码中有使用）
            for (int i = 1; i < BuffLoader.BuffCount; i++)
            {
                // 获取该 Buff 的本地化显示名称（如“坦克药剂包 (黄金)”）
                string name = Lang.GetBuffName(i);

                // 模糊匹配名字中包含“坦克药剂包”的 Buff
                if (!string.IsNullOrEmpty(name) && name.Contains("坦克药剂包"))
                {
                    _tankBuffId = i;
                    Main.NewText($"成功动态定位模组Buff: ID {i} - {name}", 0, 255, 0);
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