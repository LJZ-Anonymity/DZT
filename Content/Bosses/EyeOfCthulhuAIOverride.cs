using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class EyeOfCthulhuAIOverride : GlobalNPC
    {
        private const float SpeedFactor = 3f; // 加速倍率

        #region 减伤/判断冲刺状态

        // 冲刺期间 20% 伤害减免
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.EyeofCthulhu
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive
                && IsDashing(npc))
            {
                modifiers.FinalDamage *= 0.8f;
            }
        }

        // 判断当前是否处于冲刺状态
        public static bool IsDashing(NPC npc)
        {
            if (npc.type != NPCID.EyeofCthulhu) return false;

            // 一阶段冲刺中
            if (npc.ai[0] == 0f && npc.ai[1] == 2f) return true;
            // 三阶段普通冲刺中 / 高速冲刺中
            if (npc.ai[0] == 3f && (npc.ai[1] == 2f || npc.ai[1] == 4f)) return true;

            return false;
        }

        #endregion

        #region 拦截PreAI

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.EyeofCthulhu
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                CustomEyeAI(npc);
                return false;
            }
            return true;
        }

        #endregion

        #region 主 AI

        private static void CustomEyeAI(NPC npc)
        {
            float[] ai = npc.ai;
            float[] localAI = npc.localAI;

            // 冲刺次数计数器
            if (localAI[0] < 0f) localAI[0] = 0f;

            // 低血量狂暴判定
            bool flag2 = Main.expertMode && npc.life < npc.lifeMax * 0.12;
            bool flag3 = Main.expertMode && npc.life < npc.lifeMax * 0.04;

            // 高速冲刺持续时间（间隔）
            float num4 = (flag3 ? 10f : 20f) / SpeedFactor;

            // 目标选择
            if (npc.target < 0 || npc.target == 255
                || Main.player[npc.target].dead
                || !Main.player[npc.target].active)
            {
                npc.TargetClosest();
            }

            Player player = Main.player[npc.target];
            bool dead = player.dead;

            // 朝向计算
            float num5 = npc.position.X + npc.width / 2f - player.position.X - player.width / 2f;
            float num6 = npc.position.Y + npc.height - 59f - player.position.Y - player.height / 2f;
            float num7 = (float)Math.Atan2(num6, num5) + 1.57f;
            if (num7 < 0f) num7 += 6.283f;
            else if (num7 > 6.283f) num7 -= 6.283f;

            // 旋转插值速度
            float num8 = 0f;
            if (ai[0] == 0f && ai[1] == 0f) num8 = 0.02f * SpeedFactor;
            if (ai[0] == 0f && ai[1] == 2f && ai[2] > 40f) num8 = 0.05f * SpeedFactor;
            if (ai[0] == 3f && ai[1] == 0f) num8 = 0.05f * SpeedFactor;
            if (ai[0] == 3f && ai[1] == 2f && ai[2] > 40f) num8 = 0.08f * SpeedFactor;
            if (ai[0] == 3f && ai[1] == 4f && ai[2] > num4) num8 = 0.15f * SpeedFactor;
            if (ai[0] == 3f && ai[1] == 5f) num8 = 0.05f * SpeedFactor;

            if (Main.expertMode) num8 *= 1.5f;
            if (flag3 && Main.expertMode) num8 = 0f;

            // 平滑旋转
            if (npc.rotation < num7)
            {
                if (num7 - npc.rotation > 3.1415f) npc.rotation -= num8;
                else npc.rotation += num8;
            }
            else if (npc.rotation > num7)
            {
                if (npc.rotation - num7 > 3.1415f) npc.rotation += num8;
                else npc.rotation -= num8;
            }

            if (npc.rotation > num7 - num8 && npc.rotation < num7 + num8) npc.rotation = num7;
            if (npc.rotation < 0f) npc.rotation += 6.283f;
            else if (npc.rotation > 6.283f) npc.rotation -= 6.283f;
            if (npc.rotation > num7 - num8 && npc.rotation < num7 + num8) npc.rotation = num7;

            // 血迹粒子
            if (Main.rand.NextBool(5))
            {
                int num9 = Dust.NewDust(
                    new Vector2(npc.position.X, npc.position.Y + npc.height * 0.25f),
                    npc.width, (int)(npc.height * 0.5f),
                    DustID.Blood, npc.velocity.X, 2f);
                Main.dust[num9].velocity.X *= 0.5f;
                Main.dust[num9].velocity.Y *= 0.1f;
            }

            npc.reflectsProjectiles = false;

            // 白天 / 目标死亡
            if (Main.IsItDay() || dead)
            {
                npc.velocity.Y -= 0.04f;
                npc.EncourageDespawn(10);
                return;
            }

            #region 第一阶段

            if (ai[0] == 0f)
            {
                if (ai[1] == 0f) // 悬浮追踪
                {
                    // 移速
                    float num10 = 5f * SpeedFactor;
                    float num11 = 0.04f * SpeedFactor;
                    if (Main.expertMode) { num11 = 0.15f * SpeedFactor; num10 = 7f * SpeedFactor; }
                    if (Main.getGoodWorld) { num11 += 0.05f * SpeedFactor; num10 += 1f * SpeedFactor; }

                    Vector2 vector = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                    float num12 = player.position.X + player.width / 2f - vector.X;
                    float num13 = player.position.Y + player.height / 2f - 200f - vector.Y;

                    float num14 = (float)Math.Sqrt(num12 * num12 + num13 * num13);
                    float num15 = num14;
                    num14 = num10 / num14;
                    num12 *= num14;
                    num13 *= num14;

                    if (npc.velocity.X < num12)
                    {
                        npc.velocity.X += num11;
                        if (npc.velocity.X < 0f && num12 > 0f) npc.velocity.X += num11;
                    }
                    else if (npc.velocity.X > num12)
                    {
                        npc.velocity.X -= num11;
                        if (npc.velocity.X > 0f && num12 < 0f) npc.velocity.X -= num11;
                    }
                    if (npc.velocity.Y < num13)
                    {
                        npc.velocity.Y += num11;
                        if (npc.velocity.Y < 0f && num13 > 0f) npc.velocity.Y += num11;
                    }
                    else if (npc.velocity.Y > num13)
                    {
                        npc.velocity.Y -= num11;
                        if (npc.velocity.Y > 0f && num13 < 0f) npc.velocity.Y -= num11;
                    }

                    ai[2] += 1f;
                    // 悬浮→冲刺间隔
                    float num16 = (Main.expertMode ? 210f : 600f) / SpeedFactor;

                    if (ai[2] >= num16)
                    {
                        ai[1] = 1f;
                        ai[2] = 0f;
                        ai[3] = 0f;
                        npc.target = 255;
                        npc.netUpdate = true;
                    }
                    else if ((npc.position.Y + npc.height < player.position.Y && num15 < 500f)
                          || (Main.expertMode && num15 < 500f))
                    {
                        if (!player.dead) ai[3] += 1f;

                        // 小眼睛召唤间隔
                        float num17 = (Main.expertMode ? 44f : 110f) / SpeedFactor;
                        if (Main.getGoodWorld) num17 *= 0.8f;

                        if (ai[3] >= num17)
                        {
                            ai[3] = 0f;
                            npc.rotation = num7;
                            TrySpawnServants(npc, vector);
                        }
                    }
                }
                else if (ai[1] == 1f) // 冲刺准备
                {
                    npc.rotation = num7;

                    // 冲刺速度
                    float baseSpeed = (Main.expertMode ? 7f : 6f) * SpeedFactor;
                    if (Main.getGoodWorld) baseSpeed += 1f;

                    // 冲刺距离随机倍率
                    float[] multipliers = [0.5f, 0.75f, 1f, 1.25f, 1.5f];
                    float mult = multipliers[Main.rand.Next(multipliers.Length)];
                    float finalSpeed = baseSpeed * mult;

                    Vector2 vector4 = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                    float num24 = player.position.X + player.width / 2f - vector4.X;
                    float num25 = player.position.Y + player.height / 2f - vector4.Y;

                    // 每 3 次冲刺预判
                    if (IsPredictiveDash(npc))
                    {
                        float predict = Math.Abs(player.velocity.X) + Math.Abs(player.velocity.Y) / 4f;
                        predict += 10f - predict;
                        if (predict < 5f) predict = 5f;
                        if (predict > 15f) predict = 15f;
                        num24 -= player.velocity.X * predict;
                        num25 -= player.velocity.Y * predict / 4f;
                    }

                    float num26 = (float)Math.Sqrt(num24 * num24 + num25 * num25);
                    num26 = finalSpeed / num26;
                    npc.velocity.X = num24 * num26;
                    npc.velocity.Y = num25 * num26;

                    ai[1] = 2f;
                    localAI[0] += 1f; // 冲刺计数
                    npc.netUpdate = true;
                }
                else if (ai[1] == 2f) // 冲刺中
                {
                    ai[2] += 1f;

                    if (ai[2] >= 40f)
                    {
                        npc.velocity *= 0.98f;
                        if (Main.expertMode) npc.velocity *= 0.985f;
                        if (Main.getGoodWorld) npc.velocity *= 0.99f;

                        if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f) npc.velocity.X = 0f;
                        if (npc.velocity.Y > -0.1f && npc.velocity.Y < 0.1f) npc.velocity.Y = 0f;
                    }
                    else
                    {
                        npc.rotation = (float)Math.Atan2(npc.velocity.Y, npc.velocity.X) - 1.57f;
                    }

                    // 冲刺持续时间（间隔）
                    int num27 = (int)((Main.expertMode ? 100 : 150) / SpeedFactor);
                    if (Main.getGoodWorld) num27 -= 15;
                    if (num27 < 1) num27 = 1;

                    if (ai[2] >= num27)
                    {
                        ai[3] += 1f;
                        ai[2] = 0f;
                        npc.target = 255;
                        npc.rotation = num7;

                        if (ai[3] >= 3f)
                        {
                            ai[1] = 0f;
                            ai[3] = 0f;
                        }
                        else
                        {
                            ai[1] = 1f;
                        }
                    }
                }

                // 阶段切换
                float num28 = Main.expertMode ? 0.65f : 0.5f;
                if (npc.life < npc.lifeMax * num28)
                {
                    ai[0] = 1f;
                    ai[1] = 0f;
                    ai[2] = 0f;
                    ai[3] = 0f;
                    npc.netUpdate = true;
                }
                return;
            }

            #endregion

            #region 第二阶段

            if (ai[0] == 1f || ai[0] == 2f)
            {
                if (ai[0] == 1f || ai[3] == 1f)
                {
                    ai[2] += 0.005f;
                    if (ai[2] > 0.5f) ai[2] = 0.5f;
                }
                else
                {
                    ai[2] -= 0.005f;
                    if (ai[2] < 0f) ai[2] = 0f;
                }

                npc.rotation += ai[2];
                ai[1] += 1f;

                if (Main.getGoodWorld) npc.reflectsProjectiles = true;

                // 小眼睛召唤间隔
                int num29 = (int)(20 / SpeedFactor);
                if (num29 < 1) num29 = 1;
                if (Main.getGoodWorld && npc.life < npc.lifeMax / 3) num29 = (int)(10 / SpeedFactor);
                if (num29 < 1) num29 = 1;

                if (Main.expertMode && ai[1] % num29 == 0f)
                {
                    Vector2 vector5 = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                    TrySpawnServants(npc, vector5);
                }

                if (ai[1] >= 100f)
                {
                    if (ai[3] == 1f)
                    {
                        ai[3] = 0f;
                        ai[1] = 0f;
                    }
                    else
                    {
                        ai[0] += 1f;
                        ai[1] = 0f;

                        if (ai[0] == 3f)
                        {
                            ai[2] = 0f;
                        }
                        else
                        {
                            SoundEngine.PlaySound(SoundID.NPCDeath1, npc.Center);
                            for (int i = 0; i < 2; i++)
                            {
                                Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), 8);
                                Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), 7);
                                Gore.NewGore(npc.GetSource_FromAI(), npc.position, new Vector2(Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f), 6);
                            }

                            for (int i = 0; i < 20; i++)
                            {
                                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f);
                            }

                            SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                        }
                    }
                }

                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.Next(-30, 31) * 0.2f, Main.rand.Next(-30, 31) * 0.2f);

                npc.velocity.X *= 0.98f;
                npc.velocity.Y *= 0.98f;
                if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f) npc.velocity.X = 0f;
                if (npc.velocity.Y > -0.1f && npc.velocity.Y < 0.1f) npc.velocity.Y = 0f;
                return;
            }

            #endregion

            #region 第三阶段

            // 低血量防御力削弱
            npc.defense = 0;
            if (Main.expertMode)
            {
                if (flag2) npc.defense = -15;
                if (flag3) npc.defense = -30;
            }

            if (ai[1] == 0f && flag2) ai[1] = 5f;

            if (ai[1] == 0f) // 悬浮追踪
            {
                // 移速
                float num39 = 6f * SpeedFactor;
                float num40 = 0.07f * SpeedFactor;

                Vector2 vector8 = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                float num41 = player.position.X + player.width / 2f - vector8.X;
                float num42 = player.position.Y + player.height / 2f - 120f - vector8.Y;
                float num43 = (float)Math.Sqrt(num41 * num41 + num42 * num42);

                if (num43 > 400f && Main.expertMode)
                {
                    num39 += 1f * SpeedFactor; num40 += 0.05f * SpeedFactor;
                    if (num43 > 600f)
                    {
                        num39 += 1f * SpeedFactor; num40 += 0.05f * SpeedFactor;
                        if (num43 > 800f) { num39 += 1f * SpeedFactor; num40 += 0.05f * SpeedFactor; }
                    }
                }
                if (Main.getGoodWorld) { num39 += 1f * SpeedFactor; num40 += 0.1f * SpeedFactor; }

                num43 = num39 / num43;
                num41 *= num43;
                num42 *= num43;

                if (npc.velocity.X < num41)
                {
                    npc.velocity.X += num40;
                    if (npc.velocity.X < 0f && num41 > 0f) npc.velocity.X += num40;
                }
                else if (npc.velocity.X > num41)
                {
                    npc.velocity.X -= num40;
                    if (npc.velocity.X > 0f && num41 < 0f) npc.velocity.X -= num40;
                }
                if (npc.velocity.Y < num42)
                {
                    npc.velocity.Y += num40;
                    if (npc.velocity.Y < 0f && num42 > 0f) npc.velocity.Y += num40;
                }
                else if (npc.velocity.Y > num42)
                {
                    npc.velocity.Y -= num40;
                    if (npc.velocity.Y > 0f && num42 < 0f) npc.velocity.Y -= num40;
                }

                ai[2] += 1f;
                // 悬浮→冲刺间隔
                if (ai[2] >= 200f / SpeedFactor)
                {
                    ai[1] = 1f;
                    ai[2] = 0f;
                    ai[3] = 0f;

                    if (Main.expertMode && npc.life < npc.lifeMax * 0.35) ai[1] = 3f;

                    npc.target = 255;
                    npc.netUpdate = true;
                }

                if (Main.expertMode && flag3)
                {
                    npc.TargetClosest();
                    npc.netUpdate = true;
                    ai[1] = 3f;
                    ai[2] = 0f;
                    ai[3] -= 1000f;
                }
            }
            else if (ai[1] == 1f) // 普通冲刺准备
            {
                SoundEngine.PlaySound(SoundID.Item36, npc.Center);
                npc.rotation = num7;

                // 冲刺速度
                float baseSpeed = 6.8f * SpeedFactor;
                if (Main.expertMode && ai[3] == 1f) baseSpeed *= 1.15f;
                if (Main.expertMode && ai[3] == 2f) baseSpeed *= 1.3f;
                if (Main.getGoodWorld) baseSpeed *= 1.2f;

                // 距离倍率
                float[] multipliers = [0.5f, 0.75f, 1f, 1.25f, 1.5f];
                float mult = multipliers[Main.rand.Next(multipliers.Length)];
                float finalSpeed = baseSpeed * mult;

                Vector2 vector9 = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                float num45 = player.position.X + player.width / 2f - vector9.X;
                float num46 = player.position.Y + player.height / 2f - vector9.Y;

                // 每 3 次冲刺预判
                if (IsPredictiveDash(npc))
                {
                    float predict = Math.Abs(player.velocity.X) + Math.Abs(player.velocity.Y) / 4f;
                    predict += 10f - predict;
                    if (predict < 5f) predict = 5f;
                    if (predict > 15f) predict = 15f;
                    num45 -= player.velocity.X * predict;
                    num46 -= player.velocity.Y * predict / 4f;
                }

                float num47 = (float)Math.Sqrt(num45 * num45 + num46 * num46);
                num47 = finalSpeed / num47;
                npc.velocity.X = num45 * num47;
                npc.velocity.Y = num46 * num47;

                ai[1] = 2f;
                localAI[0] += 1f;
                npc.netUpdate = true;
            }
            else if (ai[1] == 2f) // 普通冲刺中
            {
                // 冲刺持续时间（间隔）
                float num48 = (Main.expertMode ? 50f : 40f) / SpeedFactor;
                ai[2] += 1f;

                if (ai[2] >= num48)
                {
                    npc.velocity *= 0.97f;
                    if (Main.expertMode) npc.velocity *= 0.98f;
                    if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f) npc.velocity.X = 0f;
                    if (npc.velocity.Y > -0.1f && npc.velocity.Y < 0.1f) npc.velocity.Y = 0f;
                }
                else
                {
                    npc.rotation = (float)Math.Atan2(npc.velocity.Y, npc.velocity.X) - 1.57f;
                }

                // 冲刺持续时间（间隔）
                int num49 = (int)((Main.expertMode ? 90 : 130) / SpeedFactor);
                if (num49 < 1) num49 = 1;

                if (ai[2] >= num49)
                {
                    ai[3] += 1f;
                    ai[2] = 0f;
                    npc.target = 255;
                    npc.rotation = num7;

                    if (ai[3] >= 3f)
                    {
                        ai[1] = 0f;
                        ai[3] = 0f;

                        if (Main.expertMode && Main.netMode != NetmodeID.MultiplayerClient && npc.life < npc.lifeMax * 0.5)
                        {
                            ai[1] = 3f;
                            ai[3] += Main.rand.Next(1, 4);
                        }
                        npc.netUpdate = true;
                    }
                    else
                    {
                        ai[1] = 1f;
                    }
                }
            }
            else if (ai[1] == 3f) // 高速冲刺准备
            {
                if (ai[3] == 4f && flag2 && npc.Center.Y > player.Center.Y)
                {
                    npc.TargetClosest();
                    ai[1] = 0f;
                    ai[2] = 0f;
                    ai[3] = 0f;
                    npc.netUpdate = true;
                }
                else if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    npc.TargetClosest();
                    player = Main.player[npc.target];

                    // 高速冲刺速度
                    float num50 = 20f * SpeedFactor;
                    Vector2 vector10 = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                    float num51 = player.position.X + player.width / 2f - vector10.X;
                    float num52 = player.position.Y + player.height / 2f - vector10.Y;

                    // 高速冲刺也应用距离随机
                    float[] multipliers = [0.5f, 0.75f, 1f, 1.25f, 1.5f];
                    float mult = multipliers[Main.rand.Next(multipliers.Length)];
                    num50 *= mult;

                    float num53 = Math.Abs(player.velocity.X) + Math.Abs(player.velocity.Y) / 4f;
                    num53 += 10f - num53;
                    if (num53 < 5f) num53 = 5f;
                    if (num53 > 15f) num53 = 15f;

                    if (ai[2] == -1f && !flag3) { num53 *= 4f; num50 *= 1.3f; }
                    if (flag3) num53 *= 2f;

                    num51 -= player.velocity.X * num53;
                    num52 -= player.velocity.Y * num53 / 4f;

                    num51 *= 1f + Main.rand.Next(-10, 11) * 0.01f;
                    num52 *= 1f + Main.rand.Next(-10, 11) * 0.01f;
                    if (flag3)
                    {
                        num51 *= 1f + Main.rand.Next(-10, 11) * 0.01f;
                        num52 *= 1f + Main.rand.Next(-10, 11) * 0.01f;
                    }

                    float num54 = (float)Math.Sqrt(num51 * num51 + num52 * num52);
                    float num55 = num54;
                    num54 = num50 / num54;

                    npc.velocity.X = num51 * num54;
                    npc.velocity.Y = num52 * num54;
                    npc.velocity.X += Main.rand.Next(-20, 21) * 0.1f;
                    npc.velocity.Y += Main.rand.Next(-20, 21) * 0.1f;

                    if (flag3)
                    {
                        npc.velocity.X += Main.rand.Next(-50, 51) * 0.1f;
                        npc.velocity.Y += Main.rand.Next(-50, 51) * 0.1f;

                        float num56 = Math.Abs(npc.velocity.X);
                        float num57 = Math.Abs(npc.velocity.Y);
                        if (npc.Center.X > player.Center.X) num57 *= -1f;
                        if (npc.Center.Y > player.Center.Y) num56 *= -1f;

                        npc.velocity.X = num57 + npc.velocity.X;
                        npc.velocity.Y = num56 + npc.velocity.Y;
                        npc.velocity.Normalize();
                        npc.velocity *= num50;
                        npc.velocity.X += Main.rand.Next(-20, 21) * 0.1f;
                        npc.velocity.Y += Main.rand.Next(-20, 21) * 0.1f;
                    }
                    else if (num55 < 100f)
                    {
                        if (Math.Abs(npc.velocity.X) > Math.Abs(npc.velocity.Y))
                        {
                            float num58 = Math.Abs(npc.velocity.X);
                            float num59 = Math.Abs(npc.velocity.Y);
                            if (npc.Center.X > player.Center.X) num59 *= -1f;
                            if (npc.Center.Y > player.Center.Y) num58 *= -1f;
                            npc.velocity.X = num59;
                            npc.velocity.Y = num58;
                        }
                    }
                    else if (Math.Abs(npc.velocity.X) > Math.Abs(npc.velocity.Y))
                    {
                        float num60 = (Math.Abs(npc.velocity.X) + Math.Abs(npc.velocity.Y)) / 2f;
                        float num61 = num60;
                        if (npc.Center.X > player.Center.X) num61 *= -1f;
                        if (npc.Center.Y > player.Center.Y) num60 *= -1f;
                        npc.velocity.X = num61;
                        npc.velocity.Y = num60;
                    }

                    ai[1] = 4f;
                    localAI[0] += 1f;
                    npc.netUpdate = true;
                }
            }
            else if (ai[1] == 4f) // 高速冲刺执行
            {
                if (ai[2] == 0f)
                {
                    SoundEngine.PlaySound(SoundID.NPCDeath1, npc.Center);
                }

                float num62 = num4; // num4 已 / SpeedFactor
                ai[2] += 1f;

                if (ai[2] == num62 && Vector2.Distance(npc.position, player.position) < 200f)
                    ai[2] -= 1f;

                if (ai[2] >= num62)
                {
                    npc.velocity *= 0.95f;
                    if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f) npc.velocity.X = 0f;
                    if (npc.velocity.Y > -0.1f && npc.velocity.Y < 0.1f) npc.velocity.Y = 0f;
                }
                else
                {
                    npc.rotation = (float)Math.Atan2(npc.velocity.Y, npc.velocity.X) - 1.57f;
                }

                // 额外 13 帧也按系数缩短
                float num63 = num62 + 13f / SpeedFactor;
                if (ai[2] >= num63)
                {
                    npc.netUpdate = true;
                    ai[3] += 1f;
                    ai[2] = 0f;

                    if (ai[3] >= 5f)
                    {
                        ai[1] = 0f;
                        ai[3] = 0f;

                        if (npc.target >= 0 && Main.getGoodWorld
                            && Collision.CanHit(npc.position, npc.width, npc.height, player.position, npc.width, npc.height))
                        {
                            SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                            ai[0] = 2f;
                            ai[1] = 0f;
                            ai[2] = 0f;
                            ai[3] = 1f;
                            npc.netUpdate = true;
                        }
                    }
                    else
                    {
                        ai[1] = 3f;
                    }
                }
            }
            else if (ai[1] == 5f) // 上浮后撤
            {
                float num64 = 600f;
                // 移速
                float num65 = 9f * SpeedFactor;
                float num66 = 0.3f * SpeedFactor;

                Vector2 vector11 = new(npc.position.X + npc.width * 0.5f, npc.position.Y + npc.height * 0.5f);
                float num67 = player.position.X + player.width / 2f - vector11.X;
                float num68 = player.position.Y + player.height / 2f + num64 - vector11.Y;
                float num69 = (float)Math.Sqrt(num67 * num67 + num68 * num68);
                num69 = num65 / num69;
                num67 *= num69;
                num68 *= num69;

                if (npc.velocity.X < num67)
                {
                    npc.velocity.X += num66;
                    if (npc.velocity.X < 0f && num67 > 0f) npc.velocity.X += num66;
                }
                else if (npc.velocity.X > num67)
                {
                    npc.velocity.X -= num66;
                    if (npc.velocity.X > 0f && num67 < 0f) npc.velocity.X -= num66;
                }
                if (npc.velocity.Y < num68)
                {
                    npc.velocity.Y += num66;
                    if (npc.velocity.Y < 0f && num68 > 0f) npc.velocity.Y += num66;
                }
                else if (npc.velocity.Y > num68)
                {
                    npc.velocity.Y -= num66;
                    if (npc.velocity.Y > 0f && num68 < 0f) npc.velocity.Y -= num66;
                }

                ai[2] += 1f;
                // 后撤→高速冲刺间隔
                if (ai[2] >= 70f / SpeedFactor)
                {
                    npc.TargetClosest();
                    ai[1] = 3f;
                    ai[2] = -1f;
                    ai[3] = Main.rand.Next(-3, 1);
                    npc.netUpdate = true;
                }
            }

            if (flag3 && ai[1] == 5f) ai[1] = 3f;

            #endregion
        }

        #endregion

        #region 辅助方法

        // 每 3 次冲刺有一次预判（localAI[0] 保存已开始的冲刺次数）
        private static bool IsPredictiveDash(NPC npc)
        {
            return npc.localAI[0] % 3f == 0f;
        }

        // 召唤小眼睛：一阶段上限 7，二阶段上限 11
        private static void TrySpawnServants(NPC npc, Vector2 origin)
        {
            int minionCap = npc.ai[0] == 0f ? 7 : 11; // 阶段切换小眼睛召唤上限

            int current = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.ServantofCthulhu)
                    current++;
            }

            if (current >= minionCap) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // 每批最多补 3 只，避免刷屏
            int toSpawn = Math.Min(minionCap - current, 3);
            float speed = Main.expertMode ? 6f : 5f;
            Player player = Main.player[npc.target];

            for (int k = 0; k < toSpawn; k++)
            {
                float dx = player.Center.X - origin.X + Main.rand.Next(-60, 61);
                float dy = player.Center.Y - origin.Y + Main.rand.Next(-60, 61);
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1f) len = 1f;
                float factor = speed / len;
                Vector2 vel = new(dx * factor, dy * factor);

                Vector2 spawnPos = origin + vel * 10f;

                int idx = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.ServantofCthulhu);

                if (idx < Main.maxNPCs)
                {
                    Main.npc[idx].velocity = vel;
                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, idx);
                    }
                }

                for (int m = 0; m < 5; m++)
                {
                    Dust.NewDust(spawnPos, 20, 20, DustID.Blood, vel.X * 0.4f, vel.Y * 0.4f);
                }
            }
        }

        #endregion
    }

    public class DZTEyeReflectProjectile : GlobalProjectile
    {
        public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
        {
            // 只处理 DZT 难度下射出的友好弹幕
            if (!projectile.friendly || projectile.hostile) return;
            if (target.type != NPCID.EyeofCthulhu) return;
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;

            // 仅在克苏鲁之眼冲刺时触发
            if (!EyeOfCthulhuAIOverride.IsDashing(target)) return;

            // 25% 概率判定
            if (!Main.rand.NextBool(4)) return;

            // 取消本次伤害
            modifiers.FinalDamage *= 0f;

            // 在弹幕拥有者端生成反向弹幕，确保联机同步
            if (projectile.owner == Main.myPlayer)
            {
                Vector2 reverseVelocity = projectile.velocity * -1f;

                int newProj = Projectile.NewProjectile(
                    projectile.GetSource_FromThis(),
                    projectile.Center,
                    reverseVelocity,
                    projectile.type,
                    projectile.damage,
                    projectile.knockBack,
                    Main.myPlayer
                );

                if (newProj >= 0 && newProj < Main.maxProjectiles)
                {
                    Projectile reflected = Main.projectile[newProj];
                    reflected.friendly = false;
                    reflected.hostile = true;
                    reflected.penetrate = 1;
                    reflected.netUpdate = true;
                }
            }

            projectile.Kill(); // 原弹幕销毁，避免穿透后继续命中
        }
    }
}