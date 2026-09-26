namespace Server
{
    public class GiveCreditDialog : Form
    {
        private readonly Label _targetLabel;
        private readonly NumericUpDown _amountNumeric;
        private readonly Button _okButton;
        private readonly Button _cancelButton;

        public uint Amount => (uint)_amountNumeric.Value;

        public GiveCreditDialog(string accountId, string characterName, uint currentCredit, bool online)
        {
            Text = "发送元宝";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(330, 158);

            _targetLabel = new Label
            {
                AutoSize = false,
                Location = new Point(12, 12),
                Size = new Size(300, 48),
                Text = $"账号: {accountId}"
                    + (string.IsNullOrEmpty(characterName) ? "" : $"    角色: {characterName}")
                    + $"\n当前元宝: {currentCredit}    状态: {(online ? "在线" : "离线")}"
            };

            var amountLabel = new Label { AutoSize = true, Location = new Point(14, 72), Text = "发送数量:" };

            _amountNumeric = new NumericUpDown
            {
                Location = new Point(92, 68),
                Size = new Size(160, 23),
                Minimum = 1,
                Maximum = uint.MaxValue,
                Value = 1,
                ThousandsSeparator = true
            };

            _okButton = new Button { DialogResult = DialogResult.OK, Location = new Point(92, 112), Size = new Size(80, 27), Text = "发送" };
            _cancelButton = new Button { DialogResult = DialogResult.Cancel, Location = new Point(185, 112), Size = new Size(80, 27), Text = "取消" };

            AcceptButton = _okButton;
            CancelButton = _cancelButton;

            Controls.Add(_targetLabel);
            Controls.Add(amountLabel);
            Controls.Add(_amountNumeric);
            Controls.Add(_okButton);
            Controls.Add(_cancelButton);
        }
    }
}
