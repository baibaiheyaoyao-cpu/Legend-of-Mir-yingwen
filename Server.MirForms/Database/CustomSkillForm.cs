using Server.MirEnvir;
using Server.MirObjects;

namespace Server
{
    /// <summary>
    /// 自定义技能管理面板: 编辑 Configs\CustomSkills.ini (技能槽 Spell 242-255).
    /// 模板: 1=攻击强化(近战倍率+吸血) 2=自身爆发(以自身为中心AOE) 3=目标轰炸(目标点多段AOE).
    /// 保存后热生效: 同步MagicInfo + 向在线玩家重发配置包, 无需重启/重登.
    /// 新增技能流程: 选模板→填数值→挑特效(MagicD起始帧1010步长20等)→绑印→保存.
    /// </summary>
    public class CustomSkillForm : Form
    {
        private readonly DataGridView _grid;
        private readonly Label _help;
        private readonly Button _saveButton;
        private readonly Button _closeButton;

        public CustomSkillForm()
        {
            Text = "自定义技能管理 (CustomSkills.ini · 槽位242-255 · 保存即热生效)";
            Size = new Size(1250, 640);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter,
            };

            AddTextCol("Slot", "槽位", 45, true);
            AddComboCol("Template", "模板", 90, new[] { "0=未启用", "1=攻击强化", "2=自身爆发", "3=目标轰炸" });
            AddTextCol("Name", "名称", 90, false);
            AddNumCol("BaseCost", "耗蓝", 45);
            AddNumCol("DelayMs", "冷却ms", 60);
            AddNumCol("Icon", "图标", 45);
            for (int i = 1; i <= 6; i++) AddNumCol("P" + i, "P" + i, 50);
            AddComboCol("EffectLib", "特效库", 80, new[] { "255=无", "0=Magic", "1=Magic2", "2=Magic3", "3=Magic4", "4=MagicC", "5=MagicD" });
            AddNumCol("EffectBase", "起始帧", 55);
            AddNumCol("EffectCount", "帧数", 45);
            AddNumCol("EffectStride", "步长", 45);
            AddNumCol("EffectDuration", "时长ms", 60);
            AddNumCol("SoundSpell", "音效技", 50);
            AddComboCol("CastAction", "施法动作", 80, new[] { "0=通用施法", "1=Attack1贴身" });
            AddTextCol("BindItem", "绑定印(物品全名)", 120, false);

            foreach (var cfg in Settings.CustomSkills)
            {
                int idx = _grid.Rows.Add();
                var row = _grid.Rows[idx];
                row.Cells["Slot"].Value = ((byte)cfg.Spell).ToString();
                row.Cells["Template"].Value = TemplateStr(cfg.Template);
                row.Cells["Name"].Value = cfg.Name;
                row.Cells["BaseCost"].Value = cfg.BaseCost.ToString();
                row.Cells["DelayMs"].Value = cfg.DelayMs.ToString();
                row.Cells["Icon"].Value = cfg.Icon.ToString();
                for (int i = 1; i <= 6; i++) row.Cells["P" + i].Value = (typeof(CustomSkillConfig).GetField("P" + i).GetValue(cfg)).ToString();
                row.Cells["EffectLib"].Value = LibStr(cfg.EffectLib);
                row.Cells["EffectBase"].Value = cfg.EffectBase.ToString();
                row.Cells["EffectCount"].Value = cfg.EffectCount.ToString();
                row.Cells["EffectStride"].Value = cfg.EffectStride.ToString();
                row.Cells["EffectDuration"].Value = cfg.EffectDuration.ToString();
                row.Cells["SoundSpell"].Value = cfg.SoundSpell.ToString();
                row.Cells["CastAction"].Value = ActionStr(cfg.CastAction);
                row.Cells["BindItem"].Value = cfg.BindItem;
            }
            _grid.SelectionChanged += (s, e) => UpdateHelp();

            _help = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 66,
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = Color.DarkBlue,
                Text = "选中一行查看模板参数说明。魔印特效素材举例: MagicD起始帧1010/帧数14/步长20/时长1400。",
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft };
            _closeButton = new Button { Text = "关闭", Width = 80, DialogResult = DialogResult.Cancel };
            _saveButton = new Button { Text = "保存并应用(热生效)", Width = 160 };
            _saveButton.Click += Save;
            buttons.Controls.Add(_closeButton);
            buttons.Controls.Add(_saveButton);

            Controls.Add(_grid);
            Controls.Add(_help);
            Controls.Add(buttons);

            UpdateHelp();
        }

        private static string TemplateStr(int t)
        {
            return t switch
            {
                1 => "1=攻击强化",
                2 => "2=自身爆发",
                3 => "3=目标轰炸",
                _ => "0=未启用",
            };
        }
        private static string LibStr(int l)
        {
            string[] names = { "Magic", "Magic2", "Magic3", "Magic4", "MagicC", "MagicD" };
            return l >= 0 && l <= 5 ? l + "=" + names[l] : "255=无";
        }
        private static string ActionStr(int a) => a == 1 ? "1=Attack1贴身" : "0=通用施法";

        private void AddTextCol(string name, string header, int width, bool readOnly)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, MinimumWidth = width, ReadOnly = readOnly });
        }
        private void AddNumCol(string name, string header, int width)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, MinimumWidth = width });
        }
        private void AddComboCol(string name, string header, int width, string[] items)
        {
            var col = new DataGridViewComboBoxColumn { Name = name, HeaderText = header, MinimumWidth = width };
            col.Items.AddRange(items);
            _grid.Columns.Add(col);
        }

        private void UpdateHelp()
        {
            if (_grid.CurrentRow == null) return;
            int template = ParseCell(_grid.CurrentRow.Cells["Template"].Value, 0);
            _help.Text = template switch
            {
                1 => "模板1 攻击强化(烈火式): P1=伤害倍率%(140即1.4倍)  P2=蓄力窗口毫秒(施放后限时强化下次近战)  P3=命中吸血%  P4=攻击通道(0=DC 1=MC 2=SC)\r\n施放后平砍触发, 倍率作用于面板攻击力。建议 CastAction=1(贴身动作)。",
                2 => "模板2 自身爆发: P1=半径格数(以自身为中心)  P2=固定伤害  P3=每级伤害加成  P4=攻击通道(0=DC 1=MC 2=SC)\r\n伤害=固定+每级×等级+面板攻击。建议 CastAction=0(施法动作), 特效从帧库起始帧取方向×步长。",
                3 => "模板3 目标轰炸(流星式): P1=半径格  P2=伤害段数  P3=段间隔毫秒  P4=固定伤害  P5=每级伤害  P6=攻击通道(0=DC 1=MC 2=SC)\r\n对目标点连续多段范围伤害, 需要选中目标或地面施放。",
                _ => "未启用。模板1=近战强化类(烈火式)  模板2=自身范围爆发  模板3=目标区域多段轰炸。\r\n绑定印=物品数据库中的物品全名(如【血龙印】), 佩戴/镶嵌该印即获得此技能(3级, 卸下即失); 留空则只能GM调试。",
            };
        }

        private void Save(object sender, EventArgs e)
        {
            try
            {
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    var cfg = Settings.CustomSkills.FirstOrDefault(x => (byte)x.Spell == ParseCell(row.Cells["Slot"].Value, 0));
                    if (cfg == null) continue;

                    cfg.Template = (byte)ParseCell(row.Cells["Template"].Value, 0);
                    cfg.Name = Convert.ToString(row.Cells["Name"].Value ?? "").Trim();
                    cfg.BaseCost = (ushort)Math.Max(0, ParseCell(row.Cells["BaseCost"].Value, 10));
                    cfg.DelayMs = (uint)Math.Max(500, ParseCell(row.Cells["DelayMs"].Value, 2000));
                    cfg.Icon = (byte)Math.Max(0, ParseCell(row.Cells["Icon"].Value, 0));
                    for (int i = 1; i <= 6; i++)
                        typeof(CustomSkillConfig).GetField("P" + i).SetValue(cfg, ParseCell(row.Cells["P" + i].Value, 0));
                    cfg.EffectLib = ParseLib(row.Cells["EffectLib"].Value);
                    cfg.EffectBase = (short)ParseCell(row.Cells["EffectBase"].Value, 0);
                    cfg.EffectCount = (byte)Math.Max(0, ParseCell(row.Cells["EffectCount"].Value, 0));
                    cfg.EffectStride = (byte)Math.Max(0, ParseCell(row.Cells["EffectStride"].Value, 0));
                    cfg.EffectDuration = (ushort)Math.Max(300, ParseCell(row.Cells["EffectDuration"].Value, 1400));
                    cfg.SoundSpell = (ushort)Math.Max(0, ParseCell(row.Cells["SoundSpell"].Value, 0));
                    cfg.CastAction = (byte)ParseCell(row.Cells["CastAction"].Value, 0);
                    cfg.BindItem = Convert.ToString(row.Cells["BindItem"].Value ?? "").Trim();

                    if (cfg.Enabled && string.IsNullOrEmpty(cfg.Name))
                    {
                        MessageBox.Show(this, $"槽位 {(byte)cfg.Spell} 已启用但名称为空。", "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                Settings.SaveCustomSkills();
                Envir.Main.SyncCustomSkillMagicInfo();

                var packet = PlayerObject.BuildCustomSkillConfigsPacket();
                foreach (var player in Envir.Main.Players)
                    player.Enqueue(packet);

                MessageBox.Show(this, $"已保存并热同步: {packet.Skills.Count} 个启用技能, {Envir.Main.Players.Count} 名在线玩家已刷新。\r\n新获得技能需重新镶嵌/佩戴印触发刷新。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static int ParseCell(object value, int fallback)
        {
            var s = Convert.ToString(value ?? "");
            if (s.Length > 0 && s.Contains('=')) s = s.Substring(0, s.IndexOf('='));
            return int.TryParse(s.Trim(), out int v) ? v : fallback;
        }

        private static byte ParseLib(object value)
        {
            int v = ParseCell(value, 255);
            return (byte)(v > 5 ? 255 : v);
        }
    }
}
