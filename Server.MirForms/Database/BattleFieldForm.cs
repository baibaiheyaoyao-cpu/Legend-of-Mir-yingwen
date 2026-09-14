using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// 战场控制面板: 开关/时间/分值等可视化调整(保存到 Configs\BattleField.ini, 立即生效),
    /// 实时状态显示, 以及手动"立即开战/立即结束"(方便测试, 走游戏主线程执行).
    /// </summary>
    public class BattleFieldForm : Form
    {
        private readonly CheckBox _chkEnabled = new();
        private readonly NumericUpDown _numHour = new();
        private readonly NumericUpDown _numMinute = new();
        private readonly NumericUpDown _numHour2 = new();
        private readonly NumericUpDown _numMinute2 = new();
        private readonly NumericUpDown _numDuration = new();
        private readonly NumericUpDown _numKill = new();
        private readonly NumericUpDown _numDeath = new();
        private readonly NumericUpDown _numMinLevel = new();
        private readonly TextBox _status = new();
        private readonly System.Windows.Forms.Timer _timer = new();

        public BattleFieldForm()
        {
            Text = "战场控制";
            Size = new Size(560, 480);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;

            // ===== 设置区 =====
            var settings = new GroupBox { Text = "战场设置 (保存后立即生效)", Dock = DockStyle.Top, Height = 240, Padding = new Padding(10) };
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            void AddRow(int row, string label, Control editor)
            {
                grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(4, 8, 4, 0) }, 0, row);
                grid.Controls.Add(editor, 1, row);
            }

            NumericUpDown Num(int min, int max, int value)
            {
                return new NumericUpDown { Minimum = min, Maximum = max, Value = value, Width = 100, TextAlign = HorizontalAlignment.Right };
            }

            Control Combo(Control a, Control b)
            {
                var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
                p.Controls.Add(a);
                p.Controls.Add(new Label { Text = " : ", AutoSize = true, Margin = new Padding(2, 6, 2, 0) });
                p.Controls.Add(b);
                return p;
            }

            _chkEnabled.AutoSize = true;
            _chkEnabled.Checked = BattleField.Enabled;
            _numHour = Num(0, 23, BattleField.StartHour);
            _numMinute = Num(0, 59, BattleField.StartMinute);
            _numHour2 = Num(-1, 23, BattleField.SecondStartHour);
            _numMinute2 = Num(0, 59, BattleField.SecondStartMinute);
            _numDuration = Num(5, 720, BattleField.DurationMinutes);
            _numKill = Num(1, 100, BattleField.KillScore);
            _numDeath = Num(0, 100, BattleField.DeathScore);
            _numMinLevel = Num(1, 200, BattleField.MinLevel);

            AddRow(0, "启用战场系统", _chkEnabled);
            AddRow(1, "第一场 - 时/分", Combo(_numHour, _numMinute));
            AddRow(2, "第二场 - 时/分", Combo(_numHour2, _numMinute2));
            AddRow(3, "战斗时长(分钟)", _numDuration);
            AddRow(4, "击杀得分", _numKill);
            AddRow(5, "被杀扣分", _numDeath);
            AddRow(6, "参战最低等级", _numMinLevel);
            grid.Controls.Add(new Label { Text = "第二场'时'填-1=关闭第二场", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(4, 10, 4, 0) }, 0, 7);
            grid.Controls.Add(new Label { Text = "", AutoSize = true }, 1, 7);

            var saveBtn = new Button { Text = "保存设置", AutoSize = true, Margin = new Padding(4, 8, 4, 0) };
            saveBtn.Click += (s, e) =>
            {
                BattleField.Enabled = _chkEnabled.Checked;
                BattleField.StartHour = (int)_numHour.Value;
                BattleField.StartMinute = (int)_numMinute.Value;
                BattleField.SecondStartHour = (int)_numHour2.Value;
                BattleField.SecondStartMinute = (int)_numMinute2.Value;
                BattleField.DurationMinutes = (int)_numDuration.Value;
                BattleField.KillScore = (int)_numKill.Value;
                BattleField.DeathScore = (int)_numDeath.Value;
                BattleField.MinLevel = (int)_numMinLevel.Value;
                BattleField.SaveSettings();
                MessageBox.Show(this, "已保存到 Configs\\BattleField.ini, 设置立即生效。\n(正在进行中的战斗按原定结束时间结算)", "战场控制",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            grid.Controls.Add(saveBtn, 1, 8);
            settings.Controls.Add(grid);

            // ===== 状态区 =====
            var statusBox = new GroupBox { Text = "实时状态", Dock = DockStyle.Fill, Padding = new Padding(10) };
            _status.Dock = DockStyle.Fill;
            _status.Multiline = true;
            _status.ReadOnly = true;
            _status.ScrollBars = ScrollBars.Vertical;
            _status.Font = new Font("Consolas", 9.5F);
            statusBox.Controls.Add(_status);

            // ===== 手动控制 =====
            var controls = new GroupBox { Text = "手动控制 (测试用)", Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(10) };
            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var startBtn = new Button { Text = "立即开战", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            var endBtn = new Button { Text = "立即结束", AutoSize = true, Margin = new Padding(0, 6, 12, 0) };
            startBtn.Click += (s, e) =>
            {
                if (!SMain.Envir.Running) { MessageBox.Show(this, "服务器未运行。", "战场控制", MessageBoxButtons.OK, MessageBoxIcon.Asterisk); return; }
                if (MessageBox.Show(this, "立即开战会把等候室内所有玩家分队传送进战场。\n继续?", "立即开战", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                SMain.Envir.QueueMainAction(BattleField.StartBattle);
            };
            endBtn.Click += (s, e) =>
            {
                if (!SMain.Envir.Running) { MessageBox.Show(this, "服务器未运行。", "战场控制", MessageBoxButtons.OK, MessageBoxIcon.Asterisk); return; }
                SMain.Envir.QueueMainAction(BattleField.EndBattle);
            };
            flow.Controls.Add(startBtn);
            flow.Controls.Add(endBtn);
            controls.Controls.Add(flow);

            Controls.Add(statusBox);
            Controls.Add(controls);
            Controls.Add(settings);

            _timer.Interval = 1000;
            _timer.Tick += (s, e) => RefreshStatus();
            _timer.Start();
            RefreshStatus();

            FormClosed += (s, e) => _timer.Stop();
        }

        private void RefreshStatus()
        {
            _status.Text = BattleField.StatusSnapshot() + "\r\n\r\n提示: 玩家入口在战场传送员NPC(比奇城) → 等候室 → 开战自动传送。\r\n奖励规则在脚本 Envir\\NPCs\\战场\\等候室\\勇猛的战场进入管理员.txt 中调整。";
        }
    }
}
