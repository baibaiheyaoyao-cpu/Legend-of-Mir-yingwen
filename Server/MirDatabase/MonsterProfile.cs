// ============================================================
// [AI-Claude 2026-08-29] 数据驱动怪物AI系统·配置解析器
// 功能: 读取 Envir\MonsterConfigs\<怪名>.ini 生成行为配方
// 配套: ConfiguredMonster.cs(执行器) + case 224(注册)
// 规则: 未知键自动忽略(向前兼容) 解析失败返回null(怪退化普通近战)
// ============================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Server.MirDatabase
{
    public class MonsterSkillDef
    {
        public string Name = "";
        public string Mode = "SINGLE";          // Single/Halfmoon/Line/Wide/AOE/Push/Pull/Summon
        public bool RangeAttack = false;        // true=发远程封包(带TargetID 客户端特效可用) false=近战封包
        public byte Anim = 0;                   // 近战:0-3四套动作 | 远程:0/1/2三套动作
        public string Stat = "DC";              // DC/MC/SC/NONE(纯控制)
        public int Rate = 100;                  // 伤害倍率%
        public int Range = 1;                   // 作用距离
        public int Chance = 100;                // 触发几率%
        public long Cooldown = 3000;            // 冷却毫秒
        public int MinTargets = 0;              // 范围内≥几人才放
        public int MinDistance = 0;             // 目标≥几格才放
        public int Push = 0;                    // 推离格数
        public string Status = "";              // Stun/Slow/Paralysis/Dazed/Green/Red/Bleeding
        public int StatusChance = 1;            // 1=必中, N=1/N几率
        public int StatusTime = 3;              // 状态持续秒
        public List<KeyValuePair<string, int>> SummonList = new List<KeyValuePair<string, int>>();
    }

    public class MonsterProfile
    {
        public List<MonsterSkillDef> Skills = new List<MonsterSkillDef>();
        public int SummonAtHp = -1;             // 血量≤此值触发召唤(-1=无)
        public List<KeyValuePair<string, int>> PhaseSummons = new List<KeyValuePair<string, int>>();
        public int EnrageAtHp = -1;             // 血量≤此值狂暴(-1=无)
        public int EnragePercent = 100;         // 狂暴后攻速%

        const string Folder = @".\Envir\MonsterConfigs";
        static readonly Dictionary<string, MonsterProfile> Cache = new Dictionary<string, MonsterProfile>(StringComparer.OrdinalIgnoreCase);

        public static MonsterProfile Get(string monsterName)
        {
            MonsterProfile p;
            if (Cache.TryGetValue(monsterName, out p)) return p;

            p = Load(monsterName);
            Cache[monsterName] = p; // null也缓存 无配置的怪不重复读盘
            return p;
        }

        static MonsterProfile Load(string name)
        {
            try
            {
                string path = Path.Combine(Folder, name + ".ini");
                if (!File.Exists(path)) return null;

                MonsterProfile profile = new MonsterProfile();
                MonsterSkillDef skill = null;
                string section = "";

                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue;

                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2).ToUpper();
                        if (section.StartsWith("SKILL"))
                        {
                            skill = new MonsterSkillDef();
                            profile.Skills.Add(skill);
                        }
                        else skill = null;
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToUpper();
                    string val = line.Substring(eq + 1).Trim();

                    if (section == "GENERAL")
                    {
                        switch (key)
                        {
                            case "SUMMONAT": profile.SummonAtHp = GetInt(val, -1); break;
                            case "SUMMONLIST": profile.PhaseSummons = ParseList(val); break;
                            case "ENRAGEAT": profile.EnrageAtHp = GetInt(val, -1); break;
                            case "ENRAGEPERCENT": profile.EnragePercent = GetInt(val, 100); break;
                            // [AI-Claude] 未知键忽略 以后扩展新键不破坏旧配置
                        }
                    }
                    else if (skill != null)
                    {
                        switch (key)
                        {
                            case "NAME": skill.Name = val; break;
                            case "MODE": skill.Mode = val.ToUpper(); break;
                            case "ANIM": ParseAnim(val, skill); break; // [AI-Claude] 支持Range1/2/3与数字0-3
                            case "STAT": skill.Stat = val.ToUpper(); break;
                            case "RATE": skill.Rate = GetInt(val, 100); break;
                            case "RANGE": skill.Range = GetInt(val, 1); break;
                            case "CHANCE": skill.Chance = GetInt(val, 100); break;
                            case "COOLDOWN": skill.Cooldown = GetLong(val, 3000); break;
                            case "MINTARGETS": skill.MinTargets = GetInt(val, 0); break;
                            case "MINDISTANCE": skill.MinDistance = GetInt(val, 0); break;
                            case "PUSH": skill.Push = GetInt(val, 0); break;
                            case "STATUS": skill.Status = val.ToUpper(); break;
                            case "STATUSCHANCE": skill.StatusChance = Math.Max(1, GetInt(val, 1)); break;
                            case "STATUSTIME": skill.StatusTime = GetInt(val, 3); break;
                            case "SUMMON": skill.SummonList = ParseList(val); break;
                            // [AI-Claude] 未知键忽略 以后扩展新键不破坏旧配置
                        }
                    }
                }
                return profile;
            }
            catch
            {
                return null;
            }
        }

        // Anim值解析: Range/Range1=远程动作1, Range2=远程动作2, Range3=远程动作3
        // 数字0-3=近战动作(不带TargetID 纯演出用)
        // [AI-Claude 2026-08-29] 远程封包带TargetID 客户端才知道特效挂谁身上
        static void ParseAnim(string val, MonsterSkillDef skill)
        {
            string v = val.ToUpper();
            switch (v)
            {
                case "RANGE":
                case "RANGE1": skill.RangeAttack = true; skill.Anim = 0; break;
                case "RANGE2": skill.RangeAttack = true; skill.Anim = 1; break;
                case "RANGE3": skill.RangeAttack = true; skill.Anim = 2; break;
                default: skill.RangeAttack = false; skill.Anim = (byte)Math.Min(3, GetInt(val, 0)); break;
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
