using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using DZT.Common.Systems;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class KingSlimeAIOverride : GlobalNPC
    {
        private const float KingSlimeVanillaScale = 1.25f; // 默认体型
        private const float KingSlimeMinScale = KingSlimeVanillaScale * 0.5f; // 最小体型
        private const float KingSlimeMaxScale = KingSlimeVanillaScale * 2.0f; // 最大体型
        private const int KingSlimePointCacheMax = 200; // 候选传送点数组的最大容量

        private const short SpikedSlimeID = 535; // 尖刺史莱姆内部ID

        #region 史莱姆王：初始体型为原来的50%，体型随血量降低变大，最大为原体型200%，移动速度随体型增大而变快（与原来移动速度随体型减小变快一致）

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.KingSlime
                && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                CustomKingSlimeAI(npc);
                return false;
            }
            return true;
        }

        private static void CustomKingSlimeAI(NPC npc)
        {
            float[] ai = npc.ai;
            float[] localAI = npc.localAI;

            bool flag = false;
            bool flag2 = false;
            bool flag3 = false;
            float num = 1f;

            if (Main.getGoodWorld)
            {
                float num3 = 2f - (1f - (float)npc.life / npc.lifeMax);
                num *= num3;
            }

            if (ai[3] == 0f && npc.life > 0)
                ai[3] = npc.lifeMax;

            if (localAI[3] == 0f)
            {
                localAI[3] = 1f;
                flag = true;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    ai[0] = -100f;
                    npc.TargetClosest();
                    npc.netUpdate = true;
                }
            }

            int num4 = 3000;
            if (Main.player[npc.target].dead
                || Vector2.Distance(npc.Center, Main.player[npc.target].Center) > num4)
            {
                npc.TargetClosest();
                if (Main.player[npc.target].dead
                    || Vector2.Distance(npc.Center, Main.player[npc.target].Center) > num4)
                {
                    npc.EncourageDespawn(10);
                    npc.direction = Main.player[npc.target].Center.X < npc.Center.X ? 1 : -1;

                    if (Main.netMode != NetmodeID.MultiplayerClient && ai[1] != 5f)
                    {
                        npc.netUpdate = true;
                        ai[2] = 0f;
                        ai[0] = 0f;
                        ai[1] = 5f;
                        localAI[1] = Main.maxTilesX * 16;
                        localAI[2] = Main.maxTilesY * 16;
                    }
                }
            }

            if (!Main.player[npc.target].dead && npc.timeLeft > 10
                && ai[2] >= 300f && ai[1] < 5f && npc.velocity.Y == 0f)
            {
                ai[2] = 0f;
                ai[0] = 0f;
                ai[1] = 5f;
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    npc.TargetClosest(false);
                    npc.Center.ToTileCoordinates();
                    Main.player[npc.target].Center.ToTileCoordinates();
                    Vector2 vector = Main.player[npc.target].Center - npc.Center;
                    bool antiCheese = false;
                    if (localAI[0] >= 360f || vector.Length() > 2000f)
                    {
                        if (localAI[0] >= 360f) localAI[0] = 360f;
                        antiCheese = true;
                    }

                    float spotX = localAI[1];
                    float spotY = localAI[2];
                    FindKingSlimeTeleportSpot(npc, npc.target, antiCheese, ref spotX, ref spotY);
                    localAI[1] = spotX;
                    localAI[2] = spotY;
                }
            }

            if (!Collision.CanHitLine(npc.Center, 0, 0,
                    Main.player[npc.target].Center, 0, 0)
                || Math.Abs(npc.Top.Y - Main.player[npc.target].Bottom.Y) > 160f)
            {
                ai[2] += 1f;
                if (Main.netMode != NetmodeID.MultiplayerClient) localAI[0] += 1f;
            }
            else if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                localAI[0] -= 1f;
                if (localAI[0] < 0f) localAI[0] = 0f;
            }

            if (npc.timeLeft < 10 && (ai[0] != 0f || ai[1] != 0f))
            {
                ai[0] = 0f;
                ai[1] = 0f;
                npc.netUpdate = true;
                flag2 = false;
            }

            if (ai[1] == 5f)
            {
                flag2 = true;
                ai[0] += 1f;
                num = MathHelper.Clamp((60f - ai[0]) / 60f, 0f, 1f);
                num = 0.5f + num * 0.5f;
                if (ai[0] >= 60f) flag3 = true;

                if (ai[0] == 60f)
                    Gore.NewGore(npc.GetSource_FromAI(),
                        npc.Center + new Vector2(-40f, -npc.height / 2),
                        npc.velocity, 734, 1f);

                if (ai[0] >= 60f && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    npc.Bottom = new Vector2(localAI[1], localAI[2]);
                    ai[1] = 6f;
                    ai[0] = 0f;
                    npc.netUpdate = true;
                }
                if (Main.netMode == NetmodeID.MultiplayerClient && ai[0] >= 120f)
                {
                    ai[1] = 6f;
                    ai[0] = 0f;
                }

                if (!flag3)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        int num5 = Dust.NewDust(npc.position + Vector2.UnitX * -20f,
                            npc.width + 40, npc.height, DustID.TintableDust,
                            npc.velocity.X, npc.velocity.Y,
                            150, new Color(78, 136, 255, 80), 2f);
                        Main.dust[num5].noGravity = true;
                        Main.dust[num5].velocity *= 0.5f;
                    }
                }
            }
            else if (ai[1] == 6f)
            {
                flag2 = true;
                ai[0] += 1f;
                num = MathHelper.Clamp(ai[0] / 30f, 0f, 1f);
                num = 0.5f + num * 0.5f;
                if (ai[0] >= 30f && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    ai[1] = 0f;
                    ai[0] = 0f;
                    npc.netUpdate = true;
                    npc.TargetClosest();
                }
                if (Main.netMode == NetmodeID.MultiplayerClient && ai[0] >= 60f)
                {
                    ai[1] = 0f;
                    ai[0] = 0f;
                    npc.TargetClosest();
                }
                for (int j = 0; j < 10; j++)
                {
                    int num6 = Dust.NewDust(npc.position + Vector2.UnitX * -20f,
                        npc.width + 40, npc.height, DustID.TintableDust,
                        npc.velocity.X, npc.velocity.Y,
                        150, new Color(78, 136, 255, 80), 2f);
                    Main.dust[num6].noGravity = true;
                    Main.dust[num6].velocity *= 2f;
                }
            }

            npc.dontTakeDamage = npc.hide = flag3;

            if (npc.velocity.Y == 0f)
            {
                npc.velocity.X *= 0.8f;
                if (npc.velocity.X > -0.1f && npc.velocity.X < 0.1f)
                    npc.velocity.X = 0f;

                if (!flag2)
                {
                    ai[0] += 2f;
                    if ((float)npc.life < npc.lifeMax * 0.8f) ai[0] += 1f;
                    if ((float)npc.life < npc.lifeMax * 0.6f) ai[0] += 1f;
                    if ((float)npc.life < npc.lifeMax * 0.4f) ai[0] += 2f;
                    if ((float)npc.life < npc.lifeMax * 0.2f) ai[0] += 3f;
                    if ((float)npc.life < npc.lifeMax * 0.1f) ai[0] += 4f;

                    float scaleProgress = GetScaleProgress(npc);
                    ai[0] += scaleProgress * 3f;

                    if (ai[0] >= 0f)
                    {
                        npc.netUpdate = true;
                        npc.TargetClosest();

                        float speedBoost = 1f + scaleProgress * 0.35f;

                        if (ai[1] == 3f)
                        {
                            npc.velocity.Y = -13f * speedBoost;
                            npc.velocity.X += 3.5f * npc.direction * speedBoost;
                            ai[0] = -200f;
                            ai[1] = 0f;
                        }
                        else if (ai[1] == 2f)
                        {
                            npc.velocity.Y = -6f * speedBoost;
                            npc.velocity.X += 4.5f * npc.direction * speedBoost;
                            ai[0] = -120f;
                            ai[1] += 1f;
                        }
                        else
                        {
                            npc.velocity.Y = -8f * speedBoost;
                            npc.velocity.X += 4f * npc.direction * speedBoost;
                            ai[0] = -120f;
                            ai[1] += 1f;
                        }
                    }
                }
            }
            else if (npc.target < 255)
            {
                float num7 = 3f;
                if (Main.getGoodWorld) num7 = 6f;
                if ((npc.direction == 1 && npc.velocity.X < num7)
                    || (npc.direction == -1 && npc.velocity.X > 0f - num7))
                {
                    if ((npc.direction == -1 && npc.velocity.X < 0.1f)
                        || (npc.direction == 1 && npc.velocity.X > -0.1f))
                        npc.velocity.X += 0.2f * npc.direction;
                    else
                        npc.velocity.X *= 0.93f;
                }
            }

            int num8 = Dust.NewDust(npc.position, npc.width, npc.height, DustID.TintableDust,
                npc.velocity.X, npc.velocity.Y, 255,
                new Color(0, 80, 255, 80), npc.scale * 1.2f);
            Main.dust[num8].noGravity = true;
            Main.dust[num8].velocity *= 0.5f;

            if (npc.life <= 0) return;

            float healthRatio = (float)npc.life / npc.lifeMax;
            float scaleBase = MathHelper.Lerp(KingSlimeMaxScale, KingSlimeMinScale, healthRatio);
            scaleBase *= num;

            if (scaleBase != npc.scale || flag)
            {
                npc.position.X += npc.width / 2;
                npc.position.Y += npc.height;
                npc.scale = scaleBase;
                npc.width = (int)(98f * npc.scale);
                npc.height = (int)(92f * npc.scale);
                npc.position.X -= npc.width / 2;
                npc.position.Y -= npc.height;
            }

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int num10 = (int)(npc.lifeMax * 0.05f);
            if ((float)(npc.life + num10) >= ai[3]) return;

            ai[3] = npc.life;
            int num11 = Main.rand.Next(1, 4);
            for (int k = 0; k < num11; k++)
            {
                int x = (int)(npc.position.X + Main.rand.Next(npc.width - 32));
                int y = (int)(npc.position.Y + Main.rand.Next(npc.height - 32));
                short num12 = NPCID.BlueSlime;
                if (Main.expertMode && Main.rand.NextBool(4))
                    num12 = SpikedSlimeID;

                int num13 = NPC.NewNPC(npc.GetSource_FromAI(), x, y, num12);
                Main.npc[num13].SetDefaults(num12);
                Main.npc[num13].velocity.X = Main.rand.Next(-15, 16) * 0.1f;
                Main.npc[num13].velocity.Y = Main.rand.Next(-30, 1) * 0.1f;
                Main.npc[num13].ai[0] = -1000 * Main.rand.Next(3);
                Main.npc[num13].ai[1] = -1f;

                if (Main.netMode == NetmodeID.Server && num13 < Main.maxNPCs)
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, num13);
            }
        }

        private static float GetScaleProgress(NPC npc)
        {
            float healthRatio = (float)npc.life / npc.lifeMax;
            return MathHelper.Clamp(1f - healthRatio, 0f, 1f);
        }

        #endregion

        #region 传送点搜索

        private static bool FindKingSlimeTeleportSpot(
            NPC npc, int targetPlayer, bool antiCheese,
            ref float teleportSpotX, ref float teleportSpotY)
        {
            if (antiCheese)
            {
                int num = Player.FindClosest(npc.position, npc.width, npc.height);
                teleportSpotX = Main.player[num].Bottom.X;
                teleportSpotY = Main.player[num].Bottom.Y;
                return false;
            }

            Point point = Main.player[targetPlayer].Center.ToTileCoordinates();
            Point[] cache = new Point[KingSlimePointCacheMax];

            if (BuildKingSlimeTeleportCache(npc, targetPlayer, point.X, point.Y, 10, 7, cache, out int cacheSize))
            {
                int idx = Main.rand.Next(0, cacheSize);
                teleportSpotX = cache[idx].X * 16 + 8;
                teleportSpotY = cache[idx].Y * 16;
                return true;
            }

            if (BuildKingSlimeTeleportCache(npc, targetPlayer, point.X, point.Y, 6, 2, cache, out cacheSize))
            {
                int idx = Main.rand.Next(0, cacheSize);
                teleportSpotX = cache[idx].X * 16 + 8;
                teleportSpotY = cache[idx].Y * 16;
                return true;
            }

            int num4 = Player.FindClosest(npc.position, npc.width, npc.height);
            teleportSpotX = Main.player[num4].Bottom.X;
            teleportSpotY = Main.player[num4].Bottom.Y;
            return false;
        }

        /// <summary>
        /// 在指定范围内搜索合法的传送点，并填充到缓存数组中
        /// </summary>
        private static bool BuildKingSlimeTeleportCache(
            NPC npc, int targetPlayer, int tileTargetX, int tileTargetY,
            int outerRange, int innerRange,
            Point[] cache, out int cacheSize)
        {
            cacheSize = 0;
            AddKingSlimeTeleportCacheTiles(npc, targetPlayer,
                tileTargetX - outerRange, tileTargetX - innerRange,
                tileTargetY - outerRange, tileTargetY + outerRange,
                cache, ref cacheSize);
            AddKingSlimeTeleportCacheTiles(npc, targetPlayer,
                tileTargetX + innerRange, tileTargetX + outerRange,
                tileTargetY - outerRange, tileTargetY + outerRange,
                cache, ref cacheSize);
            AddKingSlimeTeleportCacheTiles(npc, targetPlayer,
                tileTargetX - innerRange, tileTargetX + innerRange,
                tileTargetY - outerRange, tileTargetY - innerRange,
                cache, ref cacheSize);
            AddKingSlimeTeleportCacheTiles(npc, targetPlayer,
                tileTargetX - innerRange, tileTargetX + innerRange,
                tileTargetY + innerRange, tileTargetY + outerRange,
                cache, ref cacheSize);
            return cacheSize > 0;
        }

        private static void AddKingSlimeTeleportCacheTiles(
            NPC npc, int targetPlayer, int x0, int x1, int y0, int y1,
            Point[] cache, ref int cacheSize)
        {
            int minX = Math.Max(0, x0);
            int maxX = Math.Min(Main.maxTilesX - 1, x1);
            int minY = Math.Max(1, y0);
            int maxY = Math.Min(Main.maxTilesY - 1, y1);

            for (int i = minX; i <= maxX; i++)
            {
                for (int j = minY; j <= maxY; j++)
                {
                    Tile tile = Main.tile[i, j];
                    if (!tile.HasTile) continue;
                    if (cacheSize >= cache.Length) return;

                    int tileType = tile.TileType;
                    bool isSolidGround = (Main.tileSolid[tileType] && !Main.tileSolidTop[tileType])
                                      || TileID.Sets.Platforms[tileType];
                    if (!isSolidGround) continue;
                    if (WorldGen.SolidTile(Main.tile[i, j - 1])) continue;

                    Vector2 position = new(i * 16 + 8, j * 16 - npc.height / 2);
                    if (tile.LiquidType == LiquidID.Lava) continue;
                    if (!Collision.CanHitLine(position, 0, 0,
                            Main.player[targetPlayer].Center, 0, 0)) continue;

                    cache[cacheSize] = new Point(i, j);
                    cacheSize++;
                }
            }
        }

        #endregion

        #region 蓝史莱姆：10% 减伤 + 33% 反弹

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.BlueSlime &&
                ModContent.GetInstance<DZTWorldSystem>().DZTActive &&
                IsKingSlimeAlive())
            {
                modifiers.FinalDamage *= 0.9f;
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type != NPCID.BlueSlime) return;
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (!projectile.friendly || projectile.hostile) return;
            if (!IsKingSlimeAlive()) return;
            if (!Main.rand.NextBool(3)) return;

            modifiers.FinalDamage *= 0f;

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

            projectile.Kill();
        }

        /// <summary>
        /// 当前世界中是否有存活的史莱姆王
        /// </summary>
        private static bool IsKingSlimeAlive()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.KingSlime && npc.life > 0)
                    return true;
            }
            return false;
        }
        #endregion

        #region 尖刺史莱姆：射弹频率加快 50%

        public override void PostAI(NPC npc)
        {
            if (npc.type != SpikedSlimeID) return;
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;

            // 原版每帧 localAI[0] -= 1f，这里额外再减 0.5f
            // 总递减 1.5f/帧，冷却时间变为原来的 2/3，频率加快 50%
            if (npc.localAI[0] > 0f)
                npc.localAI[0] -= 0.5f;
        }

        #endregion
    }
}