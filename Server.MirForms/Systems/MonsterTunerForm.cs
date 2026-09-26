using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirObjects;

namespace Server.MirForms.Systems
{
    public partial class MonsterTunerForm : Form
    {
        public Envir Envir => SMain.Envir;

        public MonsterTunerForm()
        {
            InitializeComponent();
            
            for (int i = 0; i < Envir.MonsterInfoList.Count; i++)
            {
                SelectMonsterComboBox.Items.Add(Envir.MonsterInfoList[i]);
            }
        }

        private void SelectMonsterComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;

            MonsterInfo monster = (MonsterInfo)comboBox.SelectedItem;

            if (monster == null) return;

            MonsterNameTextBox.Text = monster.Name;
            HPTextBox.Text = monster.Stats[Stat.HP].ToString();
            EffectTextBox.Text = monster.Effect.ToString();
            LevelTextBox.Text = monster.Level.ToString();
            ViewRangeTextBox.Text = monster.ViewRange.ToString();
            CoolEyeTextBox.Text = monster.CoolEye.ToString();
            MinACTextBox.Text = monster.Stats[Stat.MinAC].ToString();
            MaxACTextBox.Text = monster.Stats[Stat.MaxAC].ToString();
            MinMACTextBox.Text = monster.Stats[Stat.MinMAC].ToString();
            MaxMACTextBox.Text = monster.Stats[Stat.MaxMAC].ToString();
            MinDCTextBox.Text = monster.Stats[Stat.MinDC].ToString();
            MaxDCTextBox.Text = monster.Stats[Stat.MaxDC].ToString();
            MinMCTextBox.Text = monster.Stats[Stat.MinMC].ToString();
            MaxMCTextBox.Text = monster.Stats[Stat.MaxMC].ToString();
            MinSCTextBox.Text = monster.Stats[Stat.MinSC].ToString();
            MaxSCTextBox.Text = monster.Stats[Stat.MaxSC].ToString();
            AccuracyTextBox.Text = monster.Stats[Stat.Accuracy].ToString();
            AgilityTextBox.Text = monster.Stats[Stat.Agility].ToString();
            ASpeedTextBox.Text = monster.AttackSpeed.ToString();
            MSpeedTextBox.Text = monster.MoveSpeed.ToString();
        }

        private bool TryApplyValues(out string errorField)
        {
            errorField = null;

            MonsterInfo monster = (MonsterInfo)SelectMonsterComboBox.SelectedItem;
            if (monster == null) return false;

            int hp, effect, level, viewRange, coolEye;
            int minAC, maxAC, minMAC, maxMAC, minDC, maxDC, minMC, maxMC, minSC, maxSC;
            int accuracy, agility, aSpeed, mSpeed;

            if (!int.TryParse(HPTextBox.Text, out hp)) { errorField = "HP"; return false; }
            if (!int.TryParse(EffectTextBox.Text, out effect) || effect < 0 || effect > 255) { errorField = "Effect"; return false; }
            if (!int.TryParse(LevelTextBox.Text, out level) || level < 0 || level > 65535) { errorField = "Level"; return false; }
            if (!int.TryParse(ViewRangeTextBox.Text, out viewRange) || viewRange < 0 || viewRange > 255) { errorField = "ViewRange"; return false; }
            if (!int.TryParse(CoolEyeTextBox.Text, out coolEye) || coolEye < 0 || coolEye > 255) { errorField = "CoolEye"; return false; }
            if (!int.TryParse(MinACTextBox.Text, out minAC)) { errorField = "MinAC"; return false; }
            if (!int.TryParse(MaxACTextBox.Text, out maxAC)) { errorField = "MaxAC"; return false; }
            if (!int.TryParse(MinMACTextBox.Text, out minMAC)) { errorField = "MinMAC"; return false; }
            if (!int.TryParse(MaxMACTextBox.Text, out maxMAC)) { errorField = "MaxMAC"; return false; }
            if (!int.TryParse(MinDCTextBox.Text, out minDC)) { errorField = "MinDC"; return false; }
            if (!int.TryParse(MaxDCTextBox.Text, out maxDC)) { errorField = "MaxDC"; return false; }
            if (!int.TryParse(MinMCTextBox.Text, out minMC)) { errorField = "MinMC"; return false; }
            if (!int.TryParse(MaxMCTextBox.Text, out maxMC)) { errorField = "MaxMC"; return false; }
            if (!int.TryParse(MinSCTextBox.Text, out minSC)) { errorField = "MinSC"; return false; }
            if (!int.TryParse(MaxSCTextBox.Text, out maxSC)) { errorField = "MaxSC"; return false; }
            if (!int.TryParse(AccuracyTextBox.Text, out accuracy) || accuracy < 0 || accuracy > 255) { errorField = "Accuracy"; return false; }
            if (!int.TryParse(AgilityTextBox.Text, out agility) || agility < 0 || agility > 255) { errorField = "Agility"; return false; }
            if (!int.TryParse(ASpeedTextBox.Text, out aSpeed)) { errorField = "AttackSpeed"; return false; }
            if (!int.TryParse(MSpeedTextBox.Text, out mSpeed)) { errorField = "MoveSpeed"; return false; }

            monster.Stats[Stat.HP] = hp;
            monster.Effect = (byte)effect;
            monster.Level = (ushort)level;
            monster.ViewRange = (byte)viewRange;
            monster.CoolEye = (byte)coolEye;
            monster.Stats[Stat.MinAC] = minAC;
            monster.Stats[Stat.MaxAC] = maxAC;
            monster.Stats[Stat.MinMAC] = minMAC;
            monster.Stats[Stat.MaxMAC] = maxMAC;
            monster.Stats[Stat.MinDC] = minDC;
            monster.Stats[Stat.MaxDC] = maxDC;
            monster.Stats[Stat.MinMC] = minMC;
            monster.Stats[Stat.MaxMC] = maxMC;
            monster.Stats[Stat.MinSC] = minSC;
            monster.Stats[Stat.MaxSC] = maxSC;
            monster.Stats[Stat.Accuracy] = accuracy;
            monster.Stats[Stat.Agility] = agility;
            monster.AttackSpeed = (ushort)aSpeed;
            monster.MoveSpeed = (ushort)mSpeed;

            return true;
        }

        private void RefreshLiveMonsters()
        {
            foreach (var item in Envir.Objects)
            {
                if (item.Race != ObjectType.Monster) continue;

                MonsterObject mob = (MonsterObject)item;

                mob.RefreshAll();
            }
        }

        private void SyncToEditEnvir(MonsterInfo monster)
        {
            var editList = SMain.EditEnvir.MonsterInfoList;

            for (int i = 0; i < editList.Count; i++)
            {
                if (editList[i].Index != monster.Index) continue;

                MonsterInfo edit = editList[i];

                edit.Effect = monster.Effect;
                edit.Level = monster.Level;
                edit.ViewRange = monster.ViewRange;
                edit.CoolEye = monster.CoolEye;
                edit.AttackSpeed = monster.AttackSpeed;
                edit.MoveSpeed = monster.MoveSpeed;

                edit.Stats.Clear();
                edit.Stats.Add(monster.Stats);

                break;
            }
        }

        private void updateButton_Click(object sender, EventArgs e)
        {
            MonsterInfo monster = (MonsterInfo)SelectMonsterComboBox.SelectedItem;

            if (monster == null) return;

            string errorField;
            if (!TryApplyValues(out errorField))
            {
                if (errorField != null)
                    MessageBox.Show("字段[" + errorField + "]数值校验失败, 未做任何修改", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            RefreshLiveMonsters();
            SyncToEditEnvir(monster);
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            MonsterInfo monster = (MonsterInfo)SelectMonsterComboBox.SelectedItem;

            if (monster == null) return;

            string errorField;
            if (!TryApplyValues(out errorField))
            {
                if (errorField != null)
                    MessageBox.Show("字段[" + errorField + "]数值校验失败, 未做任何修改", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }

            RefreshLiveMonsters();
            SyncToEditEnvir(monster);

            Envir.SaveDB();

            MessageBox.Show("已应用并保存到数据库", "怪物调整器",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
