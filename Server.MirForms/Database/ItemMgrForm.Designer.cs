namespace Server.Database
{
    partial class ItemMgrForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnTranslate = new System.Windows.Forms.Button();
            this.btnImport = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnGameShop = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblDirty = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblSaved = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripSpring = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblServer = new System.Windows.Forms.ToolStripStatusLabel();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lvItems = new System.Windows.Forms.ListView();
            this.colhIndex = new System.Windows.Forms.ColumnHeader();
            this.colhName = new System.Windows.Forms.ColumnHeader();
            this.colhType = new System.Windows.Forms.ColumnHeader();
            this.colhGrade = new System.Windows.Forms.ColumnHeader();
            this.colhPrice = new System.Windows.Forms.ColumnHeader();
            this.tlpFilter = new System.Windows.Forms.TableLayoutPanel();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lblGradeF = new System.Windows.Forms.Label();
            this.cmbGradeFilter = new System.Windows.Forms.ComboBox();
            this.lblTypeF = new System.Windows.Forms.Label();
            this.cmbTypeFilter = new System.Windows.Forms.ComboBox();
            this.panelListBtns = new System.Windows.Forms.Panel();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnCopy = new System.Windows.Forms.Button();
            this.btnDel = new System.Windows.Forms.Button();
            this.tcEdit = new System.Windows.Forms.TabControl();
            this.tpBase = new System.Windows.Forms.TabPage();
            this.tlpBase = new System.Windows.Forms.TableLayoutPanel();
            this.lblIndex = new System.Windows.Forms.Label();
            this.txtIndex = new System.Windows.Forms.TextBox();
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.lblType = new System.Windows.Forms.Label();
            this.cmbTypeEdit = new System.Windows.Forms.ComboBox();
            this.lblGrade = new System.Windows.Forms.Label();
            this.cmbGradeEdit = new System.Windows.Forms.ComboBox();
            this.lblSet = new System.Windows.Forms.Label();
            this.cmbSetEdit = new System.Windows.Forms.ComboBox();
            this.lblShape = new System.Windows.Forms.Label();
            this.numShape = new System.Windows.Forms.NumericUpDown();
            this.lblImage = new System.Windows.Forms.Label();
            this.numImage = new System.Windows.Forms.NumericUpDown();
            this.lblEffect = new System.Windows.Forms.Label();
            this.numEffect = new System.Windows.Forms.NumericUpDown();
            this.lblSlots = new System.Windows.Forms.Label();
            this.numSlots = new System.Windows.Forms.NumericUpDown();
            this.lblWeight = new System.Windows.Forms.Label();
            this.numWeight = new System.Windows.Forms.NumericUpDown();
            this.lblDura = new System.Windows.Forms.Label();
            this.numDura = new System.Windows.Forms.NumericUpDown();
            this.lblStack = new System.Windows.Forms.Label();
            this.numStack = new System.Windows.Forms.NumericUpDown();
            this.lblPrice = new System.Windows.Forms.Label();
            this.numPrice = new System.Windows.Forms.NumericUpDown();
            this.lblReqType = new System.Windows.Forms.Label();
            this.cmbReqType = new System.Windows.Forms.ComboBox();
            this.lblReqAmount = new System.Windows.Forms.Label();
            this.numReqAmount = new System.Windows.Forms.NumericUpDown();
            this.lblReqClass = new System.Windows.Forms.Label();
            this.cmbReqClass = new System.Windows.Forms.ComboBox();
            this.lblReqGender = new System.Windows.Forms.Label();
            this.cmbReqGender = new System.Windows.Forms.ComboBox();
            this.lblRandomStatsId = new System.Windows.Forms.Label();
            this.numRandomStatsId = new System.Windows.Forms.NumericUpDown();
            this.lblLightRange = new System.Windows.Forms.Label();
            this.numLightRange = new System.Windows.Forms.NumericUpDown();
            this.lblLightIntensity = new System.Windows.Forms.Label();
            this.numLightIntensity = new System.Windows.Forms.NumericUpDown();
            this.lblStartItem = new System.Windows.Forms.Label();
            this.chkStartItem = new System.Windows.Forms.CheckBox();
            this.lblToolTip = new System.Windows.Forms.Label();
            this.txtToolTip = new System.Windows.Forms.TextBox();
            this.tpStats = new System.Windows.Forms.TabPage();
            this.dgvStats = new System.Windows.Forms.DataGridView();
            this.colStatName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chkShowAllStats = new System.Windows.Forms.CheckBox();
            this.tpBind = new System.Windows.Forms.TabPage();
            this.flpBind = new System.Windows.Forms.FlowLayoutPanel();
            this.tpSpecial = new System.Windows.Forms.TabPage();
            this.lblSpecialHint = new System.Windows.Forms.Label();
            this.flpSpecial = new System.Windows.Forms.FlowLayoutPanel();
            this.grpFlags = new System.Windows.Forms.GroupBox();
            this.flpFlags = new System.Windows.Forms.FlowLayoutPanel();
            this.chkNeedIdentify = new System.Windows.Forms.CheckBox();
            this.chkShowGroupPickup = new System.Windows.Forms.CheckBox();
            this.chkGlobalDropNotify = new System.Windows.Forms.CheckBox();
            this.chkClassBased = new System.Windows.Forms.CheckBox();
            this.chkLevelBased = new System.Windows.Forms.CheckBox();
            this.chkCanMine = new System.Windows.Forms.CheckBox();
            this.chkCanFastRun = new System.Windows.Forms.CheckBox();
            this.chkCanAwakening = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tlpFilter.SuspendLayout();
            this.panelListBtns.SuspendLayout();
            this.tcEdit.SuspendLayout();
            this.tpBase.SuspendLayout();
            this.tlpBase.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numShape)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numImage)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEffect)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSlots)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWeight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDura)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStack)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPrice)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLightRange)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLightIntensity)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numReqAmount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRandomStatsId)).BeginInit();
            this.tpStats.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvStats)).BeginInit();
            this.tpSpecial.SuspendLayout();
            this.grpFlags.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.btnSave);
            this.panelTop.Controls.Add(this.btnTranslate);
            this.panelTop.Controls.Add(this.btnImport);
            this.panelTop.Controls.Add(this.btnExport);
            this.panelTop.Controls.Add(this.btnGameShop);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(8, 8, 0, 0);
            this.panelTop.Size = new System.Drawing.Size(1284, 44);
            this.panelTop.TabIndex = 0;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(11, 8);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(120, 28);
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "保存 (Ctrl+S)";
            this.btnSave.UseVisualStyleBackColor = true;
            // 
            // btnTranslate
            // 
            this.btnTranslate.Location = new System.Drawing.Point(137, 8);
            this.btnTranslate.Name = "btnTranslate";
            this.btnTranslate.Size = new System.Drawing.Size(130, 28);
            this.btnTranslate.TabIndex = 1;
            this.btnTranslate.Text = "批量翻译英文名";
            this.btnTranslate.UseVisualStyleBackColor = true;
            // 
            // btnImport
            // 
            this.btnImport.Location = new System.Drawing.Point(273, 8);
            this.btnImport.Name = "btnImport";
            this.btnImport.Size = new System.Drawing.Size(96, 28);
            this.btnImport.TabIndex = 2;
            this.btnImport.Text = "导入CSV";
            this.btnImport.UseVisualStyleBackColor = true;
            // 
            // btnExport
            // 
            this.btnExport.Location = new System.Drawing.Point(375, 8);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(96, 28);
            this.btnExport.TabIndex = 3;
            this.btnExport.Text = "导出CSV";
            this.btnExport.UseVisualStyleBackColor = true;
            // 
            // btnGameShop
            // 
            this.btnGameShop.Location = new System.Drawing.Point(477, 8);
            this.btnGameShop.Name = "btnGameShop";
            this.btnGameShop.Size = new System.Drawing.Size(90, 28);
            this.btnGameShop.TabIndex = 4;
            this.btnGameShop.Text = "+ 商城";
            this.btnGameShop.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblDirty,
            this.lblSaved,
            this.toolStripSpring,
            this.lblServer});
            this.statusStrip1.Location = new System.Drawing.Point(0, 738);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1284, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblDirty
            // 
            this.lblDirty.Name = "lblDirty";
            this.lblDirty.Size = new System.Drawing.Size(80, 17);
            this.lblDirty.Text = "无未保存修改";
            // 
            // lblSaved
            // 
            this.lblSaved.Name = "lblSaved";
            this.lblSaved.Size = new System.Drawing.Size(44, 17);
            this.lblSaved.Text = "未保存";
            // 
            // toolStripSpring
            // 
            this.toolStripSpring.Name = "toolStripSpring";
            this.toolStripSpring.Size = new System.Drawing.Size(1057, 17);
            this.toolStripSpring.Spring = true;
            // 
            // lblServer
            // 
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(68, 17);
            this.lblServer.Text = "服务器状态";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 44);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lvItems);
            this.splitContainer1.Panel1.Controls.Add(this.tlpFilter);
            this.splitContainer1.Panel1.Controls.Add(this.panelListBtns);
            this.splitContainer1.Panel1MinSize = 320;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tcEdit);
            this.splitContainer1.Size = new System.Drawing.Size(1284, 694);
            this.splitContainer1.SplitterDistance = 420;
            this.splitContainer1.SplitterWidth = 6;
            this.splitContainer1.TabIndex = 2;
            // 
            // lvItems
            // 
            this.lvItems.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colhIndex,
            this.colhName,
            this.colhType,
            this.colhGrade,
            this.colhPrice});
            this.lvItems.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lvItems.FullRowSelect = true;
            this.lvItems.HideSelection = false;
            this.lvItems.Location = new System.Drawing.Point(0, 68);
            this.lvItems.MultiSelect = false;
            this.lvItems.Name = "lvItems";
            this.lvItems.Size = new System.Drawing.Size(420, 586);
            this.lvItems.TabIndex = 2;
            this.lvItems.View = System.Windows.Forms.View.Details;
            this.lvItems.VirtualMode = true;
            // 
            // colhIndex
            // 
            this.colhIndex.Text = "编号";
            this.colhIndex.Width = 58;
            // 
            // colhName
            // 
            this.colhName.Text = "名称";
            this.colhName.Width = 160;
            // 
            // colhType
            // 
            this.colhType.Text = "类型";
            this.colhType.Width = 92;
            // 
            // colhGrade
            // 
            this.colhGrade.Text = "品质";
            this.colhGrade.Width = 66;
            // 
            // colhPrice
            // 
            this.colhPrice.Text = "价格";
            this.colhPrice.Width = 78;
            // 
            // tlpFilter
            // 
            this.tlpFilter.ColumnCount = 4;
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            this.tlpFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpFilter.Controls.Add(this.lblSearch, 0, 0);
            this.tlpFilter.Controls.Add(this.txtSearch, 1, 0);
            this.tlpFilter.Controls.Add(this.lblGradeF, 2, 0);
            this.tlpFilter.Controls.Add(this.cmbGradeFilter, 3, 0);
            this.tlpFilter.Controls.Add(this.lblTypeF, 0, 1);
            this.tlpFilter.Controls.Add(this.cmbTypeFilter, 1, 1);
            this.tlpFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.tlpFilter.Location = new System.Drawing.Point(0, 0);
            this.tlpFilter.Name = "tlpFilter";
            this.tlpFilter.Padding = new System.Windows.Forms.Padding(4);
            this.tlpFilter.RowCount = 2;
            this.tlpFilter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpFilter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tlpFilter.Size = new System.Drawing.Size(420, 68);
            this.tlpFilter.TabIndex = 0;
            // 
            // lblSearch
            // 
            this.lblSearch.AutoSize = true;
            this.lblSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSearch.Location = new System.Drawing.Point(7, 4);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(38, 30);
            this.lblSearch.TabIndex = 0;
            this.lblSearch.Text = "搜索";
            this.lblSearch.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtSearch
            // 
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.Location = new System.Drawing.Point(51, 7);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(155, 23);
            this.txtSearch.TabIndex = 1;
            // 
            // lblGradeF
            // 
            this.lblGradeF.AutoSize = true;
            this.lblGradeF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGradeF.Location = new System.Drawing.Point(212, 4);
            this.lblGradeF.Name = "lblGradeF";
            this.lblGradeF.Size = new System.Drawing.Size(38, 30);
            this.lblGradeF.TabIndex = 2;
            this.lblGradeF.Text = "品质";
            this.lblGradeF.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbGradeFilter
            // 
            this.cmbGradeFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbGradeFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGradeFilter.Location = new System.Drawing.Point(256, 7);
            this.cmbGradeFilter.Name = "cmbGradeFilter";
            this.cmbGradeFilter.Size = new System.Drawing.Size(157, 25);
            this.cmbGradeFilter.TabIndex = 3;
            // 
            // lblTypeF
            // 
            this.lblTypeF.AutoSize = true;
            this.lblTypeF.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTypeF.Location = new System.Drawing.Point(7, 34);
            this.lblTypeF.Name = "lblTypeF";
            this.lblTypeF.Size = new System.Drawing.Size(38, 30);
            this.lblTypeF.TabIndex = 4;
            this.lblTypeF.Text = "类型";
            this.lblTypeF.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbTypeFilter
            // 
            this.cmbTypeFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbTypeFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTypeFilter.Location = new System.Drawing.Point(51, 37);
            this.cmbTypeFilter.Name = "cmbTypeFilter";
            this.cmbTypeFilter.Size = new System.Drawing.Size(155, 25);
            this.cmbTypeFilter.TabIndex = 5;
            // 
            // panelListBtns
            // 
            this.panelListBtns.Controls.Add(this.btnAdd);
            this.panelListBtns.Controls.Add(this.btnCopy);
            this.panelListBtns.Controls.Add(this.btnDel);
            this.panelListBtns.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelListBtns.Location = new System.Drawing.Point(0, 654);
            this.panelListBtns.Name = "panelListBtns";
            this.panelListBtns.Padding = new System.Windows.Forms.Padding(6, 6, 0, 6);
            this.panelListBtns.Size = new System.Drawing.Size(420, 40);
            this.panelListBtns.TabIndex = 1;
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(9, 6);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(80, 28);
            this.btnAdd.TabIndex = 0;
            this.btnAdd.Text = "新增";
            this.btnAdd.UseVisualStyleBackColor = true;
            // 
            // btnCopy
            // 
            this.btnCopy.Location = new System.Drawing.Point(95, 6);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(80, 28);
            this.btnCopy.TabIndex = 1;
            this.btnCopy.Text = "复制";
            this.btnCopy.UseVisualStyleBackColor = true;
            // 
            // btnDel
            // 
            this.btnDel.Location = new System.Drawing.Point(181, 6);
            this.btnDel.Name = "btnDel";
            this.btnDel.Size = new System.Drawing.Size(80, 28);
            this.btnDel.TabIndex = 2;
            this.btnDel.Text = "删除";
            this.btnDel.UseVisualStyleBackColor = true;
            // 
            // tcEdit
            // 
            this.tcEdit.Controls.Add(this.tpBase);
            this.tcEdit.Controls.Add(this.tpStats);
            this.tcEdit.Controls.Add(this.tpBind);
            this.tcEdit.Controls.Add(this.tpSpecial);
            this.tcEdit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tcEdit.Location = new System.Drawing.Point(0, 0);
            this.tcEdit.Name = "tcEdit";
            this.tcEdit.SelectedIndex = 0;
            this.tcEdit.Size = new System.Drawing.Size(858, 694);
            this.tcEdit.TabIndex = 0;
            // 
            // tpBase
            // 
            this.tpBase.AutoScroll = true;
            this.tpBase.Controls.Add(this.tlpBase);
            this.tpBase.Location = new System.Drawing.Point(4, 28);
            this.tpBase.Name = "tpBase";
            this.tpBase.Padding = new System.Windows.Forms.Padding(8);
            this.tpBase.Size = new System.Drawing.Size(850, 662);
            this.tpBase.TabIndex = 0;
            this.tpBase.Text = "基础";
            this.tpBase.UseVisualStyleBackColor = true;
            // 
            // tlpBase
            // 
            this.tlpBase.AutoScroll = true;
            this.tlpBase.ColumnCount = 4;
            this.tlpBase.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.tlpBase.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBase.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.tlpBase.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tlpBase.Controls.Add(this.lblIndex, 0, 0);
            this.tlpBase.Controls.Add(this.txtIndex, 1, 0);
            this.tlpBase.Controls.Add(this.lblName, 2, 0);
            this.tlpBase.Controls.Add(this.txtName, 3, 0);
            this.tlpBase.Controls.Add(this.lblType, 0, 1);
            this.tlpBase.Controls.Add(this.cmbTypeEdit, 1, 1);
            this.tlpBase.Controls.Add(this.lblGrade, 2, 1);
            this.tlpBase.Controls.Add(this.cmbGradeEdit, 3, 1);
            this.tlpBase.Controls.Add(this.lblSet, 0, 2);
            this.tlpBase.Controls.Add(this.cmbSetEdit, 1, 2);
            this.tlpBase.Controls.Add(this.lblShape, 2, 2);
            this.tlpBase.Controls.Add(this.numShape, 3, 2);
            this.tlpBase.Controls.Add(this.lblImage, 0, 3);
            this.tlpBase.Controls.Add(this.numImage, 1, 3);
            this.tlpBase.Controls.Add(this.lblEffect, 2, 3);
            this.tlpBase.Controls.Add(this.numEffect, 3, 3);
            this.tlpBase.Controls.Add(this.lblSlots, 0, 4);
            this.tlpBase.Controls.Add(this.numSlots, 1, 4);
            this.tlpBase.Controls.Add(this.lblWeight, 2, 4);
            this.tlpBase.Controls.Add(this.numWeight, 3, 4);
            this.tlpBase.Controls.Add(this.lblDura, 0, 5);
            this.tlpBase.Controls.Add(this.numDura, 1, 5);
            this.tlpBase.Controls.Add(this.lblStack, 2, 5);
            this.tlpBase.Controls.Add(this.numStack, 3, 5);
            this.tlpBase.Controls.Add(this.lblPrice, 0, 6);
            this.tlpBase.Controls.Add(this.numPrice, 1, 6);
            this.tlpBase.Controls.Add(this.lblReqType, 2, 6);
            this.tlpBase.Controls.Add(this.cmbReqType, 3, 6);
            this.tlpBase.Controls.Add(this.lblReqAmount, 0, 7);
            this.tlpBase.Controls.Add(this.numReqAmount, 1, 7);
            this.tlpBase.Controls.Add(this.lblReqClass, 2, 7);
            this.tlpBase.Controls.Add(this.cmbReqClass, 3, 7);
            this.tlpBase.Controls.Add(this.lblReqGender, 0, 8);
            this.tlpBase.Controls.Add(this.cmbReqGender, 1, 8);
            this.tlpBase.Controls.Add(this.lblRandomStatsId, 2, 8);
            this.tlpBase.Controls.Add(this.numRandomStatsId, 3, 8);
            this.tlpBase.Controls.Add(this.lblLightRange, 0, 9);
            this.tlpBase.Controls.Add(this.numLightRange, 1, 9);
            this.tlpBase.Controls.Add(this.lblLightIntensity, 2, 9);
            this.tlpBase.Controls.Add(this.numLightIntensity, 3, 9);
            this.tlpBase.Controls.Add(this.lblStartItem, 0, 10);
            this.tlpBase.Controls.Add(this.chkStartItem, 1, 10);
            this.tlpBase.Controls.Add(this.lblToolTip, 0, 11);
            this.tlpBase.Controls.Add(this.txtToolTip, 1, 11);
            this.tlpBase.SetColumnSpan(this.txtToolTip, 3);
            this.tlpBase.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpBase.Location = new System.Drawing.Point(8, 8);
            this.tlpBase.Name = "tlpBase";
            this.tlpBase.RowCount = 12;
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tlpBase.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpBase.Size = new System.Drawing.Size(834, 646);
            this.tlpBase.TabIndex = 0;
            // 
            // lblIndex
            // 
            this.lblIndex.AutoSize = true;
            this.lblIndex.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblIndex.Location = new System.Drawing.Point(3, 0);
            this.lblIndex.Name = "lblIndex";
            this.lblIndex.Size = new System.Drawing.Size(84, 34);
            this.lblIndex.TabIndex = 0;
            this.lblIndex.Text = "编号";
            this.lblIndex.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtIndex
            // 
            this.txtIndex.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtIndex.Location = new System.Drawing.Point(93, 5);
            this.txtIndex.Name = "txtIndex";
            this.txtIndex.ReadOnly = true;
            this.txtIndex.Size = new System.Drawing.Size(314, 23);
            this.txtIndex.TabIndex = 1;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblName.Location = new System.Drawing.Point(413, 0);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(84, 34);
            this.lblName.TabIndex = 2;
            this.lblName.Text = "名称";
            this.lblName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtName
            // 
            this.txtName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtName.Location = new System.Drawing.Point(503, 5);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(328, 23);
            this.txtName.TabIndex = 3;
            // 
            // lblType
            // 
            this.lblType.AutoSize = true;
            this.lblType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblType.Location = new System.Drawing.Point(3, 34);
            this.lblType.Name = "lblType";
            this.lblType.Size = new System.Drawing.Size(84, 34);
            this.lblType.TabIndex = 4;
            this.lblType.Text = "类型";
            this.lblType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbTypeEdit
            // 
            this.cmbTypeEdit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbTypeEdit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTypeEdit.Location = new System.Drawing.Point(93, 37);
            this.cmbTypeEdit.Name = "cmbTypeEdit";
            this.cmbTypeEdit.Size = new System.Drawing.Size(314, 25);
            this.cmbTypeEdit.TabIndex = 5;
            // 
            // lblGrade
            // 
            this.lblGrade.AutoSize = true;
            this.lblGrade.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblGrade.Location = new System.Drawing.Point(413, 34);
            this.lblGrade.Name = "lblGrade";
            this.lblGrade.Size = new System.Drawing.Size(84, 34);
            this.lblGrade.TabIndex = 6;
            this.lblGrade.Text = "品质";
            this.lblGrade.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbGradeEdit
            // 
            this.cmbGradeEdit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbGradeEdit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbGradeEdit.Location = new System.Drawing.Point(503, 37);
            this.cmbGradeEdit.Name = "cmbGradeEdit";
            this.cmbGradeEdit.Size = new System.Drawing.Size(328, 25);
            this.cmbGradeEdit.TabIndex = 7;
            // 
            // lblSet
            // 
            this.lblSet.AutoSize = true;
            this.lblSet.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSet.Location = new System.Drawing.Point(3, 68);
            this.lblSet.Name = "lblSet";
            this.lblSet.Size = new System.Drawing.Size(84, 34);
            this.lblSet.TabIndex = 8;
            this.lblSet.Text = "套装";
            this.lblSet.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbSetEdit
            // 
            this.cmbSetEdit.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbSetEdit.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSetEdit.Location = new System.Drawing.Point(93, 71);
            this.cmbSetEdit.Name = "cmbSetEdit";
            this.cmbSetEdit.Size = new System.Drawing.Size(314, 25);
            this.cmbSetEdit.TabIndex = 9;
            // 
            // lblShape
            // 
            this.lblShape.AutoSize = true;
            this.lblShape.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblShape.Location = new System.Drawing.Point(413, 68);
            this.lblShape.Name = "lblShape";
            this.lblShape.Size = new System.Drawing.Size(84, 34);
            this.lblShape.TabIndex = 10;
            this.lblShape.Text = "形状";
            this.lblShape.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numShape
            // 
            this.numShape.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numShape.Location = new System.Drawing.Point(503, 71);
            this.numShape.Maximum = new decimal(new int[] {
            32767,
            0,
            0,
            0});
            this.numShape.Minimum = new decimal(new int[] {
            32768,
            0,
            0,
            -2147483648});
            this.numShape.Name = "numShape";
            this.numShape.Size = new System.Drawing.Size(328, 23);
            this.numShape.TabIndex = 11;
            // 
            // lblImage
            // 
            this.lblImage.AutoSize = true;
            this.lblImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblImage.Location = new System.Drawing.Point(3, 102);
            this.lblImage.Name = "lblImage";
            this.lblImage.Size = new System.Drawing.Size(84, 34);
            this.lblImage.TabIndex = 12;
            this.lblImage.Text = "外观";
            this.lblImage.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numImage
            // 
            this.numImage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numImage.Location = new System.Drawing.Point(93, 105);
            this.numImage.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numImage.Name = "numImage";
            this.numImage.Size = new System.Drawing.Size(314, 23);
            this.numImage.TabIndex = 13;
            // 
            // lblEffect
            // 
            this.lblEffect.AutoSize = true;
            this.lblEffect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblEffect.Location = new System.Drawing.Point(413, 102);
            this.lblEffect.Name = "lblEffect";
            this.lblEffect.Size = new System.Drawing.Size(84, 34);
            this.lblEffect.TabIndex = 14;
            this.lblEffect.Text = "效果";
            this.lblEffect.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numEffect
            // 
            this.numEffect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numEffect.Location = new System.Drawing.Point(503, 105);
            this.numEffect.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numEffect.Name = "numEffect";
            this.numEffect.Size = new System.Drawing.Size(328, 23);
            this.numEffect.TabIndex = 15;
            // 
            // lblSlots
            // 
            this.lblSlots.AutoSize = true;
            this.lblSlots.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblSlots.Location = new System.Drawing.Point(3, 136);
            this.lblSlots.Name = "lblSlots";
            this.lblSlots.Size = new System.Drawing.Size(84, 34);
            this.lblSlots.TabIndex = 16;
            this.lblSlots.Text = "插槽";
            this.lblSlots.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numSlots
            // 
            this.numSlots.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numSlots.Location = new System.Drawing.Point(93, 139);
            this.numSlots.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numSlots.Name = "numSlots";
            this.numSlots.Size = new System.Drawing.Size(314, 23);
            this.numSlots.TabIndex = 17;
            // 
            // lblWeight
            // 
            this.lblWeight.AutoSize = true;
            this.lblWeight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblWeight.Location = new System.Drawing.Point(413, 136);
            this.lblWeight.Name = "lblWeight";
            this.lblWeight.Size = new System.Drawing.Size(84, 34);
            this.lblWeight.TabIndex = 18;
            this.lblWeight.Text = "重量";
            this.lblWeight.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numWeight
            // 
            this.numWeight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numWeight.Location = new System.Drawing.Point(503, 139);
            this.numWeight.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numWeight.Name = "numWeight";
            this.numWeight.Size = new System.Drawing.Size(328, 23);
            this.numWeight.TabIndex = 19;
            // 
            // lblDura
            // 
            this.lblDura.AutoSize = true;
            this.lblDura.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDura.Location = new System.Drawing.Point(3, 170);
            this.lblDura.Name = "lblDura";
            this.lblDura.Size = new System.Drawing.Size(84, 34);
            this.lblDura.TabIndex = 20;
            this.lblDura.Text = "持久";
            this.lblDura.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numDura
            // 
            this.numDura.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numDura.Location = new System.Drawing.Point(93, 173);
            this.numDura.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numDura.Name = "numDura";
            this.numDura.Size = new System.Drawing.Size(314, 23);
            this.numDura.TabIndex = 21;
            // 
            // lblStack
            // 
            this.lblStack.AutoSize = true;
            this.lblStack.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStack.Location = new System.Drawing.Point(413, 170);
            this.lblStack.Name = "lblStack";
            this.lblStack.Size = new System.Drawing.Size(84, 34);
            this.lblStack.TabIndex = 22;
            this.lblStack.Text = "叠加数";
            this.lblStack.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numStack
            // 
            this.numStack.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numStack.Location = new System.Drawing.Point(503, 173);
            this.numStack.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.numStack.Name = "numStack";
            this.numStack.Size = new System.Drawing.Size(328, 23);
            this.numStack.TabIndex = 23;
            // 
            // lblPrice
            // 
            this.lblPrice.AutoSize = true;
            this.lblPrice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPrice.Location = new System.Drawing.Point(3, 204);
            this.lblPrice.Name = "lblPrice";
            this.lblPrice.Size = new System.Drawing.Size(84, 34);
            this.lblPrice.TabIndex = 24;
            this.lblPrice.Text = "价格";
            this.lblPrice.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numPrice
            // 
            this.numPrice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numPrice.Location = new System.Drawing.Point(93, 207);
            this.numPrice.Maximum = new decimal(new int[] {
            -1,
            0,
            0,
            0});
            this.numPrice.Name = "numPrice";
            this.numPrice.Size = new System.Drawing.Size(314, 23);
            this.numPrice.TabIndex = 25;
            // 
            // lblReqType
            // 
            this.lblReqType.AutoSize = true;
            this.lblReqType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblReqType.Location = new System.Drawing.Point(413, 204);
            this.lblReqType.Name = "lblReqType";
            this.lblReqType.Size = new System.Drawing.Size(84, 34);
            this.lblReqType.TabIndex = 26;
            this.lblReqType.Text = "需求类型";
            this.lblReqType.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbReqType
            // 
            this.cmbReqType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbReqType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbReqType.Location = new System.Drawing.Point(503, 207);
            this.cmbReqType.Name = "cmbReqType";
            this.cmbReqType.Size = new System.Drawing.Size(328, 25);
            this.cmbReqType.TabIndex = 27;
            // 
            // lblReqAmount
            // 
            this.lblReqAmount.AutoSize = true;
            this.lblReqAmount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblReqAmount.Location = new System.Drawing.Point(3, 238);
            this.lblReqAmount.Name = "lblReqAmount";
            this.lblReqAmount.Size = new System.Drawing.Size(84, 34);
            this.lblReqAmount.TabIndex = 28;
            this.lblReqAmount.Text = "需求数值";
            this.lblReqAmount.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numReqAmount
            // 
            this.numReqAmount.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numReqAmount.Location = new System.Drawing.Point(93, 241);
            this.numReqAmount.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numReqAmount.Name = "numReqAmount";
            this.numReqAmount.Size = new System.Drawing.Size(314, 23);
            this.numReqAmount.TabIndex = 29;
            // 
            // lblReqClass
            // 
            this.lblReqClass.AutoSize = true;
            this.lblReqClass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblReqClass.Location = new System.Drawing.Point(413, 238);
            this.lblReqClass.Name = "lblReqClass";
            this.lblReqClass.Size = new System.Drawing.Size(84, 34);
            this.lblReqClass.TabIndex = 30;
            this.lblReqClass.Text = "需求职业";
            this.lblReqClass.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbReqClass
            // 
            this.cmbReqClass.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbReqClass.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbReqClass.Location = new System.Drawing.Point(503, 241);
            this.cmbReqClass.Name = "cmbReqClass";
            this.cmbReqClass.Size = new System.Drawing.Size(328, 25);
            this.cmbReqClass.TabIndex = 31;
            // 
            // lblReqGender
            // 
            this.lblReqGender.AutoSize = true;
            this.lblReqGender.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblReqGender.Location = new System.Drawing.Point(3, 272);
            this.lblReqGender.Name = "lblReqGender";
            this.lblReqGender.Size = new System.Drawing.Size(84, 34);
            this.lblReqGender.TabIndex = 32;
            this.lblReqGender.Text = "需求性别";
            this.lblReqGender.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // cmbReqGender
            // 
            this.cmbReqGender.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbReqGender.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbReqGender.Location = new System.Drawing.Point(93, 275);
            this.cmbReqGender.Name = "cmbReqGender";
            this.cmbReqGender.Size = new System.Drawing.Size(314, 25);
            this.cmbReqGender.TabIndex = 33;
            // 
            // lblRandomStatsId
            // 
            this.lblRandomStatsId.AutoSize = true;
            this.lblRandomStatsId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRandomStatsId.Location = new System.Drawing.Point(413, 272);
            this.lblRandomStatsId.Name = "lblRandomStatsId";
            this.lblRandomStatsId.Size = new System.Drawing.Size(84, 34);
            this.lblRandomStatsId.TabIndex = 34;
            this.lblRandomStatsId.Text = "随机属性";
            this.lblRandomStatsId.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numRandomStatsId
            // 
            this.numRandomStatsId.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numRandomStatsId.Location = new System.Drawing.Point(503, 275);
            this.numRandomStatsId.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.numRandomStatsId.Name = "numRandomStatsId";
            this.numRandomStatsId.Size = new System.Drawing.Size(328, 23);
            this.numRandomStatsId.TabIndex = 35;
            // 
            // lblLightRange
            // 
            this.lblLightRange.AutoSize = true;
            this.lblLightRange.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLightRange.Location = new System.Drawing.Point(3, 306);
            this.lblLightRange.Name = "lblLightRange";
            this.lblLightRange.Size = new System.Drawing.Size(84, 34);
            this.lblLightRange.TabIndex = 36;
            this.lblLightRange.Text = "光照范围";
            this.lblLightRange.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numLightRange
            // 
            this.numLightRange.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numLightRange.Location = new System.Drawing.Point(93, 309);
            this.numLightRange.Maximum = new decimal(new int[] {
            14,
            0,
            0,
            0});
            this.numLightRange.Name = "numLightRange";
            this.numLightRange.Size = new System.Drawing.Size(314, 23);
            this.numLightRange.TabIndex = 37;
            // 
            // lblLightIntensity
            // 
            this.lblLightIntensity.AutoSize = true;
            this.lblLightIntensity.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLightIntensity.Location = new System.Drawing.Point(413, 306);
            this.lblLightIntensity.Name = "lblLightIntensity";
            this.lblLightIntensity.Size = new System.Drawing.Size(84, 34);
            this.lblLightIntensity.TabIndex = 38;
            this.lblLightIntensity.Text = "光照强度";
            this.lblLightIntensity.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // numLightIntensity
            // 
            this.numLightIntensity.Dock = System.Windows.Forms.DockStyle.Fill;
            this.numLightIntensity.Location = new System.Drawing.Point(503, 309);
            this.numLightIntensity.Maximum = new decimal(new int[] {
            17,
            0,
            0,
            0});
            this.numLightIntensity.Name = "numLightIntensity";
            this.numLightIntensity.Size = new System.Drawing.Size(328, 23);
            this.numLightIntensity.TabIndex = 39;
            // 
            // lblStartItem
            // 
            this.lblStartItem.AutoSize = true;
            this.lblStartItem.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStartItem.Location = new System.Drawing.Point(3, 340);
            this.lblStartItem.Name = "lblStartItem";
            this.lblStartItem.Size = new System.Drawing.Size(84, 34);
            this.lblStartItem.TabIndex = 40;
            this.lblStartItem.Text = "开局物品";
            this.lblStartItem.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // chkStartItem
            // 
            this.chkStartItem.AutoSize = true;
            this.chkStartItem.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chkStartItem.Location = new System.Drawing.Point(93, 343);
            this.chkStartItem.Name = "chkStartItem";
            this.chkStartItem.Size = new System.Drawing.Size(314, 28);
            this.chkStartItem.TabIndex = 41;
            this.chkStartItem.Text = "新角色开局携带";
            this.chkStartItem.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.chkStartItem.UseVisualStyleBackColor = true;
            // 
            // lblToolTip
            // 
            this.lblToolTip.AutoSize = true;
            this.lblToolTip.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblToolTip.Location = new System.Drawing.Point(3, 374);
            this.lblToolTip.Name = "lblToolTip";
            this.lblToolTip.Size = new System.Drawing.Size(84, 272);
            this.lblToolTip.TabIndex = 42;
            this.lblToolTip.Text = "提示";
            this.lblToolTip.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // txtToolTip
            // 
            this.txtToolTip.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtToolTip.Location = new System.Drawing.Point(93, 377);
            this.txtToolTip.Multiline = true;
            this.txtToolTip.Name = "txtToolTip";
            this.txtToolTip.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtToolTip.Size = new System.Drawing.Size(738, 266);
            this.txtToolTip.TabIndex = 43;
            // 
            // tpStats
            // 
            this.tpStats.Controls.Add(this.dgvStats);
            this.tpStats.Controls.Add(this.chkShowAllStats);
            this.tpStats.Location = new System.Drawing.Point(4, 28);
            this.tpStats.Name = "tpStats";
            this.tpStats.Padding = new System.Windows.Forms.Padding(8);
            this.tpStats.Size = new System.Drawing.Size(850, 662);
            this.tpStats.TabIndex = 1;
            this.tpStats.Text = "属性";
            this.tpStats.UseVisualStyleBackColor = true;
            // 
            // dgvStats
            // 
            this.dgvStats.AllowUserToAddRows = false;
            this.dgvStats.AllowUserToDeleteRows = false;
            this.dgvStats.AllowUserToResizeRows = false;
            this.dgvStats.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this.dgvStats.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colStatName,
            this.colStatValue});
            this.dgvStats.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvStats.Location = new System.Drawing.Point(8, 36);
            this.dgvStats.MultiSelect = false;
            this.dgvStats.Name = "dgvStats";
            this.dgvStats.ReadOnly = true;
            this.dgvStats.RowHeadersVisible = false;
            this.dgvStats.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dgvStats.Size = new System.Drawing.Size(834, 618);
            this.dgvStats.TabIndex = 1;
            // 
            // colStatName
            // 
            this.colStatName.FillWeight = 65F;
            this.colStatName.HeaderText = "属性";
            this.colStatName.Name = "colStatName";
            this.colStatName.ReadOnly = true;
            // 
            // colStatValue
            // 
            this.colStatValue.FillWeight = 35F;
            this.colStatValue.HeaderText = "数值";
            this.colStatValue.Name = "colStatValue";
            this.colStatValue.ReadOnly = true;
            // 
            // chkShowAllStats
            // 
            this.chkShowAllStats.AutoSize = true;
            this.chkShowAllStats.Dock = System.Windows.Forms.DockStyle.Top;
            this.chkShowAllStats.Location = new System.Drawing.Point(8, 8);
            this.chkShowAllStats.Name = "chkShowAllStats";
            this.chkShowAllStats.Size = new System.Drawing.Size(834, 21);
            this.chkShowAllStats.TabIndex = 0;
            this.chkShowAllStats.Text = "显示全部属性 (默认只显示非零属性)";
            this.chkShowAllStats.UseVisualStyleBackColor = true;
            // 
            // tpBind
            // 
            this.tpBind.Controls.Add(this.flpBind);
            this.tpBind.Location = new System.Drawing.Point(4, 28);
            this.tpBind.Name = "tpBind";
            this.tpBind.Padding = new System.Windows.Forms.Padding(8);
            this.tpBind.Size = new System.Drawing.Size(850, 662);
            this.tpBind.TabIndex = 2;
            this.tpBind.Text = "绑定";
            this.tpBind.UseVisualStyleBackColor = true;
            // 
            // flpBind
            // 
            this.flpBind.AutoScroll = true;
            this.flpBind.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpBind.Location = new System.Drawing.Point(8, 8);
            this.flpBind.Name = "flpBind";
            this.flpBind.Size = new System.Drawing.Size(834, 646);
            this.flpBind.TabIndex = 0;
            // 
            // tpSpecial
            // 
            this.tpSpecial.Controls.Add(this.grpFlags);
            this.tpSpecial.Controls.Add(this.flpSpecial);
            this.tpSpecial.Controls.Add(this.lblSpecialHint);
            this.tpSpecial.Location = new System.Drawing.Point(4, 28);
            this.tpSpecial.Name = "tpSpecial";
            this.tpSpecial.Padding = new System.Windows.Forms.Padding(8);
            this.tpSpecial.Size = new System.Drawing.Size(850, 662);
            this.tpSpecial.TabIndex = 3;
            this.tpSpecial.Text = "特殊";
            this.tpSpecial.UseVisualStyleBackColor = true;
            // 
            // lblSpecialHint
            // 
            this.lblSpecialHint.AutoSize = true;
            this.lblSpecialHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblSpecialHint.Location = new System.Drawing.Point(8, 8);
            this.lblSpecialHint.Name = "lblSpecialHint";
            this.lblSpecialHint.Size = new System.Drawing.Size(834, 17);
            this.lblSpecialHint.TabIndex = 0;
            this.lblSpecialHint.Text = "特殊效果 (戒指/项链类特效):";
            // 
            // flpSpecial
            // 
            this.flpSpecial.AutoScroll = true;
            this.flpSpecial.Dock = System.Windows.Forms.DockStyle.Top;
            this.flpSpecial.Location = new System.Drawing.Point(8, 25);
            this.flpSpecial.MinimumSize = new System.Drawing.Size(0, 120);
            this.flpSpecial.Name = "flpSpecial";
            this.flpSpecial.Padding = new System.Windows.Forms.Padding(4, 8, 4, 8);
            this.flpSpecial.Size = new System.Drawing.Size(834, 120);
            this.flpSpecial.TabIndex = 1;
            // 
            // grpFlags
            // 
            this.grpFlags.Controls.Add(this.flpFlags);
            this.grpFlags.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpFlags.Location = new System.Drawing.Point(8, 145);
            this.grpFlags.Name = "grpFlags";
            this.grpFlags.Size = new System.Drawing.Size(834, 509);
            this.grpFlags.TabIndex = 2;
            this.grpFlags.TabStop = false;
            this.grpFlags.Text = "标记开关";
            // 
            // flpFlags
            // 
            this.flpFlags.AutoScroll = true;
            this.flpFlags.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpFlags.Location = new System.Drawing.Point(3, 22);
            this.flpFlags.Name = "flpFlags";
            this.flpFlags.Size = new System.Drawing.Size(828, 484);
            this.flpFlags.TabIndex = 0;
            // 
            // chkNeedIdentify
            // 
            this.chkNeedIdentify.AutoSize = true;
            this.chkNeedIdentify.Location = new System.Drawing.Point(12, 12);
            this.chkNeedIdentify.Margin = new System.Windows.Forms.Padding(12);
            this.chkNeedIdentify.Name = "chkNeedIdentify";
            this.chkNeedIdentify.Size = new System.Drawing.Size(75, 21);
            this.chkNeedIdentify.TabIndex = 0;
            this.chkNeedIdentify.Text = "需要鉴定";
            this.chkNeedIdentify.UseVisualStyleBackColor = true;
            // 
            // chkShowGroupPickup
            // 
            this.chkShowGroupPickup.AutoSize = true;
            this.chkShowGroupPickup.Location = new System.Drawing.Point(12, 45);
            this.chkShowGroupPickup.Margin = new System.Windows.Forms.Padding(12);
            this.chkShowGroupPickup.Name = "chkShowGroupPickup";
            this.chkShowGroupPickup.Size = new System.Drawing.Size(99, 21);
            this.chkShowGroupPickup.TabIndex = 1;
            this.chkShowGroupPickup.Text = "组队显示拾取";
            this.chkShowGroupPickup.UseVisualStyleBackColor = true;
            // 
            // chkGlobalDropNotify
            // 
            this.chkGlobalDropNotify.AutoSize = true;
            this.chkGlobalDropNotify.Location = new System.Drawing.Point(12, 78);
            this.chkGlobalDropNotify.Margin = new System.Windows.Forms.Padding(12);
            this.chkGlobalDropNotify.Name = "chkGlobalDropNotify";
            this.chkGlobalDropNotify.Size = new System.Drawing.Size(99, 21);
            this.chkGlobalDropNotify.TabIndex = 2;
            this.chkGlobalDropNotify.Text = "全服掉落播报";
            this.chkGlobalDropNotify.UseVisualStyleBackColor = true;
            // 
            // chkClassBased
            // 
            this.chkClassBased.AutoSize = true;
            this.chkClassBased.Location = new System.Drawing.Point(12, 111);
            this.chkClassBased.Margin = new System.Windows.Forms.Padding(12);
            this.chkClassBased.Name = "chkClassBased";
            this.chkClassBased.Size = new System.Drawing.Size(75, 21);
            this.chkClassBased.TabIndex = 3;
            this.chkClassBased.Text = "按职业计算";
            this.chkClassBased.UseVisualStyleBackColor = true;
            // 
            // chkLevelBased
            // 
            this.chkLevelBased.AutoSize = true;
            this.chkLevelBased.Location = new System.Drawing.Point(220, 12);
            this.chkLevelBased.Margin = new System.Windows.Forms.Padding(12);
            this.chkLevelBased.Name = "chkLevelBased";
            this.chkLevelBased.Size = new System.Drawing.Size(75, 21);
            this.chkLevelBased.TabIndex = 4;
            this.chkLevelBased.Text = "按等级计算";
            this.chkLevelBased.UseVisualStyleBackColor = true;
            // 
            // chkCanMine
            // 
            this.chkCanMine.AutoSize = true;
            this.chkCanMine.Location = new System.Drawing.Point(220, 45);
            this.chkCanMine.Margin = new System.Windows.Forms.Padding(12);
            this.chkCanMine.Name = "chkCanMine";
            this.chkCanMine.Size = new System.Drawing.Size(75, 21);
            this.chkCanMine.TabIndex = 5;
            this.chkCanMine.Text = "可挖掘";
            this.chkCanMine.UseVisualStyleBackColor = true;
            // 
            // chkCanFastRun
            // 
            this.chkCanFastRun.AutoSize = true;
            this.chkCanFastRun.Location = new System.Drawing.Point(220, 78);
            this.chkCanFastRun.Margin = new System.Windows.Forms.Padding(12);
            this.chkCanFastRun.Name = "chkCanFastRun";
            this.chkCanFastRun.Size = new System.Drawing.Size(75, 21);
            this.chkCanFastRun.TabIndex = 6;
            this.chkCanFastRun.Text = "可疾跑";
            this.chkCanFastRun.UseVisualStyleBackColor = true;
            // 
            // chkCanAwakening
            // 
            this.chkCanAwakening.AutoSize = true;
            this.chkCanAwakening.Location = new System.Drawing.Point(220, 111);
            this.chkCanAwakening.Margin = new System.Windows.Forms.Padding(12);
            this.chkCanAwakening.Name = "chkCanAwakening";
            this.chkCanAwakening.Size = new System.Drawing.Size(75, 21);
            this.chkCanAwakening.TabIndex = 7;
            this.chkCanAwakening.Text = "可觉醒";
            this.chkCanAwakening.UseVisualStyleBackColor = true;
            // 
            // ItemMgrForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1284, 760);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.panelTop);
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(1000, 620);
            this.Name = "ItemMgrForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "物品管理器";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tlpFilter.ResumeLayout(false);
            this.tlpFilter.PerformLayout();
            this.panelListBtns.ResumeLayout(false);
            this.tcEdit.ResumeLayout(false);
            this.tpBase.ResumeLayout(false);
            this.tlpBase.ResumeLayout(false);
            this.tlpBase.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numShape)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numImage)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numEffect)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSlots)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numWeight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numDura)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numStack)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numPrice)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLightRange)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLightIntensity)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numReqAmount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRandomStatsId)).EndInit();
            this.tpStats.ResumeLayout(false);
            this.tpStats.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvStats)).EndInit();
            this.tpSpecial.ResumeLayout(false);
            this.tpSpecial.PerformLayout();
            this.grpFlags.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnTranslate;
        private System.Windows.Forms.Button btnImport;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnGameShop;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblDirty;
        private System.Windows.Forms.ToolStripStatusLabel lblSaved;
        private System.Windows.Forms.ToolStripStatusLabel toolStripSpring;
        private System.Windows.Forms.ToolStripStatusLabel lblServer;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView lvItems;
        private System.Windows.Forms.ColumnHeader colhIndex;
        private System.Windows.Forms.ColumnHeader colhName;
        private System.Windows.Forms.ColumnHeader colhType;
        private System.Windows.Forms.ColumnHeader colhGrade;
        private System.Windows.Forms.ColumnHeader colhPrice;
        private System.Windows.Forms.TableLayoutPanel tlpFilter;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Label lblTypeF;
        private System.Windows.Forms.ComboBox cmbTypeFilter;
        private System.Windows.Forms.Label lblGradeF;
        private System.Windows.Forms.ComboBox cmbGradeFilter;
        private System.Windows.Forms.Panel panelListBtns;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnCopy;
        private System.Windows.Forms.Button btnDel;
        private System.Windows.Forms.TabControl tcEdit;
        private System.Windows.Forms.TabPage tpBase;
        private System.Windows.Forms.TableLayoutPanel tlpBase;
        private System.Windows.Forms.Label lblIndex;
        private System.Windows.Forms.TextBox txtIndex;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.Label lblType;
        private System.Windows.Forms.ComboBox cmbTypeEdit;
        private System.Windows.Forms.Label lblGrade;
        private System.Windows.Forms.ComboBox cmbGradeEdit;
        private System.Windows.Forms.Label lblSet;
        private System.Windows.Forms.ComboBox cmbSetEdit;
        private System.Windows.Forms.Label lblShape;
        private System.Windows.Forms.NumericUpDown numShape;
        private System.Windows.Forms.Label lblImage;
        private System.Windows.Forms.NumericUpDown numImage;
        private System.Windows.Forms.Label lblEffect;
        private System.Windows.Forms.NumericUpDown numEffect;
        private System.Windows.Forms.Label lblSlots;
        private System.Windows.Forms.NumericUpDown numSlots;
        private System.Windows.Forms.Label lblWeight;
        private System.Windows.Forms.NumericUpDown numWeight;
        private System.Windows.Forms.Label lblDura;
        private System.Windows.Forms.NumericUpDown numDura;
        private System.Windows.Forms.Label lblStack;
        private System.Windows.Forms.NumericUpDown numStack;
        private System.Windows.Forms.Label lblPrice;
        private System.Windows.Forms.NumericUpDown numPrice;
        private System.Windows.Forms.Label lblReqType;
        private System.Windows.Forms.ComboBox cmbReqType;
        private System.Windows.Forms.Label lblReqAmount;
        private System.Windows.Forms.NumericUpDown numReqAmount;
        private System.Windows.Forms.Label lblReqClass;
        private System.Windows.Forms.ComboBox cmbReqClass;
        private System.Windows.Forms.Label lblReqGender;
        private System.Windows.Forms.ComboBox cmbReqGender;
        private System.Windows.Forms.Label lblRandomStatsId;
        private System.Windows.Forms.NumericUpDown numRandomStatsId;
        private System.Windows.Forms.Label lblLightRange;
        private System.Windows.Forms.NumericUpDown numLightRange;
        private System.Windows.Forms.Label lblLightIntensity;
        private System.Windows.Forms.NumericUpDown numLightIntensity;
        private System.Windows.Forms.Label lblStartItem;
        private System.Windows.Forms.CheckBox chkStartItem;
        private System.Windows.Forms.Label lblToolTip;
        private System.Windows.Forms.TextBox txtToolTip;
        private System.Windows.Forms.TabPage tpStats;
        private System.Windows.Forms.DataGridView dgvStats;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatValue;
        private System.Windows.Forms.CheckBox chkShowAllStats;
        private System.Windows.Forms.TabPage tpBind;
        private System.Windows.Forms.FlowLayoutPanel flpBind;
        private System.Windows.Forms.TabPage tpSpecial;
        private System.Windows.Forms.Label lblSpecialHint;
        private System.Windows.Forms.FlowLayoutPanel flpSpecial;
        private System.Windows.Forms.GroupBox grpFlags;
        private System.Windows.Forms.FlowLayoutPanel flpFlags;
        private System.Windows.Forms.CheckBox chkNeedIdentify;
        private System.Windows.Forms.CheckBox chkShowGroupPickup;
        private System.Windows.Forms.CheckBox chkGlobalDropNotify;
        private System.Windows.Forms.CheckBox chkClassBased;
        private System.Windows.Forms.CheckBox chkLevelBased;
        private System.Windows.Forms.CheckBox chkCanMine;
        private System.Windows.Forms.CheckBox chkCanFastRun;
        private System.Windows.Forms.CheckBox chkCanAwakening;
    }
}
