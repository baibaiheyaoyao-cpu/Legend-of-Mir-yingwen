using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// 高级设置面板: 覆盖 Setup.ini 中"服务器设置(ConfigForm)"没有入口的全部配置项.
    /// 通过反射直接绑定 Settings 静态字段, 保存时 Settings.Save() 原子写回 ini.
    /// 每项标注生效方式: [立即] 运行循环内读取, 保存即生效; [重启] 启动时读取, 需重启服务器(配合"重启=重读配置"一键生效).
    /// </summary>
    public class AdvancedConfigForm : Form
    {
        private class Row
        {
            public string Label;      //显示名
            public string Field;      //Settings 静态字段名
            public bool NeedRestart;  //是否需重启生效
            public Control Editor;    //编辑控件
            public System.Reflection.FieldInfo Info;
        }

        private readonly List<Row> _rows = new();
        private readonly ComboBox _languageBox = new();

        public AdvancedConfigForm()
        {
            Text = "高级设置 (Setup.ini)";
            Size = new Size(680, 640);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.None;

            SuspendLayout();

            var tabs = new TabControl { Dock = DockStyle.Fill };

            tabs.TabPages.Add(BuildPage("常规", new object[,]
            {
                { "服务器语言", "Language", true },
                { "GM密码", "GMPassword", false },
                { "多线程处理", "Multithreaded", true },
                { "怪物线程数", "ThreadLimit", true },
                { "测试服务器模式", "TestServer", false },
                { "启动时强制DB校验(中文怪名库保持关闭)", "EnforceDBChecks", true },
                { "无人时怪物照常AI", "MonsterProcessWhenAlone", false },
            }));

            tabs.TabPages.Add(BuildPage("地图与性能", new object[,]
            {
                { "无人地图自动卸载", "MapUnloadEnabled", true },
                { "地图卸载延迟(分钟)", "MapUnloadDelay", true },
                { "单连接最大包数", "MaxPacket", false },
                { "灵石按等级成长", "GatherOrbsPerLevel", false },
                { "怪物等级差经验衰减", "ExpMobLevelDifference", false },
            }));

            tabs.TabPages.Add(BuildPage("经济", new object[,]
            {
                { "元宝金币汇率(商城默认价=1000×此值)", "CredxGold", false },
                { "怪物掉金币开关", "DropGold", false },
                { "单次掉金上限", "MaxDropGold", false },
                { "传送到NPC费用(金币)", "TeleportToNPCCost", false },
            }));

            tabs.TabPages.Add(BuildPage("游戏行为", new object[,]
            {
                { "地面物品消失(秒)", "ItemTimeOut", false },
                { "死亡掉落物品消失(秒)", "PlayerDiedItemTimeOut", false },
                { "宠物下线保存", "PetSave", false },
                { "Boss可驯服数量上限", "MaxBossTames", false },
                { "PK红名延迟(秒)", "PKDelay", false },
                { "怪物召回开关", "MonsterRecallEnabled", false },
                { "怪物召回范围(格)", "MonsterRecallRange", false },
                { "怪物召回冷却(毫秒)", "MonsterRecallCooldown", false },
                { "新手行会名", "NewbieGuild", false },
                { "新手行会人数上限", "NewbieGuildMaxSize", false },
                { "组队邀请间隔(毫秒)", "GroupInviteDelay", false },
                { "交易间隔(毫秒)", "TradeDelay", false },
            }));

            tabs.TabPages.Add(BuildPage("物品与PVP", new object[,]
            {
                { "治愈戒指(物品名)", "HealRing", false },
                { "火焰戒指(物品名)", "FireRing", false },
                { "闪现(技能名)", "BlinkSkill", false },
                { "幸运上限", "MaxLuck", false },
                { "封印冷却(秒)", "ItemSealDelay", false },
                { "PVP可抵抗魔法", "PvpCanResistMagic", false },
                { "PVP可抵抗中毒", "PvpCanResistPoison", false },
                { "PVP可被冰冻", "PvpCanFreeze", false },
                { "远程命中加成", "RangeAccuracyBonus", false },
            }));

            tabs.TabPages.Add(BuildPage("归档", new object[,]
            {
                { "不活跃角色归档(月)", "ArchiveInactiveCharacterAfterMonths", true },
                { "已删除角色归档(月)", "ArchiveDeletedCharacterAfterMonths", true },
            }));

            tabs.TabPages.Add(BuildPage("高级(系统怪名/特殊)", new object[,]
            {
                { "道士骷髅召唤", "SkeletonName", true },
                { "神兽召唤", "ShinsuName", true },
                { "虫蝙蝠", "BugBatName", true },
                { "祖玛石像1", "Zuma1", true },
                { "祖玛石像2", "Zuma2", true },
                { "祖玛石像3", "Zuma3", true },
                { "祖玛石像4", "Zuma4", true },
                { "祖玛石像5", "Zuma5", true },
                { "祖玛石像6", "Zuma6", true },
                { "祖玛石像7", "Zuma7", true },
                { "乌龟1", "Turtle1", true },
                { "乌龟2", "Turtle2", true },
                { "乌龟3", "Turtle3", true },
                { "乌龟4", "Turtle4", true },
                { "乌龟5", "Turtle5", true },
                { "白骨门怪1", "BoneMonster1", true },
                { "白骨门怪2", "BoneMonster2", true },
                { "白骨门怪3", "BoneMonster3", true },
                { "白骨门怪4", "BoneMonster4", true },
                { "巨兽1", "BehemothMonster1", true },
                { "巨兽2", "BehemothMonster2", true },
                { "巨兽3", "BehemothMonster3", true },
                { "地狱骑士1", "HellKnight1", true },
                { "地狱骑士2", "HellKnight2", true },
                { "地狱骑士3", "HellKnight3", true },
                { "地狱骑士4", "HellKnight4", true },
                { "地狱爆弹1", "HellBomb1", true },
                { "地狱爆弹2", "HellBomb2", true },
                { "地狱爆弹3", "HellBomb3", true },
                { "喵喵将军怪1", "GeneralMeowMeowMob1", true },
                { "喵喵将军怪2", "GeneralMeowMeowMob2", true },
                { "喵喵将军怪3", "GeneralMeowMeowMob3", true },
                { "喵喵将军怪4", "GeneralMeowMeowMob4", true },
                { "海德拉王", "KingHydraxMob", true },
                { "兽角指挥官", "HornedCommanderMob", true },
                { "兽角指挥官爆弹", "HornedCommanderBombMob", true },
                { "雪原狼王", "SnowWolfKingMob", true },
                { "卷轴怪1", "ScrollMob1", true },
                { "卷轴怪2", "ScrollMob2", true },
                { "卷轴怪3", "ScrollMob3", true },
                { "卷轴怪4", "ScrollMob4", true },
                { "白蛇", "WhiteSnake", true },
                { "天使", "AngelName", true },
                { "爆蛛", "BombSpiderName", true },
                { "分身", "CloneName", true },
                { "钓鱼怪", "FishingMonster", true },
                { "刺客分身", "AssassinCloneName", true },
                { "武僧分身", "MonkCloneName", true },
                { "武僧分身血量", "MonkCloneHP", true },
                { "武僧分身最小攻击", "MonkCloneMinDC", true },
                { "武僧分身最大攻击", "MonkCloneMaxDC", true },
                { "武僧天雷震间隔", "MonkTianLeiZhenInterval", true },
                { "吸血蛛", "VampireName", true },
                { "蟾蜍", "ToadName", true },
                { "蛇图腾", "SnakeTotemName", true },
                { "蛇群", "SnakesName", true },
                { "石化怪", "StoneName", true },
                { "远古蝙蝠", "AncientBatName", true },
                { "图森将军卵", "TucsonGeneralEgg", true },
                { "PK镇地图名", "PKTownMapName", false },
                { "PK镇坐标X", "PKTownPositionX", false },
                { "PK镇坐标Y", "PKTownPositionY", false },
                { "智能生物黑曜石(物品名)", "CreatureBlackStoneName", false },
            }));

            var tip = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "绿色[立即]=保存后马上生效;  橙色[重启]=需重启服务器生效(点下方'保存并重启'一键完成)。",
                ForeColor = Color.DarkSlateGray,
            };

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
            var saveReboot = new Button { Text = "保存并重启服务器", AutoSize = true };
            var save = new Button { Text = "保存", AutoSize = true };
            var cancel = new Button { Text = "取消", AutoSize = true };
            saveReboot.Click += (s, e) => { if (Save()) DoReboot(); };
            save.Click += (s, e) => Save();
            cancel.Click += (s, e) => Close();
            buttons.Controls.Add(saveReboot);
            buttons.Controls.Add(save);
            buttons.Controls.Add(cancel);

            tabs.SelectedIndex = 0;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

            root.Controls.Add(tip, 0, 0);
            root.Controls.Add(tabs, 0, 1);
            root.Controls.Add(buttons, 0, 2);

            Controls.Add(root);
            ResumeLayout(false);
            PerformLayout();
        }

        private TabPage BuildPage(string title, object[,] rows)
        {
            var page = new TabPage(title) { AutoScroll = true, Padding = new Padding(8) };

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int count = rows.GetLength(0);
            grid.RowCount = count;
            for (int i = 0; i < count; i++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                AddRow(grid, i, (string)rows[i, 0], (string)rows[i, 1], (bool)rows[i, 2]);
            }

            page.Controls.Add(grid);
            return page;
        }

        private void AddRow(TableLayoutPanel grid, int row, string label, string field, bool needRestart)
        {
            var info = typeof(Settings).GetField(field);
            if (info == null) return; //字段不存在则跳过(防御未来改名)

            var hint = new Label
            {
                Text = needRestart ? "重启生效" : "立即",
                ForeColor = needRestart ? Color.Chocolate : Color.Green,
                AutoSize = true,
                Margin = new Padding(12, 6, 4, 4),
            };

            Control editor;
            if (field == "Language")
            {
                _languageBox.Items.Clear();
                _languageBox.Items.AddRange(new object[] { "Chinese", "English" });
                _languageBox.DropDownStyle = ComboBoxStyle.DropDownList;
                _languageBox.Width = 200;
                var cur = info.GetValue(null) as string ?? "Chinese";
                _languageBox.SelectedItem = cur == "English" ? "English" : "Chinese";
                editor = _languageBox;
            }
            else if (info.FieldType == typeof(bool))
            {
                editor = new CheckBox { Checked = (bool)info.GetValue(null), AutoSize = true };
            }
            else if (info.FieldType == typeof(string))
            {
                editor = new TextBox { Text = info.GetValue(null) as string ?? "", Width = 200 };
            }
            else
            {
                //数值类型: 按字段类型给范围
                var num = new NumericUpDown { Width = 200 };
                decimal v = Convert.ToDecimal(info.GetValue(null));
                if (info.FieldType == typeof(byte)) { num.Minimum = 0; num.Maximum = 255; }
                else if (info.FieldType == typeof(short)) { num.Minimum = short.MinValue; num.Maximum = short.MaxValue; }
                else if (info.FieldType == typeof(ushort)) { num.Minimum = 0; num.Maximum = ushort.MaxValue; }
                else if (info.FieldType == typeof(uint)) { num.Minimum = 0; num.Maximum = uint.MaxValue; }
                else if (info.FieldType == typeof(long)) { num.Minimum = long.MinValue; num.Maximum = long.MaxValue; }
                else { num.Minimum = int.MinValue; num.Maximum = int.MaxValue; }
                num.Value = Math.Max(num.Minimum, Math.Min(num.Maximum, v));
                editor = num;
            }

            _rows.Add(new Row { Label = label, Field = field, NeedRestart = needRestart, Editor = editor, Info = info });

            grid.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(4, 6, 4, 4) }, 0, row);
            grid.Controls.Add(editor, 1, row);
            grid.Controls.Add(hint, 2, row);
        }

        private bool Save()
        {
            try
            {
                foreach (var r in _rows)
                {
                    object value;
                    if (r.Editor is CheckBox cb) value = cb.Checked;
                    else if (r.Editor is TextBox tb) value = tb.Text.Trim();
                    else if (r.Editor is ComboBox combo) value = combo.SelectedItem.ToString() ?? "Chinese";
                    else if (r.Editor is NumericUpDown num) value = Convert.ChangeType(num.Value, r.Info.FieldType);
                    else continue;

                    r.Info.SetValue(null, value);
                }

                Settings.Save();
                MessageBox.Show(this,
                    "已保存到 Setup.ini。\n标注[重启生效]的项目需重启服务器后生效。",
                    "高级设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存失败: " + ex.Message, "高级设置", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void DoReboot()
        {
            if (!SMain.Envir.Running)
            {
                MessageBox.Show(this, "服务器当前未运行, 下次启动将自动使用新配置。", "高级设置",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this,
                    "重启会断开所有在线玩家(数据自动保存), 并用新配置启动。\n确定重启?",
                    "保存并重启", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            Close();
            SMain.Envir.Reboot();
        }
    }
}
