using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Server.MirScripting
{
    /// <summary>
    /// 一个JS脚本模块的运行时信息.
    /// 模块 = Envir\JsScripts\ 下一个子目录, 内含 manifest.json(中文键值 UTF-8) + NPCs\*.js + Items\*.js.
    /// 设计目标: "单独一块好管理" —— 整个目录拷入即启用, 删除/停用即整体下线, 不碰引擎代码.
    /// </summary>
    public class JsModule
    {
        /// <summary>模块目录名(英文标识, 即文件夹名)</summary>
        public string Name = "";
        /// <summary>显示名(中文, 来自 manifest.displayName)</summary>
        public string DisplayName = "";
        /// <summary>模块说明(来自 manifest.description)</summary>
        public string Description = "";
        /// <summary>是否启用(manifest.enabled, 面板可切换并写回)</summary>
        public bool Enabled = true;
        /// <summary>本次加载时间</summary>
        public DateTime LastLoad;
        /// <summary>模块根目录绝对路径</summary>
        public string RootPath = "";
        /// <summary>加载该模块时遇到的错误(缺manifest/JSON解析失败等, 不抛异常只记录)</summary>
        public readonly List<string> LoadErrors = new List<string>();

        /// <summary>NPC绑定: NPC脚本相对路径(含子目录, 无扩展名, 与NPCInfo.FileName一致) → js文件相对路径</summary>
        public Dictionary<string, string> NpcBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>物品绑定: 物品名(item.Info.FriendlyName) → js文件相对路径</summary>
        public Dictionary<string, string> ItemBindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>解析后的js文件绝对路径(仅保留真实存在的文件)</summary>
        public Dictionary<string, string> NpcFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ItemFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>NPC/物品 → 具体js脚本的绑定查询结果(只读快照, 可跨线程传递)</summary>
    public class JsBinding
    {
        /// <summary>所属模块</summary>
        public JsModule Module;
        /// <summary>js文件绝对路径</summary>
        public string ScriptPath = "";
        /// <summary>NPC绑定用的相对键(NPC对话防作弊校验/日志用)</summary>
        public string Key = "";
    }

    /// <summary>manifest.json 的反序列化模型(字段与文件内键一一对应)</summary>
    public class JsManifest
    {
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        [JsonPropertyName("npcs")] public Dictionary<string, string> Npcs { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        [JsonPropertyName("items")] public Dictionary<string, string> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// JS脚本模块注册表(静态全局).
    /// 职责: 扫描 Envir\JsScripts\ 全部模块目录, 建立 NPC/物品 → js脚本 的绑定表.
    /// 线程模型: LoadAll() 在任意线程执行(读磁盘+建表), 绑定表整体原子替换(Interlocked.Exchange);
    ///           Envir主线程只读引用, 无锁. 引擎实例由 JsScriptHost 按 Generation 惰性重建, 保证热重载安全.
    /// </summary>
    public static class JsModuleRegistry
    {
        /// <summary>模块根目录(Envir\JsScripts)</summary>
        public static string Root => Path.Combine(Server.Settings.EnvirPath, "JsScripts");

        /// <summary>绑定表代际: 每次 LoadAll 递增, JsScriptHost 据此判断脚本缓存是否需要重建</summary>
        public static int Generation => _generation;

        private static int _generation;
        private static Dictionary<string, JsModule> _modules = new Dictionary<string, JsModule>(StringComparer.OrdinalIgnoreCase); // 按模块名索引
        private static Dictionary<string, JsBinding> _npcBindings = new Dictionary<string, JsBinding>(StringComparer.OrdinalIgnoreCase); // NPC路径 → 绑定
        private static Dictionary<string, JsBinding> _itemBindings = new Dictionary<string, JsBinding>(StringComparer.OrdinalIgnoreCase); // 物品名 → 绑定
        private static bool _loaded; // 惰性加载标记(首次查询时自动扫描)
        private static readonly object _loadLock = new object();

        /// <summary>模块快照(面板用; 返回新建列表避免外部遍历时被原子替换影响)</summary>
        public static List<JsModule> Modules
        {
            get { EnsureLoaded(); lock (_loadLock) { return new List<JsModule>(_modules.Values); } }
        }

        /// <summary>按NPC脚本相对路径(如 道观\白日门\天尊)查询JS绑定; 未接管返回null(走经典@@脚本)</summary>
        public static JsBinding FindNpc(string npcFileName)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(npcFileName)) return null;
            _npcBindings.TryGetValue(NormalizeNpcKey(npcFileName), out JsBinding b);
            return b;
        }

        /// <summary>按物品显示名查询JS绑定(物品使用时触发); 未接管返回null(走经典UseItem)</summary>
        public static JsBinding FindItem(string itemFriendlyName)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(itemFriendlyName)) return null;
            _itemBindings.TryGetValue(itemFriendlyName, out JsBinding b);
            return b;
        }

        /// <summary>首次访问时惰性扫描; 之后直接返回(重载由 ReloadAll 显式触发)</summary>
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

        /// <summary>
        /// 全量重载: 重新扫描磁盘并原子替换绑定表. 可在任意线程调用(UI面板/GM命令/@reloadjs 均走这里).
        /// 引擎实例不在此处销毁 —— JsScriptHost 发现 Generation 变化后会在主线程惰性重建.
        /// </summary>
        public static void ReloadAll()
        {
            lock (_loadLock)
            {
                LoadAll();
            }
        }

        /// <summary>实际扫描逻辑(须在 _loadLock 内调用)</summary>
        private static void LoadAll()
        {
            var newModules = new Dictionary<string, JsModule>(StringComparer.OrdinalIgnoreCase);
            var newNpc = new Dictionary<string, JsBinding>(StringComparer.OrdinalIgnoreCase);
            var newItem = new Dictionary<string, JsBinding>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(Root))
            {
                foreach (string dir in Directory.GetDirectories(Root))
                {
                    JsModule module = LoadModule(dir);
                    if (module == null) continue;

                    newModules[module.Name] = module;

                    // 只把"文件真实存在"的绑定加入查询表, 缺文件的记入模块错误便于面板排查
                    foreach (KeyValuePair<string, string> kv in module.NpcBindings)
                    {
                        string abs = Path.Combine(module.RootPath, kv.Value);
                        if (!File.Exists(abs))
                        {
                            module.LoadErrors.Add($"NPC绑定 {kv.Key} → {kv.Value} 文件不存在");
                            continue;
                        }
                        module.NpcFiles[kv.Key] = abs;
                        newNpc[NormalizeNpcKey(kv.Key)] = new JsBinding { Module = module, ScriptPath = abs, Key = kv.Key };
                    }

                    foreach (KeyValuePair<string, string> kv in module.ItemBindings)
                    {
                        string abs = Path.Combine(module.RootPath, kv.Value);
                        if (!File.Exists(abs))
                        {
                            module.LoadErrors.Add($"物品绑定 {kv.Key} → {kv.Value} 文件不存在");
                            continue;
                        }
                        module.ItemFiles[kv.Key] = abs;
                        newItem[kv.Key] = new JsBinding { Module = module, ScriptPath = abs, Key = kv.Key };
                    }
                }
            }

            // 原子替换: 主线程读者的引用瞬间切到新表, 不存在半更新状态
            _modules = newModules;
            _npcBindings = newNpc;
            _itemBindings = newItem;
            Interlocked.Increment(ref _generation);
        }

        /// <summary>加载单个模块目录; 目录无 manifest.json 时返回null(子目录不构成模块)</summary>
        private static JsModule LoadModule(string dir)
        {
            string manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) return null;

            JsModule module = new JsModule
            {
                Name = Path.GetFileName(dir),
                RootPath = dir,
                LastLoad = DateTime.Now,
            };

            try
            {
                // 中文键名/描述 → 读入用默认UTF-8即可; 忽略大小写与注释容错
                JsManifest m = JsonSerializer.Deserialize<JsManifest>(File.ReadAllText(manifestPath),
                    new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

                if (m == null) { module.LoadErrors.Add("manifest.json 解析结果为空"); return module; }

                module.DisplayName = string.IsNullOrEmpty(m.DisplayName) ? module.Name : m.DisplayName;
                module.Description = m.Description ?? "";
                module.Enabled = m.Enabled;
                module.NpcBindings = m.Npcs ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                module.ItemBindings = m.Items ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                module.LoadErrors.Add("manifest.json 解析失败: " + ex.Message);
            }

            return module;
        }

        /// <summary>
        /// 启用/停用模块并持久化到 manifest.json, 然后热重载.
        /// 面板专用; 停用后该模块的全部NPC/物品绑定即时失效(回退经典@@脚本).
        /// </summary>
        public static bool SetEnabled(string moduleName, bool enabled)
        {
            lock (_loadLock)
            {
                if (!_modules.TryGetValue(moduleName, out JsModule module)) return false;

                string manifestPath = Path.Combine(module.RootPath, "manifest.json");
                if (!File.Exists(manifestPath)) return false;

                try
                {
                    // 读原文件 → 改 enabled 字段 → 缩进写回(中文不转义, 保持可读性)
                    JsManifest m = JsonSerializer.Deserialize<JsManifest>(File.ReadAllText(manifestPath),
                        new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    if (m == null) return false;

                    m.Enabled = enabled;

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 中文原样输出
                    };
                    File.WriteAllText(manifestPath, JsonSerializer.Serialize(m, options));
                }
                catch
                {
                    return false; // 写回失败不改变内存状态
                }

                LoadAll(); // 成功写回后才热重载
                return true;
            }
        }

        /// <summary>NPC路径键规范化: 正反斜杠统一为'\', 去首尾空白与首尾分隔符(与NPCInfo.FileName格式对齐)</summary>
        internal static string NormalizeNpcKey(string path)
        {
            string k = (path ?? "").Trim().Replace('/', '\\').Trim('\\');
            return k;
        }
    }
}
