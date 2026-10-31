using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Bosses
{
    internal class SkeletronAIOverride : GlobalNPC
    {
        public const short WaterSphereNPCID = 33; // 水球 NPC ID

        public override bool PreAI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return true;
            if (npc.type == NPCID.DarkCaster) // 处理暗黑法师
            {
                if (!NPC.AnyNPCs(NPCID.SkeletronHead))
                {
                    //npc.active = false;
                    return true;
                }

                // 骷髅王存活时：无敌、无接触伤害
                npc.dontTakeDamage = true;
                npc.damage = 0;

                CustomCasterAI(npc); // 接管原版法师 AI
                return false;
            }

            TestSummonCaster(npc); // 骷髅王强制召唤法师

            return true;
        }

        #region 骷髅王强制召唤法师（测试用）
        private static void TestSummonCaster(NPC npc)
        {
            // 骷髅王强制召唤法师
            if (npc.type == NPCID.SkeletronHead)
            {
                if (npc.ai[1] == 1f && Main.netMode != NetmodeID.MultiplayerClient && npc.ai[2] % 120f == 0f)
                {
                    int casterCount = 0;
                    for (int i = 0; i < Main.maxNPCs; i++)
                        if (Main.npc[i].active && Main.npc[i].type == NPCID.DarkCaster) casterCount++;

                    if (casterCount < 4)
                    {
                        Player target = Main.player[npc.target];
                        int tileX = (int)(target.Center.X / 16) + Main.rand.Next(-20, 21);
                        int tileY = (int)(target.Center.Y / 16) + Main.rand.Next(-15, 15);
                        if (WorldGen.InWorld(tileX, tileY) && !WorldGen.SolidTile(tileX, tileY) && !WorldGen.SolidTile(tileX, tileY - 1))
                        {
                            int idx = NPC.NewNPC(npc.GetSource_FromAI(), tileX * 16, tileY * 16, NPCID.DarkCaster);
                            if (idx < Main.maxNPCs) Main.npc[idx].netUpdate = true;
                        }
                    }
                }
            }
        }
        #endregion

        // 自定义法师 AI
        private static void CustomCasterAI(NPC npc)
        {
            if (npc.target < 0 || npc.target == 255 || Main.player[npc.target].dead || !Main.player[npc.target].active)
                npc.TargetClosest();
            Player player = Main.player[npc.target];
            npc.spriteDirection = npc.direction = (player.Center.X > npc.Center.X) ? 1 : -1;

            // 初始化
            if (npc.localAI[0] == 0f)
            {
                npc.localAI[0] = 1f;
                npc.ai[0] = 120f;
                npc.ai[1] = 0f; // 使用 ai[1] 记录施法次数
            }

            // 如果处于传送倒计时中
            if (npc.localAI[2] > 0f)
            {
                npc.localAI[2] -= 1f;
                npc.velocity *= 0.9f; // 施法期间原地减速悬浮

                if (npc.localAI[2] <= 0f)
                {
                    TeleportCaster(npc, player); // 倒计时结束，执行传送
                    npc.ai[1] = 0f; // 重置施法次数
                    npc.ai[0] = 120f; // 重置攻击冷却
                }
                return;
            }

            // 持续计时，达到攻击冷却时发射
            npc.ai[0] -= 1f;
            if (npc.ai[0] <= 0f && Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 3; i++) // 发射 3 发水球
                {
                    Vector2 shootVel = npc.DirectionTo(player.Center).RotatedByRandom(0.2) * 8f;
                    int sphereIdx = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, WaterSphereNPCID);
                    if (sphereIdx < Main.maxNPCs)
                    {
                        Main.npc[sphereIdx].velocity = shootVel;
                        // 第 1,3,5 轮（ai[1] 为 0,2,4）可摧毁。第 2,4 轮不可摧毁。
                        Main.npc[sphereIdx].ai[0] = (npc.ai[1] % 2 == 0) ? 1f : 0f;
                        Main.npc[sphereIdx].netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item8, npc.Center);

                npc.ai[1] += 1f; // 施法次数 +1

                // 如果已经施法 5 次，进入传送倒计时
                if (npc.ai[1] >= 5f)
                    npc.localAI[2] = 60f;
                else
                    npc.ai[0] = 120f; // 没满 5 次，重置冷却继续攻击
            }
        }

        // 传送逻辑
        private static void TeleportCaster(NPC npc, Player player)
        {
            for (int i = 0; i < 20; i++) Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror);
            for (int attempts = 0; attempts < 100; attempts++)
            {
                int tx = (int)(player.Center.X / 16) + Main.rand.Next(-20, 21);
                int ty = (int)(player.Center.Y / 16) + Main.rand.Next(-15, 16);
                if (WorldGen.InWorld(tx, ty) && !WorldGen.SolidTile(tx, ty) && !WorldGen.SolidTile(tx, ty - 1))
                {
                    npc.position = new Vector2(tx * 16, ty * 16);
                    npc.netUpdate = true;
                    break;
                }
            }
            for (int i = 0; i < 20; i++) Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror);
        }
    }

    internal class WaterSphereNPC : GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return true;
            if (npc.type != SkeletronAIOverride.WaterSphereNPCID) return true;
            npc.dontTakeDamage = npc.ai[0] == 1f; // 不可摧毁状态强制无敌
            return true; // 正常执行水球飞行追踪
        }
    }
}