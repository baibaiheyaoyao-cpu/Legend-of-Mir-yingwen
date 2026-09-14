using Server.MirDatabase;
using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// 诊断中心(Bug检测): 一键体检 商城/数据库/启动依赖/任务奖励/配置.
    /// 把本次开服踩过的所有坑(重复GIndex/买不了/静默失效/计数器/配置翻转)变成可主动发现的检查项.
    /// 数据源: EditEnvir(编辑库,进程启动后首次打开时自动加载), 检测只读, 不修改任何数据.
    /// </summary>
    public class DiagnosticsForm : Form
    {
        private class CheckResult
        {
            public string Name;
            public string Status; //通过 / 警告 / 错误
            public string Summary = "";
            public readonly List<string> Details = new();
        }

        private readonly ListView _list = new();
        private readonly TextBox _detail = new();
        private readonly Label _summary = new();
        private readonly List<CheckResult> _results = new();

        public DiagnosticsForm()
        {
            Text = "诊断中心 (Bug检测)";
            Size = new Size(760, 620);
            StartPosition = FormStartPosition.CenterParent;

            _list.Dock = DockStyle.Fill;
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.Columns.Add("检查项", 220);
            _list.Columns.Add("结果", 60);
            _list.Columns.Add("概要", 420);
            _list.SelectedIndexChanged += (s, e) =>
            {
                _detail.Clear();
                if (_list.SelectedIndices.Count == 0) return;
                var r = _results[_list.SelectedIndices[0]];
                _detail.Text = r.Details.Count > 0 ? string.Join(Environment.NewLine, r.Details) : r.Summary;
            };

            _detail.Dock = DockStyle.Bottom;
            _detail.Height = 220;
            _detail.Multiline = true;
            _detail.ScrollBars = ScrollBars.Both;
            _detail.ReadOnly = true;
            _detail.Font = new Font("Consolas", 9F);

            _summary.Dock = DockStyle.Top;
            _summary.Height = 30;
            _summary.Text = "点击[开始检测]运行全部检查。检测是只读的, 不会改动任何数据。";

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight };
            var run = new Button { Text = "开始检测", AutoSize = true };
            var copy = new Button { Text = "复制报告", AutoSize = true };
            run.Click += (s, e) => RunChecks();
            copy.Click += (s, e) => CopyReport();
            buttons.Controls.Add(run);
            buttons.Controls.Add(copy);

            Controls.Add(_list);
            Controls.Add(_detail);
            Controls.Add(buttons);
            Controls.Add(_summary);
            _summary.BringToFront();
        }

        private void RunChecks()
        {
            _results.Clear();
            _list.Items.Clear();
            _detail.Clear();
            _summary.Text = "正在检测...";

            try
            {
                var envir = SMain.EditEnvir;

                //编辑库未加载时先加载(进程启动后首次诊断)
                if (envir.ItemInfoList.Count == 0)
                {
                    try
                    {
                        envir.LoadDB();
                    }
                    catch (Exception ex)
                    {
                        Add("数据库加载", "错误", "无法加载 Server.MirDB: " + ex.Message);
                        Finish();
                        return;
                    }
                }

                var itemNames = new HashSet<string>(envir.ItemInfoList.Select(x => x.Name.Replace(" ", "")), StringComparer.OrdinalIgnoreCase);
                var itemIndexSet = new HashSet<int>(envir.ItemInfoList.Select(x => x.Index));
                var monsterNames = new HashSet<string>(envir.MonsterInfoList.Select(x => x.Name.Replace(" ", "")), StringComparer.OrdinalIgnoreCase);

                CheckShop(envir, itemIndexSet);
                CheckCounters(envir);
                CheckItems(envir);
                CheckMonsters(envir);
                CheckSystemMobs(envir, monsterNames, itemNames);
                CheckQuestRewards(envir);
                CheckConfig();
            }
            catch (Exception ex)
            {
                Add("检测过程", "错误", "检测器自身异常: " + ex);
            }

            Finish();
        }

        // ==================== 商城 ====================

        private void CheckShop(Envir envir, HashSet<int> itemIndexSet)
        {
            var shop = envir.GameShopList;

            //重复GIndex: 购买按GIndex取第一条, 其余永远买不到/买错东西
            var dups = shop.GroupBy(x => x.GIndex).Where(g => g.Count() > 1).ToList();
            if (dups.Count > 0)
            {
                var r = Add("商城-重复GIndex", "错误", $"{dups.Count} 组重复");
                foreach (var g in dups)
                    r.Details.Add($"G{g.Key}: " + string.Join(" + ", g.Select(x => $"'{x.Info?.Name ?? "?"}'")));
            }
            else Add("商城-重复GIndex", "通过", "无重复编号");

            //两种货币都未开放: 玩家购买必然失败
            var noBuy = shop.Where(x => !x.CanBuyCredit && !x.CanBuyGold).ToList();
            if (noBuy.Count > 0)
            {
                var r = Add("商城-不可购买条目", "错误", $"{noBuy.Count} 条未开放任何货币");
                foreach (var x in noBuy)
                    r.Details.Add($"G{x.GIndex} '{x.Info?.Name ?? "?"}' 金币={x.GoldPrice} 元宝={x.CreditPrice}");
            }
            else Add("商城-不可购买条目", "通过", "全部条目均可购买");

            //单次限购为0: 数量包装过大导致(StackSize*5/Count<1), 永远买不了
            var zeroLimit = shop.Where(x => x.Info != null && (int)x.Info.StackSize * 5 / Math.Max(1, (int)x.Count) < 1).ToList();
            if (zeroLimit.Count > 0)
            {
                var r = Add("商城-单次限购为0", "错误", $"{zeroLimit.Count} 条永远无法购买");
                foreach (var x in zeroLimit)
                    r.Details.Add($"G{x.GIndex} '{x.Info?.Name}' 每份数量={x.Count} 堆叠上限={x.Info.StackSize} → 单次限购=0, 请把'数量'改为1");
            }
            else Add("商城-单次限购为0", "通过", "所有条目均可正常购买");

            //引用不存在的物品
            var ghost = shop.Where(x => x.Info == null || !itemIndexSet.Contains(x.ItemIndex)).ToList();
            if (ghost.Count > 0)
            {
                var r = Add("商城-幽灵条目", "警告", $"{ghost.Count} 条引用已删除物品");
                foreach (var x in ghost)
                    r.Details.Add($"G{x.GIndex} -> 物品#{x.ItemIndex} 不存在");
            }
            else Add("商城-幽灵条目", "通过", "无失效引用");

            Add("商城-条目总数", "通过", $"{shop.Count} 条在售");
        }

        // ==================== 计数器 ====================

        private void CheckCounters(Envir envir)
        {
            var problems = new List<string>();

            if (envir.GameShopList.Count > 0)
            {
                var max = envir.GameShopList.Max(x => x.GIndex);
                if (envir.GameshopIndex < max) problems.Add($"商城GIndex计数器={envir.GameshopIndex} 落后于最大值={max}, 下次上架会撞号");
            }
            if (envir.ItemInfoList.Count > 0 && envir.ItemIndex < envir.ItemInfoList.Max(x => x.Index))
                problems.Add($"物品计数器={envir.ItemIndex} 落后于最大值={envir.ItemInfoList.Max(x => x.Index)}, 新建物品会撞号");
            if (envir.MonsterInfoList.Count > 0 && envir.MonsterIndex < envir.MonsterInfoList.Max(x => x.Index))
                problems.Add($"怪物计数器={envir.MonsterIndex} 落后于最大值={envir.MonsterInfoList.Max(x => x.Index)}");
            if (envir.NPCInfoList.Count > 0 && envir.NPCIndex < envir.NPCInfoList.Max(x => x.Index))
                problems.Add($"NPC计数器={envir.NPCIndex} 落后于最大值={envir.NPCInfoList.Max(x => x.Index)}");

            if (problems.Count > 0)
            {
                var r = Add("数据库-计数器同步", "错误", $"{problems.Count} 项落后");
                r.Details.AddRange(problems);
                r.Details.Add("(正常情况下启动时已自动同步; 若出现此错误请重启服务器)");
            }
            else Add("数据库-计数器同步", "通过", "各计数器正常");
        }

        // ==================== 物品/怪物重复 ====================

        private void CheckItems(Envir envir)
        {
            var nameDup = envir.ItemInfoList
                .GroupBy(x => x.Name.Replace(" ", ""), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).ToList();
            if (nameDup.Count > 0)
            {
                var r = Add("物品-名称重复", "警告", $"{nameDup.Count} 组重名");
                foreach (var g in nameDup)
                    r.Details.Add(string.Join(", ", g.Select(x => $"#{x.Index}'{x.Name}'")) + "  (脚本按名字引用会命中错误物品)");
            }
            else Add("物品-名称重复", "通过", "无重名");

            var idxDup = envir.ItemInfoList.GroupBy(x => x.Index).Where(g => g.Count() > 1).ToList();
            if (idxDup.Count > 0)
            {
                var r = Add("物品-索引重复", "错误", $"{idxDup.Count} 组");
                foreach (var g in idxDup)
                    r.Details.Add("索引 " + g.Key + ": " + string.Join(", ", g.Select(x => $"'{x.Name}'")));
            }
            else Add("物品-索引重复", "通过", "无重复");

            Add("物品-总数", "通过", $"{envir.ItemInfoList.Count} 件");
        }

        private void CheckMonsters(Envir envir)
        {
            var dup = envir.MonsterInfoList
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).ToList();
            if (dup.Count > 0)
            {
                var r = Add("怪物-名称重复", "警告", $"{dup.Count} 组重名");
                foreach (var g in dup)
                    r.Details.Add(string.Join(", ", g.Select(x => $"#{x.Index}'{x.Name}'")) + "  (爆率/任务按名字引用会命中错误怪物)");
            }
            else Add("怪物-名称重复", "通过", "无重名");

            Add("怪物-总数", "通过", $"{envir.MonsterInfoList.Count} 只");
        }

        // ==================== 启动依赖 ====================

        private static readonly string[] SystemMobFields =
        {
            "SkeletonName", "BugBatName", "ShinsuName",
            "Zuma1","Zuma2","Zuma3","Zuma4","Zuma5","Zuma6","Zuma7",
            "Turtle1","Turtle2","Turtle3","Turtle4","Turtle5",
            "BoneMonster1","BoneMonster2","BoneMonster3","BoneMonster4",
            "BehemothMonster1","BehemothMonster2","BehemothMonster3",
            "HellKnight1","HellKnight2","HellKnight3","HellKnight4",
            "HellBomb1","HellBomb2","HellBomb3",
            "GeneralMeowMeowMob1","GeneralMeowMeowMob2","GeneralMeowMeowMob3","GeneralMeowMeowMob4",
            "KingHydraxMob","HornedCommanderMob","HornedCommanderBombMob","SnowWolfKingMob",
            "ScrollMob1","ScrollMob2","ScrollMob3","ScrollMob4",
            "WhiteSnake","AngelName","BombSpiderName","CloneName","FishingMonster",
            "AssassinCloneName","MonkCloneName","VampireName","ToadName",
            "SnakeTotemName","SnakesName","StoneName","AncientBatName","TucsonGeneralEgg",
        };

        private void CheckSystemMobs(Envir envir, HashSet<string> monsterNames, HashSet<string> itemNames)
        {
            var missing = new List<string>();
            foreach (var f in SystemMobFields)
            {
                var info = typeof(Settings).GetField(f);
                if (info == null) continue;
                var name = info.GetValue(null) as string;
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!monsterNames.Contains(name.Replace(" ", "")))
                    missing.Add($"{f}='{name}'");
            }

            var r = missing.Count > 0
                ? Add("启动依赖-系统怪名缺失", "警告", $"{missing.Count} 个映射指向不存在的怪物")
                : Add("启动依赖-系统怪名缺失", "通过", "全部系统怪名有效");
            r.Details.AddRange(missing);
            if (missing.Count > 0)
            {
                r.Details.Add("影响: 若开启[启动时强制DB校验]将拒绝启动; 对应功能(召唤骷髅/神兽等)不可用。");
                r.Details.Add("处理: 在 配置→高级设置→高级(系统怪名) 里把名字改成库里真实存在的怪物名, 或保持DB校验关闭。");
            }

            //精炼矿石(启动校验物品)
            var ore = typeof(Settings).GetField("RefineOreName")?.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(ore) && !itemNames.Contains(ore.Replace(" ", "")))
                Add("启动依赖-精炼矿石", "警告", $"RefineOreName='{ore}' 不存在");
            else
                Add("启动依赖-精炼矿石", "通过", "有效");
        }

        // ==================== 任务奖励 ====================

        private void CheckQuestRewards(Envir envir)
        {
            var broken = new List<string>();
            foreach (var q in envir.QuestInfoList)
            {
                var badRewards = q.FixedRewards.Where(x => x.Item == null).Count()
                               + q.SelectRewards.Where(x => x.Item == null).Count();
                if (badRewards > 0)
                    broken.Add($"任务 {q.Index} '{q.Name}' ({q.FileName}.txt): {badRewards} 条奖励物品名无效(玩家完成后该项奖励会落空)");
            }

            if (broken.Count > 0)
            {
                var r = Add("任务-奖励物品失效", "警告", $"{broken.Count} 个任务存在无效奖励");
                r.Details.AddRange(broken);
                r.Details.Add("处理: 打开对应脚本修正奖励物品名(可用 DbTool quests 命令导出完整清单)。");
            }
            else Add("任务-奖励物品失效", "通过", "全部奖励有效");

            Add("任务-总数", "通过", $"{envir.QuestInfoList.Count} 个");
        }

        // ==================== 配置 ====================

        private void CheckConfig()
        {
            var r = Add("配置-关键项快照", "通过", "详情见下方");
            r.Details.Add($"Language = {Settings.Language}");
            r.Details.Add($"EnforceDBChecks = {Settings.EnforceDBChecks}" + (Settings.EnforceDBChecks ? "  (注意: 中文怪名库开启此项会拒绝启动)" : ""));
            r.Details.Add($"MapUnloadEnabled = {Settings.MapUnloadEnabled}, Delay = {Settings.MapUnloadDelay} 分钟");
            r.Details.Add($"Multithreaded = {Settings.Multithreaded}, ThreadLimit = {Settings.ThreadLimit}");

            var langFile = Path.Combine("Localization", Settings.Language + ".json");
            if (!File.Exists(langFile))
                Add("配置-语言文件", "错误", $"Localization\\{Settings.Language}.json 不存在");
            else
                Add("配置-语言文件", "通过", langFile);

            //删除标记: 已删物品等待重启彻底移除
            int suppressed = SMain.Envir.SuppressedItemIndexes.Count;
            if (suppressed > 0)
                Add("物品-删除标记", "警告", $"{suppressed} 件已删物品待重启彻底移除(期间周期存盘不会复活它们)");
            else
                Add("物品-删除标记", "通过", "无");
        }

        // ==================== 输出 ====================

        private CheckResult Add(string name, string status, string summary)
        {
            var r = new CheckResult { Name = name, Status = status, Summary = summary };
            _results.Add(r);

            var item = new ListViewItem(r.Name) { UseItemStyleForSubItems = false };
            var sub = new ListViewItem.ListViewSubItem(item, r.Status)
            {
                ForeColor = r.Status == "错误" ? Color.Red : r.Status == "警告" ? Color.Chocolate : Color.Green,
            };
            item.SubItems.Add(sub);
            item.SubItems.Add(r.Summary);
            _list.Items.Add(item);
            return r;
        }

        private void Finish()
        {
            int err = _results.Count(x => x.Status == "错误");
            int warn = _results.Count(x => x.Status == "警告");
            int ok = _results.Count(x => x.Status == "通过");
            _summary.Text = $"检测完成: {ok} 项通过, {warn} 项警告, {err} 项错误" + (err + warn == 0 ? "  —— 一切正常" : "  (点击列表行查看详情)");
            _summary.ForeColor = err > 0 ? Color.Red : warn > 0 ? Color.Chocolate : Color.Green;
        }

        private void CopyReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== 服务器诊断报告 {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            foreach (var r in _results)
            {
                sb.AppendLine($"[{r.Status}] {r.Name}: {r.Summary}");
                foreach (var d in r.Details) sb.AppendLine("    " + d);
            }
            Clipboard.SetText(sb.ToString());
            MessageBox.Show(this, "报告已复制到剪贴板。", "诊断中心", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
