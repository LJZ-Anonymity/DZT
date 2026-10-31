using Terraria.ModLoader;
using DZT.Content.Items;
using Terraria.ID;
using Terraria;

namespace DZT.Common.Players;

public class DZTPlayer : ModPlayer
{
    public override void OnEnterWorld()
    {
        // 只在服务端（单机也算）发放
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int itemType = ModContent.ItemType<DZTDifficultySwitch>();

        // 已有则不再给
        if (Player.HasItem(itemType))
            return;

        // 放到脚下
        Player.QuickSpawnItem(
            Player.GetSource_Misc("DZT"),
            itemType
        );
    }
}