using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class BrainofCthulhuAIOverride : GlobalNPC
    {
        private const float DamageBonusMultiplier = 1.1f; // 伤害较传奇难度增加的倍率
        private const int ForcedCreeperCount = 50; // 生成的飞眼怪数量
        private const int Phase2DefenseBonus = 9; // 二阶段防御增加值

        #region 伤害/防御
        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.BrainofCthulhu
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                // 伤害较传奇难度+10%
                npc.damage = (int)(npc.defDamage * DamageBonusMultiplier);

                // 二阶段防御+9
                if (npc.ai[0] < 0f)
                    npc.defense = npc.defDefense + Phase2DefenseBonus;
            }
        }
        #endregion

        #region PreAI 拦截
        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.BrainofCthulhu
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                CustomBrainAI(npc);
                return false;
            }
            return true;
        }
        #endregion

        #region 主 AI
        private static void CustomBrainAI(NPC npc)
        {
            float[] ai = npc.ai;
            float[] localAI = npc.localAI;

            NPC.crimsonBoss = npc.whoAmI; // 让飞眼怪跟着克脑

            // 生成 50 只飞眼怪
            if (Main.netMode != NetmodeID.MultiplayerClient && localAI[0] == 0f)
            {
                localAI[0] = 1f;
                for (int i = 0; i < ForcedCreeperCount; i++)
                {
                    float x = npc.Center.X + Main.rand.Next(-npc.width, npc.width);
                    float y = npc.Center.Y + Main.rand.Next(-npc.height, npc.height);

                    // 生成飞眼怪
                    int creeperIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)x, (int)y, NPCID.Creeper);
                    if (creeperIndex < Main.maxNPCs)
                    {
                        Main.npc[creeperIndex].velocity = new Vector2(Main.rand.Next(-30, 31) * 0.1f, Main.rand.Next(-30, 31) * 0.1f);
                        Main.npc[creeperIndex].netUpdate = true;
                    }
                }
            }

            // 脱战检测
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.TargetClosest();
                int despawnRange = 6000;
                if (Math.Abs(npc.Center.X - Main.player[npc.target].Center.X) + Math.Abs(npc.Center.Y - Main.player[npc.target].Center.Y) > despawnRange)
                {
                    npc.active = false;
                    npc.life = 0;
                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                    }
                    return;
                }
            }

            if (ai[0] >= 0f) // 一阶段
            {
                npc.TargetClosest();
                Vector2 center = npc.Center;
                float dx = Main.player[npc.target].Center.X - center.X;
                float dy = Main.player[npc.target].Center.Y - center.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float speed = 1f;
                if (Main.getGoodWorld) speed *= 3f;

                if (dist < speed)
                {
                    npc.velocity.X = dx;
                    npc.velocity.Y = dy;
                }
                else
                {
                    dist = speed / dist;
                    npc.velocity.X = dx * dist;
                    npc.velocity.Y = dy * dist;
                }

                if (ai[0] == 0f) // 悬浮，检测飞眼怪数量
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int creepers = 0;
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            if (Main.npc[i].active && Main.npc[i].type == NPCID.Creeper)
                                creepers++;
                        }

                        // 如果飞眼怪全死光了，切换二阶段
                        if (creepers == 0)
                        {
                            ai[0] = -1f;
                            localAI[1] = 0f;
                            npc.alpha = 0;
                            npc.netUpdate = true;
                        }
                        localAI[1] += 1f;
                        if (localAI[1] >= 120 + Main.rand.Next(300))
                        {
                            localAI[1] = 0f;
                            npc.TargetClosest();
                            int attempts = 0;
                            Player player = Main.player[npc.target];
                            do
                            {
                                attempts++;
                                int tx = (int)player.Center.X / 16;
                                int ty = (int)player.Center.Y / 16;
                                int min = 12;
                                int max = 40;
                                float mult = 16f;
                                int dirX = Main.rand.Next(min, max + 1);
                                int dirY = Main.rand.Next(min, max + 1);
                                if (Main.rand.NextBool(2)) dirX *= -1;
                                if (Main.rand.NextBool(2)) dirY *= -1;
                                Vector2 predict = new(dirX * 16, dirY * 16);
                                if (Vector2.Dot(player.velocity.SafeNormalize(Vector2.UnitY), predict.SafeNormalize(Vector2.UnitY)) > 0f)
                                {
                                    predict += predict.SafeNormalize(Vector2.Zero) * mult * player.velocity.Length();
                                }
                                tx += (int)(predict.X / 16f);
                                ty += (int)(predict.Y / 16f);
                                if (attempts > 100 || (!WorldGen.SolidTile(tx, ty) && (attempts > 75 || Collision.CanHit(new Vector2(tx * 16, ty * 16), 1, 1, player.position, player.width, player.height))))
                                {
                                    ai[0] = 1f;
                                    ai[1] = tx;
                                    ai[2] = ty;
                                    npc.netUpdate = true;
                                    break;
                                }
                            } while (attempts <= 100);
                        }
                    }
                }
                else if (ai[0] == 1f) // 传送消失
                {
                    npc.alpha += 5;
                    if (npc.alpha >= 255)
                    {
                        SoundEngine.PlaySound(SoundID.Item8, npc.Center);
                        npc.alpha = 255;
                        npc.position.X = ai[1] * 16f - (npc.width / 2);
                        npc.position.Y = ai[2] * 16f - (npc.height / 2);
                        ai[0] = 2f;
                    }
                }
                else if (ai[0] == 2f) // 传送出现
                {
                    npc.alpha -= 5;
                    if (npc.alpha <= 0)
                    {
                        npc.alpha = 0;
                        ai[0] = 0f;
                    }
                }
            }
            else // 二阶段
            {
                if (Main.getGoodWorld) NPC.brainOfGravity = npc.whoAmI;
                if (localAI[2] == 0f) // 进入二阶段初次爆裂
                {
                    SoundEngine.PlaySound(SoundID.NPCDeath1, npc.Center);
                    localAI[2] = 1f;
                    for (int i = 392; i <= 395; i++)
                    {
                        Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), i);
                    }
                    for (int i = 0; i < 20; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f);
                    }
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                }

                npc.dontTakeDamage = false;
                npc.TargetClosest();
                Vector2 center = npc.Center;
                float dx = Main.player[npc.target].Center.X - center.X;
                float dy = Main.player[npc.target].Center.Y - center.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float speed = 8f;
                dist = speed / dist;
                dx *= dist;
                dy *= dist;

                npc.velocity.X = (npc.velocity.X * 50f + dx) / 51f;
                npc.velocity.Y = (npc.velocity.Y * 50f + dy) / 51f;

                if (ai[0] == -1f) // 追踪玩家，准备传送
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        localAI[1] += 1f;
                        if (npc.justHit) localAI[1] -= Main.rand.Next(5);

                        int timer = 60 + Main.rand.Next(120);
                        if (Main.netMode != NetmodeID.SinglePlayer) timer += Main.rand.Next(30, 90);

                        if (localAI[1] >= timer)
                        {
                            localAI[1] = 0f;
                            npc.TargetClosest();
                            int attempts = 0;
                            Player player = Main.player[npc.target];
                            do
                            {
                                attempts++;
                                int tx = (int)player.Center.X / 16;
                                int ty = (int)player.Center.Y / 16;
                                int min = 10;
                                int max = 12;
                                float mult = 16f;
                                int dirX = Main.rand.Next(min, max + 1);
                                int dirY = Main.rand.Next(min, max + 1);
                                if (Main.rand.NextBool(2)) dirX *= -1;
                                if (Main.rand.NextBool(2)) dirY *= -1;
                                Vector2 predict = new(dirX * 16, dirY * 16);
                                if (Vector2.Dot(player.velocity.SafeNormalize(Vector2.UnitY), predict.SafeNormalize(Vector2.UnitY)) > 0f)
                                {
                                    predict += predict.SafeNormalize(Vector2.Zero) * mult * player.velocity.Length();
                                }
                                tx += (int)(predict.X / 16f);
                                ty += (int)(predict.Y / 16f);
                                if (attempts > 100 || !WorldGen.SolidTile(tx, ty))
                                {
                                    ai[3] = 0f;
                                    ai[0] = -2f;
                                    ai[1] = tx;
                                    ai[2] = ty;
                                    npc.netUpdate = true;
                                    break;
                                }
                            } while (attempts <= 100);
                        }
                    }
                }
                else if (ai[0] == -2f) // 消失阶段
                {
                    npc.velocity *= 0.9f;
                    if (Main.netMode != NetmodeID.SinglePlayer) ai[3] += 15f;
                    else ai[3] += 25f;

                    if (ai[3] >= 255f)
                    {
                        ai[3] = 255f;
                        npc.position.X = ai[1] * 16f - (npc.width / 2);
                        npc.position.Y = ai[2] * 16f - (npc.height / 2);
                        SoundEngine.PlaySound(SoundID.Item8, npc.Center);
                        ai[0] = -3f;
                        npc.netUpdate = true;
                    }
                    npc.alpha = (int)ai[3];
                }
                else if (ai[0] == -3f) // 出现阶段
                {
                    if (Main.netMode != NetmodeID.SinglePlayer) ai[3] -= 15f;
                    else ai[3] -= 25f;

                    if (ai[3] <= 0f)
                    {
                        ai[3] = 0f;
                        ai[0] = -1f;
                        npc.netUpdate = true;
                    }
                    npc.alpha = (int)ai[3];
                }
            }

            // 脱战与地形限制
            if (Main.player[npc.target].dead || !Main.player[npc.target].ZoneCrimson)
            {
                if (localAI[3] < 120f) localAI[3]++;
                if (localAI[3] > 60f) npc.velocity.Y += (localAI[3] - 60f) * 0.25f;
                ai[0] = 2f;
                npc.alpha = 10;
            }
            else if (localAI[3] > 0f)
            {
                localAI[3]--;
            }
        }
        #endregion
    }
}