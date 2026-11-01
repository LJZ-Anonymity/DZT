using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Items.GolemTestWeapon
{
    public class GolemTestWeapon : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 1;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.autoReuse = false;
            Item.knockBack = 0f; // 无击退
            Item.damage = 1;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = false;
            Item.rare = ItemRarityID.Red;
        }

        public override void ModifyHitNPC(Player player, NPC target, ref NPC.HitModifiers modifiers)
        {
            if (target.type == NPCID.Golem ||
                target.type == NPCID.GolemHead ||
                target.type == NPCID.GolemFistLeft ||
                target.type == NPCID.GolemFistRight ||
                target.type == NPCID.GolemHeadFree) 
            {
                int damage = (int)(target.lifeMax * 0.05f);
                modifiers.FinalDamage.Flat = damage; // 强制将最终伤害修改为 5% 最大生命值
                modifiers.DisableCrit(); // 禁用暴击
                modifiers.ArmorPenetration += 10000; // 无视防御力
                modifiers.DisableCrit(); // 无视无敌帧
            }
            else
            {
                modifiers.FinalDamage.Flat = 999999;
            }
        }

        // 穿透石巨人的原版无敌机制（核心存在时身体无敌）
        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (target.type == NPCID.Golem)
            {
                target.dontTakeDamage = false;
            }
        }
    }
}