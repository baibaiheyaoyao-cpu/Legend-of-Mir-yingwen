using System.IO;

/// <summary>
/// 自定义技能定义(Spell 242-255 槽位), 服务端 Configs\CustomSkills.ini 配置,
/// "自定义技能"面板编辑, 登录/保存时通过 S.CustomSkillConfigs 下发客户端.
/// 模板参数按 Template 取义:
///   1=攻击强化: P1=伤害倍率%(140即1.4x)  P2=蓄力窗口ms  P3=命中吸血%      P4=属性(0DC/1MC/2SC)
///   2=自身爆发: P1=半径格               P2=固定伤害     P3=每级伤害       P4=属性(0DC/1MC/2SC)
///   3=目标轰炸: P1=半径格               P2=伤害段数     P3=段间隔ms      P4=固定伤害 P5=每级伤害 P6=属性(0DC/1MC/2SC)
/// </summary>
public class CustomSkillConfig
{
    public Spell Spell;
    public string Name = "";
    public byte Template;            //0=未启用 1=攻击强化 2=自身爆发 3=目标轰炸
    public ushort BaseCost = 10;     //耗蓝
    public uint DelayMs = 2000;      //冷却毫秒
    public byte Icon;                //MagIcon图标索引
    public int P1, P2, P3, P4, P5, P6;
    public byte EffectLib = 255;     //0=Magic 1=Magic2 2=Magic3 3=Magic4 4=MagicC 5=MagicD 255=无特效
    public short EffectBase;         //特效起始帧
    public byte EffectCount;         //特效帧数
    public byte EffectStride;        //方向步长
    public ushort EffectDuration;    //特效总时长ms
    public ushort SoundSpell;        //复用该Spell编号的音效(20000+编号*10), 0=无声
    public byte CastAction;          //0=通用施法动作 1=Attack1贴身动作
    public string BindItem = "";     //绑定印名(空=未绑定, 仅GM调试)

    public bool Enabled { get { return Template > 0; } }

    public CustomSkillConfig() { }
    public CustomSkillConfig(BinaryReader reader)
    {
        Spell = (Spell)reader.ReadByte();
        Name = reader.ReadString();
        Template = reader.ReadByte();
        BaseCost = reader.ReadUInt16();
        DelayMs = reader.ReadUInt32();
        Icon = reader.ReadByte();
        P1 = reader.ReadInt32();
        P2 = reader.ReadInt32();
        P3 = reader.ReadInt32();
        P4 = reader.ReadInt32();
        P5 = reader.ReadInt32();
        P6 = reader.ReadInt32();
        EffectLib = reader.ReadByte();
        EffectBase = reader.ReadInt16();
        EffectCount = reader.ReadByte();
        EffectStride = reader.ReadByte();
        EffectDuration = reader.ReadUInt16();
        SoundSpell = reader.ReadUInt16();
        CastAction = reader.ReadByte();
        BindItem = reader.ReadString();
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write((byte)Spell);
        writer.Write(Name);
        writer.Write(Template);
        writer.Write(BaseCost);
        writer.Write(DelayMs);
        writer.Write(Icon);
        writer.Write(P1);
        writer.Write(P2);
        writer.Write(P3);
        writer.Write(P4);
        writer.Write(P5);
        writer.Write(P6);
        writer.Write(EffectLib);
        writer.Write(EffectBase);
        writer.Write(EffectCount);
        writer.Write(EffectStride);
        writer.Write(EffectDuration);
        writer.Write(SoundSpell);
        writer.Write(CastAction);
        writer.Write(BindItem);
    }
}
