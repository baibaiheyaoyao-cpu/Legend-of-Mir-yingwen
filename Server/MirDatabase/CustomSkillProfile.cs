// ============================================================
// [AI-Claude 2026-08-29] 自定义技能驱动系统·配置核心
// 思路参考: 水晶端 Custom\CustomMagic\*.ini 的数据驱动做法
//           (键名兼容其[ServerConfig]节 现成57份配置可参考移植)
// 位置: Envir\CustomSkills\<技能名>.ini
// 核心: 借用现有Spell枚举的动画渲染(BaseSpell) → 零客户端改动
// 接线: 一期=解析+注册表(本文件) 二期=玩家施法钩子(见使用手册)
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Server.MirDatabase
{
        public class CustomSkillDef
        {
            public string Name = "";               // 技能名(=文件名)
            public string BaseSpell = "";          // 借用的现有法术(动画/音效/渲染全复用) 如 ThunderBolt
            public string Description = "";        // 描述
            public int MagicId = 0;                // 绑定的现有Spell枚举ID(兼容水晶端CustomMagic: 原版INI加MagicID行即接入, 不搬枚举)

            // ---- 学习条件 ----
            public int MinLevel = 1;
            public string NeedClass = "ALL";       // ALL/Warrior/Wizard/Taoist/Assassin/Archer/Monk 逗号分隔
            public int BookItem = 0;               // 学习书物品ID(0=无书 NPC直接授)

            // ---- 消耗/冷却 ----
            public int MPCost = 0;
            public long Cooldown = 3000;           // 兼容水晶端键名: CoolDownTime

            // ---- 伤害 ----
            public string DamageStat = "MC";       // DC/MC/SC/NONE
            public int DamageRate = 100;           // 倍率%
            public bool HasDamageOverride = false; // INI显式写过DamageStat/DamageRate才允许接管引擎伤害(防原版特效INI误接管导致零伤害)
            public string AttackMode = "SINGLE";   // SINGLE/GROUP/LINE/WIDE (兼容水晶端AttackMode数字: 0=单体 1=群体)
            public int Range = 1;                  // 兼容: AttackNearRange/AttackGroupRange 取大者
            public int DamageDelay = 500;          // 兼容: DamageDelay

            // ---- 状态附加(兼容水晶端 AllowXxx/XxxChance/XxxTime/XxxDamage 键名) ----
            public List<CustomStatusDef> Statuses = new List<CustomStatusDef>();

            // ---- 位移 ----
            public int Push = 0;                   // 推离格数(兼容: RepulsionDistance)
            public bool PullTarget = false;
            public bool PullMobs = false;          // 全屏吸怪(兼容水晶端中文键: 全屏吸怪)
            public bool CanMoveBoss = true;        // 吸怪/位移是否影响BOSS(兼容: CanMoveBoss)

            // ---- 持续效果(兼容水晶端: MagicExpireTime/MagicTickTime, 毫秒) ----
            public long MagicExpireTime = 0;       // >0 = 持续型: 每MagicTickTime一跳直至MagicExpireTime
            public long MagicTickTime = 0;

            // ---- 召唤 ----
            public List<KeyValuePair<string, int>> SummonList = new List<KeyValuePair<string, int>>();

            // ---- 自增益Buff(兼容水晶端Buff键: XxxAdd 系列) ----
            public Dictionary<string, int> BuffAdds = new Dictionary<string, int>();
            public int BuffTime = 0;               // Buff持续秒

            // ---- 客户端表现(兼容水晶端[ClientConfig]/[ClientAttack], 经S.CustomMagicConfigs下发) ----
            public CustomMagicConfig Client;
        }

    public class CustomStatusDef
    {
        public string Type = "";               // Green/Red/Slow/Frozen/Stun/Paralysis/Bleeding/Burn
        public int Chance = 0;                 // 几率%(水晶端为0=不用)
        public int Time = 0;                   // 秒
        public int Damage = 0;                 // 每跳伤害(毒/流血/灼烧用)
    }

            // 注册表: 启动后首次访问时加载 Envir\CustomSkills\ 与 Custom\CustomMagic\ 全部ini
            public static class CustomSkillProfile
            {
                // 双目录: 本服自有配置 + 水晶端原版CustomMagic目录(键名兼容, 拷入即用)
                static readonly string[] Folders = { @".\Envir\CustomSkills", @".\Custom\CustomMagic" };
                // 模板/测试文件不注册(自定义技能.ini为原版主模板, 远程测试为调试件)
                static readonly HashSet<string> SkipFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "自定义技能", "远程测试" };

                static readonly Dictionary<string, CustomSkillDef> ByName = new Dictionary<string, CustomSkillDef>(StringComparer.OrdinalIgnoreCase);
                static readonly Dictionary<int, CustomSkillDef> ById = new Dictionary<int, CustomSkillDef>();
                static bool _loaded;

                public static CustomSkillDef Get(string skillName)
                {
                    LoadAll();
                    CustomSkillDef def;
                    return ByName.TryGetValue(skillName, out def) ? def : null;
                }

                /// <summary>按Spell枚举ID取已绑定的INI配置(施法钩子入口)</summary>
                public static CustomSkillDef Get(int spellId)
                {
                    LoadAll();
                    CustomSkillDef def;
                    return ById.TryGetValue(spellId, out def) ? def : null;
                }

                public static bool Contains(int spellId)
                {
                    return Get(spellId) != null;
                }

                public static ICollection<CustomSkillDef> All()
                {
                    LoadAll();
                    return ByName.Values;
                }

                /// <summary>清空缓存强制重载(面板/GM重载用)</summary>
                public static void Reload()
                {
                    _loaded = false;
                    ByName.Clear();
                    ById.Clear();
                }

                static void LoadAll()
                {
                    if (_loaded) return;
                    _loaded = true;

                    try
                    {
                        foreach (string folder in Folders)
                        {
                            if (!Directory.Exists(folder)) continue;

                            foreach (string file in Directory.GetFiles(folder, "*.ini"))
                            {
                                string name = Path.GetFileNameWithoutExtension(file);
                                if (SkipFiles.Contains(name)) continue;

                                CustomSkillDef def = Parse(file);
                                if (def == null || string.IsNullOrEmpty(def.Name)) continue;

                                ByName[def.Name] = def;
                                if (def.MagicId > 0) ById[def.MagicId] = def;
                            }
                        }
                    }
                    catch { /* 目录异常=空注册表 技能系统静默停用 */ }
                }

                static CustomSkillDef Parse(string path)
                {                    try
                    {
                        CustomSkillDef def = new CustomSkillDef();
                        def.Name = Path.GetFileNameWithoutExtension(path);
                        string section = "";
                        CustomStatusDef curStatus = null;

                        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                        {
                            string line = raw.Trim();
                            if (line.Length == 0 || line.StartsWith(";")) continue;

                            if (line.StartsWith("[") && line.EndsWith("]"))
                            {
                                section = line.Substring(1, line.Length - 2).ToUpper();
                                continue;
                            }

                            int eq = line.IndexOf('=');
                            if (eq <= 0) continue;
                            string key = line.Substring(0, eq).Trim().ToUpper();
                            string val = line.Substring(eq + 1).Trim();

                            // ---- [ClientAttack] 特效段键(六段: Self/Self2/Fly/Explosion/Target/Magic) ----
                            if (section == "CLIENTATTACK" && TryParseClientSegment(def, key, val)) continue;

                            // ---- [ClientConfig] 施法/飞行/爆炸音效(M59-0 → 20000+59*10+0) ----
                            if (section == "CLIENTCONFIG")
                            {
                                switch (key)
                                {
                                    case "MAGICCASTSOUND": EnsureClient(def).CastSoundId = ParseSoundId(val); continue;
                                    case "MAGICFLYSOUND": EnsureClient(def).FlySoundId = ParseSoundId(val); continue;
                                    case "MAGICEXPLOSIONSOUND": EnsureClient(def).ExplosionSoundId = ParseSoundId(val); continue;
                                }
                            }

                            // [AI-Claude] 未知键一律忽略(向前兼容 扩展不破坏旧配置)

                            switch (key)
                            {
                                // ---- 基础 ----
                                case "BASESPELL": def.BaseSpell = val; break;
                                case "MAGICID": def.MagicId = GetInt(val, 0); break;
                                case "DESC":
                                case "DESCRIPTION":
                                case "描述": def.Description = val; break;
                                case "MINLEVEL": def.MinLevel = GetInt(val, 1); break;
                                case "NEEDCLASS": def.NeedClass = val.ToUpper(); break;
                                case "BOOKITEM": def.BookItem = GetInt(val, 0); break;

                                // ---- 消耗/冷却(兼容水晶端 CoolDownTime) ----
                                case "MPCOST": def.MPCost = GetInt(val, 0); break;
                                case "COOLDOWN":
                                case "COOLDOWNTIME": def.Cooldown = GetLong(val, 3000); break;

                                // ---- 伤害(兼容水晶端键) ----
                                case "DAMAGESTAT": def.DamageStat = val.ToUpper(); def.HasDamageOverride = true; break;
                                case "DAMAGERATE": def.DamageRate = GetInt(val, 100); def.HasDamageOverride = true; break;
                                case "ATTACKMODE":                                  // 水晶端数字/我们的单词都收
                                    if (val == "0") def.AttackMode = "SINGLE";
                                    else if (val == "1") def.AttackMode = "GROUP";
                                    else def.AttackMode = val.ToUpper();
                                    break;
                                case "ATTACKNEARRANGE":
                                case "ATTACKGROUPRANGE":
                                    {
                                        int r = GetInt(val, 0);
                                        if (r > def.Range) def.Range = r;
                                    }
                                    break;
                                case "RANGE": def.Range = GetInt(val, 1); break;
                                case "DAMAGEDELAY": def.DamageDelay = GetInt(val, 500); break;

                                // ---- 持续效果(吸魔炎风类旋风/毒云型) ----
                                case "MAGICEXPIRETIME": def.MagicExpireTime = GetLong(val, 0); break;
                                case "MAGICTICKTIME": def.MagicTickTime = GetLong(val, 0); break;

                                // ---- 位移(兼容水晶端 RepulsionDistance) ----
                                case "PUSH":
                                case "REPULSIONDISTANCE": def.Push = GetInt(val, 0); break;
                                case "PULLTARGET": def.PullTarget = val.ToUpper() == "TRUE"; break;
                                case "全屏吸怪": def.PullMobs = val.ToUpper() == "TRUE"; break;
                                case "CANMOVEBOSS": def.CanMoveBoss = val.ToUpper() == "TRUE"; break;

                                // ---- 召唤 ----
                                case "SUMMON": def.SummonList = ParseList(val); break;

                                // ---- Buff(水晶端 XxxAdd 系列 → BuffAdds) ----
                                case "BUFFTIME": def.BuffTime = GetInt(val, 0); break;

                                // ---- 状态附加(水晶端 Allow+Chance+Time+Damage 四件套) ----
                                case "MONSTER":
                                case "MONSTERCOUNT":
                                case "NEEDITEM":
                                case "NEEDITEMCOUNT":
                                case "CUSTOMNEEDITEM":
                                case "MOVESTATETARGET":
                                    break; // 水晶端键 暂不实现 静默接收
                            }

                            // 状态四件套: AllowGreen/GreenChance/GreenTime/GreenDamage ...
                            foreach (string st in new[] { "GREEN", "RED", "SLOW", "FROZEN", "STUN", "PARALYSIS", "BLEEDING", "BURN" })
                            {
                                if (key == "ALLOW" + st)
                                {
                                    if (val.ToUpper() == "TRUE")
                                    {
                                        curStatus = new CustomStatusDef { Type = st };
                                        def.Statuses.Add(curStatus);
                                    }
                                    else curStatus = null;
                                    break;
                                }
                                if (key == st + "CHANCE" || key == st + "CHANCEADD") { if (curStatus != null) curStatus.Chance = GetInt(val, 0); break; }
                                if (key == st + "TIME" || key == st + "TIMEADD") { if (curStatus != null) curStatus.Time = GetInt(val, 0); break; }
                                if (key == st + "DAMAGE") { if (curStatus != null) curStatus.Damage = GetInt(val, 0); break; }
                            }

                            // BuffAdds: 以ADD结尾的数值键(如 MaxDCAdd/HPAdd) 收进字典
                            if (key.EndsWith("ADD") && key.Length > 3)
                            {
                                int v;
                                if (int.TryParse(val, out v) && v != 0)
                                    def.BuffAdds[key] = v;
                            }
                        }

                        // 原版语义: AllowXxx=True 但 Chance=0 视为必发(100%), 避免配置了却永远不触发
                        foreach (CustomStatusDef st in def.Statuses)
                            if (st.Chance <= 0) st.Chance = 100;

                        return (string.IsNullOrEmpty(def.BaseSpell) && def.MagicId <= 0) ? null : def; // 没借用法术且未绑定ID=无效配置
                    }
                    catch
                    {
                        return null;
                    }
                }

                static CustomMagicConfig EnsureClient(CustomSkillDef def)
                {
                    return def.Client ?? (def.Client = new CustomMagicConfig());
                }

                /// <summary>原版音效名 M59-0 → 引擎音效ID 20000+59*10+0; 非法返回0(无声)</summary>
                static int ParseSoundId(string val)
                {
                    string v = (val ?? "").Trim().ToUpper();
                    if (v.Length < 2 || v[0] != 'M') return 0;

                    int dash = v.IndexOf('-');
                    if (dash < 0) return 0;

                    int id, sub;
                    if (!int.TryParse(v.Substring(1, dash - 1), out id)) return 0;
                    if (!int.TryParse(v.Substring(dash + 1), out sub)) return 0;

                    return 20000 + id * 10 + sub;
                }

                /// <summary>
                /// [ClientAttack] 段键解析: SELF_FILE/FLY_STARTINDEX/MAGIC_STARTCOUNT/MAGICEFFECTREPEAT 等.
                /// 命中已知段前缀返回true(键被消费), 未知键返回false交回主switch.
                /// </summary>
                static bool TryParseClientSegment(CustomSkillDef def, string key, string val)
                {
                    string kind = null, field = null;

                    int us = key.IndexOf('_');
                    if (us > 0)
                    {
                        kind = key.Substring(0, us);
                        field = key.Substring(us + 1);
                    }
                    else if (key == "MAGICEFFECTREPEAT")
                    {
                        kind = "MAGIC";
                        field = "REPEAT";
                    }

                    if (kind == null) return false;

                    CustomMagicSegment seg;
                    switch (kind)
                    {
                        case "SELF": seg = GetSegment(EnsureClient(def), "Self"); break;
                        case "SELF2": seg = GetSegment(EnsureClient(def), "Self2"); break;
                        case "FLY": seg = GetSegment(EnsureClient(def), "Fly"); break;
                        case "EXPLOSION": seg = GetSegment(EnsureClient(def), "Explosion"); break;
                        case "TARGET": seg = GetSegment(EnsureClient(def), "Target"); break;
                        case "MAGIC": seg = GetSegment(EnsureClient(def), "Magic"); break;
                        default: return false; // 非特效段前缀, 交回主switch
                    }

                    bool flag = val.ToUpper() == "TRUE";

                    switch (field)
                    {
                        case "FILE": seg.File = (byte)GetInt(val, 0); break;
                        case "STARTINDEX": seg.StartIndex = (short)GetInt(val, 0); break;
                        case "STARTCOUNT": seg.PlayCount = (byte)GetInt(val, 0); break;
                        case "PLAYCOUNT": seg.PlayCount = (byte)GetInt(val, 0); break;
                        case "EMPTYCOUNT": seg.EmptyCount = (byte)GetInt(val, 0); break;
                        case "PLAYTIME": seg.PlayTime = (short)GetInt(val, 500); break;
                        case "DELAY": seg.Delay = GetInt(val, 0); break;
                        case "DIRCOUNT": seg.DirCount = (byte)GetInt(val, 0); break;
                        case "CALCDIR": seg.CalcDir = flag; break;
                        case "DRAWMODE": seg.DrawMode = (byte)GetInt(val, 0); break;
                        case "DRAWORDER": seg.DrawOrder = (byte)GetInt(val, 0); break;
                        case "REPEAT": seg.Repeat = flag; break;
                        case "DRAWBEHIND": break; // 层级渲染暂不区分
                        case "MAPEFFECT": break;
                        case "LOCKDRAW": break;
                        case "ISBUFFCK": break;
                        case "MULTIPLAY": break;
                        default: break; // 未知字段消费掉, 防落主switch误判
                    }
                    return true;
                }

                static CustomMagicSegment GetSegment(CustomMagicConfig cfg, string kind)
                {
                    for (int i = 0; i < cfg.Segments.Count; i++)
                        if (cfg.Segments[i].Kind == kind) return cfg.Segments[i];

                    CustomMagicSegment seg = new CustomMagicSegment { Kind = kind };
                    cfg.Segments.Add(seg);
                    return seg;
                }

                /// <summary>BuffAdds键名 → Stat 枚举(输入为已大写的键, 如 MAXDCADD)</summary>
                public static bool TryMapStat(string buffKey, out Stat stat)
                {
                    switch (buffKey)
                    {
                        case "MINACADD": stat = Stat.MinAC; return true;
                        case "MAXACADD": stat = Stat.MaxAC; return true;
                        case "MINMACADD": stat = Stat.MinMAC; return true;
                        case "MAXMACADD": stat = Stat.MaxMAC; return true;
                        case "MINDCADD": stat = Stat.MinDC; return true;
                        case "MAXDCADD": stat = Stat.MaxDC; return true;
                        case "MINMCADD": stat = Stat.MinMC; return true;
                        case "MAXMCADD": stat = Stat.MaxMC; return true;
                        case "MINSCADD": stat = Stat.MinSC; return true;
                        case "MAXSCADD": stat = Stat.MaxSC; return true;
                        case "HPADD": stat = Stat.HP; return true;
                        case "MPADD": stat = Stat.MP; return true;
                        case "AGILITYADD": stat = Stat.Agility; return true;
                        case "ACCURACYADD": stat = Stat.Accuracy; return true;
                        case "ATTACKSPEEDADD": stat = Stat.AttackSpeed; return true;
                        case "CRITICALRATEADD": stat = Stat.CriticalRate; return true;
                        case "CRITICALDAMAGEADD": stat = Stat.CriticalDamage; return true;
                        case "MAXACRATEADD": stat = Stat.MaxACRatePercent; return true;
                        case "MAXMACRATEADD": stat = Stat.MaxMACRatePercent; return true;
                        case "MAXDCRATEADD": stat = Stat.MaxDCRatePercent; return true;
                        case "MAXMCRATEADD": stat = Stat.MaxMCRatePercent; return true;
                        case "MAXSCRATEADD": stat = Stat.MaxSCRatePercent; return true;
                        case "HPRATEADD": stat = Stat.HPRatePercent; return true;
                        case "MPRATEADD": stat = Stat.MPRatePercent; return true;
                        default: stat = Stat.MinAC; return false;
                    }
                }

        static List<KeyValuePair<string, int>> ParseList(string val)
        {
            List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
            foreach (string part in val.Split(','))
            {
                string[] seg = part.Split(':');
                if (seg.Length == 2)
                    list.Add(new KeyValuePair<string, int>(seg[0].Trim(), GetInt(seg[1], 1)));
            }
            return list;
        }

        static int GetInt(string v, int def)
        {
            int n;
            return int.TryParse(v, out n) ? n : def;
        }

        static long GetLong(string v, long def)
        {
            long n;
            return long.TryParse(v, out n) ? n : def;
        }
    }
}
