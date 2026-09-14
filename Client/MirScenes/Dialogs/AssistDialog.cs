using Client.MirControls;
using Client.MirGraphics;
using Client.MirObjects;
using Client.MirSounds;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Client.MirScenes.Dialogs
{
    //辅助面板 - 独立窗口(默认 Ctrl+U 开关, 可在按键设置里改)
    //移植自 Crystal-Monk 的 AssistDialog, 三个页签: 职业(自动技能/挂机) / 保护(自动喝药) / 物品(自动拾取+过滤)
    //不依赖图库背景, 采用半透明底+边框(与 GroupHealthDialog 同风格), 只通过 AssistSettings/AssistHelper 读写状态
    public sealed class AssistDialog : MirImageControl
    {
        //按键设置/帮助页复用的功能描述
        public static string PanelDescription
        {
            get { return "辅助面板 开/关 (自动喝药/自动技能/挂机)"; }
        }

        private static string L(string zh, string en)
        {
            return zh; //本面板固定中文(与 Crystal-Monk 原版一致)
        }

        public const int PAGE_BASE = 0, PAGE_CLASS = 1, PAGE_PROTECT = 2, PAGE_ITEM = 3;

        private MirCheckBox[] TabPageButton;
        private static string[] GetPages()
        {
            return new[] { L("基本", "Base"), L("职业", "Class"), L("保护", "Protect"), L("物品", "Items") };
        }

        //基本页(显示选项, 移植自 Crystal-Monk 基本页)
        public MirCheckBox CheckBoxFreeShift, CheckBoxShowLevel, CheckBoxShowTransform, CheckBoxShowGuildName, CheckBoxShowGroupInfo;
        public MirCheckBox CheckBoxShowDamage, CheckBoxShowHeal, CheckBoxHideDead, CheckBoxShowMonsterName, CheckBoxHideSystem2, CheckBoxShowPing, CheckBoxShowHealth;

        //职业页
        public MirCheckBox CheckBoxSmartFire, CheckBoxSmartDaMo, CheckBoxSmartSheild, CheckBoxSmartYiJinJin, CheckBoxChangePoison, CheckBoxSpaceThrusting, CheckBoxAutoHunt;

        //职业页(刺客/弓手buff)
        public MirCheckBox CheckBoxSmartHaste, CheckBoxSmartLightBody, CheckBoxSmartSwiftFeet, CheckBoxSmartMoonLight, CheckBoxSmartConcentration;

        //保护页
        public MirCheckBox CheckBoxProtect;
        public MirLabel[] LabelProtect, LabelUse;
        public MirTextBox[] TextBoxProtectPercent, TextBoxProtectItem;
        public MirLabel LabelInterval;
        public MirTextBox TextBoxInterval;

        //物品页
        public MirCheckBox CheckBoxAutoPick;
        public MirCheckBox[] CheckBoxItemFilter;
        public MirButton PreviousButton, NextButton;
        public MirLabel PageNumberLabel;

        private const int PageSize = 10;
        private int Page = 0;
        private int StartIndex = 0;
        private bool Updating;
        private string[] FilterNames = new string[PageSize];
        private readonly List<MirControl>[] PageControls = new List<MirControl>[] { new List<MirControl>(), new List<MirControl>(), new List<MirControl>(), new List<MirControl>() };
        private int maxPage
        {
            get
            {
                return (int)Math.Ceiling((double)GameScene.Scene.AssistHelper.ItemFilterList.Count / PageSize);
            }
        }

        public MirButton CloseButton;

        public AssistDialog()
        {
            Index = -1; //无图库背景, 用BackColour绘制半透明底
            AutoSize = false;
            Movable = true;
            Sort = true;
            BackColour = Color.FromArgb(180, 0, 0, 0);
            Border = true;
            BorderColour = Color.FromArgb(180, 120, 120, 120);
            DrawControlTexture = true;
            Opacity = 1F;
            Size = new Size(434, 272);
            Location = new Point((Settings.ScreenWidth - 434) / 2, (Settings.ScreenHeight - 272) / 2);

            MirLabel TitleLabel = new MirLabel
            {
                AutoSize = true,
                NotControl = true,
                Location = new Point(12, 6),
                Parent = this,
                Text = L("辅助设置", "Assist"),
                ForeColour = Color.FromArgb(255, 255, 210, 90),
            };

            CloseButton = new MirButton
            {
                Index = 360,
                HoverIndex = 361,
                Library = Libraries.Prguse2,
                Location = new Point(Size.Width - 26, 5),
                Parent = this,
                Sound = SoundList.ButtonA,
                PressedIndex = 362,
            };
            CloseButton.Click += (o, e) => Hide();

            //页签
            string[] pages = GetPages();
            TabPageButton = new MirCheckBox[pages.Length];
            for (int i = 0; i < TabPageButton.Length; ++i)
            {
                int j = i;
                TabPageButton[i] = new MirCheckBox
                {
                    Index = 2086,
                    UnTickedIndex = 2086,
                    TickedIndex = 2087,
                    BoxIndex = 2086,
                    Parent = this,
                    Location = new Point(16 + i * 90, 28),
                    Library = Libraries.Prguse,
                    LabelText = pages[i],
                    ForeColour = Color.FromArgb(255, 255, 210, 90),
                };
                TabPageButton[i].Click += (o, e) => SwitchTab(j);
            }

            #region 基本(显示选项, 移植自 Crystal-Monk)
            CheckBoxFreeShift = CreateOptionCheckBox(PAGE_BASE, new Point(16, 60), L("免Shift攻击", "Free Shift"),
                L("无需按住Shift即可攻击玩家/带保护名目标(慎开)", "Attack players without holding Shift"));
            CheckBoxFreeShift.Click += (o, e) => AssistSettings.FreeShift = CheckBoxFreeShift.Checked;

            CheckBoxShowLevel = CreateOptionCheckBox(PAGE_BASE, new Point(16, 84), L("显示等级", "Show Level"),
                L("玩家名牌上显示等级", "Show level on name plates"));
            CheckBoxShowLevel.Click += (o, e) => AssistSettings.ShowLevel = CheckBoxShowLevel.Checked;

            CheckBoxShowTransform = CreateOptionCheckBox(PAGE_BASE, new Point(16, 108), L("显示时装", "Show Transform"),
                L("显示玩家变身/时装外形(关闭则显示本体)", "Show transform outfits"));
            CheckBoxShowTransform.Click += (o, e) => { AssistSettings.ShowTransform = CheckBoxShowTransform.Checked; if (GameScene.User != null) GameScene.User.SetLibraries(); };

            CheckBoxShowPing = CreateOptionCheckBox(PAGE_BASE, new Point(16, 132), L("显示Ping", "Show Ping"),
                L("主界面显示网络延迟", "Show network ping"));
            CheckBoxShowPing.Click += (o, e) => AssistSettings.ShowPing = CheckBoxShowPing.Checked;

            CheckBoxShowHealth = CreateOptionCheckBox(PAGE_BASE, new Point(16, 156), L("显示血量", "Show Health"),
                L("名牌上显示血量条", "Show health bars"));
            CheckBoxShowHealth.Click += (o, e) => AssistSettings.ShowHealth = CheckBoxShowHealth.Checked;

            CheckBoxShowGuildName = CreateOptionCheckBox(PAGE_BASE, new Point(156, 60), L("显示公会名", "Show Guild"),
                L("玩家名牌上显示公会名", "Show guild names"));
            CheckBoxShowGuildName.Click += (o, e) => AssistSettings.ShowGuildName = CheckBoxShowGuildName.Checked;

            CheckBoxShowGroupInfo = CreateOptionCheckBox(PAGE_BASE, new Point(156, 84), L("显示组队信息", "Group Info"),
                L("显示组队血条面板(Ctrl+P)", "Show group health panel"));
            CheckBoxShowGroupInfo.Click += (o, e) =>
            {
                AssistSettings.ShowGroupInfo = CheckBoxShowGroupInfo.Checked;
                GameScene.Scene?.GroupHealthDialog?.UpdateVisibility();
            };

            CheckBoxShowDamage = CreateOptionCheckBox(PAGE_BASE, new Point(156, 108), L("显示伤害", "Show Damage"),
                L("显示伤害数字", "Show damage numbers"));
            CheckBoxShowDamage.Click += (o, e) => AssistSettings.ShowDamage = CheckBoxShowDamage.Checked;

            CheckBoxShowHeal = CreateOptionCheckBox(PAGE_BASE, new Point(156, 132), L("显示恢复", "Show Heal"),
                L("显示恢复数字", "Show heal numbers"));
            CheckBoxShowHeal.Click += (o, e) => AssistSettings.ShowHeal = CheckBoxShowHeal.Checked;

            CheckBoxHideDead = CreateOptionCheckBox(PAGE_BASE, new Point(156, 156), L("隐藏尸体", "Hide Dead"),
                L("隐藏地图上的怪物尸体", "Hide monster corpses"));
            CheckBoxHideDead.Click += (o, e) => AssistSettings.HideDead = CheckBoxHideDead.Checked;

            CheckBoxShowMonsterName = CreateOptionCheckBox(PAGE_BASE, new Point(296, 60), L("怪物显名", "Mob Names"),
                L("显示怪物名称", "Show monster names"));
            CheckBoxShowMonsterName.Click += (o, e) => AssistSettings.ShowMonsterName = CheckBoxShowMonsterName.Checked;

            CheckBoxHideSystem2 = CreateOptionCheckBox(PAGE_BASE, new Point(296, 84), L("隐藏掉落通知", "Hide Drops"),
                L("隐藏系统掉落提示信息", "Hide drop notifications"));
            CheckBoxHideSystem2.Click += (o, e) => AssistSettings.HideSystem2 = CheckBoxHideSystem2.Checked;
            #endregion

            #region 职业(自动技能/挂机)
            CheckBoxSmartFire = CreateOptionCheckBox(PAGE_CLASS, new Point(16, 60), L("自动烈火", "Auto FlamingSword"),
                L("烈火剑法CD好且状态消失时自动施放", "Cast FlamingSword when ready"));
            CheckBoxSmartFire.Click += (o, e) => AssistSettings.SmartFireHit = CheckBoxSmartFire.Checked;

            CheckBoxSpaceThrusting = CreateOptionCheckBox(PAGE_CLASS, new Point(220, 60), L("隔位刺杀", "Space Thrust"),
                L("目标隔2格时自动出刺杀剑气(需已学刺杀剑法)", "Auto thrust when target is 2 cells away"));
            CheckBoxSpaceThrusting.Click += (o, e) => AssistSettings.SpaceThrusting = CheckBoxSpaceThrusting.Checked;

            CheckBoxSmartDaMo = CreateOptionCheckBox(PAGE_CLASS, new Point(16, 84), L("自动达摩", "Auto DaMo"),
                L("达摩棍法CD好且状态消失时自动施放", "Cast DaMoGunFa when ready"));
            CheckBoxSmartDaMo.Click += (o, e) => AssistSettings.SmartDaMo = CheckBoxSmartDaMo.Checked;

            CheckBoxSmartSheild = CreateOptionCheckBox(PAGE_CLASS, new Point(16, 108), L("自动开盾", "Auto MagicShield"),
                L("魔法盾消失时自动补盾", "Recast MagicShield when it drops"));
            CheckBoxSmartSheild.Click += (o, e) => AssistSettings.SmartSheild = CheckBoxSmartSheild.Checked;

            CheckBoxSmartYiJinJin = CreateOptionCheckBox(PAGE_CLASS, new Point(16, 132), L("自动金刚术(弓)", "Auto ElementalBarrier"),
                L("金刚术(元素盾)消失时自动施放, 需已蓄满元素球", "Recast ElementalBarrier when it drops"));
            CheckBoxSmartYiJinJin.Click += (o, e) => AssistSettings.SmartElementalBarrier = CheckBoxSmartYiJinJin.Checked;

            CheckBoxChangePoison = CreateOptionCheckBox(PAGE_CLASS, new Point(16, 156), L("自动毒符", "Auto Amulet"),
                L("施放毒/召唤类技能前自动更换对应护身符", "Swap amulets before poison/summon spells"));
            CheckBoxChangePoison.Click += (o, e) => AssistSettings.SmartChangePoison = CheckBoxChangePoison.Checked;

            //刺客buff(右列)
            CheckBoxSmartHaste = CreateOptionCheckBox(PAGE_CLASS, new Point(220, 84), L("自动体迅风(刺)", "Auto Haste"),
                L("体迅风(攻速)状态消失时自动施放", "Recast Haste when it drops"));
            CheckBoxSmartHaste.Click += (o, e) => AssistSettings.SmartHaste = CheckBoxSmartHaste.Checked;

            CheckBoxSmartLightBody = CreateOptionCheckBox(PAGE_CLASS, new Point(220, 108), L("自动风身术(刺)", "Auto LightBody"),
                L("风身术(敏捷)状态消失时自动施放", "Recast LightBody when it drops"));
            CheckBoxSmartLightBody.Click += (o, e) => AssistSettings.SmartLightBody = CheckBoxSmartLightBody.Checked;

            CheckBoxSmartSwiftFeet = CreateOptionCheckBox(PAGE_CLASS, new Point(220, 132), L("自动轻身步(刺)", "Auto SwiftFeet"),
                L("轻身步(移速)状态消失时自动施放", "Recast SwiftFeet when it drops"));
            CheckBoxSmartSwiftFeet.Click += (o, e) => AssistSettings.SmartSwiftFeet = CheckBoxSmartSwiftFeet.Checked;

            CheckBoxSmartMoonLight = CreateOptionCheckBox(PAGE_CLASS, new Point(220, 156), L("自动月影术(刺)", "Auto MoonLight"),
                L("月影术(隐身)状态消失时自动施放; 注意攻击会破隐(慎开)", "Recast MoonLight when it drops"));
            CheckBoxSmartMoonLight.Click += (o, e) => AssistSettings.SmartMoonLight = CheckBoxSmartMoonLight.Checked;

            //弓手buff(右列)
            CheckBoxSmartConcentration = CreateOptionCheckBox(PAGE_CLASS, new Point(220, 180), L("自动气流术(弓)", "Auto Concentration"),
                L("气流术(元素蓄力)消失或被打断时自动施放", "Recast Concentration when it drops"));
            CheckBoxSmartConcentration.Click += (o, e) => AssistSettings.SmartConcentration = CheckBoxSmartConcentration.Checked;

            //挂机功能暂时停用(待完善后再启用) —— 复选框先隐藏
            //CheckBoxAutoHunt = CreateOptionCheckBox(PAGE_CLASS, new Point(16, 196), L("自动打怪(挂机)", "Auto Hunt"),
            //    L("自动找怪→寻路→攻击→拾取; 关闭面板不会停止挂机", "Find/ chase/ attack/ loot automatically"));
            //CheckBoxAutoHunt.Click += CheckBoxAutoHuntClick;
            #endregion

            #region 保护(自动喝药)
            CheckBoxProtect = CreateOptionCheckBox(PAGE_PROTECT, new Point(16, 60), L("开启保护(自动喝药)", "Enable Protection"),
                L("HP/MP低于百分比自动喝对应药品", "Auto-drink potions at HP/MP thresholds"));
            CheckBoxProtect.Click += (o, e) => AssistSettings.SmartProtect = CheckBoxProtect.Checked;

            LabelProtect = new MirLabel[3];
            LabelUse = new MirLabel[3];
            TextBoxProtectPercent = new MirTextBox[3];
            TextBoxProtectItem = new MirTextBox[3];

            CreateProtectControls(0, L("生命百分比低于", "HP% below"));
            CreateProtectControls(1, L("魔法百分比低于", "MP% below"));
            CreateProtectControls(2, L("生命百分比低于", "HP% below"));

            LabelInterval = new MirLabel { AutoSize = true, Location = new Point(16, 178), Parent = this, Text = L("喝药间隔(毫秒)", "Interval (ms)") };
            PageControls[PAGE_PROTECT].Add(LabelInterval);

            TextBoxInterval = new MirTextBox { Location = new Point(110, 176), Parent = this, Size = new Size(50, 16), MaxLength = 6, CanLoseFocus = true, Font = new Font(Settings.FontName, 8F) };
            TextBoxInterval.TextBox.TextChanged += (o, e) =>
            {
                if (Updating) return;
                int temp;
                if (int.TryParse(TextBoxInterval.Text, out temp))
                    AssistSettings.UseItemInterval = Math.Max(100, temp);
            };
            PageControls[PAGE_PROTECT].Add(TextBoxInterval);
            #endregion

            #region 物品(自动拾取+过滤)
            CheckBoxAutoPick = CreateOptionCheckBox(PAGE_ITEM, new Point(16, 60), L("自动拾取", "Auto Pickup"),
                L("地面新物品自动列入下表, 勾选=拾取", "New drops are listed below; ticked = pick"));
            CheckBoxAutoPick.Click += (o, e) => AssistSettings.AutoPick = CheckBoxAutoPick.Checked;

            MirLabel filterHint = new MirLabel
            {
                AutoSize = false,
                NotControl = true,
                Size = new Size(280, 16),
                Location = new Point(120, 62),
                DrawFormat = TextFormatFlags.Left,
                ForeColour = Color.FromArgb(255, 160, 160, 160),
                Parent = this,
                Text = L("地面出现的物品会自动加入列表", "Items on the ground are added automatically"),
            };
            filterHint.Visible = false; //由SwitchTab控制
            PageControls[PAGE_ITEM].Add(filterHint);

            CheckBoxItemFilter = new MirCheckBox[PageSize];
            for (int i = 0; i < PageSize; i++)
            {
                int x = i < PageSize / 2 ? 16 : 220;
                int y = i < PageSize / 2 ? 88 + 20 * i : 88 + 20 * (i - 5);
                int j = i;
                CheckBoxItemFilter[i] = new MirCheckBox
                {
                    Index = 2086,
                    UnTickedIndex = 2086,
                    TickedIndex = 2087,
                    BoxIndex = 2086,
                    Parent = this,
                    Location = new Point(x, y),
                    Library = Libraries.Prguse,
                    Visible = false,
                };
                CheckBoxItemFilter[i].Click += (o, e) => CheckBoxItemFilterClick(j);
            }

            PageNumberLabel = new MirLabel
            {
                Text = "",
                Parent = this,
                Size = new Size(83, 17),
                Location = new Point(150, 212),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Font = new Font(Settings.FontName, 7F),
            };
            PageControls[PAGE_ITEM].Add(PageNumberLabel);

            PreviousButton = new MirButton
            {
                Index = 240,
                HoverIndex = 241,
                PressedIndex = 242,
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(240, 214),
                Sound = SoundList.ButtonA,
            };
            PageControls[PAGE_ITEM].Add(PreviousButton);
            PreviousButton.Click += (o, e) =>
            {
                Page--;
                if (Page < 0) Page = 0;
                StartIndex = PageSize * Page;

                UpdateItemFilters();
            };

            NextButton = new MirButton
            {
                Index = 243,
                HoverIndex = 244,
                PressedIndex = 245,
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(280, 214),
                Sound = SoundList.ButtonA,
            };
            PageControls[PAGE_ITEM].Add(NextButton);
            NextButton.Click += (o, e) =>
            {
                Page++;
                if ((Page + 1) > maxPage) Page--;
                StartIndex = PageSize * Page;
                UpdateItemFilters();
            };
            #endregion

            SwitchTab(0);
        }

        private MirCheckBox CreateOptionCheckBox(int page, Point location, string text, string hint)
        {
            MirCheckBox box = new MirCheckBox
            {
                Index = 2086,
                UnTickedIndex = 2086,
                TickedIndex = 2087,
                BoxIndex = 2086,
                Parent = this,
                Location = location,
                Library = Libraries.Prguse,
                LabelText = text,
                Hint = hint,
            };
            PageControls[page].Add(box);
            return box;
        }

        private void CreateProtectControls(int index, string labelText)
        {
            int height = 92 + 26 * index;
            LabelProtect[index] = new MirLabel { AutoSize = true, Location = new Point(16, height), Parent = this, Text = labelText };
            PageControls[PAGE_PROTECT].Add(LabelProtect[index]);

            TextBoxProtectPercent[index] = new MirTextBox
            {
                Location = new Point(110, height - 2),
                Parent = this,
                Size = new Size(34, 16),
                MaxLength = 3,
                CanLoseFocus = true,
                Font = new Font(Settings.FontName, 8F),
            };
            int i = index;
            TextBoxProtectPercent[index].TextBox.TextChanged += (o, e) =>
            {
                if (Updating) return;
                int temp;
                if (int.TryParse(TextBoxProtectPercent[i].Text, out temp))
                    AssistSettings.SetProtectPercent(i, temp);
            };
            PageControls[PAGE_PROTECT].Add(TextBoxProtectPercent[index]);

            LabelUse[index] = new MirLabel { AutoSize = true, Location = new Point(152, height), Parent = this, Text = L("使用", "use") };
            PageControls[PAGE_PROTECT].Add(LabelUse[index]);

            TextBoxProtectItem[index] = new MirTextBox
            {
                Location = new Point(186, height - 2),
                Parent = this,
                Size = new Size(130, 16),
                MaxLength = 20,
                CanLoseFocus = true,
                Font = new Font(Settings.FontName, 8F),
            };
            TextBoxProtectItem[index].TextBox.TextChanged += (o, e) =>
            {
                if (Updating) return;
                AssistSettings.SetProtectItemName(i, TextBoxProtectItem[i].Text);
            };
            PageControls[PAGE_PROTECT].Add(TextBoxProtectItem[index]);
        }

        private void CheckBoxAutoHuntClick(object sender, EventArgs e)
        {
            AssistSettings.AutoHunt = CheckBoxAutoHunt.Checked;
            if (GameScene.Scene == null || GameScene.Scene.AssistHelper == null) return;

            if (AssistSettings.AutoHunt)
            {
                GameScene.Scene.AssistHelper.ClearAttack();
                GameScene.Scene.ChatDialog.ReceiveChat(L("开始自动打怪", "Auto hunt started"), ChatType.System);
            }
            else
            {
                GameScene.Scene.ChatDialog.ReceiveChat(L("停止自动打怪", "Auto hunt stopped"), ChatType.System);
            }
        }

        private void CheckBoxItemFilterClick(int j)
        {
            string name = FilterNames[j];
            if (name == null) return;
            if (GameScene.Scene != null && GameScene.Scene.AssistHelper != null && GameScene.Scene.AssistHelper.ItemFilterList.ContainsKey(name))
                GameScene.Scene.AssistHelper.ItemFilterList[name].Pick = CheckBoxItemFilter[j].Checked;
        }

        private void UpdateItemFilters()
        {
            if (GameScene.Scene == null || GameScene.Scene.AssistHelper == null) return;

            ItemFilter[] items = GameScene.Scene.AssistHelper.ItemFilterList.Values.ToArray();
            for (int i = 0; i < PageSize; ++i)
            {
                if (StartIndex + i < items.Length)
                {
                    CheckBoxItemFilter[i].Visible = true;
                    FilterNames[i] = items[StartIndex + i].Name;
                    CheckBoxItemFilter[i].LabelText = items[StartIndex + i].Name;
                    CheckBoxItemFilter[i].Checked = items[StartIndex + i].Pick;
                }
                else
                {
                    FilterNames[i] = null;
                    CheckBoxItemFilter[i].Visible = false;
                }
            }

            PageNumberLabel.Text = (Page + 1) + " / " + Math.Max(1, maxPage);
        }

        private void SwitchTab(int j)
        {
            for (int i = 0; i < TabPageButton.Length; ++i)
                TabPageButton[i].Checked = i == j;

            for (int p = 0; p < PageControls.Length; p++)
                foreach (MirControl control in PageControls[p])
                    control.Visible = p == j;

            int itemCount = GameScene.Scene != null && GameScene.Scene.AssistHelper != null ? GameScene.Scene.AssistHelper.ItemFilterList.Count : 0;
            for (int i = 0; i < PageSize; i++)
                CheckBoxItemFilter[i].Visible = j == PAGE_ITEM && StartIndex + i < itemCount;

            if (j == PAGE_ITEM)
                UpdateItemFilters();
        }

        //打开时从配置刷新控件状态(每次进角色配置不同)
        public void RefreshValues()
        {
            if (GameScene.Scene == null || GameScene.Scene.AssistHelper == null) return;

            Updating = true;

            //基本页
            CheckBoxFreeShift.Checked = AssistSettings.FreeShift;
            CheckBoxShowLevel.Checked = AssistSettings.ShowLevel;
            CheckBoxShowTransform.Checked = AssistSettings.ShowTransform;
            CheckBoxShowGuildName.Checked = AssistSettings.ShowGuildName;
            CheckBoxShowGroupInfo.Checked = AssistSettings.ShowGroupInfo;
            CheckBoxShowDamage.Checked = AssistSettings.ShowDamage;
            CheckBoxShowHeal.Checked = AssistSettings.ShowHeal;
            CheckBoxHideDead.Checked = AssistSettings.HideDead;
            CheckBoxShowMonsterName.Checked = AssistSettings.ShowMonsterName;
            CheckBoxHideSystem2.Checked = AssistSettings.HideSystem2;
            CheckBoxShowPing.Checked = AssistSettings.ShowPing;
            CheckBoxShowHealth.Checked = AssistSettings.ShowHealth;

            //职业页
            CheckBoxSmartFire.Checked = AssistSettings.SmartFireHit;
            CheckBoxSmartDaMo.Checked = AssistSettings.SmartDaMo;
            CheckBoxSmartSheild.Checked = AssistSettings.SmartSheild;
            CheckBoxSmartYiJinJin.Checked = AssistSettings.SmartElementalBarrier;
            CheckBoxChangePoison.Checked = AssistSettings.SmartChangePoison;
            CheckBoxSpaceThrusting.Checked = AssistSettings.SpaceThrusting;
            //CheckBoxAutoHunt.Checked = AssistSettings.AutoHunt; //挂机暂时停用

            CheckBoxSmartHaste.Checked = AssistSettings.SmartHaste;
            CheckBoxSmartLightBody.Checked = AssistSettings.SmartLightBody;
            CheckBoxSmartSwiftFeet.Checked = AssistSettings.SmartSwiftFeet;
            CheckBoxSmartMoonLight.Checked = AssistSettings.SmartMoonLight;
            CheckBoxSmartConcentration.Checked = AssistSettings.SmartConcentration;

            CheckBoxProtect.Checked = AssistSettings.SmartProtect;
            for (int i = 0; i < 3; i++)
            {
                TextBoxProtectPercent[i].Text = AssistSettings.GetProtectPercent(i).ToString();
                TextBoxProtectItem[i].Text = AssistSettings.GetProtectItemName(i);
            }
            TextBoxInterval.Text = AssistSettings.UseItemInterval.ToString();

            CheckBoxAutoPick.Checked = AssistSettings.AutoPick;

            Updating = false;
        }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
        }

        public void Show()
        {
            if (Visible) return;
            RefreshValues();
            SwitchTab(0);
            Visible = true;
        }
    }
}
