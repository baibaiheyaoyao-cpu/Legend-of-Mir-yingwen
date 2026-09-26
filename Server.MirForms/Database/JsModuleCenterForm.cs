using Server.MirScripting;
using System.Diagnostics;

namespace Server
{
    /// <summary>
    /// 脚本模块中心(JsScripting 总控面板).
    /// 管理 Envir\JsScripts\ 下的全部JS模块(如登仙任务模块 DengXian):
    ///   - 模块列表: 启用状态/NPC与物品绑定数/最后加载时间/加载错误
    ///   - 一键 启用/停用(写回manifest.json并热生效, 停用即回退经典@@脚本)
    ///   - 热重载全部模块(与游戏内GM命令 @ReloadJS 同源)
    ///   - 绑定明细: 选中模块接管了哪些NPC/物品
    ///   - 错误日志: 脚本编译/执行异常实时滚动(数据来自 JsScriptHost 环形缓冲)
    /// </summary>
    public class JsModuleCenterForm : Form
    {
        // ---- 控件 ----
        private readonly ListView _modulesLv;      // 模块列表
        private readonly ListView _bindLv;         // 选中模块的绑定明细
        private readonly ListBox _errList;         // 错误日志
        private readonly Button _btnToggle;        // 启用/停用
        private readonly Button _btnReload;        // 全部重载
        private readonly Button _btnOpenDir;       // 打开模块目录
        private readonly Button _btnClearErr;      // 清空日志
        private readonly Label _bindLabel;         // "绑定明细"标题(随选中模块变化)
        private readonly System.Windows.Forms.Timer _timer;   // 定时刷新(状态+日志)

        public JsModuleCenterForm()
        {
            Text = "脚本模块中心 (Envir\\JsScripts · 热重载 · 与 @ReloadJS 同源)";
            Size = new Size(940, 660);
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;

            // ===== 模块列表(顶部) =====
            _modulesLv = new ListView
            {
                Dock = DockStyle.Top,
                Height = 190,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = true,
            };
            _modulesLv.Columns.Add("模块目录", 120);
            _modulesLv.Columns.Add("显示名", 130);
            _modulesLv.Columns.Add("状态", 70);
            _modulesLv.Columns.Add("NPC绑定", 70);
            _modulesLv.Columns.Add("物品绑定", 75);
            _modulesLv.Columns.Add("最后加载", 130);
            _modulesLv.Columns.Add("加载错误", 260);
            _modulesLv.SelectedIndexChanged += (s, e) => RefreshBindings();

            // ===== 操作按钮行 =====
            var btnPanel = new Panel { Dock = DockStyle.Top, Height = 44 };
            _btnToggle = MkBtn("启用/停用(选中)", 10, 8, 140, ToggleSelected);
            _btnReload = MkBtn("全部重载(热)", 158, 8, 120, (s, e) => { JsScriptHost.ReloadAllModules(); RefreshModules(); });
            _btnOpenDir = MkBtn("打开模块目录", 286, 8, 120, (s, e) => OpenModuleDir());
            _btnClearErr = MkBtn("清空日志", 414, 8, 90, (s, e) => { JsScriptHost.ClearErrors(); RefreshErrors(); });
            btnPanel.Controls.Add(_btnToggle);
            btnPanel.Controls.Add(_btnReload);
            btnPanel.Controls.Add(_btnOpenDir);
            btnPanel.Controls.Add(_btnClearErr);

            // ===== 绑定明细(中部) =====
            _bindLabel = new Label { Dock = DockStyle.Top, Height = 24, Text = "绑定明细(点击上方模块查看)", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0) };
            _bindLv = new ListView
            {
                Dock = DockStyle.Top,
                Height = 170,
                View = View.Details,
                FullRowSelect = true,
            };
            _bindLv.Columns.Add("类型", 70);
            _bindLv.Columns.Add("NPC脚本路径 / 物品名", 320);
            _bindLv.Columns.Add("JS文件", 260);
            _bindLv.Columns.Add("状态", 80);

            // ===== 错误日志(底部填充) =====
            var errLabel = new Label { Dock = DockStyle.Top, Height = 24, Text = "错误日志(实时 · 上限300条 · 脚本异常不会崩服)", TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0) };
            _errList = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9F), IntegralHeight = false };

            // Dock布局: 填充控件必须最先加入, 其余按"视觉自下而上"顺序添加
            Controls.Add(_errList);
            Controls.Add(errLabel);
            Controls.Add(_bindLv);
            Controls.Add(_bindLabel);
            Controls.Add(btnPanel);
            Controls.Add(_modulesLv);

            // 定时器: 1.5秒刷新模块状态与错误日志(快照读取, 线程安全)
            _timer = new System.Windows.Forms.Timer { Interval = 1500 };
            _timer.Tick += (s, e) => { RefreshModules(); RefreshErrors(); };
            _timer.Start();

            Load += (s, e) => { RefreshModules(); RefreshErrors(); };
        }

        // ==================================================================================
        // 数据刷新
        // ==================================================================================

        /// <summary>刷新模块列表(保留当前选中, 便于观察状态变化)</summary>
        private void RefreshModules()
        {
            string selected = _modulesLv.SelectedItems.Count > 0 ? _modulesLv.SelectedItems[0].Text : null;

            _modulesLv.BeginUpdate();
            _modulesLv.Items.Clear();

            var modules = JsModuleRegistry.Modules; // 快照
            if (modules.Count == 0)
            {
                var empty = new ListViewItem("(无模块)");
                empty.SubItems.Add(""); empty.SubItems.Add("—");
                empty.SubItems.Add("将模块目录放入 Envir\\JsScripts\\ 并包含 manifest.json");
                empty.SubItems.Add(""); empty.SubItems.Add(""); empty.SubItems.Add("");
                _modulesLv.Items.Add(empty);
            }

            foreach (var m in modules)
            {
                var item = new ListViewItem(m.Name);
                item.SubItems.Add(m.DisplayName);
                item.SubItems.Add(m.Enabled ? "启用" : "停用");
                item.SubItems.Add(m.NpcFiles.Count.ToString());
                item.SubItems.Add(m.ItemFiles.Count.ToString());
                item.SubItems.Add(m.LastLoad.ToString("MM-dd HH:mm:ss"));
                item.SubItems.Add(m.LoadErrors.Count == 0 ? "" : string.Join("; ", m.LoadErrors));
                item.SubItems.Add(m.Enabled ? "1" : "0"); // Tag位: 隐藏状态列不显示, 备用
                item.UseItemStyleForSubItems = false;
                item.ForeColor = m.Enabled ? Color.Black : Color.Gray;
                _modulesLv.Items.Add(item);

                if (selected != null && m.Name == selected) item.Selected = true;
            }

            _modulesLv.EndUpdate();
            if (_modulesLv.SelectedItems.Count == 0) { _bindLv.Items.Clear(); _bindLabel.Text = "绑定明细(点击上方模块查看)"; }
        }

        /// <summary>刷新选中模块的绑定明细(NPC接管表+物品接管表)</summary>
        private void RefreshBindings()
        {
            _bindLv.Items.Clear();
            if (_modulesLv.SelectedItems.Count == 0) return;

            string moduleName = _modulesLv.SelectedItems[0].Text;
            var module = JsModuleRegistry.Modules.Find(m => m.Name == moduleName);
            if (module == null) return;

            _bindLabel.Text = $"绑定明细 — {module.DisplayName}({module.Name}): {module.Description}";

            foreach (var kv in module.NpcFiles)
                AddBindRow("NPC", kv.Key, Path.GetFileName(kv.Value), File.Exists(kv.Value) ? "OK" : "缺文件");

            foreach (var kv in module.ItemFiles)
                AddBindRow("物品", kv.Key, Path.GetFileName(kv.Value), File.Exists(kv.Value) ? "OK" : "缺文件");

            // 声明了但文件缺失的绑定也列出, 便于排查
            foreach (var err in module.LoadErrors)
                AddBindRow("错误", err, "", "!");
        }

        private void AddBindRow(string type, string key, string file, string status)
        {
            var row = new ListViewItem(type);
            row.SubItems.Add(key);
            row.SubItems.Add(file);
            row.SubItems.Add(status);
            if (status != "OK") row.ForeColor = Color.Firebrick;
            _bindLv.Items.Add(row);
        }

        /// <summary>刷新错误日志(只追加渲染, 数据取快照)</summary>
        private void RefreshErrors()
        {
            var errors = JsScriptHost.ErrorsSnapshot(100);
            _errList.BeginUpdate();
            _errList.Items.Clear();
            foreach (var err in errors)
                _errList.Items.Add($"{err.Time:HH:mm:ss} [{err.Script}] {err.Message}");
            if (_errList.Items.Count > 0) _errList.TopIndex = _errList.Items.Count - 1; // 滚动到最新
            _errList.EndUpdate();
        }

        // ==================================================================================
        // 按钮动作
        // ==================================================================================

        /// <summary>启用/停用选中模块: 写回manifest.json + 热重载(停用后该模块NPC/物品立即回退经典@@脚本)</summary>
        private void ToggleSelected(object sender, EventArgs e)
        {
            if (_modulesLv.SelectedItems.Count == 0)
            {
                MessageBox.Show("请先在上方列表选择一个模块.", "脚本模块中心", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string moduleName = _modulesLv.SelectedItems[0].Text;
            var module = JsModuleRegistry.Modules.Find(m => m.Name == moduleName);
            if (module == null) return;

            bool newState = !module.Enabled;
            if (JsModuleRegistry.SetEnabled(moduleName, newState))
            {
                RefreshModules();
                // 提示接管变化
                MessageBox.Show($"模块 [{module.DisplayName}] 已{(newState ? "启用" : "停用")}."
                    + (newState ? $"\n接管 {module.NpcFiles.Count} 个NPC / {module.ItemFiles.Count} 个物品(对话优先走JS)." : "\n相关NPC/物品回退经典@@脚本."),
                    "脚本模块中心", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("操作失败: manifest.json 写入异常, 详情见模块列表'加载错误'列.", "脚本模块中心", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>打开模块根目录(目录不存在则创建, 方便首次投放模块)</summary>
        private void OpenModuleDir()
        {
            try
            {
                if (!Directory.Exists(JsModuleRegistry.Root)) Directory.CreateDirectory(JsModuleRegistry.Root);
                Process.Start(new ProcessStartInfo("explorer.exe", JsModuleRegistry.Root) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开目录失败: " + ex.Message);
            }
        }

        /// <summary>构造按钮的辅助方法(统一外观)</summary>
        private static Button MkBtn(string text, int x, int y, int w, EventHandler onClick)
        {
            var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 28) };
            b.Click += onClick;
            return b;
        }
    }
}
