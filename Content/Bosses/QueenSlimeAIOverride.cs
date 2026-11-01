using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Bosses
{
    internal class QueenSlimeAIOverride : GlobalNPC
    {
        private const int QueenSlimeGelAttackID = 926; // 二阶段弹幕 ID

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.QueenSlimeBoss && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
            {
                bool isPhase2 = npc.life <= npc.lifeMax / 2;
                if (isPhase2 && npc.ai[0] == 5f && npc.ai[2] == 1f && npc.ai[1] >= 9f)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        for (int i = 0; i < 20; i++)
                        {
                            Vector2 velocity = Main.rand.NextVector2Unit() * 9f;
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                velocity,
                                QueenSlimeGelAttackID,
                                30,
                                0f,
                                Main.myPlayer
                            );
                        }
                    }

                    // 强制重置 AI 状态，跳过原版 10 个圆环弹幕发射逻辑
                    npc.ai[0] = 0f;
                    npc.ai[1] = 0f;
                    npc.ai[2] = 0f;
                    npc.netUpdate = true;
                }
            }

            return true;
        }

        /// <summary>
        /// 全程获得 20% 伤害减免
        /// </summary>
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.QueenSlimeBoss && ModContent.GetInstance<DZTWorldSystem>().DZTActive)
                modifiers.FinalDamage *= 0.8f;
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.type != NPCID.QueenSlimeBoss) return;
            if (!projectile.friendly || projectile.hostile) return;
            if (!Main.rand.NextBool(10)) return; // 10% 概率反弹

            modifiers.FinalDamage *= 0f; // 抵消本次伤害
            if (projectile.owner == Main.myPlayer) // 生成反弹弹幕
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
                    reflected.friendly = false; // 变成敌对弹幕
                    reflected.hostile = true;
                    reflected.penetrate = 1;
                    reflected.netUpdate = true;
                }
            }

            projectile.Kill(); // 销毁原弹幕
        }
    }
}