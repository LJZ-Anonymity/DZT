using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class TheDestroyerAIOverride : GlobalNPC
    {
        // 必须开启每个实体独立实例，才能为各节保存状态
        public override bool InstancePerEntity => true;

        private const float UnfurledDamageReduction = 0.9999f; // 未展开时减伤倍率

        // 标记该条毁灭者是否已完成初始化与展开
        public bool Initialized = false;
        public bool HasUnfurled = false;

        public override bool PreAI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return true;

            if (npc.type == NPCID.TheDestroyer || npc.type == NPCID.TheDestroyerBody || npc.type == NPCID.TheDestroyerTail)
            {
                // 统一以头部作为主控状态
                NPC head = npc.type == NPCID.TheDestroyer ? npc :
                          (npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : null);

                if (head != null && head.active)
                {
                    var headData = head.GetGlobalNPC<TheDestroyerAIOverride>();
                    if (!headData.Initialized)
                    {
                        npc.life = npc.lifeMax;
                        headData.Initialized = true;
                    }

                    if (!headData.HasUnfurled)
                    {
                        if (!IsDestroyerUnfurling(head))
                        {
                            headData.HasUnfurled = true;
                        }
                    }
                }
            }
            return true;
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.TheDestroyer || npc.type == NPCID.TheDestroyerBody || npc.type == NPCID.TheDestroyerTail)
            {
                NPC head = npc.type == NPCID.TheDestroyer ? npc :
                          (npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : null);

                if (head != null && head.active)
                {
                    var headData = head.GetGlobalNPC<TheDestroyerAIOverride>();
                    // 只要未展开，全体节享受 99.99% 伤害减免
                    if (!headData.HasUnfurled)
                    {
                        modifiers.FinalDamage *= (1 - UnfurledDamageReduction);
                    }
                }
            }
        }

        /// <summary>
        /// 通过以尾部为中心的圆判断毁灭者是否已展开
        /// </summary>
        private static bool IsDestroyerUnfurling(NPC head)
        {
            // 顺着 ai[0] 链收集所有体节，确保按物理连接顺序排列
            var segments = new System.Collections.Generic.List<NPC>();
            int currentIndex = head.whoAmI;
            int safetyCounter = 0;

            while (currentIndex > 0 && currentIndex < Main.maxNPCs && safetyCounter < 200)
            {
                NPC current = Main.npc[currentIndex];
                if (!current.active) break;

                segments.Add(current);

                // 遇到尾部，结束收集
                if (current.type == NPCID.TheDestroyerTail) break;

                currentIndex = (int)current.ai[0]; // ai[0] 指向下一节
                safetyCounter++;
            }

            // 如果没找到尾巴或者体节太少（还在生成阶段），未展开
            if (segments.Count < 10) return true;

            NPC tail = segments[segments.Count - 1];
            if (tail.type != NPCID.TheDestroyerTail) return true;

            // 原版单节距离约 44f * scale，3 节长度约为 132，加上贴图裕量，取 150f
            // 防止最近几节正常距离被误判，排除尾部以及它前面的 4 节（共 5 节）
            float radius = 150f;
            float radiusSq = radius * radius;
            int excludeCount = 4;

            // 计算需要检查的结束索引（从第 0 节检查到倒数第 5 节之前）
            int checkEndIndex = segments.Count - 1 - excludeCount;

            // 检查远处的体节是否侵入了尾部的安全圆
            for (int i = 0; i <= checkEndIndex; i++)
            {
                NPC seg = segments[i];
                // 使用 DistanceSquared 避免开方，提升性能
                if (Vector2.DistanceSquared(seg.Center, tail.Center) < radiusSq)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal class DestroyerSegmentPatch : ModSystem
    {
        private const int TotalSegments = 100; // 总体节数

        public override void Load()
        {
            var method = typeof(NPC).GetMethod(nameof(NPC.GetDestroyerSegmentsCount),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                Func<Func<NPC, int>, NPC, int> hookDelegate = (orig, self) =>
                {
                    return TotalSegments - 2;
                };

                MonoModHooks.Add(method, hookDelegate);
            }
        }
    }
}