using DZT.Common.Networking;
using DZT.Common.Systems;
using Terraria.ModLoader;
using Terraria.ID;
using System.IO;
using Terraria;

namespace DZT
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class DZT : Mod
	{
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            DZTMessageType msgType = (DZTMessageType)reader.ReadByte();

            switch (msgType)
            {
                case DZTMessageType.ToggleDZT:
                    bool state = reader.ReadBoolean();

                    // 只允许服务器处理该请求，防止客户端非法修改世界状态
                    if (Main.netMode == NetmodeID.Server)
                    {
                        ModContent.GetInstance<DZTWorldSystem>().SetDZTActive(state);
                    }
                    break;
            }
        }
    }
}