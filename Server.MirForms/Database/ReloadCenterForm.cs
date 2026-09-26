using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// 重载中心: 集中展示所有可热更模块与最后重载时间, 一键重载.
    /// 背景: 热更能力散落在各菜单, 且"编辑器保存 vs 运行实例5分钟周期存盘"存在覆盖竞态
    /// (改完不重载, 5分钟内会被旧数据回写覆盖), 这里提供统一的操作与提醒入口.
    /// </summary>
    public class ReloadCenterForm : Form
    {
        //各模块最后重载时间(静态, 关窗不丢失)
        private static readonly Dictionary<string, DateTime> LastReload = new();

        private readonly ListBox _logBox = new();
        private readonly TableLayoutPanel _table = new();
        private int _rowIndex;

        public ReloadCenterForm()
        {
            Text = "重载中心";
            Size = new Size(560, 520);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            var tip = new Label
            {
                Dock = DockStyle.Top,
                Height = 46,
                Text = "提示: 改完数据库/脚本后请立即重载对应模块, 否则运行实例每 " +
                       Settings.SaveDelay + " 分钟的周期存盘会把旧数据写回覆盖你的修改。\r\n" +
                       "地图/怪物/任务/魔法 属于启动数据, 修改后需重启服务器(控制→重启)才生效。",
                ForeColor = Color.Firebrick,
            };

            _table.Dock = DockStyle.Top;
            _table.AutoSize = true;
            _table.ColumnCount = 3;
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            _logBox.Dock = DockStyle.Fill;
            _logBox.Font = new Font("Consolas", 9F);
            _logBox.HorizontalScrollbar = true;

            AddModule("NPC脚本", () => SMain.Envir.ReloadNPCs(), needRunning: true);
            AddModule("掉落表", () => SMain.Envir.ReloadDrops(), needRunning: true);
            AddModule("滚动公告", () => SMain.Envir.ReloadLineMessages(), needRunning: true);
            AddModule("游戏商城", () => SMain.Envir.ReloadGameShop(), needRunning: true);
            AddModule("物品热同步", () =>
            {
                //走队列: 与游戏主线程串行执行, 避免和运行中的实例抢数据
                SMain.Envir.QueueItemSync(new List<int>());
                Log("物品热同步已提交, 结果见主日志。");
            }, needRunning: true);
            AddModule("天赋表", () =>
            {
                //参数(Talent.ini)与天赋表(Talents.txt)一并重读, 并通知在线玩家
                Settings.LoadTalent();
                SMain.Envir.ReloadTalents();
                Log("天赋表+参数已重载, 在线玩家已刷新。");
            }, needRunning: true);

            var allRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
            var reloadAll = new Button { Text = "全部重载", AutoSize = true };
            reloadAll.Click += (s, e) =>
            {
                foreach (var btn in _table.Controls.OfType<Button>().ToList())
                    btn.PerformClick();
            };
            allRow.Controls.Add(reloadAll);
            _table.RowCount = _rowIndex + 1;
            _table.Controls.Add(allRow, 0, _rowIndex);
            _table.SetColumnSpan(allRow, 3);

            //常用编辑工具: 一站式"改东西→重载→验证"
            var tools = new GroupBox { Text = "常用编辑工具 (改完记得点上面的重载)", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 2, 4, 6) };
            var toolsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            tools.Controls.Add(toolsFlow);

            void AddTool(string name, Action open)
            {
                var b = new Button { Text = name, AutoSize = true };
                b.Click += (s, e) => open();
                toolsFlow.Controls.Add(b);
            }

            AddTool("物品管理器", () => new Server.Database.ItemMgrForm().ShowDialog(this));
            AddTool("物品编辑器", () => new Server.Database.ItemInfoFormNew().ShowDialog(this));
            AddTool("掉落表生成器", () => new Server.MirForms.DropBuilder.DropGenForm().ShowDialog(this));
            AddTool("怪物编辑器", () =>
            {
                new Server.Database.MonsterInfoFormNew().ShowDialog(this);
                SMain.Enqueue("提示: 怪物属性改动需重启服务器(控制→重启)后生效。");
            });
            AddTool("怪物协调器", () =>
            {
                if (!SMain.Envir.Running)
                {
                    Log("怪物协调器: 服务器必须在运行状态才能调谐怪物。");
                    return;
                }
                new Server.MirForms.Systems.MonsterTunerForm().ShowDialog(this);
            });

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); //提示
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); //重载模块
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); //工具
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); //日志
            root.Controls.Add(_logBox, 0, 3);
            root.Controls.Add(tools, 0, 2);
            root.Controls.Add(_table, 0, 1);
            root.Controls.Add(tip, 0, 0);

            Controls.Add(root);
        }

        private void AddModule(string name, Action reloadAction, bool needRunning)
        {
            int row = _rowIndex++;
            _table.RowCount = row + 1;
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var timeLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = LastReload.TryGetValue(name, out var t) ? t.ToString("HH:mm:ss") : "从未",
                ForeColor = Color.Gray,
                Tag = name,
            };

            var btn = new Button { Text = "重载", Dock = DockStyle.Fill, AutoSize = true };
            btn.Click += (s, e) =>
            {
                if (needRunning && !SMain.Envir.Running)
                {
                    Log($"[{name}] 服务器未运行, 修改会在下次启动时自动生效, 无需重载。");
                    return;
                }

                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    reloadAction();
                    sw.Stop();
                    LastReload[name] = DateTime.Now;
                    timeLabel.Text = LastReload[name].ToString("HH:mm:ss");
                    timeLabel.ForeColor = Color.Green;
                    Log($"[{name}] 重载完成 ({sw.ElapsedMilliseconds}ms)。");
                }
                catch (Exception ex)
                {
                    Log($"[{name}] 重载失败: {ex.Message}");
                }
            };

            _table.Controls.Add(new Label { Text = name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            _table.Controls.Add(timeLabel, 1, row);
            _table.Controls.Add(btn, 2, row);
        }

        private void Log(string msg)
        {
            _logBox.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {msg}");
            if (_logBox.Items.Count > 200) _logBox.Items.RemoveAt(_logBox.Items.Count - 1);
        }
    }
}
