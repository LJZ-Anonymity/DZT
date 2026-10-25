using Terraria.ModLoader;
using Terraria;

namespace DZT.Common.GlobalNPCs
{
    public class HostileContactDamageEdit : GlobalNPC
    {
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            // 只处理敌对NPC
            if (!npc.friendly)
            {
                modifiers.FinalDamage *= 1.20f; // 接触伤害提高 20%
            }
        }
    }
}