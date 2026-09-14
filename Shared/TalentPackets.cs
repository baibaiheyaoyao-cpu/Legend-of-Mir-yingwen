using System.Text;

namespace ClientPackets
{
    /// <summary>
    /// 客户端 -> 服务端: 请求天赋数据.
    /// 玩家进入游戏后发送一次, 服务端会回发 TalentInfo(全表) + PlayerTalentInfo(玩家状态).
    /// </summary>
    public sealed class ClientTalent : Packet
    {
        public override short Index { get { return (short)ClientPacketIds.ClientTalent; } }

        protected override void ReadPacket(BinaryReader reader)
        { }

        protected override void WritePacket(BinaryWriter writer)
        { }
    }

    /// <summary>
    /// 客户端 -> 服务端: 学习/升级一个天赋.
    /// 服务端校验: 职业匹配 -> 等级足够 -> 前置满足 -> 未满级 -> 点数足够,
    /// 成功后回发 TalentChange 并刷新角色属性.
    /// </summary>
    public sealed class LearnTalent : Packet
    {
        public override short Index { get { return (short)ClientPacketIds.LearnTalent; } }

        /// <summary>要学习的天赋ID(对应 Envir\Talents.txt 里的 Id 列)</summary>
        public int TalentId;

        protected override void ReadPacket(BinaryReader reader)
        {
            TalentId = reader.ReadInt32();
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            writer.Write(TalentId);
        }
    }

    /// <summary>
    /// 客户端 -> 服务端: 申请重置天赋(洗点).
    /// 消耗 Talent.ini 里配置的金币, 成功后返还全部已消耗的点数.
    /// </summary>
    public sealed class ResetTalentPoints : Packet
    {
        public override short Index { get { return (short)ClientPacketIds.ResetTalentPoints; } }

        protected override void ReadPacket(BinaryReader reader)
        { }

        protected override void WritePacket(BinaryWriter writer)
        { }
    }
}

namespace ServerPackets
{
    /// <summary>
    /// 服务端 -> 客户端: 天赋条目数据.
    /// 天赋定义由服务端单方面下发, 客户端不落盘 —— 以后改 Talents.txt 只需重载服务端, 无需重编客户端.
    /// </summary>
    public class ClientTalentInfo
    {
        public int Id;              //天赋ID
        public string Name;         //名称
        public string Description;  //描述
        public int Icon;            //图标编号(客户端图库索引)
        public byte Class;          //限制职业 (MirClass, 255=不限)
        public int RequiredLevel;   //学习需求等级
        public int MaxLevel;        //满级等级
        public int Tier;            //所在层(客户端UI按层分行)
        public int Column;          //层内列序(同一层内从左到右排序)
        public int SpecialEffect;   //特殊特效编号(0=纯属性天赋, >0=召唤类, 暂未开放)
        public Stats Stats;         //每级提供的属性加成(显示用)
        public List<int> PreIds = new List<int>();      //前置天赋ID列表
        public List<int> PreLevels = new List<int>();   //对应前置天赋的需求等级列表(与PreIds一一对应)

        /// <summary>按网络格式反序列化一条天赋(顺序必须与 WritePacket 严格一致)</summary>
        public void Read(BinaryReader reader)
        {
            Id = reader.ReadInt32();
            Name = reader.ReadString();
            Description = reader.ReadString();
            Icon = reader.ReadInt32();
            Class = reader.ReadByte();
            RequiredLevel = reader.ReadInt32();
            MaxLevel = reader.ReadInt32();
            Tier = reader.ReadInt32();
            Column = reader.ReadInt32();
            SpecialEffect = reader.ReadInt32();
            Stats = new Stats(reader);

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                PreIds.Add(reader.ReadInt32());
                PreLevels.Add(reader.ReadInt32());
            }
        }

        /// <summary>按网络格式序列化一条天赋</summary>
        public void Write(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Name);
            writer.Write(Description);
            writer.Write(Icon);
            writer.Write(Class);
            writer.Write(RequiredLevel);
            writer.Write(MaxLevel);
            writer.Write(Tier);
            writer.Write(Column);
            writer.Write(SpecialEffect);
            Stats.Save(writer);

            writer.Write(PreIds.Count);
            for (int i = 0; i < PreIds.Count; i++)
            {
                writer.Write(PreIds[i]);
                writer.Write(PreLevels[i]);
            }
        }
    }

    /// <summary>
    /// 服务端 -> 客户端: 玩家已学天赋条目(属于 PlayerTalentInfo 包).
    /// </summary>
    public class ClientUserTalent
    {
        public int Id;      //天赋ID
        public int Level;   //当前等级

        public ClientUserTalent() { }
        public ClientUserTalent(int id, int level)
        {
            Id = id;
            Level = level;
        }
    }

    /// <summary>
    /// 服务端 -> 客户端: 全天赋表.
    /// </summary>
    public sealed class TalentInfo : Packet
    {
        public override short Index { get { return (short)ServerPacketIds.TalentInfo; } }

        public List<ClientTalentInfo> Talents = new List<ClientTalentInfo>();

        protected override void ReadPacket(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var info = new ClientTalentInfo();
                info.Read(reader);
                Talents.Add(info);
            }
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            writer.Write(Talents.Count);
            for (int i = 0; i < Talents.Count; i++)
                Talents[i].Write(writer);
        }
    }

    /// <summary>
    /// 服务端 -> 客户端: 玩家天赋状态(剩余点数 + 已学列表).
    /// 在 响应ClientTalent / 洗点成功 时发送.
    /// </summary>
    public sealed class PlayerTalentInfo : Packet
    {
        public override short Index { get { return (short)ServerPacketIds.PlayerTalentInfo; } }

        public int Points;                              //当前可用天赋点
        public List<ClientUserTalent> Talents = new List<ClientUserTalent>(); //已学天赋

        protected override void ReadPacket(BinaryReader reader)
        {
            Points = reader.ReadInt32();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                Talents.Add(new ClientUserTalent(reader.ReadInt32(), reader.ReadInt32()));
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            writer.Write(Points);
            writer.Write(Talents.Count);
            for (int i = 0; i < Talents.Count; i++)
            {
                writer.Write(Talents[i].Id);
                writer.Write(Talents[i].Level);
            }
        }
    }

    /// <summary>
    /// 服务端 -> 客户端: 单个天赋等级变化(学习成功后), 客户端据此刷新格子显示与点数.
    /// </summary>
    public sealed class TalentChange : Packet
    {
        public override short Index { get { return (short)ServerPacketIds.TalentChange; } }

        public int TalentId;    //变化的天赋
        public int Level;       //新等级
        public int Points;      //剩余点数

        protected override void ReadPacket(BinaryReader reader)
        {
            TalentId = reader.ReadInt32();
            Level = reader.ReadInt32();
            Points = reader.ReadInt32();
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            writer.Write(TalentId);
            writer.Write(Level);
            writer.Write(Points);
        }
    }

    /// <summary>
    /// 服务端 -> 客户端: 洗点结果.
    /// Success=false 时 Reason 携带失败原因(金币不足等), 由客户端聊天栏提示.
    /// </summary>
    public sealed class TalentReset : Packet
    {
        public override short Index { get { return (short)ServerPacketIds.TalentReset; } }

        public bool Success;
        public string Reason = string.Empty;
        public int Points;  //洗点成功后的剩余点数

        protected override void ReadPacket(BinaryReader reader)
        {
            Success = reader.ReadBoolean();
            Reason = reader.ReadString();
            Points = reader.ReadInt32();
        }

        protected override void WritePacket(BinaryWriter writer)
        {
            writer.Write(Success);
            writer.Write(Reason);
            writer.Write(Points);
        }
    }
}
