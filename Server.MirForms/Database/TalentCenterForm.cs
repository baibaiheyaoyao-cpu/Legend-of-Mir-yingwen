using Server.MirDatabase;
using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// 天赋中心: 天赋参数(Talent.ini)面板化管理 + 天赋表(Talents.txt)一览与重载.
    /// 此前只能手改 ini + GM命令热重载, 且 ReloadTalents 不重读 ini 参数;
    /// 本面板把两者打通: 保存并重载 = 写ini + 重读参数 + 重读天赋表 + 通知在线玩家.
    /// </summary>
    public class TalentCenterForm : Form
    {
        private readonly NumericUpDown _numStartingLevel = new();
        private readonly NumericUpDown _numStepLevel = new();
        private readonly NumericUpDown _numStepPoint = new();
        private readonly NumericUpDown _numLearnCost = new();
        private readonly NumericUpDown _numResetCost = new();

        private readonly DataGridView _grid = new();
        private readonly ComboBox _classFilter = new();
        private readonly Label _statsLabel = new();

        private static readonly Dictionary<byte, string> ClassNames = new()
        {
            { (byte)MirClass.Warrior, "战士" },
            { (byte)MirClass.Wizard, "法师" },
            { (byte)MirClass.Taoist, "道士" },
            { (byte)MirClass.Assassin, "刺客" },
            { (byte)MirClass.Archer, "弓手" },
            { TalentInfo.AnyClass, "全职业" },
        };

        public TalentCenterForm()
        {
            Text = "天赋中心";
            Size = new Size(900, 640);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;

            SuspendLayout();

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildParamPage());
            tabs.TabPages.Add(BuildGridPage());
            tabs.SelectedIndex = 0;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            root.Controls.Add(tabs, 0, 0);

            Controls.Add(root);
            ResumeLayout(false);
            PerformLayout();
        }

        // ==================== 参数页 ====================

        private TabPage BuildParamPage()
        {
            var page = new TabPage("天赋参数 (Talent.ini)") { Padding = new Padding(10) };

            var intro = new Label
            {
                Dock = DockStyle.Top,
                Height = 44,
                Text = "学习消耗/洗点费用保存后立即生效;\n解锁等级与点数发放参数在下一次登录/升级补算点数时生效(只补差不回收)。",
                ForeColor = Color.DarkSlateGray,
            };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(0, 10, 0, 0),
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddParam(grid, 0, "天赋系统解锁等级", _numStartingLevel, Settings.TalentStartingLevel, 0, 200, "达到该等级开放天赋并发放第一批点数");
            AddParam(grid, 1, "每升几级发点", _numStepLevel, Settings.TalentStepLevel, 1, 100, "与「每步发放点数」配合, 如每1级发5点");
            AddParam(grid, 2, "每步发放点数", _numStepPoint, Settings.TalentStepPoint, 0, 1000000, "每次发放的天赋点数量");
            AddParam(grid, 3, "天赋升1级消耗点数", _numLearnCost, Settings.TalentLearnCost, 0, 1000000, "玩家学习/升级天赋的点数消耗");
            AddParam(grid, 4, "洗点费用(金币)", _numResetCost, (int)Math.Min(int.MaxValue, Settings.TalentResetCost), 0, int.MaxValue, "洗点全额返还已消耗点数");

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 8, 0, 0) };
            var save = new Button { Text = "保存", AutoSize = true };
            var saveReload = new Button { Text = "保存并重载(通知在线玩家)", AutoSize = true };
            save.Click += (s, e) => SaveParams(false);
            saveReload.Click += (s, e) => SaveParams(true);
            buttons.Controls.Add(save);
            buttons.Controls.Add(saveReload);

            page.Controls.Add(grid);
            page.Controls.Add(buttons);
            page.Controls.Add(intro);
            intro.BringToFront();
            return page;
        }

        private void AddParam(TableLayoutPanel grid, int row, string label, NumericUpDown num, int current, int min, int max, string hint)
        {
            grid.RowCount = row + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            num.Minimum = min;
            num.Maximum = max;
            num.Value = Math.Max(min, Math.Min(max, current));
            num.Width = 120;
            num.TextAlign = HorizontalAlignment.Right;

            grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(4, 10, 4, 0) }, 0, row);
            grid.Controls.Add(num, 1, row);
            grid.Controls.Add(new Label { Text = hint, AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(4, 10, 4, 0) }, 2, row);
        }

        private void SaveParams(bool reload)
        {
            try
            {
                //与 Settings.LoadTalent 相同的钳制规则
                Settings.TalentStartingLevel = (int)_numStartingLevel.Value;
                Settings.TalentStepLevel = Math.Max(1, (int)_numStepLevel.Value);
                Settings.TalentStepPoint = (int)_numStepPoint.Value;
                Settings.TalentLearnCost = Math.Max(0, (int)_numLearnCost.Value);
                Settings.TalentResetCost = (uint)_numResetCost.Value;

                var reader = new InIReader(Path.Combine(Settings.ConfigPath, "Talent.ini"));
                reader.Write("Config", "StartingLevel", Settings.TalentStartingLevel);
                reader.Write("Config", "StepLevel", Settings.TalentStepLevel);
                reader.Write("Config", "StepPoint", Settings.TalentStepPoint);
                reader.Write("Config", "LearnCost", Settings.TalentLearnCost);
                reader.Write("Config", "CostAmount", (int)Math.Min(int.MaxValue, Settings.TalentResetCost));

                if (reload && SMain.Envir.Running)
                {
                    //重读天赋表并通知在线玩家刷新天赋窗口(参数已在上面直接更新到内存)
                    SMain.Envir.ReloadTalents();
                    MessageBox.Show(this, "已保存并重载: 参数即时生效, 在线玩家天赋窗口已刷新。", "天赋中心",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        reload
                            ? "已保存。服务器当前未运行, 天赋表将在下次启动时加载; 参数已即时生效。"
                            : "已保存到 Configs\\Talent.ini。",
                        "天赋中心", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败: " + ex.Message, "天赋中心", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== 天赋一览页 ====================

        private TabPage BuildGridPage()
        {
            var page = new TabPage("天赋一览 (Talents.txt)") { Padding = new Padding(10) };

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, FlowDirection = FlowDirection.LeftToRight };

            _classFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _classFilter.Width = 110;
            _classFilter.Items.AddRange(new object[] { "全部职业", "战士", "法师", "道士", "刺客", "弓手", "全职业" });
            _classFilter.SelectedIndex = 0;
            _classFilter.SelectedIndexChanged += (s, e) => FillGrid();

            var refresh = new Button { Text = "刷新", AutoSize = true };
            refresh.Click += (s, e) => FillGrid();

            var reload = new Button { Text = "重载天赋表", AutoSize = true };
            reload.Click += (s, e) =>
            {
                if (!SMain.Envir.Running)
                {
                    MessageBox.Show(this, "服务器未运行, 天赋表会在下次启动时加载, 无需重载。", "天赋中心",
                        MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                    return;
                }
                SMain.Envir.ReloadTalents();
                FillGrid();
            };

            _statsLabel.AutoSize = true;
            _statsLabel.Margin = new Padding(16, 8, 4, 0);
            _statsLabel.ForeColor = Color.DarkSlateGray;

            bar.Controls.Add(new Label { Text = "职业筛选:", AutoSize = true, Margin = new Padding(2, 8, 2, 0) });
            bar.Controls.Add(_classFilter);
            bar.Controls.Add(refresh);
            bar.Controls.Add(reload);
            bar.Controls.Add(_statsLabel);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("Id", "Id");
            _grid.Columns["Id"].FillWeight = 40;
            _grid.Columns.Add("Name", "名称");
            _grid.Columns["Name"].FillWeight = 80;
            _grid.Columns.Add("Class", "职业");
            _grid.Columns["Class"].FillWeight = 50;
            _grid.Columns.Add("ReqLv", "需求等级");
            _grid.Columns["ReqLv"].FillWeight = 55;
            _grid.Columns.Add("MaxLv", "满级");
            _grid.Columns["MaxLv"].FillWeight = 40;
            _grid.Columns.Add("Tier", "层");
            _grid.Columns["Tier"].FillWeight = 30;
            _grid.Columns.Add("Col", "列");
            _grid.Columns["Col"].FillWeight = 30;
            _grid.Columns.Add("Pre", "前置(Id:级)");
            _grid.Columns["Pre"].FillWeight = 90;
            _grid.Columns.Add("Stats", "每级属性");
            _grid.Columns["Stats"].FillWeight = 160;
            _grid.Columns.Add("Effect", "特效");
            _grid.Columns["Effect"].FillWeight = 55;
            _grid.Columns.Add("Desc", "描述");
            _grid.Columns["Desc"].FillWeight = 130;

            page.Controls.Add(_grid);
            page.Controls.Add(bar);

            FillGrid();
            return page;
        }

        private void FillGrid()
        {
            _grid.Rows.Clear();

            List<TalentInfo> list;
            try
            {
                list = TalentLoader.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "读取 Talents.txt 失败: " + ex.Message, "天赋中心", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            byte? filterClass = null;
            if (_classFilter.SelectedIndex > 0)
            {
                filterClass = _classFilter.SelectedIndex switch
                {
                    1 => (byte)MirClass.Warrior,
                    2 => (byte)MirClass.Wizard,
                    3 => (byte)MirClass.Taoist,
                    4 => (byte)MirClass.Assassin,
                    5 => (byte)MirClass.Archer,
                    _ => TalentInfo.AnyClass,
                };
            }

            foreach (var t in list)
            {
                if (filterClass.HasValue && t.Class != filterClass.Value) continue;

                var pre = t.PreTalents.Count == 0 ? "-" : string.Join("/", t.PreTalents.Select(p => $"{p[0]}:{p[1]}"));
                var stats = t.Stats.Values.Count == 0 ? "-" : string.Join(", ", t.Stats.Values.Select(kv => $"{kv.Key}+{kv.Value}"));
                var effect = t.SpecialEffect == 0 ? "-" : $"{t.SpecialEffect}(召唤)";

                _grid.Rows.Add(t.Index, t.Name, ClassNames.TryGetValue(t.Class, out var cn) ? cn : t.Class.ToString(),
                    t.RequiredLevel, t.MaxLevel, t.Tier, t.Column, pre, stats, effect, t.Description);
            }

            var summonCount = list.Count(x => x.SpecialEffect > 0);
            _statsLabel.Text = $"共 {list.Count} 条天赋 | 召唤类(特效>0, 暂未开放学习) {summonCount} 条 | 文件: Envir\\Talents.txt";
        }
    }
}
