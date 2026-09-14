using System.Text;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 天赋窗口 - 分层网格布局(共用面板, 自动按职业过滤).
    ///
    /// 窗口结构:
    ///   ┌ 天赋·战士        [点数: 300]          [×] ┐   ← 标题栏(职业名自动带入)
    ///   │ 层标 │ [格子][格子][格子][格子][格子][格子] │   ← 每层一行, 左侧层标签
    ///   │ 层标 │ [格子][格子]...                     │
    ///   │ [洗点]                                  │   ← 底部操作区
    ///   └──────────────────────────────────────────┘
    ///
    /// 格子结构: 深色底槽(36x36) + 图标按钮(32x32居中) + 右下角等级小字
    /// 状态: 可学=亮色 / 不可学=灰化 / 满级=金字
    ///
    /// 数据全部来自服务端下发(TalentInfo/PlayerTalentInfo包), 客户端不落盘;
    /// 改天赋表只需服务端 @ReloadTalents, 在线客户端会自动收到新表重建网格.
    /// 背景暂用自绘半透明面板, 要换正式美术图时改下方 WindowBg 区域两行即可.
    /// </summary>
    public sealed class TalentDialog : MirImageControl
    {
        private static UserObject User => MapObject.User;

        /// <summary>窗口头部: 标题(含职业名) / 剩余点数</summary>
        private MirLabel _titleLabel, _pointsLabel;

        /// <summary>关闭按钮 / 洗点按钮</summary>
        private MirButton _closeButton;
        private MirLabel _resetButton;

        /// <summary>每层的左侧标签("第1层"~"第8层")</summary>
        private readonly List<MirLabel> _tierLabels = new List<MirLabel>();

        /// <summary>天赋格子: 底槽 / 图标按钮 / 等级小字, Key=TalentId</summary>
        private readonly Dictionary<int, MirImageControl> _slots = new Dictionary<int, MirImageControl>();
        private readonly Dictionary<int, MirButton> _cells = new Dictionary<int, MirButton>();
        private readonly Dictionary<int, MirLabel> _levelLabels = new Dictionary<int, MirLabel>();

        /// <summary>服务端下发的天赋全表(已按职业过滤)</summary>
        private readonly List<S.ClientTalentInfo> _talents = new List<S.ClientTalentInfo>();
        /// <summary>已学天赋等级, Key=TalentId</summary>
        private readonly Dictionary<int, int> _learned = new Dictionary<int, int>();

        private int _points;   //剩余天赋点
        private int _tiers = 1;//最大层数(决定网格行数)

        //---- 布局参数(想微调窗口观感改这里) ----
        private const int WindowWidth = 352;   //窗口宽度
        private const int HeaderHeight = 40;   //标题栏高度
        private const int FooterHeight = 38;   //底部操作区高度
        private const int TierLabelWidth = 40; //左侧层标签列宽
        private const int SlotSize = 36;       //格子底槽边长
        private const int IconSize = 32;       //图标边长(居中于底槽)
        private const int PitchX = 40;         //水平格距
        private const int PitchY = 40;         //垂直格距
        private const int GridOriginX = 18 + TierLabelWidth; //网格起点X(留出层标签)
        private const int GridOriginY = HeaderHeight + 8;    //网格起点Y

        //---- 配色(代码自绘的格子底槽用; 窗口底图已换成 Title.Lib 919) ----
        private static readonly Color SlotBg = Color.FromArgb(165, 28, 28, 34);            //格子底槽: 深灰
        private static readonly Color SlotBorder = Color.FromArgb(160, 90, 80, 62);        //格子边框
        private static readonly Color SlotBorderMaxed = Color.FromArgb(255, 255, 190, 90); //满级格子边框: 金
        private static readonly Color SlotBorderLearnable = Color.FromArgb(220, 120, 200, 90); //可学格子边框: 微亮绿

        public TalentDialog()
        {
            //=== 窗口背景: 正式底图(kehux\Data\Title.Lib 编号919, 352x406, 站长自制) ===
            Index = 919;
            Library = Libraries.Title;
            Size = new Size(WindowWidth, HeaderHeight + 8 * PitchY + FooterHeight);
            Movable = true;   //可拖动
            Sort = true;
            BeforeDraw += (o, e) => RefreshInterface();  //每次绘制前刷新显示(本引擎惯例)

            //=== 标题: "天赋·战士" 职业名自动带入(9号字, 微调: 下移3px) ===
            _titleLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(16, 13),
                Font = new Font(Settings.FontName, 9F, FontStyle.Bold),
                ForeColour = Color.FromArgb(255, 255, 210, 130),   //暗金色
                OutLine = true,
                OutLineColour = Color.Black,
                NotControl = true,      //纯显示文字, 不参与鼠标命中
                Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentTitle),
            };

            //=== 剩余点数(右上角, 白字, 微调: 字号9 下移3px) ===
            _pointsLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(WindowWidth - 150, 13),
                Font = new Font(Settings.FontName, 9F, FontStyle.Bold),
                ForeColour = Color.White,
                OutLine = true,
                OutLineColour = Color.Black,
                NotControl = true,
            };

            //=== 关闭按钮(复用角色窗口的关闭三态图: 常态/悬停/按下) ===
            _closeButton = new MirButton
            {
                Index = 360,
                HoverIndex = 361,
                PressedIndex = 362,
                Library = Libraries.Prguse2,
                Location = new Point(WindowWidth - 24, 4),
                Parent = this,
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();

            //=== 洗点按钮(底部左侧文字按钮, 点击弹Yes/No确认框) ===
            _resetButton = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(16, 0),   //位置在 ResizeWindow 里按高度对齐
                Font = new Font(Settings.FontName, 9F, FontStyle.Underline),
                ForeColour = Color.FromArgb(255, 255, 180, 180),
                OutLine = true,
                OutLineColour = Color.Black,
                Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentResetButton),
            };
            _resetButton.Click += (o, e) =>
            {
                //确认框(具体消耗金币数由服务端裁决, 失败原因通过聊天栏返回)
                var box = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentResetConfirm), MirMessageBoxButtons.YesNo);
                box.YesButton.Click += (o2, e2) => Network.Enqueue(new C.ResetTalentPoints());
                box.Show();
            };
        }

        /// <summary>
        /// 收到服务端全天赋表(TalentInfo包): 重建整个网格.
        /// 在 登录后主动请求 / 服务端 @ReloadTalents 后 被调用.
        /// </summary>
        public void ReceiveTalentInfo(S.TalentInfo p)
        {
            _talents.Clear();
            //只保留当前职业可学的天赋(255=全职业) —— 战士开只见战士树, 法师开自动变法师树
            _talents.AddRange(p.Talents.Where(t => t.Class == 255 || t.Class == (byte)User.Class));

            //旧格子全部销毁后重建(热重载后天赋表可能增删改)
            foreach (var slot in _slots.Values) slot.Dispose();
            foreach (var label in _tierLabels) label.Dispose();
            _slots.Clear();
            _cells.Clear();
            _levelLabels.Clear();
            _tierLabels.Clear();

            _tiers = 1;

            foreach (var talent in _talents)
            {
                if (talent.Tier > _tiers) _tiers = talent.Tier;

                //--- 格子底槽: 深色面板+细边框, 视觉上像背包格子 ---
                //注意: 不能设 NotControl=true! 本引擎命中测试是父控件不过就不下探子控件,
                //      底槽不可命中会导致里面的图标按钮收不到鼠标(悬停提示和点击全失效)
                var slot = new MirImageControl
                {
                    Index = -1,
                    BackColour = SlotBg,
                    Border = true,
                    BorderColour = SlotBorder,
                    Parent = this,
                    Size = new Size(SlotSize, SlotSize),
                    Location = new Point(GridOriginX + (talent.Column - 1) * PitchX,
                                         GridOriginY + (talent.Tier - 1) * PitchY),
                };

                //--- 图标按钮: 图标取自 MagIcon 图库(编号=Talents.txt 图标列) ---
                var cell = new MirButton
                {
                    Index = talent.Icon,
                    Library = Libraries.MagIcon,
                    Parent = slot,       //挂在底槽下, 自动居中
                    Size = new Size(IconSize, IconSize),
                    Location = new Point((SlotSize - IconSize) / 2, (SlotSize - IconSize) / 2),
                    Sound = SoundList.ButtonA,
                };

                //点击即请求学习/升级(能不能学由服务端校验, 失败原因走聊天栏)
                var talentId = talent.Id;
                cell.Click += (o, e) => Network.Enqueue(new C.LearnTalent { TalentId = talentId });

                //--- 等级小字(每次刷新时对齐到格子右下角) ---
                var levelLabel = new MirLabel
                {
                    Parent = slot,
                    AutoSize = true,
                    Font = new Font(Settings.FontName, 7.5F),
                    ForeColour = Color.White,
                    OutLine = true,
                    OutLineColour = Color.Black,
                    NotControl = true,
                    Text = "0/" + talent.MaxLevel,
                };

                _slots[talent.Id] = slot;
                _cells[talent.Id] = cell;
                _levelLabels[talent.Id] = levelLabel;
            }

            //--- 左侧层标签: "第1层"~"第N层", 与每行格子垂直居中 ---
            for (int tier = 1; tier <= _tiers; tier++)
            {
                var label = new MirLabel
                {
                    Parent = this,
                    AutoSize = true,
                    Font = new Font(Settings.FontName, 8.5F),
                    ForeColour = Color.FromArgb(255, 200, 185, 160),   //米灰色
                    OutLine = true,
                    OutLineColour = Color.Black,
                    NotControl = true,
                    Text = $"第{CN_NUM[tier]}层",
                };
                //垂直居中于该行格子(微调: 整列左移3px)
                label.Location = new Point(GridOriginX - TierLabelWidth + 1,
                                           GridOriginY + (tier - 1) * PitchY + (SlotSize - label.Size.Height) / 2);
                _tierLabels.Add(label);
            }

            ResizeWindow();

            //天赋表交给 UserObject(客户端本地属性计算需要每级属性数据)
            User.TalentInfos = _talents;

            RefreshInterface();
        }

        /// <summary>中文数字(层标签用)</summary>
        private static readonly string[] CN_NUM = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };

        /// <summary>窗口高度按层数自适应, 并把底部控件重新对位</summary>
        private void ResizeWindow()
        {
            Size = new Size(WindowWidth, GridOriginY + _tiers * PitchY + FooterHeight);
            _closeButton.Location = new Point(WindowWidth - 24, 4);
            _resetButton.Location = new Point(16, Size.Height - 26);
        }

        /// <summary>
        /// 把已学等级同步进 UserObject 并重算客户端属性.
        /// 角色窗口/血球显示的属性是客户端本地算的, 不同步的话学天赋后面板数值不会变.
        /// </summary>
        private void SyncToUser()
        {
            User.TalentLevels.Clear();
            foreach (var kv in _learned)
                User.TalentLevels[kv.Key] = kv.Value;

            User.RefreshStats();   //重算显示属性并刷新界面
        }

        /// <summary>收到玩家天赋状态(PlayerTalentInfo包): 更新全部等级与点数(登录/洗点后)</summary>
        public void ReceivePlayerTalentInfo(S.PlayerTalentInfo p)
        {
            _points = p.Points;
            _learned.Clear();

            foreach (var t in p.Talents)
                _learned[t.Id] = t.Level;

            SyncToUser();      //登录数据到齐, 重算一次属性(含天赋加成)
            RefreshInterface();
        }

        /// <summary>收到单个天赋等级变化(TalentChange包): 学习成功后增量刷新</summary>
        public void ReceiveTalentChange(S.TalentChange p)
        {
            _learned[p.TalentId] = p.Level;
            _points = p.Points;
            SyncToUser();      //学习后立即刷新角色面板显示
            RefreshInterface();
        }

        /// <summary>收到洗点结果(TalentReset包): 成功清空全部等级, 失败在聊天栏提示原因</summary>
        public void ReceiveTalentReset(S.TalentReset p)
        {
            if (!p.Success)
            {
                GameScene.Scene.ChatDialog.ReceiveChat(p.Reason, ChatType.System);
                return;
            }

            foreach (var key in _learned.Keys.ToList())
                _learned[key] = 0;

            _points = p.Points;
            SyncToUser();      //洗点后天赋加成清零, 重算属性
            RefreshInterface();
        }

        /// <summary>
        /// 每次绘制前刷新显示: 标题(职业名) / 点数文字 / 格子等级小字与颜色 /
        /// 灰化状态 / 底槽边框状态色 / 悬停提示.
        /// </summary>
        private void RefreshInterface()
        {
            //标题带上职业名(如 "天 赋 · 战士")
            _titleLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentTitle)
                             + " · " + User.Class.ToLocalizedString();
            _pointsLabel.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentPoints, _points);

            foreach (var talent in _talents)
            {
                if (!_slots.TryGetValue(talent.Id, out var slot) ||
                    !_cells.TryGetValue(talent.Id, out var cell) ||
                    !_levelLabels.TryGetValue(talent.Id, out var levelLabel)) continue;

                var level = _learned.TryGetValue(talent.Id, out var lv) ? lv : 0;
                var maxed = level >= talent.MaxLevel;

                //等级小字对齐格子右下角(每次绘制都对齐, 防止文字宽度变化后错位)
                levelLabel.Text = level + "/" + talent.MaxLevel;
                levelLabel.Location = new Point(SlotSize - levelLabel.Size.Width - 1,
                                                SlotSize - levelLabel.Size.Height - 1);

                //满级=金字 / 已学=白字 / 未学=灰字
                levelLabel.ForeColour = maxed ? Color.FromArgb(255, 255, 200, 100) : level > 0 ? Color.White : Color.Gray;

                //可学判断(与服务端校验一致, 仅影响显示; 服务端仍是最终裁决)
                var learnable = User.Level >= talent.RequiredLevel && PreRequirementsMet(talent) && !maxed && talent.SpecialEffect == 0;
                cell.GrayScale = !learnable && level == 0;  //未学且不可学才整体灰化

                //底槽边框状态色: 满级金框 / 可学微亮框 / 普通暗框
                slot.BorderColour = maxed ? SlotBorderMaxed
                                  : learnable ? SlotBorderLearnable
                                  : SlotBorder;

                //悬停提示: 名称/等级/暂未开放/需求等级/前置(带√×)/每级属性/描述
                var sb = new StringBuilder();
                sb.Append(talent.Name);
                sb.Append("  Lv").Append(level).Append('/').Append(talent.MaxLevel).Append('\n');

                if (talent.SpecialEffect != 0)
                    sb.Append(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentNotOpen)).Append('\n');

                sb.Append(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TalentRequiredLevel, talent.RequiredLevel)).Append('\n');

                for (int i = 0; i < talent.PreIds.Count; i++)
                {
                    var preName = GetTalentName(talent.PreIds[i]);
                    var preLevel = _learned.TryGetValue(talent.PreIds[i], out var plv) ? plv : 0;
                    var mark = preLevel >= talent.PreLevels[i] ? "√" : "×";   //前置达标打勾, 未达标打叉
                    sb.Append(GameLanguage.ClientTextMap.GetLocalization(
                        ClientTextKeys.TalentPreLine, mark, preName, preLevel, talent.PreLevels[i])).Append('\n');
                }

                //每级属性列表(Stat枚举名自动转本地化文字)
                foreach (var stat in talent.Stats.Values)
                    sb.Append(stat.Key.ToLocalizedString()).Append(" +").Append(stat.Value).Append('\n');

                if (!string.IsNullOrEmpty(talent.Description))
                    sb.Append(talent.Description);

                cell.Hint = sb.ToString();
            }
        }

        /// <summary>前置天赋是否全部达标(用于灰化显示, 学习校验以服务端为准)</summary>
        private bool PreRequirementsMet(S.ClientTalentInfo talent)
        {
            for (int i = 0; i < talent.PreIds.Count; i++)
            {
                var level = _learned.TryGetValue(talent.PreIds[i], out var lv) ? lv : 0;
                if (level < talent.PreLevels[i]) return false;
            }
            return true;
        }

        /// <summary>按Id取天赋名(找不到返回Id数字)</summary>
        private string GetTalentName(int id)
        {
            foreach (var t in _talents)
                if (t.Id == id) return t.Name;
            return id.ToString();
        }
    }
}
