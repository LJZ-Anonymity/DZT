using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Bosses
{
    internal class SkeletronPrimeAIOverride : GlobalNPC
    {
        private const float DamageReductionPerHand = 0.05f; // 每只手提供的减伤比例

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (npc.type == NPCID.SkeletronPrime)
            {
                int handCount = 0;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC hand = Main.npc[i];
                    if (hand.active && hand.type >= NPCID.PrimeCannon && hand.type <= NPCID.PrimeLaser && hand.ai[1] == npc.whoAmI)
                    {
                        handCount++;
                    }
                }
                modifiers.FinalDamage *= (1f - handCount * DamageReductionPerHand);
            }
        }
    }

    internal class SkeletronPrimeProjectileOverride : GlobalProjectile
    {
        public override void PrepareBombToBlow(Projectile projectile)
        {
            if (!ModContent.GetInstance<DZTWorldSystem>().DZTActive) return;
            if (projectile.type == ProjectileID.BombSkeletronPrime)
            {
                projectile.ai[1] = 1f;
            }
        }
    }
}