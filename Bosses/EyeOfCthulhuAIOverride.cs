using Terraria.ModLoader;
using Terraria.ID;
using Terraria;

namespace DZT.Bosses
{
    internal class EyeOfCthulhuAIOverride: GlobalNPC
    {
        public override bool PreAI(NPC npc)
        {
            //if (npc.type == NPCID.EyeofCthulhu)
            //{
            //    CustomEyeAI(npc);
            //    return false; // 阻止原版 AI
            //}
            return true;
        }

        private void CustomEyeAI(NPC npc)
        {
            // 自定义克苏鲁之眼 AI
        }
    }
}