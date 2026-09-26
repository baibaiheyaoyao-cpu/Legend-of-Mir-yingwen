using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Server.MirDatabase;

namespace Server
{
    /// <summary>
    /// CustomMagic数据驱动技能编辑面板(对标原版MagicSettings).
    /// 左侧技能列表, 右侧分页表单(说明标签+输入框): 基础/伤害/状态/增益/特效音效.
    /// 保存时行级改写INI(未管理的键原样保留), 热重载即时生效(特效/描述需玩家重登).
    /// </summary>
    public class CustomMagicForm : Form
    {
        private readonly ListView _list;
        private readonly TabControl _tabs;
        private readonly Label _status;

        // ---- 基础与伤害 ----
        private readonly Label _nameLabel;
        private readonly TextBox _magicId, _cooldown, _damageRate, _range, _damageDelay, _expire, _tick;
        private readonly ComboBox _damageStat, _attackMode;
        private readonly CheckBox _pullMobs, _canMoveBoss, _takeOver;
        private readonly TextBox _desc;

        // ---- 状态附加(8种) ----
        private readonly CheckBox[] _stAllow = new CheckBox[8];
        private readonly TextBox[] _stChance = new TextBox[8];
        private readonly TextBox[] _stTime = new TextBox[8];
        private readonly TextBox[] _stDamage = new TextBox[8];
        private static readonly string[] StatusTypes = { "GREEN", "RED", "SLOW", "FROZEN", "STUN", "PARALYSIS", "BLEEDING", "BURN" };
        private static readonly string[] StatusNames = { "中毒(绿)", "降防(红)", "减速", "冰冻", "眩晕", "麻痹", "流血", "灼烧" };

        // ---- 自增益 ----
        private readonly TextBox _buffTime;
        private readonly Dictionary<string, TextBox> _buffAdds = new Dictionary<string, TextBox>();

        // ---- 特效与音效 ----
        private readonly TextBox _castSound, _flySound, _expSound;
        private readonly DataGridView _fx;
        private static readonly string[] SegKinds = { "Self", "Self2", "Fly", "Explosion", "Target", "Magic" };
        private static readonly string[] SegNames = { "自身(施法者身上)", "自身2(叠加层)", "飞行", "爆炸(到点)", "目标(身上)", "法阵(脚下)" };
        private static readonly string[] SegPrefix = { "SELF", "SELF2", "FLY", "EXPLOSION", "TARGET", "MAGIC" };

        private string _currentFile;

        public CustomMagicForm()
        {
            Text = "CustomMagic 数据驱动技能编辑器 (改INI键值 · 保存即保留未知键 · 热重载)";
            Size = new Size(1180, 700);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            // ===== 左侧技能列表 =====
            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
            };
            _list.Columns.Add("技能名", 130);
            _list.Columns.Add("ID", 44);
            _list.Columns.Add("伤害", 46);
            _list.SelectedIndexChanged += (s, e) => { if (_list.SelectedItems.Count > 0) LoadFile(_list.SelectedItems[0].Text); };

            var listPanel = new Panel { Dock = DockStyle.Left, Width = 200 };
            var listTip = new Label { Dock = DockStyle.Top, Height = 30, Text = "选中技能后右侧加载编辑", BackColor = Color.LightSteelBlue };
            listPanel.Controls.Add(_list);
            listPanel.Controls.Add(listTip);

            // ===== 右侧分页表单 =====
            _tabs = new TabControl { Dock = DockStyle.Fill };

            // ---- Tab1 基础与伤害 ----
            _nameLabel = new Label { Text = "-", AutoSize = false, Height = 22, Font = new Font(Font, FontStyle.Bold) };
            _magicId = new TextBox { Width = 80 };
            _cooldown = new TextBox { Width = 80 };
            _damageStat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            _damageStat.Items.AddRange(new object[] { "DC (破坏)", "MC (魔力)", "SC (道术)" });
            _damageStat.SelectedIndex = 1;
            _damageRate = new TextBox { Width = 80 };
            _takeOver = new CheckBox { Text = "勾选=伤害走本INI; 不勾=回落引擎硬编码(保存时删伤害键)", AutoSize = true };
            _takeOver.CheckedChanged += (s, e) => { _damageStat.Enabled = _takeOver.Checked; _damageRate.Enabled = _takeOver.Checked; };
            _damageStat.Enabled = false;
            _damageRate.Enabled = false;
            _attackMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            _attackMode.Items.AddRange(new object[] { "单体 (0)", "群体 (1)", "直线 LINE", "范围 WIDE" });
            _attackMode.SelectedIndex = 0;
            _range = new TextBox { Width = 80 };
            _damageDelay = new TextBox { Width = 80 };
            _expire = new TextBox { Width = 80 };
            _tick = new TextBox { Width = 80 };
            _pullMobs = new CheckBox { Text = "开启后把范围内怪拉向中心", AutoSize = true };
            _canMoveBoss = new CheckBox { Text = "允许对BOSS生效", AutoSize = true, Checked = true };
            _desc = new TextBox { Multiline = true, Height = 90, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };

            var tab1 = new TabPage("基础 / 伤害 / 持续");
            var t1 = NewFormTable(6);
            AddRow(t1, "技能名(=INI文件名, 不可改)", _nameLabel);
            AddRow(t1, "绑定Spell编号(MagicID)\r\n0=未绑定(特效也不下发)", _magicId);
            AddRow(t1, "冷却时间(ms)\r\n※未接线: 实际冷却以魔法编辑器DB延迟为准", _cooldown);
            AddRow(t1, "伤害接管", _takeOver);
            AddRow(t1, "伤害属性(勾选接管后生效)", _damageStat);
            AddRow(t1, "伤害倍率%(100=按魔法编辑器威力)", _damageRate);
            AddRow(t1, "攻击模式", _attackMode);
            AddRow(t1, "伤害范围(格)\r\n以中心点为半径", _range);
            AddRow(t1, "首跳延迟(ms)", _damageDelay);
            AddRow(t1, "持续总时长(ms)\r\n0=单发; >0=持续型多跳", _expire);
            AddRow(t1, "每跳间隔(ms)", _tick);
            AddRow(t1, "全屏吸怪", _pullMobs);
            AddRow(t1, "BOSS可被吸怪位移", _canMoveBoss);
            AddRow(t1, "描述(玩家悬停说明)\r\n/r/n 会转成换行", _desc);
            tab1.Controls.Add(t1);

            // ---- Tab2 状态附加 ----
            var tab2 = new TabPage("状态附加(8种)");
            var t2 = NewFormTable(5);
            _stAllow[0] = null; //占位使数组长度对齐
            AddStatusHeader(t2);
            for (int i = 0; i < 8; i++)
            {
                _stAllow[i] = new CheckBox { AutoSize = true };
                _stChance[i] = new TextBox { Width = 60 };
                _stTime[i] = new TextBox { Width = 60 };
                _stDamage[i] = new TextBox { Width = 60 };
                AddStatusRow(t2, i);
            }
            var stTip = new Label { Dock = DockStyle.Bottom, Height = 40, Text = "几率%留空或0=必发; 持续=秒; 每跳伤害对中毒/流血/灼烧生效.", ForeColor = Color.DarkBlue };
            tab2.Controls.Add(t2);
            tab2.Controls.Add(stTip);

            // ---- Tab3 自增益 ----
            var tab3 = new TabPage("自增益");
            var t3 = NewFormTable(6);
            _buffTime = new TextBox { Width = 80 };
            AddRow(t3, "增益持续(秒)\r\n0=不加增益", _buffTime);
            foreach (var pair in new[] {
                new[] { "MINDCADD", "最小破坏" }, new[] { "MAXDCADD", "最大破坏" },
                new[] { "MINMCADD", "最小魔力" }, new[] { "MAXMCADD", "最大魔力" },
                new[] { "MINSCADD", "最小道术" }, new[] { "MAXSCADD", "最大道术" },
                new[] { "HPADD", "体力上限" }, new[] { "MPADD", "魔法上限" },
                new[] { "ATTACKSPEEDADD", "攻击速度" }, new[] { "AGILITYADD", "敏捷" },
                new[] { "ACCURACYADD", "准确" } })
            {
                var tb = new TextBox { Width = 80 };
                _buffAdds[pair[0]] = tb;
                AddRow(t3, pair[1], tb);
            }
            var buffTip = new Label { Dock = DockStyle.Bottom, Height = 30, Text = "数值0或留空=不加该项; 增益以\"奇药\"图标显示.", ForeColor = Color.DarkBlue };
            tab3.Controls.Add(t3);
            tab3.Controls.Add(buffTip);

            // ---- Tab4 特效与音效 ----
            var tab4 = new TabPage("特效与音效");
            var top4 = new TableLayoutPanel { Dock = DockStyle.Top, Height = 110, ColumnCount = 4, RowCount = 3, AutoSize = true };
            AddFieldRow(top4, 0, "施法音效", _castSound = new TextBox { Width = 100 });
            AddFieldRow(top4, 1, "飞行音效", _flySound = new TextBox { Width = 100 });
            AddFieldRow(top4, 2, "爆炸音效", _expSound = new TextBox { Width = 100 });
            var soundTip = new Label { Dock = DockStyle.Top, Height = 24, Text = "音效格式: M原编号-序号 (如 M110-0 → 20110), 留空=无声", ForeColor = Color.DarkBlue };

            _fx = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSize = true,
            };
            _fx.Columns.Add("seg", "段");
            _fx.Columns[0].ReadOnly = true;

            // 特效库下拉(原版CustomMagic 1基编号, 与客户端EffectLibrary映射一致)
            var libTable = new DataTable();
            libTable.Columns.Add("idx", typeof(int));
            libTable.Columns.Add("name", typeof(string));
            libTable.Rows.Add(0, "0 = 无特效");
            libTable.Rows.Add(1, "1 = Magic 主库");
            libTable.Rows.Add(2, "2 = Magic2");
            libTable.Rows.Add(3, "3 = Magic3");
            libTable.Rows.Add(4, "4 = Magic4");
            libTable.Rows.Add(5, "5 = Magic5");
            libTable.Rows.Add(6, "6 = MagicC");
            libTable.Rows.Add(7, "7 = MagicD");
            libTable.Rows.Add(8, "8 = MagicMW");
            libTable.Rows.Add(9, "9 = Magic_32bit(秘籍主库)");
            var fileCol = new DataGridViewComboBoxColumn
            {
                HeaderText = "特效库",
                DataSource = libTable,
                DisplayMember = "name",
                ValueMember = "idx",
                ValueType = typeof(int),
                DropDownWidth = 170,
            };
            _fx.Columns.Add(fileCol);

            _fx.Columns.Add("start", "起始帧");
            _fx.Columns.Add("play", "帧数");
            _fx.Columns.Add("empty", "空帧");
            _fx.Columns.Add("time", "时长ms");
            _fx.Columns.Add("delay", "延迟ms");
            _fx.Columns.Add("dir", "方向数(0/8)");

            var drawModeCol = new DataGridViewComboBoxColumn { HeaderText = "绘制模式", DropDownWidth = 100 };
            var dmTable = new DataTable();
            dmTable.Columns.Add("idx", typeof(int));
            dmTable.Columns.Add("name", typeof(string));
            dmTable.Rows.Add(0, "普通绘制");
            dmTable.Rows.Add(1, "透明绘制");
            drawModeCol.DataSource = dmTable;
            drawModeCol.DisplayMember = "name";
            drawModeCol.ValueMember = "idx";
            drawModeCol.ValueType = typeof(int);
            _fx.Columns.Add(drawModeCol);

            var calcDirCol = new DataGridViewCheckBoxColumn { HeaderText = "计算方向" };
            _fx.Columns.Add(calcDirCol);

            var repeatCol = new DataGridViewCheckBoxColumn { HeaderText = "循环(法阵)" };
            _fx.Columns.Add(repeatCol);
            for (int i = 0; i < SegKinds.Length; i++)
                _fx.Rows.Add(SegNames[i]);

            var fxTip = new Label { Dock = DockStyle.Bottom, Height = 40, Text = "特效库为原版1基编号: 1=Magic 2=Magic2 3=Magic3 4=Magic4 5=Magic5 6=MagicC 7=MagicD 8=MagicMW 9=Magic_32bit(秘籍主库). 选\"无特效\"该段不渲染. 绘制模式: 透明绘制=半透叠加(光源类用). 方向数8时: 方向步长=帧数+空帧.", ForeColor = Color.DarkBlue };

            tab4.Controls.Add(_fx);
            tab4.Controls.Add(fxTip);
            tab4.Controls.Add(soundTip);
            tab4.Controls.Add(top4);

            _tabs.TabPages.AddRange(new[] { tab1, tab2, tab3, tab4 });

            // ===== 底部按钮 =====
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };
            var saveButton = new Button { Text = "保存到INI", Width = 110 };
            saveButton.Click += (s, e) => SaveCurrent();
            var reloadButton = new Button { Text = "热重载INI", Width = 110 };
            reloadButton.Click += (s, e) => { RefreshList(); _status.Text = "已热重载: 行为改动立即生效; 特效/描述改动需玩家重登刷新客户端缓存."; };
            var openFolderButton = new Button { Text = "打开INI目录", Width = 110 };
            openFolderButton.Click += (s, e) =>
            {
                string dir = Path.GetFullPath(@".\Custom\CustomMagic");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", dir);
            };
            var closeButton = new Button { Text = "关闭", Width = 80 };
            closeButton.Click += (s, e) => Close();
            buttonPanel.Controls.AddRange(new Control[] { saveButton, reloadButton, openFolderButton, closeButton });

            _status = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "选中左侧技能加载编辑; 保存=行级改写INI(未管理的键原样保留).", ForeColor = Color.DarkBlue };

            Controls.Add(_tabs);
            Controls.Add(listPanel);
            Controls.Add(buttonPanel);
            Controls.Add(_status);

            Load += (s, e) => RefreshList();
        }

        // ==================== UI 构建辅助 ====================

        private static TableLayoutPanel NewFormTable(int columns)
        {
            return new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                ColumnCount = 2,
                AutoSize = true,
                Padding = new Padding(8),
            };
        }

        private static void AddRow(TableLayoutPanel table, string label, Control control)
        {
            var l = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                MaximumSize = new Size(320, 0),
            };
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(l);
            table.Controls.Add(control);
        }

        private static void AddFieldRow(TableLayoutPanel p, int row, string label, Control c)
        {
            p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            p.Controls.Add(c, 1, row);
        }

        private void AddStatusHeader(TableLayoutPanel t)
        {
            t.Controls.Add(new Label { Text = "类型", AutoSize = true });
            t.Controls.Add(new Label { Text = "启用", AutoSize = true });
            t.Controls.Add(new Label { Text = "几率%", AutoSize = true });
            t.Controls.Add(new Label { Text = "持续秒", AutoSize = true });
            t.Controls.Add(new Label { Text = "每跳伤害", AutoSize = true });
        }

        private void AddStatusRow(TableLayoutPanel t, int i)
        {
            t.Controls.Add(new Label { Text = StatusNames[i] + "(" + StatusTypes[i] + ")", AutoSize = true });
            t.Controls.Add(_stAllow[i]);
            t.Controls.Add(_stChance[i]);
            t.Controls.Add(_stTime[i]);
            t.Controls.Add(_stDamage[i]);
        }

        // ==================== 数据加载/保存 ====================

        private static string FindIniPath(string name)
        {
            foreach (string folder in new[] { @".\Custom\CustomMagic", @".\Envir\CustomSkills" })
            {
                string path = Path.Combine(Path.GetFullPath(folder), name + ".ini");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static Dictionary<string, string> ParseIniFlat(string path)
        {
            var dict = new Dictionary<string, string>();
            foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith("[")) continue;
                string key = line.Substring(0, eq).Trim().ToUpper();
                dict[key] = line.Substring(eq + 1).Trim(); //与服务端解析器一致: 重复键后者优先
            }
            return dict;
        }

        private static string Get(Dictionary<string, string> d, string key, string def = "")
        {
            string v;
            return d.TryGetValue(key, out v) ? v : def;
        }

        private static int GetInt(Dictionary<string, string> d, string key, int def = 0)
        {
            int n;
            return int.TryParse(Get(d, key), out n) ? n : def;
        }

        private static bool GetBool(Dictionary<string, string> d, string key)
        {
            return Get(d, key).ToUpper() == "TRUE";
        }

        private void RefreshList()
        {
            CustomSkillProfile.Reload();

            _list.BeginUpdate();
            _list.Items.Clear();

            List<CustomSkillDef> defs = CustomSkillProfile.All().ToList();
            defs.Sort((a, b) =>
            {
                int av = a.MagicId > 0 ? a.MagicId : 9999;
                int bv = b.MagicId > 0 ? b.MagicId : 9999;
                return av.CompareTo(bv);
            });

            foreach (CustomSkillDef def in defs)
            {
                var item = new ListViewItem(def.Name) { ForeColor = def.MagicId > 0 ? Color.Black : Color.Gray };
                item.SubItems.Add(def.MagicId > 0 ? def.MagicId.ToString() : "-");
                item.SubItems.Add(def.MagicId > 0 ? (def.HasDamageOverride ? "INI" : "引擎") : "-");
                _list.Items.Add(item);
            }

            _list.EndUpdate();
        }

        private void LoadFile(string name)
        {
            string path = FindIniPath(name);
            if (path == null) return;

            _currentFile = path;
            Dictionary<string, string> d = ParseIniFlat(path);

            _nameLabel.Text = name;
            _magicId.Text = Get(d, "MAGICID");
            _cooldown.Text = Get(d, "COOLDOWNTIME", Get(d, "COOLDOWN", "3000"));
            _takeOver.Checked = d.ContainsKey("DAMAGESTAT") || d.ContainsKey("DAMAGERATE");
            _damageStat.SelectedIndex = Get(d, "DAMAGESTAT", "MC") == "DC" ? 0 : Get(d, "DAMAGESTAT", "MC") == "SC" ? 2 : 1;
            _damageRate.Text = GetInt(d, "DAMAGERATE", 100).ToString();
            string am = Get(d, "ATTACKMODE", "0");
            _attackMode.SelectedIndex = am == "1" || am == "GROUP" ? 1 : am == "LINE" ? 2 : am == "WIDE" ? 3 : 0;
            _range.Text = Math.Max(GetInt(d, "ATTACKNEARRANGE"), GetInt(d, "ATTACKGROUPRANGE")).ToString();
            _damageDelay.Text = GetInt(d, "DAMAGEDELAY", 500).ToString();
            _expire.Text = Get(d, "MAGICEXPIRETIME", "0");
            _tick.Text = Get(d, "MAGICTICKTIME", "0");
            _pullMobs.Checked = GetBool(d, "全屏吸怪");
            _canMoveBoss.Checked = Get(d, "CANMOVEBOSS", "").ToUpper() != "FALSE";
            _desc.Text = Get(d, "描述", Get(d, "DESC")).Replace("/r/n", "\r\n");

            for (int i = 0; i < 8; i++)
            {
                string st = StatusTypes[i];
                _stAllow[i].Checked = GetBool(d, "ALLOW" + st);
                _stChance[i].Text = Get(d, st + "CHANCE", Get(d, st + "CHANCEADD", "0"));
                _stTime[i].Text = Get(d, st + "TIME", Get(d, st + "TIMEADD", "0"));
                _stDamage[i].Text = Get(d, st + "DAMAGE", "0");
            }

            _buffTime.Text = Get(d, "BUFFTIME", "0");
            foreach (var pair in _buffAdds)
                pair.Value.Text = Get(d, pair.Key, "0");

            _castSound.Text = Get(d, "MAGICCASTSOUND");
            _flySound.Text = Get(d, "MAGICFLYSOUND");
            _expSound.Text = Get(d, "MAGICEXPLOSIONSOUND");

            for (int r = 0; r < SegKinds.Length; r++)
            {
                string prefix = SegPrefix[r];
                int fileIdx = GetInt(d, prefix + "_FILE");
                if (fileIdx < 0 || fileIdx > 9) fileIdx = 0; //未收录库兜底为无特效
                _fx.Rows[r].Cells[1].Value = fileIdx;
                _fx.Rows[r].Cells[2].Value = GetInt(d, prefix + "_STARTINDEX").ToString();
                int play = GetInt(d, prefix + "_PLAYCOUNT", GetInt(d, prefix + "_STARTCOUNT"));
                _fx.Rows[r].Cells[3].Value = play.ToString();
                _fx.Rows[r].Cells[4].Value = GetInt(d, prefix + "_EMPTYCOUNT").ToString();
                _fx.Rows[r].Cells[5].Value = GetInt(d, prefix + "_PLAYTIME", 500).ToString();
                _fx.Rows[r].Cells[6].Value = GetInt(d, prefix + "_DELAY").ToString();
                _fx.Rows[r].Cells[7].Value = GetInt(d, prefix + "_DIRCOUNT").ToString();
                _fx.Rows[r].Cells[8].Value = GetInt(d, prefix + "_DRAWMODE"); //0普通 1透明
                _fx.Rows[r].Cells[9].Value = GetBool(d, prefix + "_CALCDIR");
                bool repeat = GetBool(d, prefix + "_REPEAT") || (prefix == "MAGIC" && GetBool(d, "MAGICEFFECTREPEAT"));
                _fx.Rows[r].Cells[10].Value = repeat;
            }

            _status.Text = "已加载: " + name;
        }

        private void SaveCurrent()
        {
            if (_currentFile == null) { MessageBox.Show("请先在左侧选择技能."); return; }

            int magicId;
            int.TryParse(_magicId.Text, out magicId);

            // ---- 收集表单 → (键, 值) 清单 ----
            var edits = new List<KeyValuePair<string, string>>();

            void Add(string key, string value) { edits.Add(new KeyValuePair<string, string>(key, value)); }
            void AddInt(string key, TextBox box, int def = 0)
            {
                int n;
                if (!int.TryParse(box.Text.Trim(), out n)) n = def;
                Add(key, n.ToString());
            }
            void AddBool(string key, bool v) { Add(key, v ? "True" : "False"); }

            Add("MAGICID", magicId.ToString());
            Add("COOLDOWNTIME", _cooldown.Text.Trim() == "" ? "3000" : _cooldown.Text.Trim());
            //伤害接管: 勾选才写DamageStat/DamageRate; 不勾则删除已有键 → 引擎硬编码伤害
            var removeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_takeOver.Checked)
            {
                Add("DAMAGESTAT", new[] { "DC", "MC", "SC" }[_damageStat.SelectedIndex]);
                AddInt("DAMAGERATE", _damageRate, 100);
            }
            else
            {
                removeKeys.Add("DAMAGESTAT");
                removeKeys.Add("DAMAGERATE");
            }
            Add("ATTACKMODE", new[] { "0", "1", "LINE", "WIDE" }[_attackMode.SelectedIndex]);
            AddInt("ATTACKNEARRANGE", _range, 1);
            Add("ATTACKGROUPRANGE", _range.Text.Trim() == "" ? "1" : _range.Text.Trim());
            AddInt("DAMAGEDELAY", _damageDelay, 500);
            Add("MAGICEXPIRETIME", _expire.Text.Trim() == "" ? "0" : _expire.Text.Trim());
            Add("MAGICTICKTIME", _tick.Text.Trim() == "" ? "0" : _tick.Text.Trim());
            AddBool("全屏吸怪", _pullMobs.Checked);
            AddBool("CANMOVEBOSS", _canMoveBoss.Checked);
            Add("描述", _desc.Text.Replace("\r\n", "/r/n").Replace("\n", "/r/n"));

            for (int i = 0; i < 8; i++)
            {
                string st = StatusTypes[i];
                AddBool("ALLOW" + st, _stAllow[i].Checked);
                int c; int.TryParse(_stChance[i].Text, out c); Add(st + "CHANCE", (c <= 0 ? 100 : c).ToString());
                int t; int.TryParse(_stTime[i].Text, out t); Add(st + "TIME", t.ToString());
                int dm; int.TryParse(_stDamage[i].Text, out dm); Add(st + "DAMAGE", dm.ToString());
            }

            Add("BUFFTIME", _buffTime.Text.Trim() == "" ? "0" : _buffTime.Text.Trim());
            foreach (var pair in _buffAdds)
            {
                int v; int.TryParse(pair.Value.Text, out v);
                Add(pair.Key, v.ToString());
            }

            Add("MAGICCASTSOUND", _castSound.Text.Trim());
            Add("MAGICFLYSOUND", _flySound.Text.Trim());
            Add("MAGICEXPLOSIONSOUND", _expSound.Text.Trim());

            for (int r = 0; r < SegKinds.Length; r++)
            {
                string prefix = SegPrefix[r];
                DataGridViewRow row = _fx.Rows[r];
                Add(prefix + "_FILE", CellText(row, 1, "0"));
                Add(prefix + "_STARTINDEX", CellText(row, 2, "0"));
                Add(prefix + "_PLAYCOUNT", CellText(row, 3, "0"));
                Add(prefix + "_EMPTYCOUNT", CellText(row, 4, "0"));
                Add(prefix + "_PLAYTIME", CellText(row, 5, "500"));
                Add(prefix + "_DELAY", CellText(row, 6, "0"));
                Add(prefix + "_DIRCOUNT", CellText(row, 7, "0"));
                Add(prefix + "_DRAWMODE", CellText(row, 8, "0"));
                Add(prefix + "_CALCDIR", CellBool(row, 9));
                if (prefix == "MAGIC")
                    Add("MAGICEFFECTREPEAT", CellBool(row, 10));
                else
                    Add(prefix + "_REPEAT", CellBool(row, 10));
            }

            // ---- 行级改写: 未管理的键原样保留 ----
            var pending = new Dictionary<string, string>();
            foreach (var e in edits) pending[e.Key.ToUpper()] = e.Value;

            List<string> lines = new List<string>(File.ReadAllLines(_currentFile, Encoding.UTF8));
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith("[")) continue;
                string key = line.Substring(0, eq).Trim().ToUpper();
                if (removeKeys.Contains(key)) { lines[i] = null; continue; }
                string value;
                if (pending.TryGetValue(key, out value))
                {
                    lines[i] = key + "=" + value;
                    pending.Remove(key);
                }
            }
            lines.RemoveAll(l => l == null); //清掉被删除的伤害键行

            // 剩余未出现的键: 追加到 [ServerConfig] (特效/音效键追加到各自节)
            var bySection = new Dictionary<string, List<string>>();
            foreach (var kv in pending)
            {
                string section = "ServerConfig";
                if (kv.Key.StartsWith("MAGICCASTSOUND") || kv.Key.StartsWith("MAGICFLYSOUND") || kv.Key.StartsWith("MAGICEXPLOSIONSOUND")) section = "ClientConfig";
                if (kv.Key.Contains("_FILE") || kv.Key.Contains("_STARTINDEX") || kv.Key.Contains("_PLAYCOUNT") || kv.Key.Contains("_STARTCOUNT") ||
                    kv.Key.Contains("_EMPTYCOUNT") || kv.Key.Contains("_PLAYTIME") || kv.Key.Contains("_DELAY") || kv.Key.Contains("_DIRCOUNT") ||
                    kv.Key.Contains("_REPEAT") || kv.Key == "MAGICEFFECTREPEAT" || kv.Key.Contains("_CALCDIR") || kv.Key.Contains("_DRAWMODE") ||
                    kv.Key.Contains("_DRAWORDER")) section = "ClientAttack";

                List<string> bucket;
                if (!bySection.TryGetValue(section, out bucket)) { bucket = new List<string>(); bySection[section] = bucket; }
                bucket.Add(kv.Key + "=" + kv.Value);
            }

            foreach (var pair in bySection)
            {
                string header = "[" + pair.Key + "]";
                int headerIndex = -1;
                for (int i = 0; i < lines.Count; i++)
                    if (lines[i].Trim().Equals(header, StringComparison.OrdinalIgnoreCase)) { headerIndex = i; break; }

                if (headerIndex >= 0)
                    lines.InsertRange(headerIndex + 1, pair.Value);
                else
                {
                    lines.Add("");
                    lines.Add(header);
                    lines.AddRange(pair.Value);
                }
            }

            File.WriteAllLines(_currentFile, lines, new UTF8Encoding(false));
            CustomSkillProfile.Reload();
            RefreshList();

            _status.Text = "已保存: " + Path.GetFileName(_currentFile) + " — 行为立即生效; 特效/描述需玩家重登.";
            MessageBox.Show("保存成功。\n\n行为改动: 已热重载, 下次施法即生效。\n特效/描述/音效改动: 需玩家重登刷新客户端缓存。", "CustomMagic");
        }

        private static string CellText(DataGridViewRow row, int index, string def)
        {
            object v = row.Cells[index].Value;
            string s = v == null ? "" : v.ToString().Trim();
            return s == "" ? def : s;
        }

        private static string CellBool(DataGridViewRow row, int index)
        {
            object v = row.Cells[index].Value;
            return v is bool && (bool)v ? "True" : "False";
        }
    }
}
