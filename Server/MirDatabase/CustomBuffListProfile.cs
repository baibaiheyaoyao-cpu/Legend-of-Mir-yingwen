using System;
using System.Collections.Generic;
using System.IO;

namespace Server.MirDatabase
{
    /// <summary>一条自定义Buff定义(来自 Custom\CustomBuffList.txt 的一行, 作者水晶端原版格式)</summary>
    public class CustomBuffDef
    {
        /// <summary>Buff名(如 战羽化登仙-5) —— JS脚本 GIVEBUFF/HASBUFF/REMOVEBUFF 按此名匹配</summary>
        public string Name = "";
        /// <summary>表内Id(列1): 战=1~30 法=31~60 道=61~90 刺=91~120 弓=121~150 僧=151~180, 其余为杂项Buff</summary>
        public int Id;
        /// <summary>描述(列3, 如 lv：5)</summary>
        public string Description = "";
        /// <summary>该Buff生效的属性加成(只含非零项)</summary>
        public Stats Stats = new Stats();
    }

    /// <summary>
    /// [登仙后期系统 2026-09-25] 自定义Buff表加载器.
    /// 数据源: Custom\CustomBuffList.txt —— 作者水晶端原版58列CSV(战/法/道/刺/弓/僧羽化登仙各30阶 + 杂项Buff, 共214行).
    /// 引擎侧只按需取其中登仙六系(按名前缀), 杂项行加载但不影响现有系统.
    ///
    /// 列映射(经作者数据交叉验证: 战-1(220,110) 法-1(140,210) 道-1(170,180) 职业取向吻合):
    ///   列35 → Stat.MaxDC   (最大攻击加成: 战220→510/阶+10, 法140→285, 道170→…)
    ///   列36 → Stat.MaxMC   (最大魔法加成: 法210→500/阶+10, 战110→255, 道180→…)
    ///   列51 → Stat.Accuracy(命中加成: 80→138/阶+2)
    ///   列54 → Stat.HealthRecovery(体力恢复: 10→24)
    ///   列55 → Stat.SpellRecovery (法力恢复: 10→24)
    /// 未映射(语义待定, 需要时在 BuildStats 补): 列4~9为16阶后启用的职业分化加成, 列20~27的-1系.
    /// </summary>
    public static class CustomBuffListProfile
    {
        /// <summary>表文件路径(Custom\CustomBuffList.txt)</summary>
        public static string FilePath => Path.Combine(Server.Settings.EnvirPath, "..", "Custom", "CustomBuffList.txt");

        private static readonly Dictionary<string, CustomBuffDef> ByName = new Dictionary<string, CustomBuffDef>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;
        private static readonly object _loadLock = new object();

        /// <summary>按名查找(如 "战羽化登仙-5"); 找不到返回null</summary>
        public static CustomBuffDef Find(string name)
        {
            EnsureLoaded();
            return ByName.TryGetValue(name ?? "", out CustomBuffDef def) ? def : null;
        }

        /// <summary>全部定义(面板/调试用)</summary>
        public static ICollection<CustomBuffDef> All()
        {
            EnsureLoaded();
            return ByName.Values;
        }

        /// <summary>热重载(JS模块面板"全部重载"/@ReloadJS 时由 JsScriptHost 调用)</summary>
        public static void Reload()
        {
            lock (_loadLock)
            {
                _loaded = false;
                ByName.Clear();
                LoadAll();
                _loaded = true;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_loadLock)
            {
                if (_loaded) return;
                LoadAll();
                _loaded = true;
            }
        }

        private static void LoadAll()
        {
            try
            {
                if (!File.Exists(FilePath)) return;

                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;

                    string[] cols = line.Split(',');
                    if (cols.Length < 56) continue; // 完整登仙行为58列, 不足的杂项行跳过

                    CustomBuffDef def = new CustomBuffDef
                    {
                        Name = cols[0].Trim(),
                        Description = cols.Length > 3 ? cols[3].Trim() : "",
                    };

                    if (!int.TryParse(cols[1], out def.Id)) continue;
                    if (string.IsNullOrEmpty(def.Name)) continue;

                    def.Stats = BuildStats(cols);
                    ByName[def.Name] = def;
                }
            }
            catch
            {
                // 表异常=空注册表, 登仙Buff系统静默停用(与CustomSkillProfile同策略)
            }
        }

        /// <summary>列→Stat 映射(数值为0的项自动不入Stats). 列含义见类头注释, 调整只改这里.</summary>
        private static Stats BuildStats(string[] cols)
        {
            Stats stats = new Stats();

            int v;
            if (int.TryParse(cols[35], out v) && v != 0) stats[Stat.MaxDC] = v;
            if (int.TryParse(cols[36], out v) && v != 0) stats[Stat.MaxMC] = v;
            if (int.TryParse(cols[51], out v) && v != 0) stats[Stat.Accuracy] = v;
            if (int.TryParse(cols[54], out v) && v != 0) stats[Stat.HealthRecovery] = v;
            if (int.TryParse(cols[55], out v) && v != 0) stats[Stat.SpellRecovery] = v;

            return stats;
        }
    }
}
