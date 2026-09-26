using System.Text;
using System.Text.RegularExpressions;
using Server.MirEnvir;

namespace Server.Database
{
    /// <summary>
    /// 物品管理器: 左侧搜索列表 + 右侧分类表单(基础/属性/绑定/特殊), 全中文界面.
    /// 编辑 Envir.Edit, 保存时写盘并热同步到运行中的 Envir.Main(在线立即生效, 防周期存盘回滚).
    /// </summary>
    public partial class ItemMgrForm : Form
    {
        public Envir Envir => SMain.EditEnvir;

        private sealed class ComboEntry
        {
            public object Value;
            public string Text;
            public override string ToString() => Text ?? "";
        }

        private bool _loading;
        private readonly List<ItemInfo> _view = new();
        private readonly HashSet<int> _dirty = new();
        private readonly HashSet<int> _added = new();
        private readonly List<int> _deleted = new();
        private ItemInfo _current;
        private DateTime? _lastSaved;
        private readonly ScriptRenameSync _renameSync = new();
        private readonly Dictionary<BindMode, CheckBox> _bindChecks = new();
        private readonly Dictionary<SpecialItemMode, CheckBox> _specialChecks = new();

        public ItemMgrForm()
        {
            InitializeComponent();

            colStatValue.ReadOnly = false;

            InitCombos();
            InitFlagPanels();
            WireEvents();

            RebuildView();
            LoadPanel();
            UpdateStatus();
        }

        #region 初始化

        private void InitCombos()
        {
            cmbTypeFilter.Items.Add(new ComboEntry { Value = null, Text = "全部类型" });
            foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
                cmbTypeFilter.Items.Add(new ComboEntry { Value = t, Text = ItemMgrLabels.Of(t) });
            cmbTypeFilter.SelectedIndex = 0;

            cmbGradeFilter.Items.Add(new ComboEntry { Value = null, Text = "全部品质" });
            foreach (ItemGrade g in Enum.GetValues(typeof(ItemGrade)))
                cmbGradeFilter.Items.Add(new ComboEntry { Value = g, Text = ItemMgrLabels.Of(g) });
            cmbGradeFilter.SelectedIndex = 0;

            foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
                cmbTypeEdit.Items.Add(new ComboEntry { Value = t, Text = ItemMgrLabels.Of(t) });

            foreach (ItemGrade g in Enum.GetValues(typeof(ItemGrade)))
                cmbGradeEdit.Items.Add(new ComboEntry { Value = g, Text = ItemMgrLabels.Of(g) });

            foreach (ItemSet s in Enum.GetValues(typeof(ItemSet)))
                cmbSetEdit.Items.Add(new ComboEntry { Value = s, Text = ItemMgrLabels.Of(s) });

            foreach (RequiredType r in Enum.GetValues(typeof(RequiredType)))
                cmbReqType.Items.Add(new ComboEntry { Value = r, Text = ItemMgrLabels.Of(r) });

            foreach (RequiredClass r in Enum.GetValues(typeof(RequiredClass)))
                cmbReqClass.Items.Add(new ComboEntry { Value = r, Text = ItemMgrLabels.Of(r) });

            foreach (RequiredGender r in Enum.GetValues(typeof(RequiredGender)))
                cmbReqGender.Items.Add(new ComboEntry { Value = r, Text = ItemMgrLabels.Of(r) });
        }

        private void InitFlagPanels()
        {
            foreach (BindMode b in Enum.GetValues(typeof(BindMode)))
            {
                if (b == BindMode.None) continue;
                var cb = new CheckBox { Text = ItemMgrLabels.Of(b), Tag = b, AutoSize = true, Margin = new Padding(12, 8, 4, 4) };
                cb.CheckedChanged += BindCheckChanged;
                flpBind.Controls.Add(cb);
                _bindChecks[b] = cb;
            }

            foreach (SpecialItemMode s in Enum.GetValues(typeof(SpecialItemMode)))
            {
                if (s == SpecialItemMode.None) continue;
                var cb = new CheckBox { Text = ItemMgrLabels.Of(s), Tag = s, AutoSize = true, Margin = new Padding(12, 8, 4, 4) };
                cb.CheckedChanged += SpecialCheckChanged;
                flpSpecial.Controls.Add(cb);
                _specialChecks[s] = cb;
            }
        }

        private void WireEvents()
        {
            txtSearch.TextChanged += (s, e) => RebuildView();
            cmbTypeFilter.SelectedIndexChanged += (s, e) => RebuildView();
            cmbGradeFilter.SelectedIndexChanged += (s, e) => RebuildView();

            lvItems.RetrieveVirtualItem += LvItems_RetrieveVirtualItem;
            lvItems.SelectedIndexChanged += LvItems_SelectedIndexChanged;

            btnAdd.Click += BtnAdd_Click;
            btnCopy.Click += BtnCopy_Click;
            btnDel.Click += BtnDel_Click;

            btnSave.Click += (s, e) => SaveAll();
            btnTranslate.Click += BtnTranslate_Click;
            btnImport.Click += BtnImport_Click;
            btnExport.Click += BtnExport_Click;
            btnGameShop.Click += BtnGameShop_Click;

            txtName.TextChanged += TxtName_TextChanged;
            txtToolTip.TextChanged += TxtToolTip_TextChanged;

            numShape.ValueChanged += Num_ValueChanged;
            numImage.ValueChanged += Num_ValueChanged;
            numEffect.ValueChanged += Num_ValueChanged;
            numSlots.ValueChanged += Num_ValueChanged;
            numWeight.ValueChanged += Num_ValueChanged;
            numDura.ValueChanged += Num_ValueChanged;
            numStack.ValueChanged += Num_ValueChanged;
            numPrice.ValueChanged += Num_ValueChanged;
            numReqAmount.ValueChanged += Num_ValueChanged;
            numRandomStatsId.ValueChanged += Num_ValueChanged;
            numLightRange.ValueChanged += Num_ValueChanged;
            numLightIntensity.ValueChanged += Num_ValueChanged;

            cmbTypeEdit.SelectedIndexChanged += EditCombo_Changed;
            cmbGradeEdit.SelectedIndexChanged += EditCombo_Changed;
            cmbSetEdit.SelectedIndexChanged += EditCombo_Changed;
            cmbReqType.SelectedIndexChanged += EditCombo_Changed;
            cmbReqClass.SelectedIndexChanged += EditCombo_Changed;
            cmbReqGender.SelectedIndexChanged += EditCombo_Changed;

            chkStartItem.CheckedChanged += FlagCheck_Changed;
            chkNeedIdentify.CheckedChanged += FlagCheck_Changed;
            chkShowGroupPickup.CheckedChanged += FlagCheck_Changed;
            chkGlobalDropNotify.CheckedChanged += FlagCheck_Changed;
            chkClassBased.CheckedChanged += FlagCheck_Changed;
            chkLevelBased.CheckedChanged += FlagCheck_Changed;
            chkCanMine.CheckedChanged += FlagCheck_Changed;
            chkCanFastRun.CheckedChanged += FlagCheck_Changed;
            chkCanAwakening.CheckedChanged += FlagCheck_Changed;

            chkShowAllStats.CheckedChanged += (s, e) => RebuildStatsGrid();

            dgvStats.CellValidating += DgvStats_CellValidating;
            dgvStats.CellEndEdit += (s, e) => dgvStats.Rows[e.RowIndex].ErrorText = null;
            dgvStats.CellValueChanged += DgvStats_CellValueChanged;

            FormClosing += ItemMgrForm_FormClosing;
        }

        #endregion

        #region 左侧列表

        private void LvItems_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex < 0 || e.ItemIndex >= _view.Count)
            {
                e.Item = new ListViewItem();
                return;
            }

            var it = _view[e.ItemIndex];
            var lvi = new ListViewItem(it.Index.ToString()) { Tag = it };
            lvi.SubItems.Add(it.Name);
            lvi.SubItems.Add(ItemMgrLabels.Of(it.Type));
            lvi.SubItems.Add(ItemMgrLabels.Of(it.Grade));
            lvi.SubItems.Add(it.Price.ToString());
            e.Item = lvi;
        }

        private void LvItems_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;

            var sel = lvItems.SelectedIndices.Count > 0 && lvItems.SelectedIndices[0] < _view.Count
                ? _view[lvItems.SelectedIndices[0]]
                : null;

            if (ReferenceEquals(sel, _current)) return;

            _current = sel;
            LoadPanel();
        }

        private void RebuildView()
        {
            var kw = txtSearch.Text.Trim();
            var ft = cmbTypeFilter.SelectedItem as ComboEntry;
            var fg = cmbGradeFilter.SelectedItem as ComboEntry;

            _view.Clear();
            foreach (var it in Envir.ItemInfoList)
            {
                if (!string.IsNullOrEmpty(kw) && it.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (ft?.Value is ItemType t && it.Type != t) continue;
                if (fg?.Value is ItemGrade g && it.Grade != g) continue;
                _view.Add(it);
            }

            lvItems.BeginUpdate();
            try
            {
                lvItems.VirtualListSize = _view.Count;

                if (_current != null)
                {
                    var idx = _view.IndexOf(_current);
                    if (idx >= 0)
                    {
                        lvItems.SelectedIndices.Clear();
                        lvItems.SelectedIndices.Add(idx);
                    }
                }
            }
            finally
            {
                lvItems.EndUpdate();
            }
        }

        private void SelectItem(ItemInfo info)
        {
            _current = info;
            LoadPanel();

            var idx = _view.IndexOf(info);
            if (idx >= 0)
            {
                lvItems.SelectedIndices.Clear();
                lvItems.SelectedIndices.Add(idx);
                lvItems.EnsureVisible(idx);
            }
        }

        #endregion

        #region 载入右侧面板

        private void LoadPanel()
        {
            _loading = true;
            try
            {
                try { dgvStats.EndEdit(); } catch { }

                var it = _current;
                tcEdit.Enabled = it != null;

                txtIndex.Text = it?.Index.ToString() ?? "";
                txtName.Text = it?.Name ?? "";
                txtName.BackColor = SystemColors.Window;
                txtToolTip.Text = it?.ToolTip ?? "";

                if (it == null)
                {
                    SelectCombo(cmbTypeEdit, null);
                    SelectCombo(cmbGradeEdit, null);
                    SelectCombo(cmbSetEdit, null);
                    SelectCombo(cmbReqType, null);
                    SelectCombo(cmbReqClass, null);
                    SelectCombo(cmbReqGender, null);

                    numShape.Value = 0; numImage.Value = 0; numEffect.Value = 0; numSlots.Value = 0;
                    numWeight.Value = 0; numDura.Value = 0; numStack.Value = 0; numPrice.Value = 0;
                    numReqAmount.Value = 0; numRandomStatsId.Value = 0;
                    numLightRange.Value = 0; numLightIntensity.Value = 0;

                    chkStartItem.Checked = false;
                    foreach (var cb in _bindChecks.Values) cb.Checked = false;
                    foreach (var cb in _specialChecks.Values) cb.Checked = false;
                    chkNeedIdentify.Checked = false;
                    chkShowGroupPickup.Checked = false;
                    chkGlobalDropNotify.Checked = false;
                    chkClassBased.Checked = false;
                    chkLevelBased.Checked = false;
                    chkCanMine.Checked = false;
                    chkCanFastRun.Checked = false;
                    chkCanAwakening.Checked = false;
                }
                else
                {
                    SelectCombo(cmbTypeEdit, it.Type);
                    SelectCombo(cmbGradeEdit, it.Grade);
                    SelectCombo(cmbSetEdit, it.Set);
                    SelectCombo(cmbReqType, it.RequiredType);
                    SelectCombo(cmbReqClass, it.RequiredClass);
                    SelectCombo(cmbReqGender, it.RequiredGender);

                    numShape.Value = Clamp(it.Shape, (int)numShape.Minimum, (int)numShape.Maximum);
                    numImage.Value = it.Image;
                    numEffect.Value = it.Effect;
                    numSlots.Value = it.Slots;
                    numWeight.Value = it.Weight;
                    numDura.Value = it.Durability;
                    numStack.Value = it.StackSize;
                    numPrice.Value = it.Price;
                    numReqAmount.Value = it.RequiredAmount;
                    numRandomStatsId.Value = it.RandomStatsId;
                    numLightRange.Value = it.Light % 15;
                    numLightIntensity.Value = it.Light / 15;

                    chkStartItem.Checked = it.StartItem;

                    foreach (var pair in _bindChecks)
                        pair.Value.Checked = it.Bind.HasFlag(pair.Key);
                    foreach (var pair in _specialChecks)
                        pair.Value.Checked = it.Unique.HasFlag(pair.Key);

                    chkNeedIdentify.Checked = it.NeedIdentify;
                    chkShowGroupPickup.Checked = it.ShowGroupPickup;
                    chkGlobalDropNotify.Checked = it.GlobalDropNotify;
                    chkClassBased.Checked = it.ClassBased;
                    chkLevelBased.Checked = it.LevelBased;
                    chkCanMine.Checked = it.CanMine;
                    chkCanFastRun.Checked = it.CanFastRun;
                    chkCanAwakening.Checked = it.CanAwakening;
                }

                RebuildStatsGrid();
            }
            finally
            {
                _loading = false;
            }
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        private static void SelectCombo(ComboBox cb, object value)
        {
            if (value == null) { cb.SelectedIndex = -1; return; }

            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboEntry ce && Equals(ce.Value, value))
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
            cb.SelectedIndex = -1;
        }

        private void RebuildStatsGrid()
        {
            _loading = true;
            try
            {
                dgvStats.Rows.Clear();
                if (_current == null) return;

                foreach (Stat st in Enum.GetValues(typeof(Stat)))
                {
                    if (st == Stat.Unknown) continue;

                    var v = _current.Stats[st];
                    if (!chkShowAllStats.Checked && v == 0) continue;

                    var row = dgvStats.Rows.Add(ItemMgrLabels.Of(st), v.ToString());
                    dgvStats.Rows[row].Tag = st;
                }
            }
            finally
            {
                _loading = false;
            }
        }

        #endregion

        #region 编辑事件

        private void TxtName_TextChanged(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;

            var newName = txtName.Text;
            if (string.Equals(newName, _current.Name, StringComparison.Ordinal)) return;

            var oldName = _current.Name;

            txtName.BackColor = !string.IsNullOrEmpty(newName) && Envir.ItemInfoList.Any(x =>
                !ReferenceEquals(x, _current) &&
                string.Equals(x.Name, newName, StringComparison.OrdinalIgnoreCase))
                ? Color.MistyRose
                : SystemColors.Window;

            _current.Name = newName;
            MarkDirty(_current);

            if (!string.IsNullOrWhiteSpace(oldName) && !string.IsNullOrWhiteSpace(newName) &&
                !oldName.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                _renameSync.Rename(oldName, newName);
            }

            RebuildView();
        }

        private void TxtToolTip_TextChanged(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;
            if (string.Equals(txtToolTip.Text, _current.ToolTip, StringComparison.Ordinal)) return;

            _current.ToolTip = txtToolTip.Text;
            MarkDirty(_current);
        }

        private void Num_ValueChanged(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;
            var it = _current;

            switch (((Control)sender).Name)
            {
                case nameof(numShape): it.Shape = (short)numShape.Value; break;
                case nameof(numImage): it.Image = (ushort)numImage.Value; break;
                case nameof(numEffect): it.Effect = (byte)numEffect.Value; break;
                case nameof(numSlots): it.Slots = (byte)numSlots.Value; break;
                case nameof(numWeight): it.Weight = (byte)numWeight.Value; break;
                case nameof(numDura): it.Durability = (ushort)numDura.Value; break;
                case nameof(numStack): it.StackSize = (ushort)numStack.Value; break;
                case nameof(numPrice): it.Price = (uint)numPrice.Value; break;
                case nameof(numReqAmount): it.RequiredAmount = (byte)numReqAmount.Value; break;
                case nameof(numRandomStatsId): it.RandomStatsId = (byte)numRandomStatsId.Value; break;
                case nameof(numLightRange): it.Light = (byte)((int)numLightRange.Value + (it.Light / 15) * 15); break;
                case nameof(numLightIntensity): it.Light = (byte)((it.Light % 15) + (int)numLightIntensity.Value * 15); break;
                default: return;
            }

            MarkDirty(it);
        }

        private void EditCombo_Changed(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;
            if (sender is not ComboBox cb || cb.SelectedItem is not ComboEntry ce || ce.Value == null) return;

            var it = _current;
            switch (cb.Name)
            {
                case nameof(cmbTypeEdit): it.Type = (ItemType)ce.Value; RebuildView(); break;
                case nameof(cmbGradeEdit): it.Grade = (ItemGrade)ce.Value; RebuildView(); break;
                case nameof(cmbSetEdit): it.Set = (ItemSet)ce.Value; break;
                case nameof(cmbReqType): it.RequiredType = (RequiredType)ce.Value; break;
                case nameof(cmbReqClass): it.RequiredClass = (RequiredClass)ce.Value; break;
                case nameof(cmbReqGender): it.RequiredGender = (RequiredGender)ce.Value; break;
                default: return;
            }

            MarkDirty(it);
        }

        private void FlagCheck_Changed(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;
            var it = _current;

            switch (((Control)sender).Name)
            {
                case nameof(chkStartItem): it.StartItem = chkStartItem.Checked; break;
                case nameof(chkNeedIdentify): it.NeedIdentify = chkNeedIdentify.Checked; break;
                case nameof(chkShowGroupPickup): it.ShowGroupPickup = chkShowGroupPickup.Checked; break;
                case nameof(chkGlobalDropNotify): it.GlobalDropNotify = chkGlobalDropNotify.Checked; break;
                case nameof(chkClassBased): it.ClassBased = chkClassBased.Checked; break;
                case nameof(chkLevelBased): it.LevelBased = chkLevelBased.Checked; break;
                case nameof(chkCanMine): it.CanMine = chkCanMine.Checked; break;
                case nameof(chkCanFastRun): it.CanFastRun = chkCanFastRun.Checked; break;
                case nameof(chkCanAwakening): it.CanAwakening = chkCanAwakening.Checked; break;
                default: return;
            }

            MarkDirty(it);
        }

        private void BindCheckChanged(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;
            if (sender is not CheckBox cb || cb.Tag is not BindMode b) return;

            _current.Bind = cb.Checked ? (_current.Bind | b) : (_current.Bind & ~b);
            MarkDirty(_current);
        }

        private void SpecialCheckChanged(object sender, EventArgs e)
        {
            if (_loading || _current == null) return;
            if (sender is not CheckBox cb || cb.Tag is not SpecialItemMode s) return;

            _current.Unique = cb.Checked ? (_current.Unique | s) : (_current.Unique & ~s);
            MarkDirty(_current);
        }

        private void DgvStats_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex != 1) return;

            var row = dgvStats.Rows[e.RowIndex];

            if (!int.TryParse(Convert.ToString(e.FormattedValue), out var v))
            {
                e.Cancel = true;
                row.ErrorText = "必须是整数";
                return;
            }

            if (v < 0 && row.Tag is Stat st && st != Stat.AttackSpeed)
            {
                e.Cancel = true;
                row.ErrorText = "只有攻击速度允许负数";
                return;
            }

            row.ErrorText = null;
        }

        private void DgvStats_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (_loading || _current == null || e.ColumnIndex != 1) return;

            var row = dgvStats.Rows[e.RowIndex];
            if (row.Tag is not Stat st) return;

            if (!int.TryParse(Convert.ToString(row.Cells[1].Value ?? "0"), out var v)) v = 0;

            _current.Stats[st] = v;
            MarkDirty(_current);
        }

        private void MarkDirty(ItemInfo info)
        {
            _dirty.Add(info.Index);
            UpdateStatus();
        }

        #endregion

        #region 增 / 复制 / 删

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            var type = (cmbTypeFilter.SelectedItem as ComboEntry)?.Value is ItemType t ? t : ItemType.Nothing;

            Envir.CreateItemInfo(type);
            var info = Envir.ItemInfoList[^1];
            info.Name = "新物品";

            _added.Add(info.Index);
            MarkDirty(info);

            RebuildView();
            SelectItem(info);
            txtName.Focus();
            txtName.SelectAll();
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            if (_current == null) return;

            var copy = _current.CloneItemInfo();
            copy.Index = ++Envir.ItemIndex;
            copy.Name = UniqueCopyName(_current.Name);

            Envir.ItemInfoList.Add(copy);
            _added.Add(copy.Index);
            MarkDirty(copy);

            RebuildView();
            SelectItem(copy);
            txtName.Focus();
            txtName.SelectAll();
        }

        private string UniqueCopyName(string baseName)
        {
            var name = baseName + "副本";
            var n = 2;
            while (Envir.ItemInfoList.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                name = baseName + "副本" + n++;
            return name;
        }

        private void BtnDel_Click(object sender, EventArgs e)
        {
            if (_current == null) return;
            var it = _current;

            if (MessageBox.Show($"确定删除物品 [{it.Index}: {it.Name}] 吗?\n保存后生效; 玩家手里已有的该物品不受影响。",
                    "删除物品", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            Envir.ItemInfoList.Remove(it);

            if (_added.Remove(it.Index))
                _dirty.Remove(it.Index);
            else if (!_deleted.Contains(it.Index))
                _deleted.Add(it.Index);

            _dirty.Remove(it.Index);

            if (ReferenceEquals(_current, it)) _current = null;

            LoadPanel();
            RebuildView();
            UpdateStatus();
        }

        private void BtnGameShop_Click(object sender, EventArgs e)
        {
            if (_current == null) return;

            Envir.AddToGameShop(_current);
            MessageBox.Show($"已将 [{_current.Name}] 加入商城列表, 点击[保存]后写入数据库。\n价格可稍后在商城面板里调整。",
                "加入商城", MessageBoxButtons.OK, MessageBoxIcon.Information);
            MarkDirty(_current);
        }

        #endregion

        #region 保存

        private bool SaveAll()
        {
            try { dgvStats.EndEdit(); } catch { }

            if (!ValidateAll(out var errors))
            {
                MessageBox.Show("保存失败, 请先修正以下问题:\n\n" + errors, "验证未通过",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            //随机属性模板重挂(参照 LoadDB 的做法)
            foreach (var idx in _dirty)
            {
                var it = Envir.ItemInfoList.FirstOrDefault(x => x.Index == idx);
                if (it == null) continue;
                it.RandomStats = it.RandomStatsId < Settings.RandomItemStatsList.Count
                    ? Settings.RandomItemStatsList[it.RandomStatsId]
                    : null;
            }

            Envir.SaveDB();      //Edit 写盘(SaveDB 自带旧库备份)
            _renameSync.Flush(); //物品改名同步到 Drops/Recipe/Quests/NPCs 脚本

            //热同步到运行中的服务器(服务器线程执行, 在线立即生效, 防周期存盘回滚)
            var deleted = _deleted.Except(_added).ToList();
            var changeCount = _dirty.Count;

            SMain.Envir.QueueItemSync(deleted);

            _dirty.Clear();
            _added.Clear();
            _deleted.Clear();
            _lastSaved = DateTime.Now;
            UpdateStatus();

            SMain.Enqueue($"物品管理器: 已保存 {changeCount} 处修改并热同步到运行中的服务器。");
            return true;
        }

        private bool ValidateAll(out string errors)
        {
            var sb = new StringBuilder();
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var it in Envir.ItemInfoList)
            {
                if (string.IsNullOrWhiteSpace(it.Name))
                {
                    sb.AppendLine($"编号 {it.Index}: 名称为空");
                    continue;
                }

                if (seen.TryGetValue(it.Name, out var other))
                    sb.AppendLine($"编号 {it.Index} 与编号 {other} 重名: {it.Name}");
                else
                    seen[it.Name] = it.Index;
            }

            errors = sb.ToString();
            return sb.Length == 0;
        }

        private void UpdateStatus()
        {
            var pending = _dirty.Count + _deleted.Count;
            lblDirty.Text = pending > 0 ? $"未保存修改 {pending} 处" : "无未保存修改";
            lblSaved.Text = pending > 0 ? "未保存" : (_lastSaved?.ToString("已保存 HH:mm:ss") ?? "尚未保存过");
            lblServer.Text = SMain.Envir.Running ? "服务器: 运行中(保存后热同步生效)" : "服务器: 已停止";
        }

        private void ItemMgrForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var pending = _dirty.Count + _deleted.Count;
            if (pending == 0) return;

            var r = MessageBox.Show(
                $"有 {pending} 处未保存修改。\n\n[是] 保存并关闭\n[否] 丢弃修改并关闭(将重新从数据库加载)\n[取消] 返回继续编辑",
                "未保存的修改", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (r == DialogResult.Yes)
            {
                if (!SaveAll()) e.Cancel = true;
            }
            else if (r == DialogResult.No)
            {
                _dirty.Clear();
                _added.Clear();
                _deleted.Clear();
                _current = null;
                Envir.LoadDB(); //从未保存的磁盘数据整体回滚
                RebuildView();
                LoadPanel();
                UpdateStatus();
            }
            else
            {
                e.Cancel = true;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.S))
            {
                SaveAll();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion

        #region 批量翻译英文名

        private void BtnTranslate_Click(object sender, EventArgs e)
        {
            var plan = new List<(ItemInfo Info, string OldName, string NewName)>();
            var skipped = new List<string>();
            var names = new HashSet<string>(
                Envir.ItemInfoList.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var it in Envir.ItemInfoList)
            {
                if (!ItemMgrLabels.EnglishItemNames.TryGetValue(it.Name, out var cn)) continue;

                if (!cn.Equals(it.Name, StringComparison.OrdinalIgnoreCase) && names.Contains(cn))
                {
                    skipped.Add($"{it.Name} → {cn} (目标名已存在, 跳过)");
                    continue;
                }

                plan.Add((it, it.Name, cn));
            }

            if (plan.Count == 0)
            {
                MessageBox.Show("没有找到可翻译的英文物品名。" +
                    (skipped.Count > 0 ? "\n\n以下因重名冲突跳过:\n" + string.Join("\n", skipped) : ""),
                    "批量翻译", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new Form
            {
                Text = $"批量翻译预览 (共 {plan.Count} 件)",
                StartPosition = FormStartPosition.CenterParent,
                Size = new Size(700, 520),
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false
            };

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add("idx", "编号");
            grid.Columns.Add("old", "旧名(英文)");
            grid.Columns.Add("new", "新名(中文)");
            foreach (var p in plan)
                grid.Rows.Add(p.Info.Index, p.OldName, p.NewName);

            var btnOk = new Button { Text = "应用改名", Dock = DockStyle.Bottom, Height = 38, DialogResult = DialogResult.OK };
            dlg.Controls.Add(grid);
            dlg.Controls.Add(btnOk);

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            foreach (var p in plan)
            {
                _renameSync.Rename(p.OldName, p.NewName);
                p.Info.Name = p.NewName;
                _dirty.Add(p.Info.Index);
            }

            RebuildView();
            LoadPanel();
            UpdateStatus();

            MessageBox.Show($"已将 {plan.Count} 件物品改为中文名, 点击[保存]写入数据库并同步脚本。" +
                (skipped.Count > 0 ? "\n\n以下因重名冲突跳过:\n" + string.Join("\n", skipped) : ""),
                "批量翻译", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion

        #region CSV 导入/导出 (与旧版物品表格 CSV 列名互通)

        private static readonly string[] FixedColumns =
        {
            "ItemIndex", "ItemName", "ItemType", "ItemGrade", "ItemRequiredType", "ItemRequiredGender",
            "ItemRequiredClass", "ItemSet", "ItemRandomStatsId", "ItemRequiredAmount", "ItemImage",
            "ItemShape", "ItemEffect", "ItemStackSize", "ItemSlots", "ItemWeight", "ItemLightRange",
            "ItemLightIntensity", "ItemDurability", "ItemPrice", "ItemToolTip", "StartItem",
            "NeedIdentify", "ShowGroupPickup", "GlobalDropNotify", "ClassBased", "LevelBased",
            "CanMine", "CanFastRun", "CanAwakening"
        };

        private static IEnumerable<string> AllColumns()
        {
            foreach (var c in FixedColumns) yield return c;
            foreach (Stat st in Enum.GetValues(typeof(Stat)))
                if (st != Stat.Unknown) yield return "Stat" + st;
            foreach (BindMode b in Enum.GetValues(typeof(BindMode)))
                if (b != BindMode.None) yield return "Bind" + b;
            foreach (SpecialItemMode s in Enum.GetValues(typeof(SpecialItemMode)))
                if (s != SpecialItemMode.None) yield return "Special" + s;
        }

        private static string CellValue(ItemInfo it, string col)
        {
            switch (col)
            {
                case "ItemIndex": return it.Index.ToString();
                case "ItemName": return Quote(it.Name);
                case "ItemType": return it.Type.ToString();
                case "ItemGrade": return it.Grade.ToString();
                case "ItemRequiredType": return it.RequiredType.ToString();
                case "ItemRequiredGender": return it.RequiredGender.ToString();
                case "ItemRequiredClass": return it.RequiredClass.ToString();
                case "ItemSet": return it.Set.ToString();
                case "ItemRandomStatsId": return it.RandomStatsId.ToString();
                case "ItemRequiredAmount": return it.RequiredAmount.ToString();
                case "ItemImage": return it.Image.ToString();
                case "ItemShape": return it.Shape.ToString();
                case "ItemEffect": return it.Effect.ToString();
                case "ItemStackSize": return it.StackSize.ToString();
                case "ItemSlots": return it.Slots.ToString();
                case "ItemWeight": return it.Weight.ToString();
                case "ItemLightRange": return (it.Light % 15).ToString();
                case "ItemLightIntensity": return (it.Light / 15).ToString();
                case "ItemDurability": return it.Durability.ToString();
                case "ItemPrice": return it.Price.ToString();
                case "ItemToolTip": return Quote((it.ToolTip ?? "").Replace("\r\n", "\\r\\n"));
                case "StartItem": return it.StartItem.ToString();
                case "NeedIdentify": return it.NeedIdentify.ToString();
                case "ShowGroupPickup": return it.ShowGroupPickup.ToString();
                case "GlobalDropNotify": return it.GlobalDropNotify.ToString();
                case "ClassBased": return it.ClassBased.ToString();
                case "LevelBased": return it.LevelBased.ToString();
                case "CanMine": return it.CanMine.ToString();
                case "CanFastRun": return it.CanFastRun.ToString();
                case "CanAwakening": return it.CanAwakening.ToString();
            }

            if (col.StartsWith("Stat"))
                return it.Stats[Enum.Parse<Stat>(col.Substring(4))].ToString();

            if (col.StartsWith("Bind"))
                return it.Bind.HasFlag(Enum.Parse<BindMode>(col.Substring(4))).ToString();

            if (col.StartsWith("Special"))
                return it.Unique.HasFlag(Enum.Parse<SpecialItemMode>(col.Substring(7))).ToString();

            return "";
        }

        private static string Quote(string v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;
                    else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                    else sb.Append(c);
                }
            }

            result.Add(sb.ToString());
            return result.ToArray();
        }

        private static string Unquote(string v) =>
            (v ?? "").Trim().Trim('"').Replace("\"\"", "\"");

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (Envir.ItemInfoList.Count == 0)
            {
                MessageBox.Show("没有物品可导出。", "导出CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = $"物品导出 {DateTime.Now:yyyyMMdd HHmmss}.csv"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var cols = AllColumns().ToList();
                var lines = new List<string>(Envir.ItemInfoList.Count + 1) { string.Join(",", cols) };

                foreach (var it in Envir.ItemInfoList)
                {
                    var sb = new StringBuilder();
                    foreach (var c in cols)
                        sb.Append(CellValue(it, c)).Append(',');
                    lines.Add(sb.ToString(0, sb.Length - 1));
                }

                File.WriteAllLines(sfd.FileName, lines, Encoding.UTF8);
                MessageBox.Show($"已导出 {Envir.ItemInfoList.Count} 件物品。\n格式与旧版物品表格互通。", "导出CSV",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出失败: " + ex.Message, "导出CSV", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var rows = File.ReadAllLines(ofd.FileName, Encoding.UTF8);
            if (rows.Length < 2)
            {
                MessageBox.Show("文件没有数据行。", "导入CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var columns = SplitCsvLine(rows[0]);
            var colPos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < columns.Length; i++)
            {
                var name = Unquote(columns[i]);
                if (!string.IsNullOrWhiteSpace(name) && !colPos.ContainsKey(name))
                    colPos[name] = i;
            }

            if (!colPos.ContainsKey("ItemIndex") || !colPos.ContainsKey("ItemName"))
            {
                MessageBox.Show("CSV 缺少 ItemIndex / ItemName 列。", "导入CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int lastIndex = 0;
            foreach (var x in Envir.ItemInfoList)
                if (x.Index > lastIndex) lastIndex = x.Index;

            int imported = 0;
            var errors = new List<string>();

            for (int r = 1; r < rows.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(rows[r])) continue;

                var cells = SplitCsvLine(rows[r]);
                string Get(string col) => colPos.TryGetValue(col, out var p) && p < cells.Length ? cells[p] : null;

                var indexRaw = Unquote(Get("ItemIndex"));
                var nameRaw = Unquote(Get("ItemName"));

                if (string.IsNullOrWhiteSpace(nameRaw)) continue;

                ItemInfo item;
                bool isNew = false;

                if (!string.IsNullOrWhiteSpace(indexRaw) && int.TryParse(indexRaw, out var idx))
                {
                    item = Envir.ItemInfoList.FirstOrDefault(x => x.Index == idx);
                    if (item == null)
                    {
                        item = new ItemInfo { Index = ++lastIndex };
                        Envir.ItemInfoList.Add(item);
                        isNew = true;
                    }
                }
                else
                {
                    item = new ItemInfo { Index = ++lastIndex };
                    Envir.ItemInfoList.Add(item);
                    isNew = true;
                }

                try
                {
                    if (!ApplyCsvRow(item, colPos, cells, Get, errors, r))
                        continue; //整行报错已记录

                    _dirty.Add(item.Index);
                    if (isNew) _added.Add(item.Index);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"第 {r} 行 [{nameRaw}]: {ex.Message}");
                }
            }

            RebuildView();
            LoadPanel();
            UpdateStatus();

            MessageBox.Show(
                $"已导入 {imported} 件物品, 点击[保存]写入数据库。" +
                (errors.Count > 0 ? "\n\n以下行有错误被跳过:\n" + string.Join("\n", errors.Take(20)) : ""),
                "导入CSV", MessageBoxButtons.OK,
                errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        /// <summary>按列名把一行 CSV 写回物品; 返回 false 表示本行放弃。</summary>
        private bool ApplyCsvRow(ItemInfo item, Dictionary<string, int> colPos, string[] cells,
            Func<string, string> get, List<string> errors, int rowNumber)
        {
            foreach (var (col, pos) in colPos)
            {
                if (col == "ItemIndex" || pos >= cells.Length) continue;

                var raw = cells[pos];
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var v = Unquote(raw);

                try
                {
                    switch (col)
                    {
                        case "ItemName":
                            if (!string.Equals(item.Name, v, StringComparison.OrdinalIgnoreCase))
                            {
                                var dup = Envir.ItemInfoList.FirstOrDefault(x =>
                                    !ReferenceEquals(x, item) &&
                                    string.Equals(x.Name, v, StringComparison.OrdinalIgnoreCase));
                                if (dup != null)
                                    throw new Exception($"名称 {v} 已被编号 {dup.Index} 占用");

                                _renameSync.Rename(item.Name, v);
                                item.Name = v;
                            }
                            break;

                        case "ItemType": item.Type = Enum.Parse<ItemType>(v); break;
                        case "ItemGrade": item.Grade = Enum.Parse<ItemGrade>(v); break;
                        case "ItemRequiredType": item.RequiredType = Enum.Parse<RequiredType>(v); break;
                        case "ItemRequiredGender": item.RequiredGender = Enum.Parse<RequiredGender>(v); break;
                        case "ItemRequiredClass": item.RequiredClass = Enum.Parse<RequiredClass>(v); break;
                        case "ItemSet": item.Set = Enum.Parse<ItemSet>(v); break;
                        case "ItemRandomStatsId": item.RandomStatsId = byte.Parse(v); break;
                        case "ItemRequiredAmount": item.RequiredAmount = byte.Parse(v); break;
                        case "ItemImage": item.Image = ushort.Parse(v); break;
                        case "ItemShape": item.Shape = short.Parse(v); break;
                        case "ItemEffect": item.Effect = byte.Parse(v); break;
                        case "ItemStackSize": item.StackSize = ushort.Parse(v); break;
                        case "ItemSlots": item.Slots = byte.Parse(v); break;
                        case "ItemWeight": item.Weight = byte.Parse(v); break;
                        case "ItemLightRange":
                            if (byte.TryParse(v, out var lr))
                                item.Light = (byte)((item.Light / 15) * 15 + lr % 15);
                            break;
                        case "ItemLightIntensity":
                            if (byte.TryParse(v, out var li))
                                item.Light = (byte)((item.Light % 15) + Math.Min(li, (byte)17) * 15);
                            break;
                        case "ItemDurability": item.Durability = ushort.Parse(v); break;
                        case "ItemPrice": item.Price = uint.Parse(v); break;
                        case "ItemToolTip": item.ToolTip = v.Replace("\\r\\n", "\r\n"); break;

                        case "StartItem": item.StartItem = ParseBool(v); break;
                        case "NeedIdentify": item.NeedIdentify = ParseBool(v); break;
                        case "ShowGroupPickup": item.ShowGroupPickup = ParseBool(v); break;
                        case "GlobalDropNotify": item.GlobalDropNotify = ParseBool(v); break;
                        case "ClassBased": item.ClassBased = ParseBool(v); break;
                        case "LevelBased": item.LevelBased = ParseBool(v); break;
                        case "CanMine": item.CanMine = ParseBool(v); break;
                        case "CanFastRun": item.CanFastRun = ParseBool(v); break;
                        case "CanAwakening": item.CanAwakening = ParseBool(v); break;

                        default:
                            if (col.StartsWith("Stat"))
                                item.Stats[Enum.Parse<Stat>(col.Substring(4))] = int.Parse(v);
                            else if (col.StartsWith("Bind") && ParseBool(v))
                                item.Bind |= Enum.Parse<BindMode>(col.Substring(4));
                            else if (col.StartsWith("Special") && ParseBool(v))
                                item.Unique |= Enum.Parse<SpecialItemMode>(col.Substring(7));
                            break;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"第 {rowNumber} 行 [{item.Name}] 列 {col}={v}: {ex.Message}");
                }
            }

            return true;
        }

        private static bool ParseBool(string v) =>
            v == "1" || string.Equals(v, "True", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "-1";

        #endregion

        #region 物品改名 → 同步 Drops/Recipe/Quests/NPCs 脚本

        /// <summary>
        /// 惰性加载 + 批量缓冲: 只有发生改名才读脚本, 保存时只写有变化的文件.
        /// 逻辑与旧版 ItemInfoFormNew 保持一致(正则/匹配范围相同), 但不改动旧窗体.
        /// </summary>
        private sealed class ScriptRenameSync
        {
            private Dictionary<string, string[]> _dropFiles, _recipeFiles, _questFiles, _npcFiles;
            private bool _dropChanged, _recipeChanged, _questChanged, _npcChanged;

            private static Dictionary<string, string[]> LoadFiles(string path, string pattern)
            {
                var d = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                if (!Directory.Exists(path)) return d;
                foreach (var f in Directory.GetFiles(path, pattern, SearchOption.AllDirectories))
                    d[f] = File.ReadAllLines(f);
                return d;
            }

            public void Rename(string oldName, string newName)
            {
                if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName) ||
                    oldName.Equals(newName, StringComparison.OrdinalIgnoreCase)) return;

                _dropFiles ??= LoadFiles(@"Envir\Drops", "*.txt");
                _recipeFiles ??= LoadFiles(@"Envir\Recipe", "*.txt");
                _questFiles ??= LoadFiles(@"Envir\Quests", "*.txt");
                _npcFiles ??= LoadFiles(@"Envir\NPCs", "*.txt");

                foreach (var file in _dropFiles)
                {
                    var lines = file.Value;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        var match = Regex.Match(line, @"^\d+/\d+\s+(\S+)");
                        if (match.Success && match.Groups[1].Value.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = line.Replace(match.Groups[1].Value, newName);
                            _dropChanged = true;
                        }
                    }
                }

                foreach (var file in _recipeFiles.Keys.ToList())
                {
                    if (!_recipeFiles.ContainsKey(file)) continue;
                    var lines = _recipeFiles[file];

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("[")) continue;

                        var match = Regex.Match(line, @"^([^\s|[]+)(?=\s?)");
                        if (match.Success && match.Groups[1].Value.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = line.Replace(match.Groups[1].Value, newName);
                            _recipeChanged = true;
                        }
                    }

                    if (Path.GetFileNameWithoutExtension(file).Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    {
                        RenameRecipeFile(oldName, newName);
                        _recipeChanged = true;
                    }
                }

                foreach (var file in _questFiles)
                {
                    var lines = file.Value;
                    var isItem = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (line.StartsWith("[@ItemTasks]") || line.StartsWith("[@CarryItems]") ||
                            line.StartsWith("[@FixedRewards]") || line.StartsWith("[@SelectRewards]"))
                        {
                            isItem = true;
                        }
                        else if (string.IsNullOrWhiteSpace(line) || line.StartsWith("["))
                        {
                            isItem = false;
                        }
                        else if (isItem)
                        {
                            var match = Regex.Match(line, @"^([^\s|[]+)(?=\s?)");
                            if (match.Success && match.Groups[1].Value.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                            {
                                lines[i] = line.Replace(match.Groups[1].Value, newName);
                                _questChanged = true;
                            }
                        }
                    }
                }

                foreach (var file in _npcFiles)
                {
                    var lines = file.Value;
                    var isConversion = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        if (line.StartsWith("[RECIPE]") || line.StartsWith("[Trade]"))
                            isConversion = true;
                        else if (isConversion && line.StartsWith("["))
                            isConversion = false;
                        else if (isConversion)
                        {
                            var match = Regex.Match(line, @"^(\S+)(?=\s?)");
                            if (match.Success && match.Groups[1].Value.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                            {
                                lines[i] = line.Replace(match.Groups[1].Value, newName);
                                _npcChanged = true;
                            }
                        }
                    }
                }
            }

            private void RenameRecipeFile(string oldName, string newName)
            {
                var oldFilePath = _recipeFiles.Keys.FirstOrDefault(x =>
                    Path.GetFileNameWithoutExtension(x).Equals(oldName, StringComparison.OrdinalIgnoreCase));
                if (oldFilePath == null) return;

                var newFilePath = Path.Combine(Path.GetDirectoryName(oldFilePath), newName + Path.GetExtension(oldFilePath));
                if (File.Exists(oldFilePath)) File.Move(oldFilePath, newFilePath);
                _recipeFiles[newFilePath] = _recipeFiles[oldFilePath];
                _recipeFiles.Remove(oldFilePath);
            }

            public void Flush()
            {
                if (_dropChanged) WriteAll(_dropFiles);
                if (_recipeChanged) WriteAll(_recipeFiles);
                if (_questChanged) WriteAll(_questFiles);
                if (_npcChanged) WriteAll(_npcFiles);

                _dropChanged = _recipeChanged = _questChanged = _npcChanged = false;
            }

            private static void WriteAll(Dictionary<string, string[]> files)
            {
                foreach (var file in files)
                    File.WriteAllLines(file.Key, file.Value);
            }
        }

        #endregion
    }
}
