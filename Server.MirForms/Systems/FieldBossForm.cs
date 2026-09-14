using Server.MirDatabase;
using Server.MirEnvir;

namespace Server
{
    public partial class FieldBossForm : Form
    {
        public Envir Envir => SMain.Envir;

        private List<FieldBossInfo> InfoList => Envir.FieldBossInfoList;

        private System.Windows.Forms.Timer StatusTimer;

        public FieldBossForm()
        {
            InitializeComponent();

            //服务器未运行(或首次)时以 txt 为准, 避免旧内存数据覆盖手改的文件
            if (!Envir.Running || InfoList.Count == 0)
                Envir.FieldBossInfoList = FieldBossLoader.Load();

            FilterMonsters(string.Empty);

            foreach (MapInfo map in Envir.MapInfoList.OrderBy(x => x.FileName))
                MapNameComboBox.Items.Add(new MapListItem(map));

            StatusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            StatusTimer.Tick += (s, e) => UpdateStatusColumn();
            StatusTimer.Start();
        }

        private void MonsterSearchTextBox_TextChanged(object sender, EventArgs e)
        {
            FilterMonsters(MonsterSearchTextBox.Text);
        }

        private void FilterMonsters(string keyword)
        {
            keyword = keyword == null ? string.Empty : keyword.Trim();

            MonsterNameComboBox.Items.Clear();

            IEnumerable<MonsterInfo> query = Envir.MonsterInfoList;
            if (keyword.Length > 0)
                query = query.Where(x => x.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (MonsterInfo monster in query.OrderBy(x => x.Name))
                MonsterNameComboBox.Items.Add(new MonsterListItem(monster));
        }

        private class MonsterListItem
        {
            public MonsterInfo Info;

            public MonsterListItem(MonsterInfo info) { Info = info; }

            public override string ToString()
            {
                return Info.Name;
            }
        }

        private class MapListItem
        {
            public MapInfo Info;

            public MapListItem(MapInfo info) { Info = info; }

            public override string ToString()
            {
                return string.Format("{0} ({1})", Info.FileName, Info.Title);
            }
        }

        private void FieldBossForm_Load(object sender, EventArgs e)
        {
            RefreshList();
        }

        private FieldBossInfo SelectedInfo()
        {
            if (BossListView.SelectedIndices.Count == 0) return null;
            int index = BossListView.SelectedIndices[0];

            if (index < 0 || index >= InfoList.Count) return null;

            return InfoList[index];
        }

        private void RefreshList()
        {
            int selectedIndex = BossListView.SelectedIndices.Count > 0 ? BossListView.SelectedIndices[0] : -1;

            BossListView.BeginUpdate();
            BossListView.Items.Clear();

            foreach (FieldBossInfo info in InfoList)
            {
                ListViewItem item = new ListViewItem(info.Enabled ? "启用" : "停用");
                item.SubItems.Add(info.MonsterName);
                item.SubItems.Add(info.MapFileName);
                item.SubItems.Add(info.Location.X < 0 || info.Location.Y < 0 ? "随机" : info.Location.X + "," + info.Location.Y);
                item.SubItems.Add(info.SpawnTimes);
                item.SubItems.Add("-");

                if (!info.Enabled)
                    item.ForeColor = Color.Gray;

                BossListView.Items.Add(item);
            }

            BossListView.EndUpdate();

            if (selectedIndex >= 0 && selectedIndex < BossListView.Items.Count)
                BossListView.Items[selectedIndex].Selected = true;

            UpdateStatusColumn();
        }

        private void UpdateStatusColumn()
        {
            FieldBossSystem system = Envir.FieldBossSystem;

            for (int i = 0; i < BossListView.Items.Count && i < InfoList.Count; i++)
            {
                FieldBossInfo info = InfoList[i];
                string status;

                if (!info.Enabled)
                {
                    status = "已停用";
                }
                else if (system == null || !Envir.Running)
                {
                    status = "服务器未运行";
                }
                else
                {
                    FieldBossEntry entry = null;
                    for (int j = 0; j < system.Entries.Count; j++)
                        if (system.Entries[j].Info == info) { entry = system.Entries[j]; break; }

                    if (entry != null && entry.CurrentBoss != null && !entry.CurrentBoss.Dead)
                        status = "存活中 (" + entry.CurrentBoss.CurrentMap.Info.Title + ")";
                    else
                    {
                        DateTime? next = entry != null ? system.GetNextSpawnAt(entry) : null;
                        status = next.HasValue ? "下刷 " + next.Value.ToString("HH:mm") : "无时间点";
                    }
                }

                if (BossListView.Items[i].SubItems[5].Text != status)
                    BossListView.Items[i].SubItems[5].Text = status;
            }
        }

        private void BossListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();

            EditGroupBox.Enabled = info != null;
            SpawnNowButton.Enabled = info != null;
            DeleteButton.Enabled = info != null;

            if (info == null) return;

            EnableCheckBox.Checked = info.Enabled;
            MonsterNameComboBox.Text = ResolveMonsterDisplay(info.MonsterName);
            MapNameComboBox.Text = ResolveMapDisplay(info.MapFileName);
            XTextBox.Text = info.Location.X.ToString();
            YTextBox.Text = info.Location.Y.ToString();
            SpawnTimesDisplay.Text = info.SpawnTimes;
            TimePicker.Value = DateTime.Now;
        }

        private string ResolveMonsterDisplay(string name)
        {
            MonsterInfo monster = Envir.MonsterInfoList.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            return monster != null ? monster.Name : name;
        }

        private string ResolveMapDisplay(string fileName)
        {
            MapInfo map = Envir.MapInfoList.FirstOrDefault(x => string.Equals(x.FileName, fileName, StringComparison.OrdinalIgnoreCase));
            return map != null ? string.Format("{0} ({1})", map.FileName, map.Title) : fileName;
        }

        private void EnableCheckBox_CheckStateChanged(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            info.Enabled = EnableCheckBox.Checked;

            int index = InfoList.IndexOf(info);
            if (index >= 0 && index < BossListView.Items.Count)
            {
                BossListView.Items[index].SubItems[0].Text = info.Enabled ? "启用" : "停用";
                BossListView.Items[index].ForeColor = info.Enabled ? SystemColors.WindowText : Color.Gray;
            }
        }

        private void MonsterNameComboBox_TextChanged(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            string text = MonsterNameComboBox.Text.Trim();
            MonsterInfo monster = Envir.MonsterInfoList.FirstOrDefault(x => string.Equals(x.Name, text, StringComparison.OrdinalIgnoreCase));

            info.MonsterName = monster != null ? monster.Name : text;

            int index = InfoList.IndexOf(info);
            if (index >= 0 && index < BossListView.Items.Count)
                BossListView.Items[index].SubItems[1].Text = info.MonsterName;
        }

        private void MapNameComboBox_TextChanged(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            string text = MapNameComboBox.Text.Trim();
            MapInfo map = Envir.MapInfoList.FirstOrDefault(x => string.Equals(x.FileName, text, StringComparison.OrdinalIgnoreCase))
                ?? Envir.MapInfoList.FirstOrDefault(x => string.Format("{0} ({1})", x.FileName, x.Title) == text);

            info.MapFileName = map != null ? map.FileName : text;

            int index = InfoList.IndexOf(info);
            if (index >= 0 && index < BossListView.Items.Count)
                BossListView.Items[index].SubItems[2].Text = info.MapFileName;
        }

        private void XTextBox_TextChanged(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            int temp;
            if (!int.TryParse(XTextBox.Text, out temp))
            {
                XTextBox.BackColor = Color.Red;
                return;
            }
            XTextBox.BackColor = SystemColors.Window;

            info.Location.X = temp;

            int index = InfoList.IndexOf(info);
            if (index >= 0 && index < BossListView.Items.Count)
                BossListView.Items[index].SubItems[3].Text = info.Location.X < 0 || info.Location.Y < 0 ? "随机" : info.Location.X + "," + info.Location.Y;
        }

        private void YTextBox_TextChanged(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            int temp;
            if (!int.TryParse(YTextBox.Text, out temp))
            {
                YTextBox.BackColor = Color.Red;
                return;
            }
            YTextBox.BackColor = SystemColors.Window;

            info.Location.Y = temp;

            int index = InfoList.IndexOf(info);
            if (index >= 0 && index < BossListView.Items.Count)
                BossListView.Items[index].SubItems[3].Text = info.Location.X < 0 || info.Location.Y < 0 ? "随机" : info.Location.X + "," + info.Location.Y;
        }

        private void UpdateSpawnTimes(FieldBossInfo info)
        {
            SpawnTimesDisplay.Text = info.SpawnTimes;

            int index = InfoList.IndexOf(info);
            if (index >= 0 && index < BossListView.Items.Count)
                BossListView.Items[index].SubItems[4].Text = info.SpawnTimes;
        }

        private void AddTimeButton_Click(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            string time = TimePicker.Value.ToString("HH:mm");

            List<TimeSpan> times = FieldBossInfo.ParseTimes(info.SpawnTimes);
            TimeSpan t = new TimeSpan(TimePicker.Value.Hour, TimePicker.Value.Minute, 0);

            if (!times.Contains(t)) times.Add(t);
            times.Sort();

            List<string> parts = new List<string>();
            foreach (TimeSpan ts in times)
                parts.Add(((int)ts.TotalHours).ToString("00") + ":" + ts.Minutes.ToString("00"));

            info.SpawnTimes = string.Join(",", parts);

            UpdateSpawnTimes(info);
        }

        private void ClearTimesButton_Click(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            info.SpawnTimes = string.Empty;

            UpdateSpawnTimes(info);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            FieldBossInfo info = new FieldBossInfo
            {
                Enabled = true,
                MonsterName = "NewBoss",
                MapFileName = "3",
                Location = new Point(-1, -1),
                SpawnTimes = "12:00,20:00",
            };

            InfoList.Add(info);
            RefreshList();
            BossListView.Items[InfoList.Count - 1].Selected = true;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            InfoList.Remove(info);
            Envir.FieldBossSystem?.SyncEntries(InfoList);
            RefreshList();
        }

        private void SpawnNowButton_Click(object sender, EventArgs e)
        {
            FieldBossInfo info = SelectedInfo();
            if (info == null) return;

            if (!Envir.Running)
            {
                MessageBox.Show("服务器必须在运行状态才能立即刷新。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (Envir.FieldBossSystem == null)
                Envir.FieldBossSystem = new FieldBossSystem(InfoList);

            Envir.FieldBossSystem.SyncEntries(InfoList);

            FieldBossEntry entry = null;
            for (int i = 0; i < Envir.FieldBossSystem.Entries.Count; i++)
                if (Envir.FieldBossSystem.Entries[i].Info == info) { entry = Envir.FieldBossSystem.Entries[i]; break; }

            if (entry == null)
            {
                MessageBox.Show("未找到对应的Boss配置。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            bool ok = Envir.FieldBossSystem.SpawnNow(entry);
            string msg = ok
                ? string.Format("野外Boss [{0}] 刷新成功！", info.MonsterName)
                : string.Format("野外Boss [{0}] 刷新失败, 请检查怪物名/地图/坐标是否正确。", info.MonsterName);

            MessageBox.Show(msg, "提示", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Asterisk : MessageBoxIcon.Hand);

            UpdateStatusColumn();
        }

        private void Save()
        {
            try
            {
                FieldBossLoader.Save(InfoList);
                Envir.FieldBossSystem?.SyncEntries(InfoList);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存 FieldBoss.txt 失败: " + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }

        private void FieldBossForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            StatusTimer.Stop();
            Save();
        }
    }
}
