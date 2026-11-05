using Microsoft.Xna.Framework;
using Terraria.ModLoader;
using DZT.Common.Systems;
using Terraria.ID;
using Terraria;
using System;

namespace DZT.Content.Bosses
{
    internal class TheTwinsAIOverride : GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Retinazer || entity.type == NPCID.Spazmatism;
        }

        public override void PostAI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;

            // 二阶段冲刺速度与距离 +20%
            if (npc.ai[0] == 3f && npc.ai[1] == 2f && npc.ai[2] == 0f)
            {
                npc.velocity *= 1.2f;
            }

            // 二阶段转向速度 +50%
            if (npc.ai[0] == 3f && npc.ai[1] == 0f)
            {
                float baseTurn = (npc.type == NPCID.Spazmatism) ? 0.2f : 0.1f;
                float extraTurn = baseTurn * 0.5f;

                float targetAngle = (float)(Math.Atan2(
                    Main.player[npc.target].Center.Y - npc.Center.Y,
                    Main.player[npc.target].Center.X - npc.Center.X
                ) + 1.57f);

                // 将目标角度和当前角度差值标准化到 -π 到 π 之间
                float angleDiff = targetAngle - npc.rotation;
                while (angleDiff > Math.PI) angleDiff -= 6.283f;
                while (angleDiff < -Math.PI) angleDiff += 6.283f;

                // 在原版已经转了 baseTurn 的基础上，再额外多转 extraTurn
                if (Math.Abs(angleDiff) > extraTurn)
                {
                    npc.rotation += (angleDiff > 0 ? extraTurn : -extraTurn);
                }
                else
                {
                    // 如果剩余角度很小，直接对齐，防止抖动
                    npc.rotation = targetAngle;
                }
            }

            // 弹幕数量 +5
            HandleExtraProjectiles(npc);
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.ai[0] == 3f && (npc.ai[1] == 1f || npc.ai[1] == 2f))
                modifiers.FinalDamage *= 0.75f; // 二阶段冲刺时 25% 减伤
        }

        /// <summary>
        /// 弹幕追加
        /// </summary>
        private static void HandleExtraProjectiles(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (npc.type == NPCID.Spazmatism)
            {
                // 魔焰眼仅一阶段发射咒火球
                if (npc.ai[0] == 0f && npc.ai[1] == 0f)
                {
                    TryFireSpazmatismExtra(npc);
                }
            }
            else if (npc.type == NPCID.Retinazer)
            {
                // 激光眼两个阶段均会发射激光
                if (npc.ai[1] == 0f) // 处于远程射击状态
                {
                    TryFireRetinazerExtra(npc);
                }
            }
        }

        /// <summary>
        /// 咒火球追加
        /// </summary>
        private static void TryFireSpazmatismExtra(NPC npc)
        {
            if (!npc.HasValidTarget) return;
            Player player = Main.player[npc.target];

            if (!Collision.CanHit(npc.position, npc.width, npc.height,
                    player.position, player.width, player.height))
                return;

            npc.localAI[2] += 1f;
            float fireInterval = Main.expertMode ? 45f : 60f;
            if ((double)npc.life < (double)npc.lifeMax * 0.5) fireInterval *= 0.75f;

            if (npc.localAI[2] < fireInterval) return;
            npc.localAI[2] = 0f;

            Vector2 center = npc.Center;
            Vector2 toPlayer = player.Center - center;
            float baseAngle = (float)Math.Atan2(toPlayer.Y, toPlayer.X);
            int damage = npc.defDamage / 2;

            for (int i = 0; i < 5; i++)
            {
                float spread = MathHelper.Lerp(-0.25f, 0.25f, i / 4f);
                float angle = baseAngle + spread;
                Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 6f;

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(), center, velocity,
                    ProjectileID.CursedFlameHostile, damage, 0f, Main.myPlayer
                );
            }
        }

        /// <summary>
        /// 魔眼激光/死亡激光追加
        /// </summary>
        private static void TryFireRetinazerExtra(NPC npc)
        {
            if (!npc.HasValidTarget) return;
            Player player = Main.player[npc.target];

            if (!Collision.CanHit(npc.position, npc.width, npc.height,
                    player.position, player.width, player.height))
                return;

            npc.localAI[2] += 1f;
            // 第一阶段射击较慢，第二阶段射击更快
            float fireInterval = npc.ai[0] == 0f ? 75f : 55f;
            if (Main.expertMode) fireInterval *= 0.8f;
            if ((double)npc.life < (double)npc.lifeMax * 0.5) fireInterval *= 0.7f;

            if (npc.localAI[2] < fireInterval) return;
            npc.localAI[2] = 0f;

            Vector2 center = npc.Center;
            Vector2 toPlayer = player.Center - center;
            float baseAngle = (float)Math.Atan2(toPlayer.Y, toPlayer.X);
            int damage = npc.defDamage / 2;

            // 第一阶段使用魔眼激光，第二阶段使用死亡激光
            int projType = npc.ai[0] == 0f ? ProjectileID.EyeLaser : ProjectileID.DeathLaser;
            float speed = npc.ai[0] == 0f ? 8f : 10f; // 第二阶段弹速更快

            for (int i = 0; i < 5; i++)
            {
                float spread = MathHelper.Lerp(-0.2f, 0.2f, i / 4f);
                float angle = baseAngle + spread;
                Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(), center, velocity,
                    projType, damage, 0f, Main.myPlayer
                );
            }
        }
    }
}