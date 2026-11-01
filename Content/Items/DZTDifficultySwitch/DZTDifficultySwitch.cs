using Microsoft.Xna.Framework;
using DZT.Common.Networking;
using Terraria.Localization;
using Terraria.ModLoader;
using DZT.Common.Systems;
using Terraria.ID;
using Terraria;

namespace DZT.Content.Items.DZTDifficultySwitch;

public class DZTDifficultySwitch : ModItem
{
    public override LocalizedText Tooltip => base.Tooltip;

    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 24;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = 30;
        Item.useAnimation = 30;
        Item.UseSound = SoundID.Item4;
        Item.rare = ItemRarityID.Purple;
        Item.value = 0;
        Item.consumable = false;
    }

    public override bool? UseItem(Player player)
    {
        var sys = ModContent.GetInstance<DZTWorldSystem>();
        bool newState = !sys.DZTActive;

        if (Main.netMode == NetmodeID.MultiplayerClient) // 客户端：发请求给服务器
        {
            ModPacket packet = Mod.GetPacket();
            packet.Write((byte)DZTMessageType.ToggleDZT);
            packet.Write(newState);
            packet.Send();
        }
        else // 单机或主机：直接修改
        {
            sys.SetDZTActive(newState);
        }

        // 本地提示（客户端会显示；服务器端的提示不会广播给其他玩家）
        if (Main.netMode != NetmodeID.Server)
        {
            Main.NewText(
                newState ? "DZT 难度：已启用" : "DZT 难度：已关闭",
                newState ? Color.OrangeRed : Color.LightGreen
            );
        }

        return true;
    }
}