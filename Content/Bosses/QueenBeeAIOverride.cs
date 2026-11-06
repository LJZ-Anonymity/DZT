using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class QueenBeeAIOverride : GlobalNPC
    {
        private const float StingerAccuracyBonus = 0.33f; // 毒刺精准度提升比例
        private const float DashDamageReduction = 0.15f; // 冲刺减伤比例
        private const float BaseEnrageFactor = 0.75f; // 基础激怒系数
        private const float StingerSpeed = 24f; // 毒刺速度

        #region 冲刺减伤 / 冲刺判定

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.QueenBee
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive
                && IsDashing(npc))
            {
                modifiers.FinalDamage *= (1f - DashDamageReduction); // 15% 伤害减免
            }
        }

        // 判断蜂王是否处于冲刺状态
        public static bool IsDashing(NPC npc)
        {
            if (npc.type != NPCID.QueenBee) return false;

            // ai[0] == 0：水平调整/冲刺阶段
            // ai[2] == 1：正在向玩家冲刺
            if (npc.ai[0] == 0f && npc.ai[2] == 1f) return true;

            // ai[0] == 4：远距离高速冲向玩家
            if (npc.ai[0] == 4f) return true;

            return false;
        }

        #endregion

        #region PreAI 拦截

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.QueenBee
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                CustomQueenBeeAI(npc);
                return false;
            }
            return true;
        }

        #endregion

        #region 主 AI

        private static void CustomQueenBeeAI(NPC npc)
        {
            float[] ai = npc.ai;
            float[] localAI = npc.localAI;

            if (Main.expertMode)
            {
                int num657 = (int)(20f * (1f - (float)npc.life / npc.lifeMax));
                npc.defense = npc.defDefense + num657;
            }

            if (npc.target < 0 || npc.target == 255
                || Main.player[npc.target].dead
                || !Main.player[npc.target].active)
            {
                npc.TargetClosest();
            }

            bool dead = Main.player[npc.target].dead;

            #region 激怒系数

            float enrageFactor = 0f;

            // DZT 始终获得 0.75 的基础激怒系数
            if (ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                enrageFactor += BaseEnrageFactor;

            // 高于地表 +1
            if ((double)(npc.position.Y / 16f) < Main.worldSurface)
                enrageFactor += 1f;

            // 玩家不在丛林 +1
            if (!Main.player[npc.target].ZoneJungle)
                enrageFactor += 1f;

            // FTW +0.5
            if (Main.getGoodWorld)
                enrageFactor += 0.5f;

            #endregion

            float distanceToPlayer = Vector2.Distance(npc.Center, Main.player[npc.target].Center);

            if (ai[0] != 5f) 
            {
                if (npc.timeLeft < 60) npc.timeLeft = 60;

                if (distanceToPlayer > 3000f)
                {
                    ai[0] = 4f;
                    npc.netUpdate = true;
                }
            }

            if (dead)
            {
                ai[0] = 5f;
                npc.netUpdate = true;
            }

            if (ai[0] == 5f) // 脱战/玩家死亡，向世界边缘飞离
            {
                npc.velocity.Y *= 0.98f;

                if (npc.velocity.X < 0f) npc.direction = -1;
                else npc.direction = 1;

                npc.spriteDirection = npc.direction;

                if (npc.position.X < Main.maxTilesX * 8)
                {
                    if (npc.velocity.X > 0f) npc.velocity.X *= 0.98f;
                    else localAI[0] = 1f;

                    npc.velocity.X -= 0.08f;
                }
                else
                {
                    if (npc.velocity.X < 0f) npc.velocity.X *= 0.98f;
                    else localAI[0] = 1f;

                    npc.velocity.X += 0.08f;
                }

                npc.EncourageDespawn(10);
            }
            else if (ai[0] == -1f) // 随机选择下一个攻击模式
            {
                if (Main.netMode == NetmodeID.MultiplayerClient) return;

                float previousMode = ai[1];
                int nextMode;
                do
                {
                    nextMode = Main.rand.Next(3);
                    switch (nextMode)
                    {
                        case 1: nextMode = 2; break;
                        case 2: nextMode = 3; break;
                    }
                }
                while (nextMode == previousMode);

                ai[0] = nextMode;
                ai[1] = 0f;
                ai[2] = 0f;
                npc.netUpdate = true;
            }
            else if (ai[0] == 0f) // 水平调整 + 冲刺
            {
                int dashCount = 2; // 原版基础冲刺次数

                if (Main.expertMode)
                {
                    if (npc.life < npc.lifeMax / 2) dashCount++;
                    if (npc.life < npc.lifeMax / 3) dashCount++;
                    if (npc.life < npc.lifeMax / 5) dashCount++;
                }

                // 每 1 激怒系数增加 2 次冲刺
                dashCount += (int)(enrageFactor * 2f);

                // 达到冲刺次数上限切换到下一个攻击模式
                if (ai[1] > dashCount && ai[1] % 2f == 0f)
                {
                    ai[0] = -1f;
                    ai[1] = 0f;
                    ai[2] = 0f;
                    npc.netUpdate = true;
                    return;
                }

                // 隔帧执行一次调整/冲刺判定
                if (ai[1] % 2f == 0f)
                {
                    npc.TargetClosest();

                    float verticalAlignRange = 20f + 20f * enrageFactor;

                    // 如果玩家在垂直范围内，立即冲刺
                    if (Math.Abs(npc.Center.Y - Main.player[npc.target].Center.Y) < verticalAlignRange)
                    {
                        localAI[0] = 1f;
                        ai[1] += 1f;
                        ai[2] = 0f;
                        npc.netUpdate = true;

                        float dashSpeed = 12f;
                        if (Main.expertMode)
                        {
                            dashSpeed = 16f;
                            if (npc.life < npc.lifeMax * 0.75f) dashSpeed += 2f;
                            if (npc.life < npc.lifeMax * 0.5f) dashSpeed += 2f;
                            if (npc.life < npc.lifeMax * 0.25f) dashSpeed += 2f;
                            if (npc.life < npc.lifeMax * 0.1f) dashSpeed += 2f;
                        }
                        dashSpeed += 7f * enrageFactor;

                        Vector2 beeCenter = new(npc.position.X + npc.width * 0.5f,
                                                 npc.position.Y + npc.height * 0.5f);
                        float dx = Main.player[npc.target].position.X + Main.player[npc.target].width / 2f - beeCenter.X;
                        float dy = Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f - beeCenter.Y;
                        float len = (float)Math.Sqrt(dx * dx + dy * dy);
                        len = dashSpeed / len;

                        npc.velocity.X = dx * len;
                        npc.velocity.Y = dy * len;
                        npc.spriteDirection = npc.direction;

                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                        return;
                    }

                    // 不在垂直范围内，上下调整
                    localAI[0] = 0f;

                    float maxVerticalSpeed = 12f;
                    float verticalAccel = 0.15f;

                    if (Main.expertMode)
                    {
                        if (npc.life < npc.lifeMax * 0.75f) { maxVerticalSpeed += 1f; verticalAccel += 0.05f; }
                        if (npc.life < npc.lifeMax * 0.5f) { maxVerticalSpeed += 1f; verticalAccel += 0.05f; }
                        if (npc.life < npc.lifeMax * 0.25f) { maxVerticalSpeed += 2f; verticalAccel += 0.05f; }
                        if (npc.life < npc.lifeMax * 0.1f) { maxVerticalSpeed += 2f; verticalAccel += 0.1f; }
                    }

                    maxVerticalSpeed += 3f * enrageFactor;
                    verticalAccel += 0.5f * enrageFactor;

                    if (npc.position.Y + npc.height / 2f < Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f)
                        npc.velocity.Y += verticalAccel;
                    else
                        npc.velocity.Y -= verticalAccel;

                    if (npc.velocity.Y < -maxVerticalSpeed) npc.velocity.Y = -maxVerticalSpeed;
                    if (npc.velocity.Y > maxVerticalSpeed) npc.velocity.Y = maxVerticalSpeed;

                    float horizontalDiff = Math.Abs(npc.Center.X - Main.player[npc.target].Center.X);

                    if (horizontalDiff > 600f)
                        npc.velocity.X += 0.15f * npc.direction;
                    else if (horizontalDiff < 300f)
                        npc.velocity.X -= 0.15f * npc.direction;
                    else
                        npc.velocity.X *= 0.8f;

                    if (npc.velocity.X < -16f) npc.velocity.X = -16f;
                    if (npc.velocity.X > 16f) npc.velocity.X = 16f;

                    npc.spriteDirection = npc.direction;
                    return;
                }

                // 非判定帧：更新朝向、检查是否该进入减速/冲刺
                if (npc.velocity.X < 0f) npc.direction = -1;
                else npc.direction = 1;

                npc.spriteDirection = npc.direction;

                int chargeDistanceThreshold = 600;
                if (Main.expertMode)
                {
                    if (npc.life < npc.lifeMax * 0.1f) chargeDistanceThreshold = 300;
                    else if (npc.life < npc.lifeMax * 0.25f) chargeDistanceThreshold = 450;
                    else if (npc.life < npc.lifeMax * 0.5f) chargeDistanceThreshold = 500;
                    else if (npc.life < npc.lifeMax * 0.75f) chargeDistanceThreshold = 550;
                }

                int facingDirection = 1;
                if (npc.Center.X < Main.player[npc.target].Center.X) facingDirection = -1;

                chargeDistanceThreshold -= (int)(100f * enrageFactor);

                bool shouldCharge = false;
                if (npc.direction == facingDirection
                    && Math.Abs(npc.Center.X - Main.player[npc.target].Center.X) > chargeDistanceThreshold)
                {
                    ai[2] = 1f;
                    shouldCharge = true;
                }

                if (Math.Abs(npc.Center.Y - Main.player[npc.target].Center.Y) > chargeDistanceThreshold * 1.5f)
                {
                    ai[2] = 1f;
                    shouldCharge = true;
                }

                if (enrageFactor > 0f && shouldCharge)
                    npc.velocity *= 0.5f;

                if (ai[2] == 1f)
                {
                    npc.TargetClosest();
                    npc.spriteDirection = npc.direction;
                    localAI[0] = 0f;
                    npc.velocity *= 0.9f;

                    float slowDownThreshold = 0.1f;
                    if (Main.expertMode)
                    {
                        if (npc.life < npc.lifeMax / 2) { npc.velocity *= 0.9f; slowDownThreshold += 0.05f; }
                        if (npc.life < npc.lifeMax / 3) { npc.velocity *= 0.9f; slowDownThreshold += 0.05f; }
                        if (npc.life < npc.lifeMax / 5) { npc.velocity *= 0.9f; slowDownThreshold += 0.05f; }
                    }

                    if (enrageFactor > 0f) npc.velocity *= 0.7f;

                    if (Math.Abs(npc.velocity.X) + Math.Abs(npc.velocity.Y) < slowDownThreshold)
                    {
                        ai[2] = 0f;
                        ai[1] += 1f;
                        npc.netUpdate = true;
                    }
                }
                else
                {
                    localAI[0] = 1f;
                }
            }
            else if (ai[0] == 2f) // 悬停到玩家上方
            {
                npc.TargetClosest();
                npc.spriteDirection = npc.direction;

                float hoverSpeed = 12f;
                float hoverAccel = 0.07f;
                if (Main.expertMode) hoverAccel = 0.1f;

                Vector2 beeCenter = new(npc.position.X + npc.width * 0.5f,
                                         npc.position.Y + npc.height * 0.5f);
                float dx = Main.player[npc.target].position.X + Main.player[npc.target].width / 2f - beeCenter.X;
                float dy = Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f - 200f - beeCenter.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                if (dist < 200f)
                {
                    ai[0] = 1f;
                    ai[1] = 0f;
                    npc.netUpdate = true;
                    return;
                }

                dist = hoverSpeed / dist;

                if (npc.velocity.X < dx)
                {
                    npc.velocity.X += hoverAccel;
                    if (npc.velocity.X < 0f && dx > 0f) npc.velocity.X += hoverAccel;
                }
                else if (npc.velocity.X > dx)
                {
                    npc.velocity.X -= hoverAccel;
                    if (npc.velocity.X > 0f && dx < 0f) npc.velocity.X -= hoverAccel;
                }

                if (npc.velocity.Y < dy)
                {
                    npc.velocity.Y += hoverAccel;
                    if (npc.velocity.Y < 0f && dy > 0f) npc.velocity.Y += hoverAccel;
                }
                else if (npc.velocity.Y > dy)
                {
                    npc.velocity.Y -= hoverAccel;
                    if (npc.velocity.Y > 0f && dy < 0f) npc.velocity.Y -= hoverAccel;
                }
            }
            else if (ai[0] == 1f) // 召唤蜜蜂 + 调整位置
            {
                localAI[0] = 0f;
                npc.TargetClosest();

                Vector2 spawnPoint = new(npc.position.X + npc.width / 2f + Main.rand.Next(20) * npc.direction,
                                         npc.position.Y + npc.height * 0.8f);
                Vector2 beeCenter = new(npc.position.X + npc.width * 0.5f,
                                         npc.position.Y + npc.height * 0.5f);
                float dx = Main.player[npc.target].position.X + Main.player[npc.target].width / 2f - beeCenter.X;
                float dy = Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f - beeCenter.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                ai[1] += 1f;

                if (Main.expertMode)
                {
                    int nearbyPlayers = 0;
                    for (int i = 0; i < 255; i++)
                    {
                        if (Main.player[i].active && !Main.player[i].dead
                            && (npc.Center - Main.player[i].Center).Length() < 1000f)
                            nearbyPlayers++;
                    }
                    ai[1] += nearbyPlayers / 2;

                    if (npc.life < npc.lifeMax * 0.75f) ai[1] += 0.25f;
                    if (npc.life < npc.lifeMax * 0.5f) ai[1] += 0.25f;
                    if (npc.life < npc.lifeMax * 0.25f) ai[1] += 0.25f;
                    if (npc.life < npc.lifeMax * 0.1f) ai[1] += 0.25f;
                }

                bool spawnBee = false;
                int beeSpawnInterval = (int)(40f - 18f * enrageFactor);

                if (ai[1] > beeSpawnInterval)
                {
                    ai[1] = 0f;
                    ai[2]++;
                    spawnBee = true;
                }

                if (Collision.CanHit(spawnPoint, 1, 1, Main.player[npc.target].position,
                        Main.player[npc.target].width, Main.player[npc.target].height) && spawnBee)
                {
                    SoundEngine.PlaySound(SoundID.NPCHit1, npc.Center); // 修正：NPCHit -> NPCHit1
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int beeType = Main.rand.Next(210, 212);
                        int beeIdx = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPoint.X, (int)spawnPoint.Y, beeType);
                        Main.npc[beeIdx].velocity = Main.player[npc.target].Center - npc.Center;
                        Main.npc[beeIdx].velocity.Normalize();
                        Main.npc[beeIdx].velocity *= 5f;
                        Main.npc[beeIdx].CanBeReplacedByOtherNPCs = true;
                        Main.npc[beeIdx].localAI[0] = 60f;
                        Main.npc[beeIdx].netUpdate = true;
                    }
                }

                if (dist > 400f || !Collision.CanHit(new Vector2(spawnPoint.X, spawnPoint.Y - 30f), 1, 1,
                        Main.player[npc.target].position, Main.player[npc.target].width, Main.player[npc.target].height))
                {
                    float approachSpeed = 14f;
                    float approachAccel = 0.1f;
                    beeCenter = spawnPoint;
                    dx = Main.player[npc.target].position.X + Main.player[npc.target].width / 2f - beeCenter.X;
                    dy = Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f - beeCenter.Y;
                    dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    dist = approachSpeed / dist;

                    if (npc.velocity.X < dx)
                    {
                        npc.velocity.X += approachAccel;
                        if (npc.velocity.X < 0f && dx > 0f) npc.velocity.X += approachAccel;
                    }
                    else if (npc.velocity.X > dx)
                    {
                        npc.velocity.X -= approachAccel;
                        if (npc.velocity.X > 0f && dx < 0f) npc.velocity.X -= approachAccel;
                    }

                    if (npc.velocity.Y < dy)
                    {
                        npc.velocity.Y += approachAccel;
                        if (npc.velocity.Y < 0f && dy > 0f) npc.velocity.Y += approachAccel;
                    }
                    else if (npc.velocity.Y > dy)
                    {
                        npc.velocity.Y -= approachAccel;
                        if (npc.velocity.Y > 0f && dy < 0f) npc.velocity.Y -= approachAccel;
                    }
                }
                else
                {
                    npc.velocity *= 0.9f;
                }

                npc.spriteDirection = npc.direction;

                if (ai[2] > 5f)
                {
                    ai[0] = -1f;
                    ai[1] = 1f;
                    npc.netUpdate = true;
                }
            }
            else if (ai[0] == 3f) // 发射毒刺
            {
                float stingerBaseSpeed = 4f; // 原版基础速度
                float stingerAccel = 0.05f;

                if (Main.expertMode) { stingerAccel = 0.075f; stingerBaseSpeed = 6f; }

                stingerAccel += 0.2f * enrageFactor;
                stingerBaseSpeed += 6f * enrageFactor;

                if (ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                    stingerBaseSpeed = StingerSpeed; // 毒刺速度固定为 24

                Vector2 stingerSpawnPoint = new(npc.position.X + npc.width / 2f + Main.rand.Next(20) * npc.direction,
                                                npc.position.Y + npc.height * 0.8f);
                Vector2 beeCenter = new(npc.position.X + npc.width * 0.5f,
                                         npc.position.Y + npc.height * 0.5f);
                float dx = Main.player[npc.target].position.X + Main.player[npc.target].width / 2f - beeCenter.X;
                float dy = Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f - 300f - beeCenter.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                ai[1] += 1f;

                int fireInterval = 40;
                if (Main.expertMode)
                {
                    if (npc.life < npc.lifeMax * 0.1f) fireInterval = 15;
                    else if (npc.life < npc.lifeMax / 3) fireInterval = 25;
                    else if (npc.life >= npc.lifeMax / 2) fireInterval = 35;
                    else fireInterval = 30;
                }

                fireInterval -= (int)(5f * enrageFactor);

                if (ai[1] % fireInterval == fireInterval - 1
                    && npc.position.Y + npc.height < Main.player[npc.target].position.Y
                    && Collision.CanHit(stingerSpawnPoint, 1, 1,
                        Main.player[npc.target].position, Main.player[npc.target].width, Main.player[npc.target].height))
                {
                    SoundEngine.PlaySound(SoundID.Item17, npc.position);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float projSpeed = stingerBaseSpeed; // 弹幕速度
                        projSpeed += 7f * enrageFactor;

                        // 精准度增加 33%（散布范围缩小至 67%）
                        float spreadX = 80f - 39f * enrageFactor;
                        float spreadY = 40f - 19f * enrageFactor;

                        if (ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                        {
                            spreadX *= (1f - StingerAccuracyBonus);
                            spreadY *= (1f - StingerAccuracyBonus);
                        }

                        int minSpreadX = (int)spreadX;
                        int minSpreadY = (int)spreadY;
                        if (minSpreadX < 1) minSpreadX = 1;
                        if (minSpreadY < 1) minSpreadY = 1;

                        float targetX = Main.player[npc.target].position.X + Main.player[npc.target].width * 0.5f
                                      - stingerSpawnPoint.X
                                      + Main.rand.Next(-minSpreadX, minSpreadX + 1);
                        float targetY = Main.player[npc.target].position.Y + Main.player[npc.target].height * 0.5f
                                      - stingerSpawnPoint.Y
                                      + Main.rand.Next(-minSpreadY, minSpreadY + 1);

                        float targetDist = (float)Math.Sqrt(targetX * targetX + targetY * targetY);
                        targetDist = projSpeed / targetDist;
                        targetX *= targetDist;
                        targetY *= targetDist;

                        int projIdx = Projectile.NewProjectile(npc.GetSource_FromAI(),
                            stingerSpawnPoint.X, stingerSpawnPoint.Y,
                            targetX, targetY, ProjectileID.QueenBeeStinger, 11, 0f, Main.myPlayer);
                        Main.projectile[projIdx].timeLeft = 300;
                    }
                }

                if (!Collision.CanHit(new Vector2(stingerSpawnPoint.X, stingerSpawnPoint.Y - 30f), 1, 1,
                        Main.player[npc.target].position, Main.player[npc.target].width, Main.player[npc.target].height))
                {
                    float catchUpSpeed = 14f;
                    float catchUpAccel = 0.1f;
                    if (enrageFactor > 0f) catchUpAccel = 0.5f;

                    beeCenter = stingerSpawnPoint;
                    dx = Main.player[npc.target].position.X + Main.player[npc.target].width / 2f - beeCenter.X;
                    dy = Main.player[npc.target].position.Y + Main.player[npc.target].height / 2f - beeCenter.Y;
                    dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    dist = catchUpSpeed / dist;

                    if (npc.velocity.X < dx)
                    {
                        npc.velocity.X += catchUpAccel;
                        if (npc.velocity.X < 0f && dx > 0f) npc.velocity.X += catchUpAccel;
                    }
                    else if (npc.velocity.X > dx)
                    {
                        npc.velocity.X -= catchUpAccel;
                        if (npc.velocity.X > 0f && dx < 0f) npc.velocity.X -= catchUpAccel;
                    }

                    if (npc.velocity.Y < dy)
                    {
                        npc.velocity.Y += catchUpAccel;
                        if (npc.velocity.Y < 0f && dy > 0f) npc.velocity.Y += catchUpAccel;
                    }
                    else if (npc.velocity.Y > dy)
                    {
                        npc.velocity.Y -= catchUpAccel;
                        if (npc.velocity.Y > 0f && dy < 0f) npc.velocity.Y -= catchUpAccel;
                    }
                }
                else if (dist > 100f)
                {
                    npc.TargetClosest();
                    npc.spriteDirection = npc.direction;
                    dist = stingerBaseSpeed / dist;

                    if (npc.velocity.X < dx)
                    {
                        npc.velocity.X += stingerAccel;
                        if (npc.velocity.X < 0f && dx > 0f) npc.velocity.X += stingerAccel * 2f;
                    }
                    else if (npc.velocity.X > dx)
                    {
                        npc.velocity.X -= stingerAccel;
                        if (npc.velocity.X > 0f && dx < 0f) npc.velocity.X -= stingerAccel * 2f;
                    }

                    if (npc.velocity.Y < dy)
                    {
                        npc.velocity.Y += stingerAccel;
                        if (npc.velocity.Y < 0f && dy > 0f) npc.velocity.Y += stingerAccel * 2f;
                    }
                    else if (npc.velocity.Y > dy)
                    {
                        npc.velocity.Y -= stingerAccel;
                        if (npc.velocity.Y > 0f && dy < 0f) npc.velocity.Y -= stingerAccel * 2f;
                    }
                }

                float stingerModeDuration = 20f - 5f * enrageFactor;
                if (ai[1] > fireInterval * stingerModeDuration)
                {
                    ai[0] = -1f;
                    ai[1] = 3f;
                    npc.netUpdate = true;
                }
            }
            else if (ai[0] == 4f) // 远距离快速追回
            {
                localAI[0] = 1f;
                float chaseSpeed = 14f;
                Vector2 toPlayer = Main.player[npc.target].Center - npc.Center;
                toPlayer.Normalize();
                toPlayer *= chaseSpeed;
                npc.velocity = (npc.velocity * chaseSpeed + toPlayer) / (chaseSpeed + 1f);

                if (npc.velocity.X < 0f) npc.direction = -1;
                else npc.direction = 1;

                npc.spriteDirection = npc.direction;

                if (distanceToPlayer < 2000f)
                {
                    ai[0] = -1f;
                    localAI[0] = 0f;
                }
            }
        }

        #endregion
    }
}