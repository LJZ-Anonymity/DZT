using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class GolemAIOverride : GlobalNPC
    {
        public override bool? CanFallThroughPlatforms(NPC npc)
        {
            if (npc.type != NPCID.Golem || !ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                return null;
            if (npc.velocity.Y < 0f) return false;
            if (npc.velocity.Y >= 0f)
            {
                int startX = (int)((npc.position.X + 8) / 16f);
                int endX = (int)((npc.position.X + npc.width - 8) / 16f);
                int startY = (int)((npc.position.Y + npc.height) / 16f);

                // 向下扫描 2 格，检查是否有平台或实心方块
                int endY = startY + 2;

                bool hitPlatform = false;
                bool hitSolid = false;

                for (int y = startY; y <= endY; y++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        if (WorldGen.InWorld(x, y))
                        {
                            Tile tile = Main.tile[x, y];
                            if (tile.HasTile)
                            {
                                if (TileID.Sets.Platforms[tile.TileType])
                                {
                                    hitPlatform = true;
                                }
                                else if (Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                                {
                                    hitSolid = true;
                                }
                            }
                        }
                    }
                }

                if (hitSolid) return false; // 不穿透实心方块
                if (hitPlatform) return true; // 穿透平台
            }
            return null;
        }

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.Golem || !ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;

            #region 每次承受 5% 伤害随机缩放
            // localAI[2] 上次触发缩放时的生命值
            // localAI[3] 目标缩放值
            if (npc.localAI[2] == 0f && npc.life > 0)
            {
                npc.localAI[2] = npc.life;
                npc.localAI[3] = 1f;
            }

            if (npc.life > npc.localAI[2])
                npc.localAI[2] = npc.life;

            if (npc.localAI[2] - npc.life >= npc.lifeMax * 0.05f)
            {
                npc.localAI[2] = npc.life;
                npc.localAI[3] = Main.rand.NextFloat(0.5f, 1.5f);
            }

            if (Math.Abs(npc.scale - npc.localAI[3]) > 0.01f)
            {
                float nextScale = MathHelper.Lerp(npc.scale, npc.localAI[3], 0.05f);

                Vector2 bottomCenter = new(npc.Center.X, npc.Bottom.Y);

                npc.scale = nextScale;
                npc.width = (int)(114f * npc.scale);
                npc.height = (int)(160f * npc.scale);

                npc.position.X = bottomCenter.X - npc.width / 2f;
                npc.position.Y = bottomCenter.Y - npc.height;

                npc.netUpdate = true;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC child = Main.npc[i];
                    if (child.active && (child.type == NPCID.GolemHead || child.type == NPCID.GolemFistLeft || child.type == NPCID.GolemFistRight || child.type == NPCID.GolemHeadFree))
                    {
                        if (Vector2.DistanceSquared(child.Center, npc.Center) < 4000f * 4000f)
                        {
                            Vector2 childOldCenter = child.Center;
                            child.scale = npc.scale;

                            if (child.type == NPCID.GolemHead) { child.width = (int)(80f * npc.scale); child.height = (int)(80f * npc.scale); }
                            else if (child.type == NPCID.GolemFistLeft || child.type == NPCID.GolemFistRight) { child.width = (int)(40f * npc.scale); child.height = (int)(40f * npc.scale); }
                            else if (child.type == NPCID.GolemHeadFree) { child.width = (int)(60f * npc.scale); child.height = (int)(60f * npc.scale); }

                            child.Center = childOldCenter;
                            child.netUpdate = true;
                        }
                    }
                }
            }
            else if (npc.scale != npc.localAI[3])
            {
                npc.scale = npc.localAI[3];
                npc.width = (int)(114f * npc.scale);
                npc.height = (int)(160f * npc.scale);
            }
            #endregion
        }
    }
}