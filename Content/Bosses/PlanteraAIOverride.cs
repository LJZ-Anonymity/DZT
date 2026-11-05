using Microsoft.Xna.Framework;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Bosses
{
    internal class PlanteraAIOverride : GlobalNPC
    {
        public override void AI(NPC npc)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.type == NPCID.Plantera && npc.aiStyle == NPCAIStyleID.Plantera)
            {
                npc.localAI[1] += 0.3f; // 世纪之花射弹冷却降低 30%
            }
        }

        // 触手有 30% 概率反弹射弹
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.aiStyle != NPCAIStyleID.PlanteraHook) return;
            if (!projectile.friendly || projectile.hostile) return;
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
    }
}