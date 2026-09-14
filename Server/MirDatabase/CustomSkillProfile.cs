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
        public string AttackMode = "SINGLE";   // SINGLE/GROUP/LINE/WIDE (兼容水晶端AttackMode数字: 0=单体 1=群体)
        public int Range = 1;                  // 兼容: AttackNearRange/AttackGroupRange 取大者
        public int DamageDelay = 500;          // 兼容: DamageDelay

        // ---- 状态附加(兼容水晶端 AllowXxx/XxxChance/XxxTime/XxxDamage 键名) ----
        public List<CustomStatusDef> Statuses = new List<CustomStatusDef>();

        // ---- 位移 ----
        public int Push = 0;                   // 推离格数(兼容: RepulsionDistance)
        public bool PullTarget = false;

        // ---- 召唤 ----
        public List<KeyValuePair<string, int>> SummonList = new List<KeyValuePair<string, int>>();

        // ---- 自增益Buff(兼容水晶端Buff键: XxxAdd 系列) ----
        public Dictionary<string, int> BuffAdds = new Dictionary<string, int>();
        public int BuffTime = 0;               // Buff持续秒
    }

    public class CustomStatusDef
    {
        public string Type = "";               // Green/Red/Slow/Frozen/Stun/Paralysis/Bleeding/Burn
        public int Chance = 0;                 // 几率%(水晶端为0=不用)
        public int Time = 0;                   // 秒
        public int Damage = 0;                 // 每跳伤害(毒/流血/灼烧用)
    }

    // 注册表: 启动后首次访问时加载 Envir\CustomSkills\ 全部ini
    public static class CustomSkillProfile
    {
        const string Folder = @".\Envir\CustomSkills";

        static readonly Dictionary<string, CustomSkillDef> ByName = new Dictionary<string, CustomSkillDef>(StringComparer.OrdinalIgnoreCase);
        static bool _loaded;

        public static CustomSkillDef Get(string skillName)
        {
            LoadAll();
            CustomSkillDef def;
            return ByName.TryGetValue(skillName, out def) ? def : null;
        }

        public static ICollection<CustomSkillDef> All()
        {
            LoadAll();
            return ByName.Values;
        }

        static void LoadAll()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                if (!Directory.Exists(Folder)) return;

                foreach (string file in Directory.GetFiles(Folder, "*.ini"))
                {
                    CustomSkillDef def = Parse(file);
                    if (def != null && !string.IsNullOrEmpty(def.Name))
                        ByName[def.Name] = def;
                }
            }
            catch { /* 目录异常=空注册表 技能系统静默停用 */ }
        }

        static CustomSkillDef Parse(string path)
        {
            try
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

                    // [AI-Claude] 未知键一律忽略(向前兼容 扩展不破坏旧配置)

                    switch (key)
                    {
                        // ---- 基础 ----
                        case "BASESPELL": def.BaseSpell = val; break;
                        case "DESC":
                        case "DESCRIPTION": def.Description = val; break;
                        case "MINLEVEL": def.MinLevel = GetInt(val, 1); break;
                        case "NEEDCLASS": def.NeedClass = val.ToUpper(); break;
                        case "BOOKITEM": def.BookItem = GetInt(val, 0); break;

                        // ---- 消耗/冷却(兼容水晶端 CoolDownTime) ----
                        case "MPCOST": def.MPCost = GetInt(val, 0); break;
                        case "COOLDOWN":
                        case "COOLDOWNTIME": def.Cooldown = GetLong(val, 3000); break;

                        // ---- 伤害(兼容水晶端键) ----
                        case "DAMAGESTAT": def.DamageStat = val.ToUpper(); break;
                        case "DAMAGERATE": def.DamageRate = GetInt(val, 100); break;
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

                        // ---- 位移(兼容水晶端 RepulsionDistance) ----
                        case "PUSH":
                        case "REPULSIONDISTANCE": def.Push = GetInt(val, 0); break;
                        case "PULLTARGET": def.PullTarget = val.ToUpper() == "TRUE"; break;

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

                return string.IsNullOrEmpty(def.BaseSpell) ? null : def; // 没借用法术=无效配置
            }
            catch
            {
                return null;
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
