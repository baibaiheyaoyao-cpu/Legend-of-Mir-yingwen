using System;
using System.Drawing;
using System.Text.RegularExpressions;
using Jint;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;
using S = ServerPackets;

namespace Server.MirScripting
{
    /// <summary>
    /// JS脚本可调用的宿主API(与作者水晶端的 Lua/JS API 同名同语义, 脚本可近乎原样移植).
    /// 线程模型: 所有API只允许在Envir主线程内被调用(由 JsScriptHost 在 Invoke 前后设置调用上下文),
    ///          因此上下文用静态字段即可, 无需加锁.
    /// </summary>
    internal static class JsApi
    {
        /// <summary>当前调用上下文: 正在与NPC对话/使用物品的玩家(Invoke 期间有效)</summary>
        internal static PlayerObject Player;
        /// <summary>当前对话的NPC(物品脚本调用时为null)</summary>
        internal static NPCObject Npc;

        private static Envir Envir => Envir.Main;

        /// <summary>对话按钮提取正则: 匹配 &lt;文字/@FUNC&gt; 与 &lt;&lt;文字/@FUNC/颜色&gt;&gt; 两种标记, 捕获函数名(支持中文函数名, 如月灵.js的 @检测/@战士学习)</summary>
        private static readonly Regex ButtonRegex = new Regex(@"<+\s*[^<>]*?/@([A-Za-z0-9_\u4e00-\u9fff]+)[^<>]*?>+", RegexOptions.Compiled);

        /// <summary>
        /// 向指定引擎注册全部宿主函数.
        /// Jint 默认沙箱: 不暴露任何CLR类型, 脚本只能调用这里显式注册的函数 —— 天然防逃逸.
        /// </summary>
        internal static void Register(Engine engine)
        {
            // ---- 对话 ----
            engine.SetValue("OpenNPC", (Action<string>)OpenNpc);                       // 打开NPC对话(全文文本, 含<文字/@函数>按钮标记)
            // ---- 物品 ----
            engine.SetValue("CHECKITEM", (Func<string, int, int, int>)CheckItemDura);       // 查背包物品数量(可选第3参: 最低耐久千分值, 0=不检查)
            engine.SetValue("CHECKITEM1", (Func<string, int>)CheckItem);                   // 简写: 只查数量
            engine.SetValue("TAKEITEM", (Func<string, int, int, bool>)TakeItemDura);       // 扣除背包物品(可选第3参: 耐久过滤), 返回是否扣到
            engine.SetValue("TAKEITEM1", (Func<string, int, bool>)TakeItem);               // 简写: 只按数量扣
            engine.SetValue("GIVEITEM", (Action<string, int>)GiveItem);                // 给予物品(按可堆叠拆分)
            engine.SetValue("GIVEQUESTITEM", (Action<string, int>)GiveQuestItem);      // 给予任务物品(与GIVEITEM同实现, 语义占位)
            // ---- 技能 ----
            engine.SetValue("CHECKSKILL", (Func<string, int, bool>)CheckSkill);        // 技能名+等级 → 是否已学且达到等级
            // ---- 旗标(与经典引擎 CHECK/SET 同一存储: player.Info.Flags) ----
            engine.SetValue("CHECK", (Func<int, bool>)CheckFlag);                      // 读旗标
            engine.SetValue("SET", (Action<int, bool>)SetFlag);                        // 写旗标(同步刷新NPC可见性, 与经典SET一致)
            // ---- 任务 ----
            engine.SetValue("CHECKQUEST", (Func<int, string, bool>)CheckQuest);        // 任务状态: "Active"=进行中, 其余=已完成
            // ---- 玩家信息 ----
            engine.SetValue("CHECKGENDER", (Func<string>)CheckGender);                 // → "男性"/"女性"
            engine.SetValue("CHECKCLASS", (Func<string>)CheckClass);                   // → 战士/法师/道士/刺客/弓箭手/武僧
            engine.SetValue("CHECKLEVEL", (Func<double>)CheckLevel);                   // → 玩家当前等级
            engine.SetValue("USERNAME", (Func<string>)UserName);                       // → 玩家角色名
            engine.SetValue("CHECKGOLD", (Func<double>)CheckGold);                     // → 金币余额
            // ---- 传送/金币 ----
            engine.SetValue("MOVE", (Func<string, int, int, bool>)Move);               // 传送到地图(文件名)坐标
            engine.SetValue("TAKEGOLD", (Func<double, bool>)TakeGold);                 // 扣金币(不足返回false且不扣)
            // ---- 表现 ----
            engine.SetValue("GLOBALMESSAGE", (Action<double, string>)GlobalMessage);   // 全服公告(参数1=ChatType数字, 4=Announcement)
            engine.SetValue("LOCALMESSAGE", (Action<double, string>)LocalMessage);    // 当前玩家聊天窗提示(参数1=ChatType数字, 3=Hint)
            engine.SetValue("PLAYSOUND", (Action<int>)PlaySound);                      // 播放音效ID
            // ---- [登仙后期系统] Buff/特效/技能/登仙状态 ----
            engine.SetValue("GIVEBUFF", (Func<string, bool>)GiveBuff);                 // 按CustomBuffList名上Buff(替换式, 永久)
            engine.SetValue("HASBUFF", (Func<string, bool>)HasBuff);                  // 是否持有指定名Buff
            engine.SetValue("REMOVEBUFF", (Func<string, bool>)RemoveBuff);            // 移除指定名Buff
            engine.SetValue("REFRESHEFFECTS", (Action)RefreshEffects);                // 刷新人物特效(登仙龙特效等, 按旗标990~998)
            engine.SetValue("GIVESKILL", (Action<string, int>)GiveSkill);             // 给/学技能(中文名或英文枚举名, 等级0~3)
            engine.SetValue("CHECKHUMUP", (Func<bool>)CheckHumUp);                    // 是否已羽化登仙(任务318已完成)
        }

        // ==================================================================================
        // 对话
        // ==================================================================================

        /// <summary>
        /// 打开NPC对话: 文本按行拆分发送给客户端(S.NPCResponse), 同时登记本页按钮供防作弊校验
        /// (玩家点击未在本页出现过的函数 = 非法调用, JsScriptHost 会拦截, 与经典引擎按钮校验同强度).
        /// </summary>
        private static void OpenNpc(string text)
        {
            PlayerObject player = Player;
            if (player == null || string.IsNullOrEmpty(text)) return;

            // 规范换行并按行拆分; 去掉末尾多余空行(经典引擎同样不发送纯空尾巴)
            text = text.Replace("\r\n", "\n");
            string[] lines = text.Split('\n');
            int last = lines.Length;
            while (last > 1 && lines[last - 1].Trim().Length == 0) last--;

            var page = new List<string>();
            for (int i = 0; i < last; i++) page.Add(lines[i]);

            player.NPCSpeech = page;
            player.Enqueue(new S.NPCResponse { Page = page });

            // 登记本页出现的按钮函数名(大写), 供后续点击校验
            var buttons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in ButtonRegex.Matches(text))
                buttons.Add(m.Groups[1].Value.ToUpper());
            JsScriptHost.SetPageButtons(player, buttons);
        }

        // ==================================================================================
        // 物品(实现语义与经典引擎 NPCSegment ActionType.GiveItem/TakeItem 完全一致)
        // ==================================================================================

        /// <summary>统计背包中指定物品总数(按物品名解析Info, 与经典CHECKITEM同源)</summary>
        private static int CheckItem(string itemName)
        {
            return CheckItemDura(itemName, 0, 1);
        }

        /// <summary>
        /// 统计背包中指定物品总数; minDuraK>0 时只统计耐久达标的格子(耐久千分值, 与经典CHECKITEM第3参一致).
        /// 例: CHECKITEM("银矿", 0, 15) = 查询耐久≥15的银矿数量(数量参数为0表示只按耐久过滤, 返回总数).
        /// </summary>
        private static int CheckItemDura(string itemName, int countHint, int minDuraK)
        {
            PlayerObject player = Player;
            if (player == null) return 0;

            var info = Envir.GetItemInfo(itemName);
            if (info == null) return 0;

            int count = 0;
            foreach (var item in player.Info.Inventory)
            {
                if (item == null || item.Info != info) continue;
                if (minDuraK > 0 && item.CurrentDura < minDuraK * 1000) continue; // 耐久不足的格子不计
                count += item.Count;
            }

            return count;
        }

        /// <summary>扣除背包物品(按数量, 不检查耐久)</summary>
        private static bool TakeItem(string itemName, int count)
        {
            return TakeItemDura(itemName, count, 0);
        }

        /// <summary>扣除背包物品; minDuraK>0 时只扣耐久达标的格子(与经典TAKEITEM第3参一致)</summary>
        private static bool TakeItemDura(string itemName, int count, int minDuraK)
        {
            PlayerObject player = Player;
            if (player == null || count <= 0) return false;

            var info = Envir.GetItemInfo(itemName);
            if (info == null) return false;

            bool removed = false;
            for (int i = 0; i < player.Info.Inventory.Length && count > 0; i++)
            {
                UserItem item = player.Info.Inventory[i];
                if (item == null || item.Info != info) continue;
                if (minDuraK > 0 && item.CurrentDura < minDuraK * 1000) continue; // 耐久不足的格子不扣

                removed = true;

                if (count > item.Count)
                {
                    // 整格不够扣: 整格删除, 继续下一格
                    player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    player.Info.Inventory[i] = null;
                    count -= item.Count;
                }
                else
                {
                    // 本格够扣: 扣除数量
                    player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = (ushort)count });
                    if (count == item.Count)
                        player.Info.Inventory[i] = null;
                    else
                        item.Count -= (ushort)count;
                    count = 0;
                }
            }

            if (removed) player.RefreshStats();
            return removed;
        }

        /// <summary>给予物品(可堆叠自动拆格; 背包满则丢弃剩余 —— 与经典GIVEITEM行为一致)</summary>
        private static void GiveItem(string itemName, int count)
        {
            PlayerObject player = Player;
            if (player == null || count <= 0) return;

            var info = Envir.GetItemInfo(itemName);
            if (info == null)
            {
                JsScriptHost.RecordError("", "GIVEITEM 找不到物品: " + itemName, "");
                return;
            }

            while (count > 0)
            {
                UserItem item = Envir.CreateFreshItem(info);
                if (item == null) return;

                if (item.Info.StackSize > count)
                {
                    item.Count = (ushort)count;
                    count = 0;
                }
                else
                {
                    count -= item.Info.StackSize;
                    item.Count = item.Info.StackSize;
                }

                if (player.CanGainItem(item))
                    player.GainItem(item);
            }
        }

        /// <summary>
        /// 给予任务物品: 走【任务物品栏QuestInventory】而非普通背包 ——
        /// 引擎任务物品任务的计数逻辑(ProcessItem)只扫任务栏, 普通背包里的不计数;
        /// 作者端GIVEQUESTITEM的真实语义即"给进任务栏", 此前为占位实现(等同GIVEITEM), 现补全.
        /// 入栏后对所有 NeedItem 匹配的进行中任务即时刷新计数并推送客户端(照抄引擎CheckNeedQuestItem链路).
        /// </summary>
        private static void GiveQuestItem(string itemName, int count)
        {
            PlayerObject player = Player;
            if (player == null || count <= 0) return;

            var info = Envir.GetItemInfo(itemName);
            if (info == null)
            {
                JsScriptHost.RecordError("", "GIVEQUESTITEM 找不到物品: " + itemName, "");
                return;
            }

            while (count > 0)
            {
                UserItem item = Envir.CreateFreshItem(info);
                if (item == null) return;

                if (item.Info.StackSize > count)
                {
                    item.Count = (ushort)count;
                    count = 0;
                }
                else
                {
                    count -= item.Info.StackSize;
                    item.Count = item.Info.StackSize;
                }

                // 任务栏容量检查(满则引擎已向玩家提示, 中止发放)
                if (!player.CanGainQuestItem(item)) return;

                // 进任务物品栏
                player.GainQuestItem(item);

                // 刷新所有需要该物品的进行中任务计数并推送(与掉落捕获一致的表现: 任务栏更新+发现提示)
                foreach (QuestProgressInfo quest in player.CurrentQuests)
                {
                    if (quest.ItemTaskCount.Count == 0) continue;
                    if (!quest.NeedItem(item.Info)) continue;

                    quest.ProcessItem(player.Info.QuestInventory);
                    player.SendUpdateQuest(quest, QuestState.Update);
                    player.Enqueue(new S.SendOutputMessage { Message = "发现任务物品: " + item.FriendlyName, Type = OutputMessageType.Quest });
                }
            }
        }

        // ==================================================================================
        // 技能
        // ==================================================================================

        /// <summary>按技能中文名(或英文枚举名)查询玩家是否已学且达到指定等级</summary>
        private static bool CheckSkill(string skillName, int level)
        {
            PlayerObject player = Player;
            if (player == null || string.IsNullOrEmpty(skillName)) return false;

            // 在MagicInfo表中按中文名/英文枚举名匹配(不区分大小写)
            MagicInfo magic = null;
            foreach (var mi in Envir.MagicInfoList)
            {
                if (string.Equals(mi.Name, skillName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mi.Spell.ToString(), skillName, StringComparison.OrdinalIgnoreCase))
                {
                    magic = mi;
                    break;
                }
            }

            if (magic == null) return false;

            foreach (var um in player.Info.Magics)
                if (um.Info == magic && um.Level >= level)
                    return true;

            return false;
        }

        // ==================================================================================
        // 旗标(存储与经典引擎同一数组: player.Info.Flags —— 经典脚本与JS脚本互通旗标)
        // ==================================================================================

        /// <summary>读旗标(与经典 CHECK [n] 1 同一数据)</summary>
        private static bool CheckFlag(int flagIndex)
        {
            PlayerObject player = Player;
            if (player == null || flagIndex < 0 || flagIndex >= Globals.FlagIndexCount) return false;
            return player.Info.Flags[flagIndex];
        }

        /// <summary>写旗标; 同步刷新周围NPC可见性并触发任务旗标检查(与经典 SET 命令行为一致)</summary>
        private static void SetFlag(int flagIndex, bool value)
        {
            PlayerObject player = Player;
            if (player == null || flagIndex < 0 || flagIndex >= Globals.FlagIndexCount) return;

            player.Info.Flags[flagIndex] = value;

            // 经典SET的伴生行为: 旗标变化可能影响NPC可见性(如FlagNeeded), 立即刷新周围NPC
            for (int i = player.CurrentMap.NPCs.Count - 1; i >= 0; i--)
            {
                if (Functions.InRange(player.CurrentMap.NPCs[i].CurrentLocation, player.CurrentLocation, Globals.DataRange))
                    player.CurrentMap.NPCs[i].CheckVisible(player);
            }

            if (value) player.CheckNeedQuestFlag(flagIndex);
        }

        // ==================================================================================
        // 任务
        // ==================================================================================

        /// <summary>任务状态查询: state="Active" → 进行中; 其余(Complete/1/2...) → 已完成(兼容作者脚本的数字写法)</summary>
        private static bool CheckQuest(int questIndex, string state)
        {
            PlayerObject player = Player;
            if (player == null) return false;

            if (string.Equals(state, "Active", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var q in player.CurrentQuests)
                    if (q.Index == questIndex) return true;
                return false;
            }

            return player.CompletedQuests.Contains(questIndex);
        }

        // ==================================================================================
        // 玩家信息
        // ==================================================================================

        /// <summary>玩家性别 → "男性"/"女性"(作者脚本用中文比较)</summary>
        private static string CheckGender()
        {
            PlayerObject player = Player;
            if (player == null) return "";
            return player.Gender == MirGender.Male ? "男性" : "女性";
        }

        /// <summary>玩家职业 → 中文名(与作者端CHECKCLASS返回一致)</summary>
        private static string CheckClass()
        {
            PlayerObject player = Player;
            if (player == null) return "";

            switch (player.Class)
            {
                case MirClass.Warrior: return "战士";
                case MirClass.Wizard: return "法师";
                case MirClass.Taoist: return "道士";
                case MirClass.Assassin: return "刺客";
                case MirClass.Archer: return "弓箭手";
                case MirClass.Monk: return "武僧";
                default: return "";
            }
        }

        /// <summary>玩家角色名</summary>
        private static string UserName() => Player?.Name ?? "";

        /// <summary>玩家当前等级</summary>
        private static double CheckLevel() => Player?.Level ?? 0;

        /// <summary>金币余额(玩家账号仓库金币, 与经典CHECKGOLD同源)</summary>
        private static double CheckGold() => Player?.Account?.Gold ?? 0;

        // ==================================================================================
        // 传送 / 金币
        // ==================================================================================

        /// <summary>
        /// 传送到指定地图(按地图文件名)坐标; 坐标非正时随机落点(与经典MOVE命令一致).
        /// </summary>
        private static bool Move(string mapName, int x, int y)
        {
            PlayerObject player = Player;
            if (player == null || string.IsNullOrEmpty(mapName)) return false;

            Map target = Envir.GetMapByNameAndInstance(mapName);
            if (target == null)
            {
                JsScriptHost.RecordError("", "MOVE 找不到地图: " + mapName, "");
                return false;
            }

            if (x > 0 && y > 0)
                return player.Teleport(target, new Point(x, y));

            player.TeleportRandom(200, 0, target);
            return true;
        }

        /// <summary>扣金币: 余额不足返回false且不扣(作者端月灵NPC"1000万学技能"需先判断后扣)</summary>
        private static bool TakeGold(double amount)
        {
            PlayerObject player = Player;
            if (player == null || player.Account == null || amount <= 0 || amount > uint.MaxValue) return false;

            uint gold = (uint)amount;
            if (player.Account.Gold < gold) return false;

            player.Account.Gold -= gold;
            player.Enqueue(new S.LoseGold { Gold = gold });
            return true;
        }

        // ==================================================================================
        // 表现
        // ==================================================================================

        /// <summary>全服公告: type为ChatType数字(0=普通 1=喊话 2=系统 3=提示 4=公告...), 与作者端GLOBALMESSAGE(4,...)一致</summary>
        private static void GlobalMessage(double type, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            byte t = (byte)type;
            if (!Enum.IsDefined(typeof(ChatType), t)) return; // 非法类型静默忽略, 防脚本崩主循环

            Envir.Broadcast(new S.Chat { Message = message, Type = (ChatType)t });
        }

        /// <summary>当前玩家聊天窗提示: type为ChatType数字(3=Hint系统提示), 对话流程外的操作反馈用</summary>
        private static void LocalMessage(double type, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            byte t = (byte)type;
            if (!Enum.IsDefined(typeof(ChatType), t)) return;

            Player?.ReceiveChat(message, (ChatType)t);
        }

        /// <summary>给当前玩家播放音效(音效ID)</summary>
        private static void PlaySound(int soundId)
        {
            Player?.Enqueue(new S.PlaySound { Sound = soundId });
        }

        // ==================================================================================
        // 登仙后期系统: Buff(阶段属性) / 特效(龙光) / 技能 / 登仙状态
        // ==================================================================================

        /// <summary>当前登仙Buff(BuffType.Ascension) — 单实例设计: 升阶=移除旧的再上新的</summary>
        private static Buff FindAscensionBuff(PlayerObject player)
        {
            foreach (var b in player.Buffs)
                if (b != null && b.Type == BuffType.Ascension)
                    return b;
            return null;
        }

        /// <summary>
        /// 按CustomBuffList名上登仙Buff(替换式): 先移除已有AscensionBuff再上新阶;
        /// 永久生效(duration=0, 与引擎Mentor/GameMaster永久Buff同惯例), Values[0]存表Id作阶段身份.
        /// 返回是否成功.
        /// </summary>
        private static bool GiveBuff(string buffName)
        {
            PlayerObject player = Player;
            if (player == null) return false;

            var def = CustomBuffListProfile.Find(buffName);
            if (def == null)
            {
                JsScriptHost.RecordError("", "GIVEBUFF 找不到Buff: " + buffName + " (检查Custom\\CustomBuffList.txt)", "");
                return false;
            }

            // 替换式: 先移除当前登仙Buff(若有)
            if (FindAscensionBuff(player) != null)
                player.RemoveBuff(BuffType.Ascension);

            player.AddBuff(BuffType.Ascension, null, 0, def.Stats, true, false, def.Id);
            return true;
        }

        /// <summary>是否持有指定名的登仙Buff(按表Id匹配Values[0])</summary>
        private static bool HasBuff(string buffName)
        {
            PlayerObject player = Player;
            if (player == null) return false;

            var def = CustomBuffListProfile.Find(buffName);
            if (def == null) return false;

            Buff b = FindAscensionBuff(player);
            return b != null && b.Values != null && b.Values.Length > 0 && b.Values[0] == def.Id;
        }

        /// <summary>移除指定名的登仙Buff(名字与当前不符则不移除, 防误删)</summary>
        private static bool RemoveBuff(string buffName)
        {
            PlayerObject player = Player;
            if (player == null) return false;

            if (!HasBuff(buffName)) return false;

            player.RemoveBuff(BuffType.Ascension);
            return true;
        }

        /// <summary>
        /// 刷新人物特效: 重算旗标990~998→LevelEffects并推送给本人与周围
        /// (镜像经典引擎 ActionType.RefreshEffects, 登仙龙特效升降档后调用)
        /// </summary>
        private static void RefreshEffects()
        {
            PlayerObject player = Player;
            if (player == null) return;

            player.SetLevelEffects();
            var p = new S.ObjectLevelEffects { ObjectID = player.ObjectID, LevelEffects = player.LevelEffects };
            player.Enqueue(p);
            player.Broadcast(p);
        }

        /// <summary>给/学技能: 名字支持中文MagicInfo名或英文Spell枚举名; 已学则改等级(镜像经典GIVESKILL)</summary>
        private static void GiveSkill(string skillName, int level)
        {
            PlayerObject player = Player;
            if (player == null || string.IsNullOrEmpty(skillName)) return;

            // 解析Spell: 先按英文枚举名, 再按MagicInfo中文名
            Spell skill;
            if (!Enum.TryParse(skillName, true, out skill))
            {
                foreach (var mi in Envir.MagicInfoList)
                {
                    if (!string.Equals(mi.Name, skillName, StringComparison.OrdinalIgnoreCase)) continue;
                    skill = mi.Spell;
                    break;
                }
            }

            if (!Enum.IsDefined(typeof(Spell), skill)) return;
            if (skill == Spell.None) return;

            byte spellLevel = (byte)Math.Max(0, Math.Min(3, level));

            // 已学: 改等级
            foreach (var um in player.Info.Magics)
            {
                if (um.Spell != skill) continue;
                um.Level = spellLevel;
                player.SendMagicInfo(um);
                return;
            }

            // 未学: 新学
            var magic = new UserMagic(skill) { Level = spellLevel };
            if (magic.Info == null) return;

            player.Info.Magics.Add(magic);
            player.SendMagicInfo(magic);
        }

        /// <summary>是否已羽化登仙(登仙任务链318已完成)</summary>
        private static bool CheckHumUp()
        {
            return Player != null && Player.CompletedQuests.Contains(318);
        }
    }
}
