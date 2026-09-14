using CustomFormControl;
using Server.Account;
using Server.Database;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirForms.Systems;
using Server.MirObjects;
using Server.Systems;
using System.Collections;

namespace Server
{
    public partial class SMain : Form
    {
        public static Envir Envir => Envir.Main;

        public static Envir EditEnvir => Envir.Edit;

        protected static MessageQueue MessageQueue => MessageQueue.Instance;

        public SMain()
        {
            InitializeComponent();

            AutoResize();

            //自定义技能管理入口: 挂到"高级设置"所在菜单(先定位再添加, 避免遍历中修改集合)
            ToolStripMenuItem advancedMenu = null;
            foreach (ToolStripItem topItem in MainMenu.Items)
            {
                if (topItem is not ToolStripMenuItem topMenu) continue;
                foreach (ToolStripItem child in topMenu.DropDownItems)
                {
                    if (child.Name != "advancedConfigToolStripMenuItem") continue;
                    advancedMenu = topMenu;
                    break;
                }
                if (advancedMenu != null) break;
            }
            advancedMenu?.DropDownItems.Add(new ToolStripMenuItem("自定义技能管理", null, customSkillToolStripMenuItem_Click) { Name = "customSkillToolStripMenuItem" });
        }

        private void customSkillToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CustomSkillForm form = new CustomSkillForm();
            form.ShowDialog();
        }

        private void AutoResize()
        {
            int columnCount = PlayersOnlineListView.Columns.Count;

            foreach (ColumnHeader column in PlayersOnlineListView.Columns)
            {
                column.Width = PlayersOnlineListView.Width / (columnCount - 1) - 1;
            }

            indexHeader.Width = 2;
        }

        public class ListViewItemComparer : IComparer // For Players Online tab level sorting
        {
            private int col;
            private SortOrder order;

            public ListViewItemComparer(int column, SortOrder order)
            {
                col = column;
                this.order = order;
            }

            public int Compare(object x, object y)
            {
                ListViewItem itemX = x as ListViewItem;
                ListViewItem itemY = y as ListViewItem;

                string stringX = itemX?.SubItems[col].Text ?? "";
                string stringY = itemY?.SubItems[col].Text ?? "";

                int result;

                if (col == 2)
                {
                    int intX = 0, intY = 0;
                    int.TryParse(stringX, out intX);
                    int.TryParse(stringY, out intY);

                    result = intX.CompareTo(intY);
                }
                else
                {
                    result = String.Compare(stringX, stringY);
                }

                if (order == SortOrder.Descending)
                    result = -result;

                return result;
            }
        }

        public static void Enqueue(Exception ex)
        {
            MessageQueue.Enqueue(ex);
        }

        public static void EnqueueDebugging(string msg)
        {
            MessageQueue.EnqueueDebugging(msg);
        }

        public static void EnqueueChat(string msg)
        {
            MessageQueue.EnqueueChat(msg);
        }

        public static void Enqueue(string msg)
        {
            MessageQueue.Enqueue(msg);
        }

        private void configToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void InterfaceTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                //生命周期状态刷新(轮询, 兼容重启/异常退出等各种状态变化)
                UpdateLifecycleButtons();

                //启动失败弹窗: 原先只写一行日志, 用户盯着面板毫无察觉
                if (Envir.LastStartError != null)
                {
                    var err = Envir.LastStartError;
                    Envir.LastStartError = null;
                    MessageBox.Show(this, "服务器启动失败:\n\n" + err, "启动失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                Text = $"总量: {Envir.LastCount}, 实际: {Envir.LastRealCount}";
                PlayersLabel.Text = $"玩家: {Envir.Players.Count}";
                MonsterLabel.Text = $"怪物: {Envir.MonsterCount}";
                ConnectionsLabel.Text = $"连接数: {Envir.Connections.Count}";
                BlockedIPsLabel.Text = $"屏蔽IP: {Envir.IPBlocks.Count(x => x.Value > Envir.Now)}";
                UpTimeLabel.Text = $"运行: {Envir.Stopwatch.ElapsedMilliseconds / 1000 / 60 / 60 / 24}天{Envir.Stopwatch.ElapsedMilliseconds / 1000 / 60 / 60 % 24}时{Envir.Stopwatch.ElapsedMilliseconds / 1000 / 60 % 60}分{Envir.Stopwatch.ElapsedMilliseconds / 1000 % 60}秒";

                if (Settings.Multithreaded && (Envir.MobThreads != null))
                {
                    CycleDelayLabel.Text = $"循环耗时: {Envir.LastRunTime:0000}";
                    for (int i = 0; i < Envir.MobThreads.Length; i++)
                    {
                        if (Envir.MobThreads[i] == null) break;
                        CycleDelayLabel.Text = CycleDelayLabel.Text + $"|{Envir.MobThreads[i].LastRunTime:0000}";

                    }
                }
                else
                    CycleDelayLabel.Text = $"循环耗时: {Envir.LastRunTime}";

                while (!MessageQueue.MessageLog.IsEmpty)
                {
                    string message;

                    if (!MessageQueue.MessageLog.TryDequeue(out message)) continue;

                    LogTextBox.AppendText(message);
                }

                while (!MessageQueue.DebugLog.IsEmpty)
                {
                    string message;

                    if (!MessageQueue.DebugLog.TryDequeue(out message)) continue;

                    DebugLogTextBox.AppendText(message);
                }

                while (!MessageQueue.ChatLog.IsEmpty)
                {
                    string message;

                    if (!MessageQueue.ChatLog.TryDequeue(out message)) continue;

                    ChatLogTextBox.AppendText(message);
                }

                ProcessPlayersOnlineTab(false);
                ProcessGuildViewTab(false);
                ProcessScheduledAnnouncements();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private ListViewItem CreateListView(CharacterInfo character)
        {
            ListViewItem ListItem = new ListViewItem(character.Index.ToString()) { Tag = character };

            ListItem.SubItems.Add(character.Name);
            ListItem.SubItems.Add(character.Level.ToString());
            ListItem.SubItems.Add(character.Class.ToString());
            ListItem.SubItems.Add(character.Gender.ToString());

            string mapName = MapInfo.GetMapTitleByIndex(character.CurrentMapIndex);
            ListItem.SubItems.Add($"{mapName}");

            return ListItem;
        }

        private void ProcessPlayersOnlineTab(bool forced = false)
        {
            if (PlayersOnlineListView.Items.Count != Envir.Players.Count || forced == true)
            {
                PlayersOnlineListView.Items.Clear();

                for (int i = PlayersOnlineListView.Items.Count; i < Envir.Players.Count; i++)
                {
                    CharacterInfo character = Envir.Players[i].Info;

                    ListViewItem tempItem = CreateListView(character);

                    PlayersOnlineListView.Items.Add(tempItem);
                }
            }
        }

        private void startServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Envir.Running)
            {
                Enqueue("服务器已在运行中, 无需再次启动。");
                return;
            }

            Envir.Start();
            UpdateLifecycleButtons("启动中...");
        }

        private void stopServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Envir.Running || _lifecycleBusy) return;

            _lifecycleBusy = true;
            UpdateLifecycleButtons("停止中(正在保存数据)...");

            //异步停止: 保存玩家/行会/攻城数据可能耗时数秒, 不能卡死面板UI线程
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Envir.Stop();
                    Envir.MonsterCount = 0;
                }
                finally
                {
                    BeginInvoke(new Action(() =>
                    {
                        _lifecycleBusy = false;
                        UpdateLifecycleButtons();
                    }));
                }
            });
        }

        private void SMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            //关闭前同步停止并保存(账号/行会/攻城落盘后进程才退出), 这里保持阻塞是刻意的
            Envir.Stop();
        }

        private void closeServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private bool _lifecycleBusy; //异步停止进行中

        /// <summary>按服务器实际状态刷新 启动/停止/重启 按钮的可用性(由界面定时器轮询调用)</summary>
        private void UpdateLifecycleButtons(string busyText = null)
        {
            if (_lifecycleBusy)
            {
                startServerToolStripMenuItem.Enabled = false;
                stopServerToolStripMenuItem.Enabled = false;
                rebootServerToolStripMenuItem.Enabled = false;
                return;
            }

            startServerToolStripMenuItem.Enabled = !Envir.Running;
            stopServerToolStripMenuItem.Enabled = Envir.Running;
            rebootServerToolStripMenuItem.Enabled = Envir.Running;

            if (busyText != null) Enqueue(busyText);
        }

        private void itemInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemInfoForm form = new ItemInfoForm();

            form.ShowDialog();
        }

        private void monsterInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MonsterInfoForm form = new MonsterInfoForm();

            form.ShowDialog();
        }

        private void nPCInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NPCInfoForm form = new NPCInfoForm();

            form.ShowDialog();
        }

        private void balanceConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalanceConfigForm form = new BalanceConfigForm();

            form.ShowDialog();
        }

        private void questInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            QuestInfoForm form = new QuestInfoForm();

            form.ShowDialog();
        }

        private void serverToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConfigForm form = new ConfigForm();

            form.ShowDialog();
        }

        private void advancedConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AdvancedConfigForm form = new AdvancedConfigForm();
            form.ShowDialog();
        }

        private void talentCenterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TalentCenterForm form = new TalentCenterForm();
            form.ShowDialog();
        }

        private void battleFieldToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BattleFieldForm form = new BattleFieldForm();
            form.ShowDialog();
        }

        private void diagnosticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DiagnosticsForm form = new DiagnosticsForm();
            form.ShowDialog();
        }

        private void balanceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BalanceConfigForm form = new BalanceConfigForm();

            form.ShowDialog();
        }

        private void accountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AccountInfoForm form = new AccountInfoForm();

            form.ShowDialog();
        }

        private void mapInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MapInfoForm form = new MapInfoForm();

            form.ShowDialog();
        }

        private void itemInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            ItemInfoForm form = new ItemInfoForm();

            form.ShowDialog();
        }

        private void monsterInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            MonsterInfoForm form = new MonsterInfoForm();

            form.ShowDialog();
        }

        private void nPCInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            NPCInfoForm form = new NPCInfoForm();

            form.ShowDialog();
        }

        private void questInfoToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            QuestInfoForm form = new QuestInfoForm();

            form.ShowDialog();
        }

        private void dragonSystemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DragonInfoForm form = new DragonInfoForm();

            form.ShowDialog();
        }

        private void fieldBossSystemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FieldBossForm form = new FieldBossForm();

            form.ShowDialog();
        }

        private void miningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MiningInfoForm form = new MiningInfoForm();

            form.ShowDialog();
        }

        private void guildsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GuildInfoForm form = new GuildInfoForm();

            form.ShowDialog();
        }

        private void fishingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(0);

            form.ShowDialog();
        }

        private void GlobalMessageButton_Click(object sender, EventArgs e)
        {
            if (GlobalMessageTextBox.Text.Length < 1) return;

            foreach (var player in Envir.Players)
            {
                player.ReceiveChat(GlobalMessageTextBox.Text, ChatType.Announcement);
            }

            EnqueueChat(GlobalMessageTextBox.Text);
            GlobalMessageTextBox.Text = string.Empty;
        }

        private void PlayersOnlineListView_DoubleClick(object sender, EventArgs e)
        {
            CustomFormControl.ListViewNF list = (CustomFormControl.ListViewNF)sender;

            if (list.SelectedItems.Count > 0)
            {
                ListViewItem item = list.SelectedItems[0];
                string index = item.SubItems[0].Text;

                PlayerInfoForm form = new PlayerInfoForm(Convert.ToUInt32(index));

                form.ShowDialog();
            }
        }

        private void PlayersOnlineListView_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            e.Cancel = true;
            e.NewWidth = PlayersOnlineListView.Columns[e.ColumnIndex].Width;
        }

        private void mailToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(1);

            form.ShowDialog();
        }

        private void gmMailRewardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Envir.Running)
            {
                MessageBox.Show("服务器必须在运行状态才能使用 GM 奖励邮件。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            MailRewardForm form = new MailRewardForm();

            form.ShowDialog();
        }

        private void goodsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(2);

            form.ShowDialog();
        }

        private void relationshipToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(4);

            form.ShowDialog();
        }

        private void refiningToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(3);

            form.ShowDialog();
        }

        private void mentorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(5);

            form.ShowDialog();
        }

        private void magicInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MagicInfoForm form = new MagicInfoForm();
            form.ShowDialog();
        }

        private void SMain_Load(object sender, EventArgs e)
        {
            var loaded = EditEnvir.LoadDB();

            if (loaded)
            {
                Envir.Start();
            }

            ScheduledAnnouncementManager.Load();
            RefreshScheduledList();
            RepeatComboBox.SelectedIndex = 0;

            AutoResize();
        }

        private void gemToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(6);

            form.ShowDialog();
        }

        private void conquestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ConquestInfoForm form = new ConquestInfoForm();

            form.ShowDialog();
        }

        private void rebootServerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Envir.Running)
            {
                Enqueue("服务器未在运行, 直接点'启动'即可。");
                return;
            }

            if (MessageBox.Show(this,
                    "重启会断开所有在线玩家(数据自动保存), 并重新读取 Setup.ini 配置。\n确定重启?",
                    "重启服务器", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            Envir.Reboot();
        }

        private void reloadCenterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReloadCenterForm form = new ReloadCenterForm();
            form.Show(this);
        }

        private void respawnsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(7);

            form.ShowDialog();
        }

        private void monsterTunerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!SMain.Envir.Running)
            {
                MessageBox.Show("服务器必须在运行状态才能调谐怪物", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            MonsterTunerForm form = new MonsterTunerForm();

            form.ShowDialog();
        }

        private void gameshopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GameShop form = new GameShop();
            form.ShowDialog();
        }

        private void itemNEWToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemInfoFormNew form = new ItemInfoFormNew();

            form.ShowDialog();
        }

        private void itemMgrToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ItemMgrForm form = new ItemMgrForm();

            form.ShowDialog();
        }

        private void monsterExperimentalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MonsterInfoFormNew form = new MonsterInfoFormNew();

            form.ShowDialog();
        }

        private void dropBuilderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MirForms.DropBuilder.DropGenForm GenForm = new MirForms.DropBuilder.DropGenForm();

            GenForm.ShowDialog();
        }

        private void clearBlockedIPsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.IPBlocks.Clear();
        }

        private void nPCsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadNPCs();
        }

        private void dropsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadDrops();
        }

        private void lineMessageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Envir.ReloadLineMessages();
        }

        #region Guild View Tab
        public void ProcessGuildViewTab(bool forced = false)
        {
            if (GuildListView.Items.Count != Envir.GuildList.Count || forced == true)
            {
                GuildListView.Items.Clear();

                foreach (GuildInfo guild in Envir.GuildList)
                {
                    ListViewItem tempItem = new ListViewItem(guild.GuildIndex.ToString()) { Tag = this };

                    tempItem.SubItems.Add(guild.Name);

                    if (guild.Ranks.Count > 0 && guild.Ranks[0].Members.Count > 0)
                    {
                        tempItem.SubItems.Add(guild.Ranks[0].Members[0].Name);
                    }
                    else
                    {
                        tempItem.SubItems.Add("已解散");
                        tempItem.ForeColor = Color.Red;
                    }

                    tempItem.SubItems.Add($"{guild.Membercount}/{guild.MemberCap}");
                    tempItem.SubItems.Add(guild.Level.ToString());
                    tempItem.SubItems.Add($"{guild.Gold}");
                    tempItem.SubItems.Add(guild.HasGT ? guild.GTRent.ToString() : "无");

                    GuildListView.Items.Add(tempItem);
                }
            }
        }

        private void GuildListView_DoubleClick(object sender, EventArgs e)
        {
            ListViewNF list = (ListViewNF)sender;

            if (list.SelectedItems.Count <= 0) return;

            ListViewItem item = list.SelectedItems[0];
            int index = Int32.Parse(item.Text);

            GuildObject Guild = Envir.GetGuild(index);
            GuildItemForm form = new GuildItemForm
            {
                GuildName = Guild.Name,
                Guild = Guild,
                main = this,
            };

            form.SetMemberCount(Guild.Info.Membercount, Guild.Info.MemberCap);
            form.SetGuildNotice(Guild.Info.Notice);
            form.SetBuffList(Guild.Info.BuffList, Settings.Guild_BuffList);
            form.SetGuildPoints(Guild.Info.SparePoints);
            form.SetGuildExperience(Guild.Info.Experience);

            if (Guild == null) return;

            foreach (var i in Guild.StoredItems)
            {
                if (i == null) continue;
                ListViewItem tempItem = new ListViewItem(i.Item.UniqueID.ToString()) { Tag = this };

                CharacterInfo character = Envir.GetCharacterInfo((int)i.UserId);
                if (character != null)
                    tempItem.SubItems.Add(character.Name);
                else if (i.UserId == -1)
                    tempItem.SubItems.Add("系统");
                else
                    tempItem.SubItems.Add("未知");

                tempItem.SubItems.Add(i.Item.FriendlyName);
                tempItem.SubItems.Add(i.Item.Count.ToString());
                tempItem.SubItems.Add(i.Item.CurrentDura + "/" + i.Item.MaxDura);

                form.GuildItemListView.Items.Add(tempItem);
            }

            foreach (var r in Guild.Ranks)
                foreach (var m in r.Members)
                {
                    ListViewItem tempItem = new ListViewItem(m.Name) { Tag = this };
                    tempItem.SubItems.Add(r.Name);
                    form.MemberListView.Items.Add(tempItem);
                }
            form.SetGuildRanks(Guild.Ranks);

            form.ShowDialog();
        }
        #endregion

        private void MainTabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            ProcessPlayersOnlineTab(true);
            ProcessGuildViewTab(true);
        }

        private void heroesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SystemInfoForm form = new SystemInfoForm(8);

            form.ShowDialog();
        }

        private void CharacterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CharacterInfoForm form = new CharacterInfoForm();

            form.ShowDialog();
        }

        private void recipeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RecipeInfoForm form = new RecipeInfoForm();

            form.ShowDialog();
        }

        private void accountsToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            AccountInfoForm form = new AccountInfoForm();

            form.ShowDialog();
        }

        private void marketToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Market form = new Market();

            form.ShowDialog();
        }

        private void namelistsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Namelists form = new Namelists();

            form.ShowDialog();
        }

        private int sortColumn = -1;
        private void PlayersOnlineListView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != sortColumn)
            {
                sortColumn = e.Column;
                PlayersOnlineListView.Sorting = SortOrder.Ascending;
            }
            else
            {
                PlayersOnlineListView.Sorting =
                    PlayersOnlineListView.Sorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }

            PlayersOnlineListView.ListViewItemSorter = new ListViewItemComparer(sortColumn, PlayersOnlineListView.Sorting);

            PlayersOnlineListView.Sort();
        }

        #region Scheduled Announcements 定时公告

        private void RefreshScheduledList()
        {
            ScheduledListView.BeginUpdate();
            ScheduledListView.Items.Clear();

            foreach (var item in ScheduledAnnouncementManager.Items)
            {
                ListViewItem tempItem = new ListViewItem(item.Message) { Tag = item };

                tempItem.SubItems.Add(item.NextRun.ToString("yyyy-MM-dd HH:mm:ss"));
                tempItem.SubItems.Add(item.RepeatText);
                tempItem.SubItems.Add(item.Repeat == ScheduledAnnouncementRepeat.Interval ? item.IntervalMinutes.ToString() : "-");

                ScheduledListView.Items.Add(tempItem);
            }

            ScheduledListView.EndUpdate();
        }

        private void BroadcastAnnouncement(string message)
        {
            foreach (var player in Envir.Players)
            {
                player.ReceiveChat(message, ChatType.Announcement);
            }

            EnqueueChat(message);
        }

        private void ProcessScheduledAnnouncements()
        {
            if (!Envir.Running) return;

            var fired = ScheduledAnnouncementManager.ProcessDue(DateTime.Now);

            foreach (var item in fired)
            {
                BroadcastAnnouncement(item.Message);
                Enqueue($"[定时公告] 已发送: {item.Message}");
            }

            if (fired.Count > 0)
                RefreshScheduledList();
        }

        private void AddScheduledButton_Click(object sender, EventArgs e)
        {
            var message = ScheduledMessageTextBox.Text.Trim();

            if (message.Length < 1)
            {
                MessageBox.Show("请输入公告内容。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            var repeat = (ScheduledAnnouncementRepeat)RepeatComboBox.SelectedIndex;
            var nextRun = ScheduledTimePicker.Value;

            if (repeat == ScheduledAnnouncementRepeat.Once && nextRun <= DateTime.Now)
            {
                MessageBox.Show("单次公告的发送时间必须晚于当前时间。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            if (repeat == ScheduledAnnouncementRepeat.Daily)
                nextRun = DateTime.Today.Add(nextRun.TimeOfDay) <= DateTime.Now
                    ? DateTime.Today.AddDays(1).Add(nextRun.TimeOfDay)
                    : DateTime.Today.Add(nextRun.TimeOfDay);

            var item = new ScheduledAnnouncement
            {
                Message = message,
                NextRun = nextRun,
                Repeat = repeat,
                IntervalMinutes = (int)IntervalNumeric.Value,
            };

            ScheduledAnnouncementManager.Items.Add(item);
            ScheduledAnnouncementManager.Save();
            RefreshScheduledList();

            ScheduledMessageTextBox.Text = string.Empty;

            Enqueue($"[定时公告] 已加入队列: {message} | 下次发送: {item.NextRun:yyyy-MM-dd HH:mm:ss} | {item.RepeatText}");
        }

        private void RemoveScheduledButton_Click(object sender, EventArgs e)
        {
            if (ScheduledListView.SelectedItems.Count < 1) return;

            foreach (ListViewItem sel in ScheduledListView.SelectedItems)
            {
                var item = sel.Tag as ScheduledAnnouncement;

                if (item == null) continue;

                ScheduledAnnouncementManager.Items.Remove(item);
            }

            ScheduledAnnouncementManager.Save();
            RefreshScheduledList();
        }

        private void SendNowScheduledButton_Click(object sender, EventArgs e)
        {
            if (ScheduledListView.SelectedItems.Count < 1) return;

            foreach (ListViewItem sel in ScheduledListView.SelectedItems)
            {
                var item = sel.Tag as ScheduledAnnouncement;

                if (item == null) continue;

                BroadcastAnnouncement(item.Message);
                Enqueue($"[定时公告] 手动立即发送: {item.Message}");
            }
        }

        private void RepeatComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            IntervalNumeric.Enabled = RepeatComboBox.SelectedIndex == (int)ScheduledAnnouncementRepeat.Interval;
        }

        #endregion
    }
}