using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{
    public class BuffDialog : MirImageControl
    {
        public List<ClientBuff> Buffs = new List<ClientBuff>();

        protected MirButton _expandCollapseButton;
        protected MirLabel _buffCountLabel;
        protected List<MirImageControl> _buffList = new List<MirImageControl>();
        protected bool _fadedOut, _fadedIn;
        protected int _buffCount;
        protected long _nextFadeTime;
        public Func<bool> GetExpandedParameter;
        public Action<bool> SetExpandedParameter;
        private bool ExpandedBuffWindow
        {
            get { return GetExpandedParameter(); }
            set { SetExpandedParameter(value); }
        }

    protected const long FadeDelay = 55;
        protected const float FadeRate = 0.2f;

        public BuffDialog()
        {
            Index = 20;
            Library = Libraries.Prguse2;
            Movable = false;
            Size = new Size(44, 34);
            Location = new Point(Settings.ScreenWidth - 170, 0);
            Sort = true;

            Opacity = 0f;
            _fadedOut = true;

            _expandCollapseButton = new MirButton
            {
                Index = 7,
                HoverIndex = 8,
                Size = new Size(16, 15),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 9,
                Sound = SoundList.ButtonA,
                Opacity = 0f
            };
            _expandCollapseButton.Click += (o, e) =>
            {
                if (_buffCount == 1)
                {
                    ExpandedBuffWindow = true;
                }
                else
                {
                    ExpandedBuffWindow = !ExpandedBuffWindow;
                }

                UpdateWindow();
            };

            _buffCountLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Font = new Font(Settings.FontName, 10F, FontStyle.Bold),
                NotControl = true,
                Sort = true,
                Visible = false,
                ForeColour = Color.Yellow,
                OutLineColour = Color.Black,
            };
        }

        public void CreateBuff(ClientBuff buff)
        {
            var buffImage = BuffImage(buff.Type);

            var buffLibrary = Libraries.BuffIcon;

            if (buffImage >= 30000)
            {
                buffImage -= 30000;
                buffLibrary = Libraries.MagIcon;
            }

            if (buffImage >= 20000)
            {
                buffImage -= 20000;
                buffLibrary = Libraries.Prguse2;
            }

            if (buffImage >= 10000)
            {
                buffImage -= 10000;
                buffLibrary = Libraries.Prguse;
            }

            var image = new MirImageControl
            {
                Library = buffLibrary,
                Parent = this,
                Visible = true,
                Sort = false,
                Index = buffImage
            };

            _buffList.Insert(0, image);
            UpdateWindow();
        }

        public void RemoveBuff(int buffId)
        {
            _buffList[buffId].Dispose();
            _buffList.RemoveAt(buffId);

            UpdateWindow();
        }

        public void Process()
        {
            if (!Visible) return;

            if (_buffList.Count != _buffCount)
            {
                UpdateWindow();
            }

            for (var i = 0; i < _buffList.Count; i++)
            {
                var image = _buffList[i];
                var buff = Buffs[i];

                var buffImage = BuffImage(buff.Type);
                var buffLibrary = Libraries.BuffIcon;

                //ArcherSpells - VampireShot,PoisonShot
                if (buffImage >= 30000)
                {
                    buffImage -= 30000;
                    buffLibrary = Libraries.MagIcon;
                }

                if (buffImage >= 20000)
                {
                    buffImage -= 20000;
                    buffLibrary = Libraries.Prguse2;
                }

                if (buffImage >= 10000)
                {
                    buffImage -= 10000;
                    buffLibrary = Libraries.Prguse;
                }

                var location = new Point(Size.Width - 10 - 23 - (i * 23) + ((10 * 23) * (i / 10)), 6 + ((i / 10) * 24));

                image.Location = new Point(location.X, location.Y);
                image.Hint = ExpandedBuffWindow ? BuffString(buff) : CombinedBuffText();
                image.Index = buffImage;
                image.Library = buffLibrary;

                if (ExpandedBuffWindow || !ExpandedBuffWindow && i == 0)
                {
                    image.Visible = true;
                    image.Opacity = 1f;
                }
                else
                {
                    image.Visible = false;
                    image.Opacity = 0.6f;
                }

                if (buff.Paused || buff.Infinite || !(Math.Round((buff.ExpireTime - CMain.Time) / 1000D) <= 5))
                    continue;

                var time = (buff.ExpireTime - CMain.Time) / 100D;

                if (Math.Round(time) % 10 < 5)
                    image.Index = -1;
            }

            if (IsMouseOver(CMain.MPoint))
            { 
                if (_buffCount == 0 || (!_fadedIn && CMain.Time <= _nextFadeTime))
                    return;

                Opacity += FadeRate;
                _expandCollapseButton.Opacity += FadeRate;

                if (Opacity > 1f)
                {
                    Opacity = 1f;
                    _expandCollapseButton.Opacity = 1f;
                    _fadedIn = true;
                    _fadedOut = false;
                }

                _nextFadeTime = CMain.Time + FadeDelay;
            }
            else
            {
                if (!_fadedOut && CMain.Time <= _nextFadeTime)
                    return;

                Opacity -= FadeRate;
                _expandCollapseButton.Opacity -= FadeRate;

                if (Opacity < 0f)
                {
                    Opacity = 0f;
                    _expandCollapseButton.Opacity = 0f;
                    _fadedOut = true;
                    _fadedIn = false;
                }
                    
                _nextFadeTime = CMain.Time + FadeDelay;
            }
        }

        private void UpdateWindow()
        {
            _buffCount = _buffList.Count;

            var baseImage = 20;
            var heightOffset = Location.Y;

            //foreach (var dialog in GameScene.Scene.BuffDialogs)
            //{
            //    if (dialog.Category == Category) break;

            //    if (dialog.Buffs.Count > 0)
            //    {
            //        heightOffset += dialog.Size.Height;
            //    }
            //}

            if (_buffCount > 0 && ExpandedBuffWindow)
            {
                var oldWidth = Size.Width;

                if (_buffCount <= 10)
                    Index = baseImage + _buffCount - 1;
                else if (_buffCount > 10)
                    Index = baseImage + 10;
                else if (_buffCount > 20)
                    Index = baseImage + 11;
                else if (_buffCount > 30)
                    Index = baseImage + 12;
                else if (_buffCount > 40)
                    Index = baseImage + 13;

                var newX = Location.X - Size.Width + oldWidth;
                var newY = heightOffset;
                Location = new Point(newX, newY);

                _buffCountLabel.Visible = false;

                _expandCollapseButton.Location = new Point(Size.Width - 15, 0);
                Size = new Size((_buffCount > 10 ? 10 : _buffCount) * 23, 24 + (_buffCount / 10) * 24);
            }
            else if (_buffCount > 0 && !ExpandedBuffWindow)
            {
                var oldWidth = Size.Width;

                Index = 20;
            
                var newX = Location.X - Size.Width + oldWidth;
                var newY = heightOffset;
                Location = new Point(newX, newY);

                _buffCountLabel.Visible = true;
                _buffCountLabel.Text = $"{_buffCount}";
                _buffCountLabel.Location = new Point(Size.Width / 2 - _buffCountLabel.Size.Width / 2, Size.Height / 2 - 10);
                _buffCountLabel.BringToFront();

                _expandCollapseButton.Location = new Point(Size.Width - 15, 0);
                Size = new Size(44, 34);
            }
        }

        public string BuffString(ClientBuff buff)
        {
            string text = RegexFunctions.SeperateCamelCase(buff.Type.ToLocalizedString()) + "\n";
            bool overridestats = false;

            switch (buff.Type)
            {
                case BuffType.GameMaster:
                    GMOptions options = (GMOptions)buff.Values[0];

                    if (options.HasFlag(GMOptions.GameMaster)) text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Invisible);
                    if (options.HasFlag(GMOptions.Superman)) text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Superman);
                    if (options.HasFlag(GMOptions.Observer)) text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Observer);
                    break;
                case BuffType.MentalState:
                    switch (buff.Values[0])
                    {
                        case 0:
                            text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AgressiveFullDamageCantShootOverWalls);
                            break;
                        case 1:
                            text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TrickShotMinimalDamage);
                            break;
                        case 2:
                            text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.GroupModeMediumDamageDontStealAgro);
                            break;
                    }
                    break;
                case BuffType.Hiding:
                case BuffType.ClearRing:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.InvisibleToManyMonsters);
                    break;
                case BuffType.MoonLight:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.InvisibleToPlayersAndMonstersAtDistance);
                    break;
                case BuffType.EnergyShield:
                    overridestats = true;
                    text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ChanceGainHpWhenAttacked), buff.Stats[Stat.EnergyShieldPercent], buff.Stats[Stat.EnergyShieldHPGain]);
                    break;
                case BuffType.DarkBody:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.InvisibleToManyMonstersAbleToMove);
                    break;
                case BuffType.VampireShot:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.GivesVampiricAbility);
                    break;
                case BuffType.PoisonShot:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.GivesPoisonAbility);
                    break;
                case BuffType.Concentration:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.IncreaseElementExtractionChance);
                    break;
                case BuffType.MagicBooster:
                    overridestats = true;
                    text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.IncreaseMcAndConsumption), buff.Stats[Stat.MinMC], buff.Stats[Stat.MaxMC], buff.Stats[Stat.ManaPenaltyPercent]);
                    break;
                case BuffType.Transform:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.DisguisesYourAppearance);
                    break;
                case BuffType.Mentee:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.LearnSkillPointsTwiceAsQuick);
                    break;
                case BuffType.Guild:
                    text += GameScene.Scene.GuildDialog.ActiveStats;
                    break;
                case BuffType.Blindness:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ReducesVisibility);
                    break;
                case BuffType.Newbie:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.GuildMemberBoost);
                    break;
            }

            if (!overridestats)
            {
                foreach (var val in buff.Stats.Values)
                {
                    var c = val.Value < 0 ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Decreases) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Increases);
                    var strKey = val.Key.ToLocalizedString();

                    var sign = "";

                    if (val.Key.ToString().Contains("Percent"))
                        sign = "%";
                    else if (val.Key.ToString().Contains("Multiplier"))
                        sign = "x";

                    var txt = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BuffEffect), c, strKey, val.Value, sign);

                    text += txt;
                }
            }

            if (buff.Paused)
            {
                text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ExpirePaused);
            }
            else if (buff.Infinite)
            {
                text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ExpireNever);
            }
            else
            {
                text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.Expire), Functions.PrintTimeSpanFromSeconds(Math.Round((buff.ExpireTime - CMain.Time) / 1000D)));
            }

            if (!string.IsNullOrEmpty(buff.Caster)) text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CasterName), buff.Caster);

            return text;
        }

        private string CombinedBuffText()
        {
            string text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ActiveBuffs);
            var stats = new Stats();

            for (var i = 0; i < _buffList.Count; i++)
            {
                var buff = Buffs[i];

                stats.Add(buff.Stats);
            }

            foreach (var val in stats.Values)
            {
                var c = val.Value < 0 ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Decreases) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Increases);
                var strKey = val.Key.ToLocalizedString();

                var sign = "";

                if (val.Key.ToString().Contains("Percent"))
                    sign = "%";
                else if (val.Key.ToString().Contains("Multiplier"))
                    sign = "x";

                var txt = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BuffEffect), c, strKey, val.Value, sign);;

                text += txt;
            }

            return text;
        }

        private int BuffImage(BuffType type)
        {
            switch (type)
            {
                //Skills (30000 = MagIcon, 20000 = Prguse2, 10000 = Prguse, else BuffIcon)
                case BuffType.TemporalFlux:
                    return 75;
                case BuffType.Hiding:
                    return 30034;
                case BuffType.Haste:
                    return 30120;
                case BuffType.SwiftFeet:
                    return 30134;
                case BuffType.Fury:
                    return 30152;
                case BuffType.SoulShield:
                    return 30026;
                case BuffType.BlessedArmour:
                    return 30028;
                case BuffType.LightBody:
                    return 30136;
                case BuffType.UltimateEnhancer:
                    return 30070;
                case BuffType.ProtectionField:
                    return 30100;
                case BuffType.Rage:
                    return 30098;
                case BuffType.Curse:
                    return 30090;
                case BuffType.MoonLight:
                    return 30130;
                case BuffType.DarkBody:
                    return 30140;
                case BuffType.Concentration:
                    return 30192;
                case BuffType.VampireShot:
                    return 30200;
                case BuffType.PoisonShot:
                    return 30204;
                case BuffType.CounterAttack:
                    return 30144;
                case BuffType.MentalState:
                    return 59;
                case BuffType.EnergyShield:
                    return 30114;
                case BuffType.MagicBooster:
                    return 30146;
                case BuffType.ImmortalSkin:
                    return 30160;
                case BuffType.MagicShield:
                    return 30060;

                //Monster
                case BuffType.RhinoPriestDebuff:
                    return 115;
                case BuffType.Blindness:
                    return 88;

                //Special
                case BuffType.GameMaster:
                    return 20121;
                case BuffType.General:
                    return 20122;
                case BuffType.Exp:
                    return 334;
                case BuffType.Drop:
                    return 336;
                case BuffType.BagWeight:
                    return 12;
                case BuffType.Gold:
                    return 316;
                case BuffType.Transform:
                    return 10890;
                case BuffType.Lover:
                    return 179;
                case BuffType.Mentee:
                case BuffType.Mentor:
                    return 80;
                case BuffType.Guild:
                    return 63;
                case BuffType.Prison:
                    return 70;
                case BuffType.Rested:
                    return 508;
                case BuffType.Skill:
                    return 60;
                case BuffType.ClearRing:
                    return 24;
                case BuffType.Newbie:
                    return 10903;

                //Stats
                case BuffType.Impact:
                    return 307;
                case BuffType.Magic:
                    return 308;
                case BuffType.Taoist:
                    return 309;
                case BuffType.Storm:
                    return 306;
                case BuffType.HealthAid:
                    return 310;
                case BuffType.ManaAid:
                    return 311;
                case BuffType.Defence:
                    return 300;
                case BuffType.MagicDefence:
                    return 313;
                case BuffType.WonderDrug:
                    return 317;
                case BuffType.LuckAid:
                    return 319;
                case BuffType.AccuracyAid:
                    return 320;
                case BuffType.AgilityAid:
                    return 10893;
                case BuffType.Knapsack:
                    return 10872;
                case BuffType.CreatureBuff:
                    return 510;
                case BuffType.SnackTier1:
                case BuffType.SnackTier2:
                case BuffType.SnackTier3:
                case BuffType.SnackTier4:
                    return 510;
                default:
                    return 0;
            }
        }
    }


    //UNFINISHED
    public class ClientPoisonBuff
    {
        public PoisonType Type;
        public string Caster;
        public int Value;
        public int TickSpeed;
        public long ExpireTime;
    }

    public sealed class PoisonBuffDialog : MirImageControl
    {
        public List<ClientPoisonBuff> Buffs = new List<ClientPoisonBuff>();

        private MirButton _expandCollapseButton;
        private MirLabel _buffCountLabel;
        private List<MirImageControl> _buffList = new List<MirImageControl>();
        private bool _fadedOut, _fadedIn;
        private int _buffCount;
        private long _nextFadeTime;

        private const long FadeDelay = 55;
        private const float FadeRate = 0.2f;

        public PoisonBuffDialog()
        {
            Index = 40;
            Library = Libraries.Prguse2;
            Movable = false;
            Size = new Size(44, 34);
            Location = new Point(Settings.ScreenWidth - 170, 0);
            Sort = true;

            Opacity = 0f;
            _fadedOut = true;

            _expandCollapseButton = new MirButton
            {
                Index = 7,
                HoverIndex = 8,
                Size = new Size(16, 15),
                Library = Libraries.Prguse2,
                Parent = this,
                PressedIndex = 9,
                Sound = SoundList.ButtonA,
                Opacity = 0f
            };
            _expandCollapseButton.Click += (o, e) =>
            {
                if (_buffCount == 1)
                {
                    Settings.ExpandedBuffWindow = true;
                }
                else
                {
                    Settings.ExpandedBuffWindow = !Settings.ExpandedBuffWindow;
                }

                UpdateWindow();
            };

            _buffCountLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
                Font = new Font(Settings.FontName, 10F, FontStyle.Bold),
                NotControl = true,
                Sort = true,
                Visible = false,
                ForeColour = Color.Yellow,
                OutLineColour = Color.Black,
            };
        }

        public void CreateBuff(ClientPoisonBuff buff)
        {
            var buffImage = BuffImage(buff.Type);

            var buffLibrary = Libraries.BuffIcon;

            if (buffImage >= 20000)
            {
                buffImage -= 20000;
                buffLibrary = Libraries.MagIcon;
            }

            if (buffImage >= 10000)
            {
                buffImage -= 10000;
                buffLibrary = Libraries.Prguse2;
            }

            var image = new MirImageControl
            {
                Library = buffLibrary,
                Parent = this,
                Visible = true,
                Sort = false,
                Index = buffImage
            };

            _buffList.Insert(0, image);
            UpdateWindow();
        }

        public string BuffString(ClientPoisonBuff buff)
        {
            string text = RegexFunctions.SeperateCamelCase(buff.Type.ToLocalizedString()) + "\n";

            switch (buff.Type)
            {
                case PoisonType.Green:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Seconds) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Second);

                        text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ReceiveDamageEveryTick), buff.Value, tick, tickName);
                    }
                    break;
                case PoisonType.Red:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Seconds) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Second);

                        text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.ReducesArmourRatePerTick), tick, tickName);
                    }
                    break;
                case PoisonType.Slow:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ReducesMovementSpeed);
                    break;
                case PoisonType.Frozen:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PreventsCastingMovingAttacking);
                    break;
                case PoisonType.Stun:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Seconds) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Second);

                        text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.DamageReceivedIncrease), tick, tickName);
                    }
                    break;
                case PoisonType.Paralysis:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PreventsMoveAndAttack);
                    break;
                case PoisonType.DelayedExplosion:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TickingTimeBomb);
                    break;
                case PoisonType.Bleeding:
                    {
                        var tick = buff.TickSpeed / 1000;
                        var tickName = tick > 1 ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Seconds) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Second);

                        text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.RecieveDamageEveryTime), buff.Value, tick, tickName);
                    }
                    break;
                case PoisonType.LRParalysis:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PreventsMoveAttackCancelOnHit);
                    break;
                case PoisonType.Blindness:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.CausesTemporaryBlindness);
                    break;
                case PoisonType.Dazed:
                    text += GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PreventsAttacking);
                    break;
            }

            text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.Expire), Functions.PrintTimeSpanFromSeconds(Math.Round((buff.ExpireTime - CMain.Time) / 1000D)));

            if (!string.IsNullOrEmpty(buff.Caster)) text += GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.CasterName), buff.Caster);

            return text;
        }

        private int BuffImage(PoisonType type)
        {
            switch (type)
            {
                case PoisonType.Green:
                    return 221;
                case PoisonType.Red:
                    return 222;
                case PoisonType.Slow:
                    return 225;
                case PoisonType.Frozen:
                    return 223;
                case PoisonType.Stun:
                    return 224;
                case PoisonType.Paralysis:
                    return 233;
                case PoisonType.DelayedExplosion:
                    return 229;
                case PoisonType.Bleeding:
                    return 231;
                case PoisonType.LRParalysis:
                    return 233;
                case PoisonType.Blindness:
                    return 226;
                case PoisonType.Dazed:
                    return 230;
                default:
                    return 0;
            }
        }

        public void Process()
        {
            if (!Visible) return;

            if (_buffList.Count != _buffCount)
            {
                UpdateWindow();
            }

            for (var i = 0; i < _buffList.Count; i++)
            {
                var image = _buffList[i];
                var buff = Buffs[i];

                var buffImage = BuffImage(buff.Type);
                var buffLibrary = Libraries.BuffIcon;

                //ArcherSpells - VampireShot,PoisonShot
                if (buffImage >= 20000)
                {
                    buffImage -= 20000;
                    buffLibrary = Libraries.MagIcon;
                }

                if (buffImage >= 10000)
                {
                    buffImage -= 10000;
                    buffLibrary = Libraries.Prguse2;
                }

                var location = new Point(Size.Width - 10 - 23 - (i * 23) + ((10 * 23) * (i / 10)), 6 + ((i / 10) * 24));

                image.Location = new Point(location.X, location.Y);
                image.Hint = Settings.ExpandedBuffWindow ? BuffString(buff) : CombinedBuffText();
                image.Index = buffImage;
                image.Library = buffLibrary;

                if (Settings.ExpandedBuffWindow || !Settings.ExpandedBuffWindow && i == 0)
                {
                    image.Visible = true;
                    image.Opacity = 1f;
                }
                else
                {
                    image.Visible = false;
                    image.Opacity = 0.6f;
                }

                if (!(Math.Round((buff.ExpireTime - CMain.Time) / 1000D) <= 5))
                    continue;

                var time = (buff.ExpireTime - CMain.Time) / 100D;

                if (Math.Round(time) % 10 < 5)
                    image.Index = -1;
            }

            if (IsMouseOver(CMain.MPoint))
            {
                if (_buffCount == 0 || (!_fadedIn && CMain.Time <= _nextFadeTime))
                    return;

                Opacity += FadeRate;
                _expandCollapseButton.Opacity += FadeRate;

                if (Opacity > 1f)
                {
                    Opacity = 1f;
                    _expandCollapseButton.Opacity = 1f;
                    _fadedIn = true;
                    _fadedOut = false;
                }

                _nextFadeTime = CMain.Time + FadeDelay;
            }
            else
            {
                if (!_fadedOut && CMain.Time <= _nextFadeTime)
                    return;

                Opacity -= FadeRate;
                _expandCollapseButton.Opacity -= FadeRate;

                if (Opacity < 0f)
                {
                    Opacity = 0f;
                    _expandCollapseButton.Opacity = 0f;
                    _fadedOut = true;
                    _fadedIn = false;
                }

                _nextFadeTime = CMain.Time + FadeDelay;
            }
        }

        private void UpdateWindow()
        {
            _buffCount = _buffList.Count;

            var baseImage = 20;
            var heightOffset = 36;

            if (_buffCount > 0 && Settings.ExpandedBuffWindow)
            {
                var oldWidth = Size.Width;

                if (_buffCount <= 10)
                    Index = baseImage + _buffCount - 1;
                else if (_buffCount > 10)
                    Index = baseImage + 10;
                else if (_buffCount > 20)
                    Index = baseImage + 11;
                else if (_buffCount > 30)
                    Index = baseImage + 12;
                else if (_buffCount > 40)
                    Index = baseImage + 13;

                var newX = Location.X - Size.Width + oldWidth;
                var newY = heightOffset;
                Location = new Point(newX, newY);

                _buffCountLabel.Visible = false;

                _expandCollapseButton.Location = new Point(Size.Width - 15, 0);
                Size = new Size((_buffCount > 10 ? 10 : _buffCount) * 23, 24 + (_buffCount / 10) * 24);
            }
            else if (_buffCount > 0 && !Settings.ExpandedBuffWindow)
            {
                var oldWidth = Size.Width;

                Index = 20;

                var newX = Location.X - Size.Width + oldWidth;
                var newY = heightOffset;
                Location = new Point(newX, newY);

                _buffCountLabel.Visible = true;
                _buffCountLabel.Text = $"{_buffCount}";
                _buffCountLabel.Location = new Point(Size.Width / 2 - _buffCountLabel.Size.Width / 2, Size.Height / 2 - 10);
                _buffCountLabel.BringToFront();

                _expandCollapseButton.Location = new Point(Size.Width - 15, 0);
                Size = new Size(44, 34);
            }
        }

        private string CombinedBuffText()
        {
            string text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.ActivePoisons);

            return text;
        }
    }

}

