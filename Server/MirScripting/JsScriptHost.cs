using System;
using System.Collections.Generic;
using System.IO;
using Jint;
using Jint.Runtime;
using Server.MirObjects;
using S = ServerPackets;

namespace Server.MirScripting
{
    /// <summary>单份js脚本的缓存项: 引擎实例 + 函数名映射(大写 → 实际声明名), 按代际失效</summary>
    internal class JsCachedScript
    {
        /// <summary>所属绑定键(NPC路径或物品名, 日志定位用)</summary>
        public string Key = "";
        /// <summary>js文件绝对路径</summary>
        public string Path = "";
        /// <summary>创建该缓存时的注册表代际; 与当前代际不符 → 下次访问重建(热重载机制)</summary>
        public int Generation;
        /// <summary>引擎实例(编译失败时为null)</summary>
        public Engine Engine;
        /// <summary>全局函数名映射: 大写名 → 实际声明名(脚本里函数可能小写声明如 @exit, 统一大写匹配)</summary>
        public Dictionary<string, string> FuncMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>编译失败原因(Engine为null时非空)</summary>
        public string LoadError = "";
    }

    /// <summary>脚本错误记录条目(面板"错误日志"数据源)</summary>
    public class JsErrorEntry
    {
        public DateTime Time;
        public string Script = "";
        public string Message = "";
    }

    /// <summary>
    /// JS引擎宿主(静态全局). NPC对话与物品使用事件在此分发给js脚本.
    /// 线程模型: 所有引擎的创建与调用只发生在Envir主线程(NPC/物品消息处理线程);
    ///           UI面板线程仅触发 JsModuleRegistry.ReloadAll()(纯绑定表替换), 引擎按代际惰性重建, 天然无竞态.
    /// </summary>
    public static class JsScriptHost
    {
        /// <summary>脚本缓存: 绝对路径(小写) → 缓存项</summary>
        private static readonly Dictionary<string, JsCachedScript> _cache = new Dictionary<string, JsCachedScript>(StringComparer.OrdinalIgnoreCase);

        /// <summary>玩家 → 最近一次OpenNPC页面上的合法按钮集合(防伪造点击, 与经典引擎按钮校验同强度)</summary>
        private static readonly Dictionary<int, HashSet<string>> _pageButtons = new Dictionary<int, HashSet<string>>();

        /// <summary>错误环形缓冲(上限300条, 面板读取)</summary>
        private static readonly Queue<JsErrorEntry> _errors = new Queue<JsErrorEntry>();
        private static readonly object _errorLock = new object();
        private const int MaxErrors = 300;

        // ==================================================================================
        // 对外入口(PlayerObject 钩子调用)
        // ==================================================================================

        /// <summary>
        /// NPC对话分发入口(在 PlayerObject.CallNPC 中被调用, 替代经典@@脚本).
        /// rawKey 形如 "[@MAIN]"(初次点击) 或 "@TALK"(按钮), 已被上层转大写.
        /// </summary>
        public static void CallNpcFunction(JsBinding binding, PlayerObject player, NPCObject npc, string rawKey)
        {
            // 1. 规范化函数名: 去掉 [@...]/@ 前后缀 → 大写函数名
            string funcUpper = NormalizeKey(rawKey);

            // 2. EXIT 是通用关闭按钮: 不进脚本, 直接清空对话(客户端收到空页即关闭窗口)
            if (funcUpper == "EXIT")
            {
                _pageButtons.Remove(player.Info.Index);
                player.Enqueue(new S.NPCResponse { Page = new List<string>() });
                return;
            }

            // 3. 取脚本(按代际惰性重建; 编译失败则给玩家兜底提示)
            JsCachedScript script = GetScript(binding);
            if (script == null || script.Engine == null)
            {
                player.Enqueue(new S.NPCResponse { Page = new List<string> { "该NPC脚本加载失败, 请联系管理员.", script?.LoadError ?? "" } });
                return;
            }

            // 4. 防伪造点击: 非MAIN的函数必须出现在最近一次OpenNPC页面的按钮里(经典引擎同强度校验)
            if (funcUpper != "MAIN" && !IsAllowedButton(player, funcUpper))
            {
                RecordError(script.Path, $"拦截非法调用 {rawKey} (玩家 {player.Name}, NPC {npc?.Name})", "");
                return;
            }

            // 5. 查实际函数名(脚本内声明可能是小写, 如 function exit())
            if (!script.FuncMap.TryGetValue(funcUpper, out string actualName))
            {
                RecordError(script.Path, $"函数未定义: {funcUpper} (玩家 {player.Name})", "");
                return;
            }

            // 6. 设置调用上下文并执行(与经典引擎一致: 记录NPC对话归属, 供后续包校验)
            if (npc != null) player.NPCObjectID = npc.ObjectID;

            JsApi.Player = player;
            JsApi.Npc = npc;
            try
            {
                script.Engine.Invoke(actualName);
            }
            catch (Exception ex)
            {
                // 脚本异常不崩服务端: 记日志 + 给玩家兜底对话
                RecordError(script.Path, $"执行 {actualName} 异常 (玩家 {player.Name}): {ex.Message}", ex.ToString());
                player.Enqueue(new S.NPCResponse { Page = new List<string> { "脚本执行出错, 已记录日志.", "" } });
            }
            finally
            {
                JsApi.Player = null;
                JsApi.Npc = null;
            }
        }

        /// <summary>
        /// 物品使用分发入口(在 PlayerObject.UseItem 顶部被调用).
        /// 返回true = 脚本已处理且该物品应被消耗1个; false = 不处理(走经典UseItem)或不消耗.
        /// </summary>
        public static bool CallItemUse(JsBinding binding, PlayerObject player, UserItem item) // UserItem定义于Shared(全局命名空间)
        {
            JsCachedScript script = GetScript(binding);
            if (script == null || script.Engine == null) return false;

            if (!script.FuncMap.TryGetValue("ITEMUSE", out string actualName))
            {
                RecordError(script.Path, $"物品脚本缺少 ITEMUSE 入口: {binding.Key}", "");
                return false;
            }

            JsApi.Player = player;
            JsApi.Npc = null;
            try
            {
                // 返回值为boolean true → 消耗物品; 其余(false/无返回) → 不消耗
                var rv = script.Engine.Invoke(actualName);
                return rv.IsBoolean() && rv.AsBoolean();
            }
            catch (Exception ex)
            {
                RecordError(script.Path, $"物品脚本 {actualName} 异常 (玩家 {player.Name}, 物品 {item.Info.FriendlyName}): {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                JsApi.Player = null;
                JsApi.Npc = null;
            }
        }

        // ==================================================================================
        // 供 JsApi.OpenNpc 回调登记按钮 / 错误记录
        // ==================================================================================

        /// <summary>登记玩家当前对话页的合法按钮集合(JsApi.OpenNpc 每次发包时调用)</summary>
        internal static void SetPageButtons(PlayerObject player, HashSet<string> buttons)
        {
            // 简单防膨胀: 极端在线量时清表重建(正常情况玩家数即条目数, 无需担心)
            if (_pageButtons.Count > 10000) _pageButtons.Clear();
            _pageButtons[player.Info.Index] = buttons;
        }

        /// <summary>记录脚本错误: 入环形缓冲 + SMain消息队列(控制台立即可见)</summary>
        public static void RecordError(string scriptPath, string message, string detail)
        {
            lock (_errorLock)
            {
                _errors.Enqueue(new JsErrorEntry { Time = DateTime.Now, Script = Path.GetFileName(scriptPath), Message = message });
                while (_errors.Count > MaxErrors) _errors.Dequeue();
            }

            // SMain控制台同步输出(带模块前缀便于过滤); detail(完整异常栈)仅调试时手动查
            try { Server.MessageQueue.Instance.Enqueue($"[JS模块] {Path.GetFileName(scriptPath)}: {message}"); } catch { }
        }

        /// <summary>错误快照(面板定时刷新用; 返回最新若干条, 新的在后)</summary>
        public static List<JsErrorEntry> ErrorsSnapshot(int maxCount = 100)
        {
            lock (_errorLock)
            {
                var list = new List<JsErrorEntry>(_errors);
                if (list.Count > maxCount) list.RemoveRange(0, list.Count - maxCount);
                return list;
            }
        }

        /// <summary>清空错误日志(面板按钮)</summary>
        public static void ClearErrors()
        {
            lock (_errorLock) _errors.Clear();
        }

        /// <summary>热重载全部模块(面板"全部重载"与GM命令 @ReloadJS 共用): 只换绑定表+抬代际, 引擎下次访问时惰性重建</summary>
        public static void ReloadAllModules()
        {
            JsModuleRegistry.ReloadAll();
            // [登仙后期系统] 自定义Buff表(CustomBuffList.txt)随JS模块一并热重载
            Server.MirDatabase.CustomBuffListProfile.Reload();
            // 缓存不主动清空: 代际不匹配的条目会在下次访问时被重建, 避免与主线程竞态
        }

        // ==================================================================================
        // 脚本缓存(仅Envir主线程访问)
        // ==================================================================================

        private static bool IsAllowedButton(PlayerObject player, string funcUpper)
        {
            return _pageButtons.TryGetValue(player.Info.Index, out HashSet<string> buttons)
                   && buttons.Contains(funcUpper);
        }

        /// <summary>取脚本缓存; 代际变化或首次访问时(重新)编译. 编译失败缓存失败结果避免反复读盘.</summary>
        private static JsCachedScript GetScript(JsBinding binding)
        {
            if (binding?.ScriptPath == null) return null;

            if (_cache.TryGetValue(binding.ScriptPath, out JsCachedScript cached)
                && cached.Generation == JsModuleRegistry.Generation)
                return cached;

            cached = Compile(binding);
            _cache[binding.ScriptPath] = cached;
            return cached;
        }

        /// <summary>编译单份脚本: 建沙箱引擎 → 注册宿主API → 执行脚本文本 → 收集全局函数名</summary>
        private static JsCachedScript Compile(JsBinding binding)
        {
            var cached = new JsCachedScript
            {
                Key = binding.Key,
                Path = binding.ScriptPath,
                Generation = JsModuleRegistry.Generation,
            };

            try
            {
                string source = File.ReadAllText(binding.ScriptPath);

                // 沙箱配置: 限制递归/语句数/执行时长, 防脚本死循环卡死Envir主循环; 不开放任何CLR互操作
                var engine = new Engine(options => options
                    .LimitRecursion(64)
                    .MaxStatements(200000)
                    .TimeoutInterval(TimeSpan.FromSeconds(2)));

                JsApi.Register(engine);
                engine.Execute(source);

                // 收集脚本声明的全局函数名(大写→实际名):
                // 用 join 拼串再拆分, 规避跨版本 JsValue 数组遍历API差异
                string names = engine.Evaluate("Object.getOwnPropertyNames(globalThis).join('\\n')").AsString();
                foreach (string name in names.Split('\n'))
                {
                    string n = name.Trim();
                    if (n.Length == 0) continue;
                    // 排除宿关注册的API名与全局对象(它们不是脚本函数, 但收录进来也无害 —— 按钮只会调脚本函数)
                    cached.FuncMap[n.ToUpper()] = n;
                }

                cached.Engine = engine;
            }
            catch (Exception ex)
            {
                cached.Engine = null;
                cached.LoadError = ex.Message;
                RecordError(binding.ScriptPath, $"编译失败: {ex.Message}", ex.ToString());
            }

            return cached;
        }

        /// <summary>对话键规范化: "[@MAIN]"→"MAIN", "@TALK"→"TALK", "TALK"→"TALK";
        /// 彩色按钮回发的 "@函数/颜色"(如 @SHENGDUAN/cornflowerblue, 且已被上层转大写)在此剥离颜色后缀 → "SHENGDUAN"</summary>
        private static string NormalizeKey(string rawKey)
        {
            string k = (rawKey ?? "").Trim();
            if (k.StartsWith("[")) k = k.Trim('[', ']');
            if (k.StartsWith("@")) k = k.Substring(1);
            k = k.Split('/')[0]; // [彩色按钮修复 2026-09-25] 剥离 /颜色 后缀(函数名从不含/)
            return k.ToUpper();
        }
    }
}
