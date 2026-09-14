using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    //组队血条面板 - 独立面板, 与组队信息面板(P键)分离
    //组队成功后自动显示, 解散/退出后自动隐藏; 可拖动
    public sealed class GroupHealthDialog : MirImageControl
    {
        public const int PanelWidth = 180;
        public const int RowHeight = 26;
        public const int BarHeight = 6;

        //按键设置/帮助页复用的功能描述(双语)
        public static string PanelDescription
        {
            get { return Settings.Language == "Chinese" ? "组队血条面板 开/关" : "Group HP Panel (open/close)"; }
        }

        public MirLabel TitleLabel;
        public MirLabel[] NameLabels, PercentLabels;
        public MirImageControl[] BarBacks, BarFills;
        public int[] LastFillWidths;

        public GroupHealthDialog()
        {
            Index = -1; //无图库背景, 用BackColour绘制半透明底
            AutoSize = false;
            Movable = true;
            Sort = true;
            BackColour = Color.FromArgb(150, 0, 0, 0);
            Border = true;
            BorderColour = Color.FromArgb(180, 120, 120, 120);
            DrawControlTexture = true;
            Opacity = 1F;
            Size = new Size(PanelWidth, 30);
            Location = new Point(Settings.ScreenWidth - PanelWidth - 10, 200);
            Visible = false; //由GameScene在组队成功时Show

            TitleLabel = new MirLabel
            {
                AutoSize = true,
                NotControl = true,
                Location = new Point(8, 4),
                Parent = this,
                Text = string.Format(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Groups), "P"),
                ForeColour = Color.FromArgb(255, 255, 210, 90),
            };

            NameLabels = new MirLabel[Globals.MaxGroup];
            PercentLabels = new MirLabel[Globals.MaxGroup];
            BarBacks = new MirImageControl[Globals.MaxGroup];
            BarFills = new MirImageControl[Globals.MaxGroup];
            LastFillWidths = new int[Globals.MaxGroup];

            for (int i = 0; i < Globals.MaxGroup; i++)
            {
                int y = 22 + i * RowHeight;

                NameLabels[i] = new MirLabel
                {
                    AutoSize = true,
                    NotControl = true,
                    Location = new Point(8, y),
                    Parent = this,
                };

                PercentLabels[i] = new MirLabel
                {
                    AutoSize = false,
                    NotControl = true,
                    Size = new Size(40, 14),
                    Location = new Point(PanelWidth - 48, y + 1),
                    DrawFormat = TextFormatFlags.Right,
                    ForeColour = Color.FromArgb(255, 90, 220, 90),
                    Parent = this,
                };

                BarBacks[i] = new MirImageControl
                {
                    Index = -1,
                    AutoSize = false,
                    NotControl = true,
                    Size = new Size(PanelWidth - 16, BarHeight),
                    Location = new Point(8, y + 16),
                    BackColour = Color.FromArgb(255, 60, 15, 15),
                    DrawControlTexture = true,
                    Parent = this,
                };

                BarFills[i] = new MirImageControl
                {
                    Index = -1,
                    AutoSize = false,
                    NotControl = true,
                    Size = new Size(0, BarHeight),
                    Location = new Point(8, y + 16),
                    BackColour = Color.FromArgb(255, 200, 45, 45),
                    DrawControlTexture = true,
                    Parent = this,
                };
            }

            BeforeDraw += GroupHealthPanel_BeforeDraw;
        }

        //显示组队信息开关(辅助面板基本页): 关闭时强制隐藏, 开启且有队伍时恢复显示
        public void UpdateVisibility()
        {
            if (!AssistSettings.ShowGroupInfo || GroupDialog.GroupList.Count == 0)
            {
                if (Visible) Visible = false;
            }
        }

        private void GroupHealthPanel_BeforeDraw(object sender, EventArgs e)
        {
            UpdateVisibility();
            if (!Visible) return;

            int count = GroupDialog.GroupList.Count;

            int height = 22 + count * RowHeight + 6;
            if (Size.Height != height)
                Size = new Size(PanelWidth, height);

            for (int i = 0; i < NameLabels.Length; i++)
            {
                bool show = i < count;
                NameLabels[i].Visible = show;
                PercentLabels[i].Visible = show;
                BarBacks[i].Visible = show;

                if (!show)
                {
                    if (BarFills[i].Visible) BarFills[i].Visible = false;
                    if (NameLabels[i].Text.Length > 0) NameLabels[i].Text = string.Empty;
                    if (PercentLabels[i].Text.Length > 0) PercentLabels[i].Text = string.Empty;
                    continue;
                }

                string name = GroupDialog.GroupList[i];
                if (NameLabels[i].Text != name)
                    NameLabels[i].Text = name;

                byte percent;
                if (name == MapObject.User.Name)
                {
                    int maxHp = Math.Max(1, MapObject.User.Stats[Stat.HP]);
                    percent = (byte)Math.Min(100, MapObject.User.HP * 100 / maxHp);
                }
                else if (!GroupDialog.GroupHealth.TryGetValue(name, out percent))
                    percent = 0;

                int width = (int)((PanelWidth - 16) * percent / 100F);
                BarFills[i].Visible = width > 0;
                if (LastFillWidths[i] != width)
                {
                    LastFillWidths[i] = width;
                    BarFills[i].Size = new Size(width, BarHeight);
                }

                string percentText = percent + "%";
                if (PercentLabels[i].Text != percentText)
                    PercentLabels[i].Text = percentText;

                //悬停提示队友所在地图
                string map;
                NameLabels[i].Hint = GroupDialog.GroupMembersMap.TryGetValue(name, out map) ? map : null;
            }
        }
    }
}
