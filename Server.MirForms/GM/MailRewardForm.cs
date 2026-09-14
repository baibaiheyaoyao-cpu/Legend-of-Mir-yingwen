using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace Server
{
    public sealed class MailRewardForm : Form
    {
        private const string GMSender = "GM";
        private const int MaxAttachItems = 40;
        private const int MaxMessageLength = 500;

        private sealed class AttachRow
        {
            public ItemInfo Info;
            public int Count;
        }

        private readonly List<AttachRow> _rows = new List<AttachRow>();
        private readonly List<ItemInfo> _filteredItems = new List<ItemInfo>();

        private TextBox _playerNameBox;
        private Label _playerHintLabel;
        private ComboBox _onlineCombo;
        private TextBox _messageBox;
        private TextBox _goldBox;
        private TextBox _itemSearchBox;
        private ListBox _itemListBox;
        private TextBox _itemCountBox;
        private ListView _attachmentList;
        private Label _attachmentHintLabel;

        public MailRewardForm()
        {
            Text = "GM 奖励邮件";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(590, 700);
            Font = new Font("Microsoft YaHei UI", 9F);

            BuildUi();

            RefreshOnlinePlayers();
            RefreshItemSearch();
        }

        private void BuildUi()
        {
            SuspendLayout();

            var recipientGroup = new GroupBox { Text = "收件人（可发给在线或离线角色）", Location = new Point(14, 14), Size = new Size(562, 90) };

            AddLabel(recipientGroup, "角色名：", new Point(12, 26));
            _playerNameBox = AddTextBox(recipientGroup, new Point(66, 22), new Size(160, 24), string.Empty);
            _playerNameBox.TextChanged += (s, e) => _playerHintLabel.Text = string.Empty;

            var checkButton = AddButton(recipientGroup, "校验", new Point(234, 21), new Size(56, 26));
            checkButton.Click += CheckPlayerButton_Click;

            AddLabel(recipientGroup, "在线玩家：", new Point(308, 26));
            _onlineCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(372, 22), Size = new Size(176, 26) };
            _onlineCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_onlineCombo.SelectedItem is CharacterInfo info)
                    _playerNameBox.Text = info.Name;
            };
            recipientGroup.Controls.Add(_onlineCombo);

            _playerHintLabel = AddLabel(recipientGroup, string.Empty, new Point(12, 58), new Size(540, 20));

            var contentGroup = new GroupBox { Text = "邮件内容", Location = new Point(14, 112), Size = new Size(562, 128) };

            AddLabel(contentGroup, "留言：", new Point(12, 22));
            _messageBox = new TextBox { Location = new Point(66, 18), Size = new Size(480, 60), Multiline = true, ScrollBars = ScrollBars.Vertical };
            _messageBox.TextChanged += (s, e) =>
            {
                if (_messageBox.Text.Length > MaxMessageLength)
                    _messageBox.Text = _messageBox.Text.Substring(0, MaxMessageLength);
            };
            contentGroup.Controls.Add(_messageBox);

            AddLabel(contentGroup, $"留言内容最多 {MaxMessageLength} 字。", new Point(66, 84));

            AddLabel(contentGroup, "金币：", new Point(12, 106));
            _goldBox = AddTextBox(contentGroup, new Point(66, 102), new Size(120, 24), "0");

            var itemGroup = new GroupBox { Text = "附带物品（堆叠物品自动按堆叠上限拆分）", Location = new Point(14, 248), Size = new Size(562, 388) };

            AddLabel(itemGroup, "物品搜索：", new Point(12, 22));
            _itemSearchBox = AddTextBox(itemGroup, new Point(78, 18), new Size(240, 24), string.Empty);
            _itemSearchBox.TextChanged += (s, e) => RefreshItemSearch();

            AddLabel(itemGroup, "数量：", new Point(340, 22));
            _itemCountBox = AddTextBox(itemGroup, new Point(380, 18), new Size(56, 24), "1");

            var addButton = AddButton(itemGroup, "添加", new Point(456, 17), new Size(92, 26));
            addButton.Click += AddItemButton_Click;

            _itemListBox = new ListBox { Location = new Point(12, 52), Size = new Size(536, 186) };
            itemGroup.Controls.Add(_itemListBox);

            _attachmentHintLabel = AddLabel(itemGroup, string.Empty, new Point(12, 248), new Size(300, 20));

            var removeButton = AddButton(itemGroup, "删除选中", new Point(340, 246), new Size(90, 26));
            removeButton.Click += RemoveItemButton_Click;

            var clearButton = AddButton(itemGroup, "清空清单", new Point(438, 246), new Size(110, 26));
            clearButton.Click += (s, e) => ClearAttachments();

            _attachmentList = new ListView { Location = new Point(12, 282), Size = new Size(536, 92), View = View.Details, FullRowSelect = true, HideSelection = false };
            _attachmentList.Columns.Add("物品名称", 400);
            _attachmentList.Columns.Add("数量", 90);
            itemGroup.Controls.Add(_attachmentList);

            var sendButton = AddButton(this, "发送邮件", new Point(438, 648), new Size(128, 36));
            sendButton.Click += SendButton_Click;

            Controls.Add(recipientGroup);
            Controls.Add(contentGroup);
            Controls.Add(itemGroup);

            ResumeLayout(false);
        }

        private void CheckPlayerButton_Click(object sender, EventArgs e)
        {
            string name = _playerNameBox.Text.Trim();
            if (name.Length == 0)
            {
                _playerHintLabel.ForeColor = Color.Red;
                _playerHintLabel.Text = "请输入角色名。";
                return;
            }

            CharacterInfo target = GetTarget(name);
            if (target == null)
            {
                _playerHintLabel.ForeColor = Color.Red;
                _playerHintLabel.Text = $"找不到角色“{name}”。";
                return;
            }

            _playerHintLabel.ForeColor = target.Player != null ? Color.Green : Color.DimGray;
            _playerHintLabel.Text = $"“{target.Name}” Lv.{target.Level} {target.Class} {target.Gender}，{(target.Player != null ? "在线" : "离线")}。";
        }

        private void AddItemButton_Click(object sender, EventArgs e)
        {
            if (_itemListBox.SelectedItem == null)
            {
                MessageBox.Show("请先在列表中选中要添加的物品。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int selectedIndex = _itemListBox.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _filteredItems.Count) return;

            ItemInfo info = _filteredItems[selectedIndex];

            if (!int.TryParse(_itemCountBox.Text.Trim(), out int count) || count < 1)
            {
                MessageBox.Show("数量必须是大于 0 的整数。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (AttachRow row in _rows)
            {
                if (row.Info.Index != info.Index) continue;
                row.Count += count;
                RefreshAttachments();
                return;
            }

            _rows.Add(new AttachRow { Info = info, Count = count });
            RefreshAttachments();
        }

        private void RemoveItemButton_Click(object sender, EventArgs e)
        {
            if (_attachmentList.SelectedItems.Count == 0) return;

            AttachRow row = _attachmentList.SelectedItems[0].Tag as AttachRow;
            if (row == null) return;

            _rows.Remove(row);
            RefreshAttachments();
        }

        private void RefreshItemSearch()
        {
            string filter = _itemSearchBox.Text.Trim();

            _filteredItems.Clear();

            foreach (ItemInfo info in SMain.Envir.ItemInfoList)
            {
                if (info == null || string.IsNullOrEmpty(info.Name)) continue;
                if (filter.Length > 0 && info.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                _filteredItems.Add(info);
            }

            _itemListBox.BeginUpdate();
            _itemListBox.Items.Clear();
            foreach (ItemInfo info in _filteredItems)
                _itemListBox.Items.Add(info.Name);
            _itemListBox.EndUpdate();

            if (_itemListBox.Items.Count > 0)
                _itemListBox.SelectedIndex = 0;
        }

        private void RefreshAttachments()
        {
            _attachmentList.BeginUpdate();
            _attachmentList.Items.Clear();

            int total = 0;
            foreach (AttachRow row in _rows)
            {
                var listItem = new ListViewItem(row.Info.Name) { Tag = row };
                listItem.SubItems.Add(row.Count.ToString());
                _attachmentList.Items.Add(listItem);

                total += row.Info.StackSize > 1
                    ? (row.Count + row.Info.StackSize - 1) / row.Info.StackSize
                    : row.Count;
            }
            _attachmentList.EndUpdate();

            _attachmentHintLabel.Text = $"清单内物品总件数：{total}（单封上限 {MaxAttachItems} 件）";
        }

        private void ClearAttachments()
        {
            _rows.Clear();
            RefreshAttachments();
        }

        private void SendButton_Click(object sender, EventArgs e)
        {
            if (!SMain.Envir.Running)
            {
                MessageBox.Show("服务器必须在运行状态才能发送奖励邮件。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            string name = _playerNameBox.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("请填写收件角色名。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CharacterInfo target = GetTarget(name);
            if (target == null)
            {
                MessageBox.Show($"找不到角色“{name}”。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string message = _messageBox.Text.Trim();
            if (message.Length > MaxMessageLength)
            {
                MessageBox.Show($"留言内容过长，最多 {MaxMessageLength} 字。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            uint gold = 0;
            string goldText = _goldBox.Text.Trim();
            if (goldText.Length > 0 && !uint.TryParse(goldText, out gold))
            {
                MessageBox.Show("金币必须是 0 到 4294967295 之间的整数。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (message.Length == 0 && gold == 0 && _rows.Count == 0)
            {
                MessageBox.Show("留言、金币、物品至少需要填一项。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<UserItem> attachItems;
            try
            {
                attachItems = BuildAttachItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成附件失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                MailInfo mail = new MailInfo(target.Index, false)
                {
                    Sender = GMSender,
                    Message = message,
                    Gold = gold,
                    Items = attachItems
                };

                mail.Send();

                string itemSummary = BuildSummary(attachItems, gold);
                string logText = $"GM 奖励邮件 -> 角色 [{target.Name}]：{itemSummary}";
                SMain.Enqueue(logText);

                MessageBox.Show($"已向 [{target.Name}] 发送奖励邮件。\r\n{itemSummary}", "发送成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SMain.Enqueue("GM 奖励邮件发送失败：" + ex);
                MessageBox.Show("发送失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private CharacterInfo GetTarget(string name)
        {
            return SMain.Envir.GetCharacterInfo(name);
        }

        private List<UserItem> BuildAttachItems()
        {
            List<UserItem> result = new List<UserItem>();

            foreach (AttachRow row in _rows)
            {
                if (row.Info.StackSize > 1)
                {
                    int remaining = row.Count;
                    while (remaining > 0)
                    {
                        int chunk = Math.Min(row.Info.StackSize, remaining);
                        UserItem item = SMain.Envir.CreateFreshItem(row.Info);
                        item.GMMade = true;
                        item.Count = (ushort)chunk;
                        result.Add(item);
                        remaining -= chunk;
                    }
                }
                else
                {
                    for (int i = 0; i < row.Count; i++)
                    {
                        UserItem item = SMain.Envir.CreateFreshItem(row.Info);
                        item.GMMade = true;
                        item.Count = 1;
                        result.Add(item);
                    }
                }
            }

            if (result.Count > MaxAttachItems)
                throw new InvalidOperationException($"附件总件数不能超过 {MaxAttachItems} 件，当前 {result.Count} 件。");

            return result;
        }

        private static string BuildSummary(List<UserItem> items, uint gold)
        {
            var names = new List<string>();
            foreach (UserItem item in items)
                names.Add(item.FriendlyName);

            string joined = names.Count > 0 ? string.Join("、", names) : "无";
            return gold > 0 ? $"{joined}（金币 {gold:N0}）" : joined;
        }

        private void RefreshOnlinePlayers()
        {
            _onlineCombo.BeginUpdate();
            _onlineCombo.Items.Clear();

            if (SMain.Envir.Running)
            {
                foreach (PlayerObject player in SMain.Envir.Players)
                {
                    if (player?.Info == null) continue;
                    _onlineCombo.Items.Add(player.Info);
                }
            }
            _onlineCombo.EndUpdate();
        }

        private static Label AddLabel(Control parent, string text, Point location, Size? size = null)
        {
            var label = new Label { Text = text, Location = location, AutoSize = size == null };
            if (size.HasValue)
                label.Size = size.Value;
            parent.Controls.Add(label);
            return label;
        }

        private static TextBox AddTextBox(Control parent, Point location, Size size, string text)
        {
            var box = new TextBox { Location = location, Size = size, Text = text };
            parent.Controls.Add(box);
            return box;
        }

        private static Button AddButton(Control parent, string text, Point location, Size size)
        {
            var button = new Button { Text = text, Location = location, Size = size };
            parent.Controls.Add(button);
            return button;
        }
    }
}
