using System.Collections.Generic;
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
        public override bool InstancePerEntity => true;

        private const float ScaleThreshold = 0.05f; // 触发缩放的血量比例阈值
        private const float MinScale = 0.5f; // 最小体型倍率
        private const float MaxScale = 1.5f; // 最大体型倍率

        // 记录头/手/本体各自已跨过的 5% 档位（key 用 whoAmI）
        private readonly Dictionary<int, int> partTriggerCounts = [];

        // 当前目标缩放值
        private float targetScale = 1f;

        // 首次触发前不接管缩放，避免干扰原版
        private bool scaleActive = false;

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
                                    hitPlatform = true;
                                else if (Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                                    hitSolid = true;
                            }
                        }
                    }
                }

                if (hitSolid) return false;
                if (hitPlatform) return true;
            }
            return null;
        }

        public override void PostAI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;

            if (npc.type == NPCID.Golem)
                PostAI_Body(npc);
            else if (IsGolemPart(npc.type))
                PostAI_Part(npc);
        }

        private void PostAI_Body(NPC npc)
        {
            if (npc.life <= 0) return;

            CleanupStaleKeys(npc);

            // 阶段判定
            bool phase1 = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC part = Main.npc[i];
                if (part.active && part.type == NPCID.GolemHead &&
                    BelongsTo(part, npc) && part.life > 0)
                {
                    phase1 = true;
                    break;
                }
            }

            bool shouldScale = false;

            if (phase1)
            {
                // 一阶段，头 + 双拳，各自独立 5%
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC part = Main.npc[i];
                    if (!part.active) continue;
                    if (part.type != NPCID.GolemHead &&
                        part.type != NPCID.GolemFistLeft &&
                        part.type != NPCID.GolemFistRight) continue;
                    if (!BelongsTo(part, npc)) continue;
                    if (part.lifeMax <= 0) continue;

                    if (CheckThreshold(part.whoAmI, part.life, part.lifeMax))
                        shouldScale = true;
                }
            }
            else
            {
                // 二阶段只按本体身体 5%
                if (npc.lifeMax > 0)
                {
                    if (CheckThreshold(npc.whoAmI, npc.life, npc.lifeMax))
                        shouldScale = true;
                }
            }

            if (shouldScale)
            {
                scaleActive = true;
                targetScale = Main.rand.NextFloat(MinScale, MaxScale);
            }

            if (!scaleActive) return;

            // 本体平滑缩放
            if (Math.Abs(npc.scale - targetScale) > 0.001f)
            {
                float next = Math.Abs(npc.scale - targetScale) > 0.01f
                    ? MathHelper.Lerp(npc.scale, targetScale, 0.05f)
                    : targetScale;

                // 脚底中心锚定，避免掉进地里
                Vector2 bottomCenter = new(npc.Center.X, npc.Bottom.Y);

                npc.scale = next;
                npc.width = (int)(114f * npc.scale);
                npc.height = (int)(160f * npc.scale);

                npc.position.X = bottomCenter.X - npc.width / 2f;
                npc.position.Y = bottomCenter.Y - npc.height;

                npc.netUpdate = true;
            }

            // 同帧强制同步所有归属部件
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC child = Main.npc[i];
                if (!child.active || !IsGolemPart(child.type)) continue;
                if (!BelongsTo(child, npc)) continue;

                SyncPartScale(child, npc.scale);
            }
        }

        private static void PostAI_Part(NPC part)
        {
            NPC body = FindGolemBody(part);
            if (body == null || !body.active) return;
            SyncPartScale(part, body.scale);
        }

        /// <summary>
        /// 同步身体缩放
        /// </summary>
        private static void SyncPartScale(NPC part, float s)
        {
            float baseSize = GetBaseSize(part.type);
            int w = (int)(baseSize * s);
            int h = (int)(baseSize * s);

            if (Math.Abs(part.scale - s) > 0.0001f ||
                part.width != w || part.height != h)
            {
                Vector2 oldCenter = part.Center;

                part.scale = s;
                part.width = w;
                part.height = h;

                part.Center = oldCenter;
                part.netUpdate = true;
            }
        }

        // 首次记录基线不触发；跨过新的 5% 档位返回 true
        private bool CheckThreshold(int key, int life, int lifeMax)
        {
            if (lifeMax <= 0) return false;

            float lostRatio = 1f - ((float)life / lifeMax);
            int currentCount = (int)(lostRatio / ScaleThreshold);

            if (!partTriggerCounts.TryGetValue(key, out int recorded))
            {
                partTriggerCounts[key] = currentCount;
                return false;
            }

            if (currentCount > recorded)
            {
                partTriggerCounts[key] = currentCount;
                return true;
            }

            return false;
        }

        // 清理已失效的 whoAmI，防止复用污染
        private void CleanupStaleKeys(NPC body)
        {
            List<int> toRemove = null;

            foreach (int k in partTriggerCounts.Keys)
            {
                if (k < 0 || k >= Main.maxNPCs)
                {
                    (toRemove ??= []).Add(k);
                    continue;
                }

                NPC n = Main.npc[k];
                bool valid = n.active && (
                    n.type == NPCID.Golem ||
                    (IsGolemPart(n.type) && BelongsTo(n, body))
                );

                if (!valid)
                    (toRemove ??= []).Add(k);
            }

            if (toRemove != null)
                foreach (int k in toRemove)
                    partTriggerCounts.Remove(k);
        }

        private static bool IsGolemPart(int type)
        {
            return type == NPCID.GolemHead ||
                   type == NPCID.GolemHeadFree ||
                   type == NPCID.GolemFistLeft ||
                   type == NPCID.GolemFistRight;
        }

        private static float GetBaseSize(int type)
        {
            if (type == NPCID.GolemHead) return 80f;
            if (type == NPCID.GolemHeadFree) return 60f;
            return 40f; // 左右拳
        }

        private static bool BelongsTo(NPC part, NPC body)
        {
            if ((int)part.ai[1] == body.whoAmI) return true;
            if (part.realLife == body.whoAmI) return true;
            return Vector2.DistanceSquared(part.Center, body.Center) < 2000f * 2000f;
        }

        private static NPC FindGolemBody(NPC part)
        {
            int idx = (int)part.ai[1];
            if (idx >= 0 && idx < Main.maxNPCs)
            {
                NPC n = Main.npc[idx];
                if (n.active && n.type == NPCID.Golem) return n;
            }

            idx = part.realLife;
            if (idx >= 0 && idx < Main.maxNPCs)
            {
                NPC n = Main.npc[idx];
                if (n.active && n.type == NPCID.Golem) return n;
            }

            NPC closest = null;
            float best = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == NPCID.Golem)
                {
                    float d = Vector2.DistanceSquared(n.Center, part.Center);
                    if (d < best) { best = d; closest = n; }
                }
            }
            return closest;
        }
    }
}