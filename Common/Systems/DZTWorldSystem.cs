using Terraria.ModLoader.IO;
using Terraria.ModLoader;
using Terraria.ID;
using System.IO;
using Terraria;

namespace DZT.Common.Systems;

public class DZTWorldSystem : ModSystem
{
    /// <summary>
    /// 当前世界是否启用了 DZT 难度。
    /// 世界级状态：每个世界独立保存。
    /// </summary>
    public bool DZTActive { get; private set; }

    // 保存到 .wld 世界文件
    public override void SaveWorldData(TagCompound tag)
    {
        tag["DZTActive"] = DZTActive;
    }

    // 从世界文件读取
    public override void LoadWorldData(TagCompound tag)
    {
        DZTActive = tag.GetBool("DZTActive");
    }

    // 多人游戏：服务器把世界数据发给客户端时调用
    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(DZTActive);
    }

    // 多人游戏：客户端接收服务器同步的世界数据
    public override void NetReceive(BinaryReader reader)
    {
        DZTActive = reader.ReadBoolean();
    }

    /// <summary>
    /// 设置 DZT 难度状态。
    /// 注意：多人游戏中只应在服务端/主机调用。
    /// 客户端切换入口后续通过 ModPacket 向服务器发请求。
    /// </summary>
    public void SetDZTActive(bool active)
    {
        if (DZTActive == active)
            return;

        DZTActive = active;

        // 服务端修改后，广播世界数据，触发客户端 NetReceive
        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendData(MessageID.WorldData);
        }
    }
}