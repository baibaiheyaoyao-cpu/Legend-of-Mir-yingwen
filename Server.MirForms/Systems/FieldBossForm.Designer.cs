namespace Server
{
    partial class FieldBossForm
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
            this.BossListView = new System.Windows.Forms.ListView();
            this.colEnabled = new System.Windows.Forms.ColumnHeader();
            this.colMonster = new System.Windows.Forms.ColumnHeader();
            this.colMap = new System.Windows.Forms.ColumnHeader();
            this.colLocation = new System.Windows.Forms.ColumnHeader();
            this.colTimes = new System.Windows.Forms.ColumnHeader();
            this.colStatus = new System.Windows.Forms.ColumnHeader();
            this.EditGroupBox = new System.Windows.Forms.GroupBox();
            this.MapHintLabel = new System.Windows.Forms.Label();
            this.SpawnTimesLabel = new System.Windows.Forms.Label();
            this.TimePicker = new System.Windows.Forms.DateTimePicker();
            this.AddTimeButton = new System.Windows.Forms.Button();
            this.ClearTimesButton = new System.Windows.Forms.Button();
            this.SpawnTimesDisplay = new System.Windows.Forms.TextBox();
            this.YTextBox = new System.Windows.Forms.TextBox();
            this.YLabel = new System.Windows.Forms.Label();
            this.XTextBox = new System.Windows.Forms.TextBox();
            this.XLabel = new System.Windows.Forms.Label();
            this.MapNameComboBox = new System.Windows.Forms.ComboBox();
            this.MapNameLabel = new System.Windows.Forms.Label();
            this.MonsterSearchTextBox = new System.Windows.Forms.TextBox();
            this.MonsterNameComboBox = new System.Windows.Forms.ComboBox();
            this.EnableCheckBox = new System.Windows.Forms.CheckBox();
            this.AddButton = new System.Windows.Forms.Button();
            this.DeleteButton = new System.Windows.Forms.Button();
            this.SpawnNowButton = new System.Windows.Forms.Button();
            this.EditGroupBox.SuspendLayout();
            this.SuspendLayout();
            //
            // BossListView
            //
            this.BossListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colEnabled,
            this.colMonster,
            this.colMap,
            this.colLocation,
            this.colTimes,
            this.colStatus});
            this.BossListView.FullRowSelect = true;
            this.BossListView.GridLines = true;
            this.BossListView.HideSelection = false;
            this.BossListView.Location = new System.Drawing.Point(12, 12);
            this.BossListView.MultiSelect = false;
            this.BossListView.Name = "BossListView";
            this.BossListView.Size = new System.Drawing.Size(620, 200);
            this.BossListView.TabIndex = 0;
            this.BossListView.UseCompatibleStateImageBehavior = false;
            this.BossListView.View = System.Windows.Forms.View.Details;
            this.BossListView.SelectedIndexChanged += new System.EventHandler(this.BossListView_SelectedIndexChanged);
            //
            // colEnabled
            //
            this.colEnabled.Text = "启用";
            this.colEnabled.Width = 50;
            //
            // colMonster
            //
            this.colMonster.Text = "怪物名";
            this.colMonster.Width = 120;
            //
            // colMap
            //
            this.colMap.Text = "地图";
            this.colMap.Width = 100;
            //
            // colLocation
            //
            this.colLocation.Text = "坐标";
            this.colLocation.Width = 60;
            //
            // colTimes
            //
            this.colTimes.Text = "刷新时间";
            this.colTimes.Width = 140;
            //
            // colStatus
            //
            this.colStatus.Text = "状态";
            this.colStatus.Width = 130;
            //
            // EditGroupBox
            //
            this.EditGroupBox.Controls.Add(this.MapHintLabel);
            this.EditGroupBox.Controls.Add(this.SpawnTimesLabel);
            this.EditGroupBox.Controls.Add(this.TimePicker);
            this.EditGroupBox.Controls.Add(this.AddTimeButton);
            this.EditGroupBox.Controls.Add(this.ClearTimesButton);
            this.EditGroupBox.Controls.Add(this.SpawnTimesDisplay);
            this.EditGroupBox.Controls.Add(this.YTextBox);
            this.EditGroupBox.Controls.Add(this.YLabel);
            this.EditGroupBox.Controls.Add(this.XTextBox);
            this.EditGroupBox.Controls.Add(this.XLabel);
            this.EditGroupBox.Controls.Add(this.MapNameComboBox);
            this.EditGroupBox.Controls.Add(this.MapNameLabel);
            this.EditGroupBox.Controls.Add(this.MonsterSearchTextBox);
            this.EditGroupBox.Controls.Add(this.MonsterNameComboBox);
            this.EditGroupBox.Controls.Add(this.EnableCheckBox);
            this.EditGroupBox.Location = new System.Drawing.Point(12, 218);
            this.EditGroupBox.Name = "EditGroupBox";
            this.EditGroupBox.Size = new System.Drawing.Size(620, 115);
            this.EditGroupBox.TabIndex = 1;
            this.EditGroupBox.TabStop = false;
            this.EditGroupBox.Text = "编辑选中项";
            //
            // EnableCheckBox
            //
            this.EnableCheckBox.AutoSize = true;
            this.EnableCheckBox.Location = new System.Drawing.Point(16, 28);
            this.EnableCheckBox.Name = "EnableCheckBox";
            this.EnableCheckBox.Size = new System.Drawing.Size(48, 16);
            this.EnableCheckBox.TabIndex = 0;
            this.EnableCheckBox.Text = "启用";
            this.EnableCheckBox.UseVisualStyleBackColor = true;
            this.EnableCheckBox.CheckStateChanged += new System.EventHandler(this.EnableCheckBox_CheckStateChanged);
            //
            // MonsterSearchTextBox
            //
            this.MonsterSearchTextBox.Location = new System.Drawing.Point(80, 26);
            this.MonsterSearchTextBox.Name = "MonsterSearchTextBox";
            this.MonsterSearchTextBox.PlaceholderText = "搜索怪物";
            this.MonsterSearchTextBox.Size = new System.Drawing.Size(95, 21);
            this.MonsterSearchTextBox.TabIndex = 1;
            this.MonsterSearchTextBox.TextChanged += new System.EventHandler(this.MonsterSearchTextBox_TextChanged);
            //
            // MonsterNameComboBox
            //
            this.MonsterNameComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.MonsterNameComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.MonsterNameComboBox.FormattingEnabled = true;
            this.MonsterNameComboBox.Location = new System.Drawing.Point(180, 25);
            this.MonsterNameComboBox.Name = "MonsterNameComboBox";
            this.MonsterNameComboBox.Size = new System.Drawing.Size(140, 20);
            this.MonsterNameComboBox.TabIndex = 2;
            this.MonsterNameComboBox.TextChanged += new System.EventHandler(this.MonsterNameComboBox_TextChanged);
            //
            // MapNameLabel
            //
            this.MapNameLabel.AutoSize = true;
            this.MapNameLabel.Location = new System.Drawing.Point(330, 30);
            this.MapNameLabel.Name = "MapNameLabel";
            this.MapNameLabel.Size = new System.Drawing.Size(41, 12);
            this.MapNameLabel.TabIndex = 3;
            this.MapNameLabel.Text = "地图:";
            //
            // MapNameComboBox
            //
            this.MapNameComboBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.MapNameComboBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.MapNameComboBox.FormattingEnabled = true;
            this.MapNameComboBox.Location = new System.Drawing.Point(370, 25);
            this.MapNameComboBox.Name = "MapNameComboBox";
            this.MapNameComboBox.Size = new System.Drawing.Size(200, 20);
            this.MapNameComboBox.TabIndex = 4;
            this.MapNameComboBox.TextChanged += new System.EventHandler(this.MapNameComboBox_TextChanged);
            //
            // XLabel
            //
            this.XLabel.AutoSize = true;
            this.XLabel.Location = new System.Drawing.Point(14, 62);
            this.XLabel.Name = "XLabel";
            this.XLabel.Size = new System.Drawing.Size(23, 12);
            this.XLabel.TabIndex = 5;
            this.XLabel.Text = "X:";
            //
            // XTextBox
            //
            this.XTextBox.Location = new System.Drawing.Point(32, 58);
            this.XTextBox.Name = "XTextBox";
            this.XTextBox.Size = new System.Drawing.Size(55, 21);
            this.XTextBox.TabIndex = 6;
            this.XTextBox.TextChanged += new System.EventHandler(this.XTextBox_TextChanged);
            //
            // YLabel
            //
            this.YLabel.AutoSize = true;
            this.YLabel.Location = new System.Drawing.Point(100, 62);
            this.YLabel.Name = "YLabel";
            this.YLabel.Size = new System.Drawing.Size(23, 12);
            this.YLabel.TabIndex = 7;
            this.YLabel.Text = "Y:";
            //
            // YTextBox
            //
            this.YTextBox.Location = new System.Drawing.Point(118, 58);
            this.YTextBox.Name = "YTextBox";
            this.YTextBox.Size = new System.Drawing.Size(55, 21);
            this.YTextBox.TabIndex = 8;
            this.YTextBox.TextChanged += new System.EventHandler(this.YTextBox_TextChanged);
            //
            // SpawnTimesLabel
            //
            this.SpawnTimesLabel.AutoSize = true;
            this.SpawnTimesLabel.Location = new System.Drawing.Point(182, 62);
            this.SpawnTimesLabel.Name = "SpawnTimesLabel";
            this.SpawnTimesLabel.Size = new System.Drawing.Size(65, 12);
            this.SpawnTimesLabel.TabIndex = 9;
            this.SpawnTimesLabel.Text = "刷新时间:";
            //
            // TimePicker
            //
            this.TimePicker.CustomFormat = "HH:mm";
            this.TimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.TimePicker.Location = new System.Drawing.Point(248, 57);
            this.TimePicker.Name = "TimePicker";
            this.TimePicker.ShowUpDown = true;
            this.TimePicker.Size = new System.Drawing.Size(70, 21);
            this.TimePicker.TabIndex = 10;
            //
            // AddTimeButton
            //
            this.AddTimeButton.Location = new System.Drawing.Point(322, 55);
            this.AddTimeButton.Name = "AddTimeButton";
            this.AddTimeButton.Size = new System.Drawing.Size(55, 24);
            this.AddTimeButton.TabIndex = 11;
            this.AddTimeButton.Text = "添加";
            this.AddTimeButton.UseVisualStyleBackColor = true;
            this.AddTimeButton.Click += new System.EventHandler(this.AddTimeButton_Click);
            //
            // ClearTimesButton
            //
            this.ClearTimesButton.Location = new System.Drawing.Point(381, 55);
            this.ClearTimesButton.Name = "ClearTimesButton";
            this.ClearTimesButton.Size = new System.Drawing.Size(55, 24);
            this.ClearTimesButton.TabIndex = 12;
            this.ClearTimesButton.Text = "清空";
            this.ClearTimesButton.UseVisualStyleBackColor = true;
            this.ClearTimesButton.Click += new System.EventHandler(this.ClearTimesButton_Click);
            //
            // SpawnTimesDisplay
            //
            this.SpawnTimesDisplay.BackColor = System.Drawing.SystemColors.Control;
            this.SpawnTimesDisplay.Location = new System.Drawing.Point(440, 58);
            this.SpawnTimesDisplay.Name = "SpawnTimesDisplay";
            this.SpawnTimesDisplay.ReadOnly = true;
            this.SpawnTimesDisplay.Size = new System.Drawing.Size(172, 21);
            this.SpawnTimesDisplay.TabIndex = 13;
            //
            // MapHintLabel
            //
            this.MapHintLabel.AutoSize = true;
            this.MapHintLabel.ForeColor = System.Drawing.Color.Gray;
            this.MapHintLabel.Location = new System.Drawing.Point(14, 88);
            this.MapHintLabel.Name = "MapHintLabel";
            this.MapHintLabel.Size = new System.Drawing.Size(263, 12);
            this.MapHintLabel.TabIndex = 11;
            this.MapHintLabel.Text = "地图可逗号分隔多个(随机选一), X/Y填-1为随机刷点";
            //
            // AddButton
            //
            this.AddButton.Location = new System.Drawing.Point(12, 340);
            this.AddButton.Name = "AddButton";
            this.AddButton.Size = new System.Drawing.Size(75, 28);
            this.AddButton.TabIndex = 2;
            this.AddButton.Text = "新增";
            this.AddButton.UseVisualStyleBackColor = true;
            this.AddButton.Click += new System.EventHandler(this.AddButton_Click);
            //
            // DeleteButton
            //
            this.DeleteButton.Location = new System.Drawing.Point(95, 340);
            this.DeleteButton.Name = "DeleteButton";
            this.DeleteButton.Size = new System.Drawing.Size(75, 28);
            this.DeleteButton.TabIndex = 3;
            this.DeleteButton.Text = "删除";
            this.DeleteButton.UseVisualStyleBackColor = true;
            this.DeleteButton.Click += new System.EventHandler(this.DeleteButton_Click);
            //
            // SpawnNowButton
            //
            this.SpawnNowButton.Location = new System.Drawing.Point(530, 340);
            this.SpawnNowButton.Name = "SpawnNowButton";
            this.SpawnNowButton.Size = new System.Drawing.Size(102, 28);
            this.SpawnNowButton.TabIndex = 4;
            this.SpawnNowButton.Text = "立即刷新";
            this.SpawnNowButton.UseVisualStyleBackColor = true;
            this.SpawnNowButton.Click += new System.EventHandler(this.SpawnNowButton_Click);
            //
            // FieldBossForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(644, 382);
            this.Controls.Add(this.BossListView);
            this.Controls.Add(this.EditGroupBox);
            this.Controls.Add(this.AddButton);
            this.Controls.Add(this.DeleteButton);
            this.Controls.Add(this.SpawnNowButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "FieldBossForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "野外Boss系统";
            this.Load += new System.EventHandler(this.FieldBossForm_Load);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FieldBossForm_FormClosed);
            this.EditGroupBox.ResumeLayout(false);
            this.EditGroupBox.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.ListView BossListView;
        private System.Windows.Forms.ColumnHeader colEnabled;
        private System.Windows.Forms.ColumnHeader colMonster;
        private System.Windows.Forms.ColumnHeader colMap;
        private System.Windows.Forms.ColumnHeader colLocation;
        private System.Windows.Forms.ColumnHeader colTimes;
        private System.Windows.Forms.ColumnHeader colStatus;
        private System.Windows.Forms.GroupBox EditGroupBox;
        private System.Windows.Forms.CheckBox EnableCheckBox;
        private System.Windows.Forms.TextBox MonsterSearchTextBox;
        private System.Windows.Forms.ComboBox MonsterNameComboBox;
        private System.Windows.Forms.Label MapNameLabel;
        private System.Windows.Forms.ComboBox MapNameComboBox;
        private System.Windows.Forms.Label XLabel;
        private System.Windows.Forms.TextBox XTextBox;
        private System.Windows.Forms.Label YLabel;
        private System.Windows.Forms.TextBox YTextBox;
        private System.Windows.Forms.Label SpawnTimesLabel;
        private System.Windows.Forms.DateTimePicker TimePicker;
        private System.Windows.Forms.Button AddTimeButton;
        private System.Windows.Forms.Button ClearTimesButton;
        private System.Windows.Forms.TextBox SpawnTimesDisplay;
        private System.Windows.Forms.Label MapHintLabel;
        private System.Windows.Forms.Button AddButton;
        private System.Windows.Forms.Button DeleteButton;
        private System.Windows.Forms.Button SpawnNowButton;
    }
}
