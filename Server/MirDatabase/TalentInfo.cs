using Server.MirEnvir;

namespace Server.MirDatabase
{
    /// <summary>
    /// 天赋系统 - 天赋定义(全局静态数据).
    /// 数据来源: Envir\Talents.txt (明文Tab分隔, 可用GM命令 @ReloadTalents 热重载).
    ///
    /// 文件格式(#开头为注释行):
    /// Id  名称  图标  职业  需求等级  满级  层  列  前置  每级属性  特效编号  描述
    /// 示例:
    /// 1   治愈  0    Taoist  10  5   1  1  -            HealthRecovery=5,SpellRecovery=5  0  生命/法力恢复+5
    /// 15  治愈秘笈  0 Taoist  40  3   3  1  1:5         HealthRecovery=5,SpellRecovery=5  0  强化版恢复
    ///
    /// 前置列格式: "-" 表示无前置; "天赋Id:需求等级" 为一组, 多组用 "/" 分隔, 全部满足才可学.
    /// 属性列格式: "-" 表示无属性; "Stat枚举名=数值" 为一条, 多条用 "," 分隔, 效果按天赋等级叠加.
    /// </summary>
    public class TalentInfo
    {
        /// <summary>天赋职业限制用 255 表示"全职业可学"</summary>
        public const byte AnyClass = 255;

        public int Index;                  //天赋ID(文件中的Id列)
        public string Name = string.Empty; //名称
        public int Icon;                   //图标编号(客户端图库索引)
        public byte Class = AnyClass;      //限制职业(MirClass字节值, 255=不限)
        public int RequiredLevel;          //学习需求等级
        public int MaxLevel = 1;           //满级等级
        public int Tier = 1;               //所在层(1起, 客户端按层分行显示)
        public int Column;                 //层内列序(同层内从左到右)
        public int SpecialEffect;          //特殊特效编号(0=纯属性; >0=召唤类, 暂未开放学习)
        public string Description = string.Empty; //描述文本

        /// <summary>前置天赋: [0]=前置天赋Id, [1]=该前置需要达到的等级</summary>
        public List<int[]> PreTalents = new List<int[]>();

        /// <summary>每升1级提供的属性加成(按等级叠加: Lv3 即3倍)</summary>
        public Stats Stats = new Stats();

        public bool MatchesClass(MirClass mirClass)
        {
            return Class == AnyClass || Class == (byte)mirClass;
        }
    }

    /// <summary>
    /// 天赋系统 - 角色已学习的单个天赋(存进 CharacterInfo, 随角色数据库保存).
    /// </summary>
    public class UserTalent
    {
        public int Id;      //对应 TalentInfo.Index
        public int Level;   //当前等级(1起, 上限为 TalentInfo.MaxLevel)

        public UserTalent() { }
        public UserTalent(int id, int level)
        {
            Id = id;
            Level = level;
        }

        /// <summary>从角色数据库读取</summary>
        public UserTalent(BinaryReader reader)
        {
            Id = reader.ReadInt32();
            Level = reader.ReadInt32();
        }

        /// <summary>保存进角色数据库</summary>
        public void Save(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Level);
        }
    }

    /// <summary>
    /// 天赋系统 - Talents.txt 加载器.
    /// 解析失败只跳过该行并记录日志, 不会让整个服务端启动失败.
    /// </summary>
    public static class TalentLoader
    {
        private static MessageQueue MessageQueue => MessageQueue.Instance;

        /// <summary>天赋表文件路径(Envir\Talents.txt)</summary>
        public static string FilePath => Path.Combine(Settings.EnvirPath, "Talents.txt");

        /// <summary>
        /// 加载(或重载)天赋表. 返回解析成功的条目列表.
        /// </summary>
        public static List<TalentInfo> Load()
        {
            var list = new List<TalentInfo>();

            //文件不存在时创建一个带表头注释的空文件, 方服主后续填写
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath,
                    "# 天赋表 - Tab分隔, #开头为注释\r\n" +
                    "# Id  名称  图标  职业  需求等级  满级  层  列  前置  每级属性  特效编号  描述\r\n");
                return list;
            }

            var lines = File.ReadAllLines(FilePath);
            var idSet = new HashSet<int>(); //用于前置引用校验的第一遍收集

            //第一遍: 解析所有行, 收集合法Id(重复Id只保留第一个)
            var rawList = new List<TalentInfo>();
            for (var i = 0; i < lines.Length; i++)
            {
                var talent = ParseLine(lines[i], i + 1);
                if (talent == null) continue;

                if (idSet.Contains(talent.Index))
                {
                    MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentDuplicateId, talent.Index));
                    continue;
                }

                idSet.Add(talent.Index);
                rawList.Add(talent);
            }

            //第二遍: 校验前置引用都存在, 剔除引用了不存在天赋的条目
            foreach (var talent in rawList)
            {
                var valid = true;
                foreach (var pre in talent.PreTalents)
                {
                    if (!idSet.Contains(pre[0]))
                    {
                        MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentMissingPre, talent.Name, pre[0]));
                        valid = false;
                        break;
                    }
                }

                if (valid) list.Add(talent);
            }

            //按 层 -> 列 排序, 保证下发给客户端的顺序即UI显示顺序
            list.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : a.Column.CompareTo(b.Column));

            return list;
        }

        /// <summary>
        /// 解析单行. 空行/注释行/格式错误的行返回 null(错误行只记日志不中断加载).
        /// </summary>
        private static TalentInfo ParseLine(string line, int lineNo)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("#")) return null;

            var parts = line.Split('\t');
            if (parts.Length < 12)
            {
                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentBadLine, lineNo, parts.Length));
                return null;
            }

            var talent = new TalentInfo();

            //1. Id(解析失败视为坏行)
            if (!int.TryParse(parts[0].Trim(), out var id))
            {
                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentBadLine, lineNo, parts.Length));
                return null;
            }
            talent.Index = id;

            //2. 名称 / 3. 图标 / 12. 描述
            talent.Name = parts[1].Trim();
            int.TryParse(parts[2].Trim(), out talent.Icon);
            talent.Description = parts[11].Trim();

            //4. 职业: Taoist/Warrior/Wizard/Assassin/Archer 或 All
            var classStr = parts[3].Trim();
            if (string.Compare(classStr, "All", true) == 0 || classStr == "-")
                talent.Class = TalentInfo.AnyClass;
            else if (Enum.TryParse(classStr, true, out MirClass mirClass))
                talent.Class = (byte)mirClass;
            else
            {
                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentBadLine, lineNo, parts.Length));
                return null;
            }

            //5. 需求等级 / 6. 满级 / 7. 层 / 8. 列 / 11. 特效编号
            int.TryParse(parts[4].Trim(), out talent.RequiredLevel);
            int.TryParse(parts[5].Trim(), out talent.MaxLevel);
            int.TryParse(parts[6].Trim(), out talent.Tier);
            int.TryParse(parts[7].Trim(), out talent.Column);
            int.TryParse(parts[10].Trim(), out talent.SpecialEffect);

            //满级至少为1
            if (talent.MaxLevel < 1) talent.MaxLevel = 1;

            //9. 前置: "1:3/2:1" 或 "-"
            var preStr = parts[8].Trim();
            if (preStr != "-" && preStr.Length > 0)
            {
                foreach (var seg in preStr.Split('/'))
                {
                    var kv = seg.Split(':');
                    if (kv.Length == 2 && int.TryParse(kv[0].Trim(), out var preId) && int.TryParse(kv[1].Trim(), out var preLv))
                        talent.PreTalents.Add(new[] { preId, preLv });
                }
            }

            //10. 每级属性: "HealthRecovery=5,SpellRecovery=5" 或 "-"
            var statStr = parts[9].Trim();
            if (statStr != "-" && statStr.Length > 0)
            {
                foreach (var seg in statStr.Split(','))
                {
                    var kv = seg.Split('=');
                    if (kv.Length == 2 && Enum.TryParse(kv[0].Trim(), true, out Stat stat) && int.TryParse(kv[1].Trim(), out var value))
                        talent.Stats[stat] = value;
                    else
                        MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentBadStat, talent.Name, seg.Trim()));
                }
            }

            return talent;
        }
    }
}
