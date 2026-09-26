using System.Collections.Generic;
using System.IO;

/// <summary>
/// CustomMagic数据驱动技能的客户端配置(原版水晶端 Custom\CustomMagic\*.ini 兼容).
/// 服务端解析INI后经 S.CustomMagicConfigs 下发, 客户端按段渲染特效/描述/音效.
/// 段类型: Self/Self2=施法者身上, Magic=脚下法阵, Fly/Explosion/Target=目标点延迟落地.
/// </summary>
public class CustomMagicSegment
{
    public string Kind = "";          // Self/Self2/Fly/Explosion/Target/Magic
    public byte File;                 // 原版库索引(客户端经映射表转实际Lib)
    public short StartIndex;          // 起始帧
    public byte PlayCount;            // 帧数
    public byte EmptyCount;           // 空帧数(方向步长=PlayCount+EmptyCount)
    public short PlayTime = 500;      // 播放时长ms
    public int Delay;                 // 延迟ms
    public byte DirCount;             // 方向数(>0为8方向序列帧)
    public bool CalcDir;              // 按目标方向取向
    public byte DrawMode;
    public byte DrawOrder;
    public bool Repeat;               // 循环播放(法阵/持续类)

    public CustomMagicSegment() { }

    public void Save(BinaryWriter writer)
    {
        writer.Write(Kind);
        writer.Write(File);
        writer.Write(StartIndex);
        writer.Write(PlayCount);
        writer.Write(EmptyCount);
        writer.Write(PlayTime);
        writer.Write(Delay);
        writer.Write(DirCount);
        writer.Write(CalcDir);
        writer.Write(DrawMode);
        writer.Write(DrawOrder);
        writer.Write(Repeat);
    }

    public CustomMagicSegment(BinaryReader reader)
    {
        Kind = reader.ReadString();
        File = reader.ReadByte();
        StartIndex = reader.ReadInt16();
        PlayCount = reader.ReadByte();
        EmptyCount = reader.ReadByte();
        PlayTime = reader.ReadInt16();
        Delay = reader.ReadInt32();
        DirCount = reader.ReadByte();
        CalcDir = reader.ReadBoolean();
        DrawMode = reader.ReadByte();
        DrawOrder = reader.ReadByte();
        Repeat = reader.ReadBoolean();
    }
}

public class CustomMagicConfig
{
    public Spell Spell;
    public string Name = "";
    public string Description = "";
    public int CastSoundId;           // 20000+原编号*10+n, 0=无
    public int FlySoundId;
    public int ExplosionSoundId;
    public List<CustomMagicSegment> Segments = new List<CustomMagicSegment>();

    public CustomMagicConfig() { }

    public void Save(BinaryWriter writer)
    {
        writer.Write((byte)Spell);
        writer.Write(Name);
        writer.Write(Description);
        writer.Write(CastSoundId);
        writer.Write(FlySoundId);
        writer.Write(ExplosionSoundId);
        writer.Write(Segments.Count);
        for (int i = 0; i < Segments.Count; i++)
            Segments[i].Save(writer);
    }

    public CustomMagicConfig(BinaryReader reader)
    {
        Spell = (Spell)reader.ReadByte();
        Name = reader.ReadString();
        Description = reader.ReadString();
        CastSoundId = reader.ReadInt32();
        FlySoundId = reader.ReadInt32();
        ExplosionSoundId = reader.ReadInt32();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
            Segments.Add(new CustomMagicSegment(reader));
    }
}
