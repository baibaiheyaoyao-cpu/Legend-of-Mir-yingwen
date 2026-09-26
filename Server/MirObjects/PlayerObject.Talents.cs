using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects
{
    /// <summary>
    /// 天赋系统 - PlayerObject 的天赋逻辑 (partial 拆分文件, 主文件: PlayerObject.cs).
    ///
    /// 职责:
    ///   1. 点数发放: 按等级计算应得点数, 升级/登录时补发差额(改配置只会多补不会回收)
    ///   2. 学习/升级: 校验 职业/等级/前置/满级/点数, 成功后叠加属性并回包
    ///   3. 洗点: 扣除金币, 清空已学天赋并全额返还点数
    ///   4. 属性生效: RefreshTalentStats() 在每次 RefreshStats 时把已学天赋的属性加进角色属性
    /// </summary>
    public partial class PlayerObject
    {
        /// <summary>
        /// 按等级计算该角色"应得"的天赋点总量(静态方法, 客户端不参与计算).
        /// 公式: 达到解锁等级当场给第一批, 之后每升 TalentStepLevel 级再给一批 TalentStepPoint 点.
        /// 例: StartingLevel=10/StepLevel=1/StepPoint=100 时, 10级=100点, 11级=200点, 20级=1100点.
        /// </summary>
        public static int CalcTalentPointsForLevel(int level)
        {
            if (level < Settings.TalentStartingLevel) return 0;

            var batches = (level - Settings.TalentStartingLevel) / Settings.TalentStepLevel + 1;
            return batches * Settings.TalentStepPoint;
        }

        /// <summary>
        /// 补发天赋点: 若按等级计算的应得总量 > 历史已发放量, 则补差额.
        /// 在 登录(StartGameSuccess) / 升级(LevelUp) 时调用;
        /// 配置改大只会多补, 改小不会回收已发点数, 对玩家友好.
        /// </summary>
        public void EnsureTalentPoints()
        {
            var deserved = CalcTalentPointsForLevel(Level);
            if (deserved <= Info.TalentPointsGranted) return;

            var gained = deserved - Info.TalentPointsGranted;
            Info.TalentPoints += gained;
            Info.TalentPointsGranted = deserved;

            //聊天栏提示获得了多少点
            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentPointsGained), gained), ChatType.Hint);
        }

        /// <summary>
        /// 天赋属性生效: 把所有已学天赋的属性按等级叠加进角色 Stats.
        /// 由 HumanObject.RefreshStats() 统一调度(在百分比加成计算之前),
        /// 装备变化/升级/学天赋/洗点等任何属性重算都会走到这里, 无需手动触发.
        /// </summary>
        public override void RefreshTalentStats()
        {
            for (var i = 0; i < Info.TalentList.Count; i++)
            {
                var userTalent = Info.TalentList[i];
                var talent = GetTalentInfo(userTalent.Id);
                if (talent == null) continue; //天赋表被删/改名后残留的旧记录直接忽略

                //逐条叠加(每级属性 x 当前等级), 避免重复构造Stats对象
                foreach (var pair in talent.Stats.Values)
                    Stats[pair.Key] += pair.Value * userTalent.Level;
            }
        }

        /// <summary>按Id查找天赋定义(找不到返回null, 天赋表可能被热重载修改)</summary>
        public TalentInfo GetTalentInfo(int id)
        {
            for (var i = 0; i < Envir.TalentInfoList.Count; i++)
                if (Envir.TalentInfoList[i].Index == id)
                    return Envir.TalentInfoList[i];

            return null;
        }

        /// <summary>
        /// 下发天赋数据: 全天赋表(TalentInfo包) + 玩家自身状态(PlayerTalentInfo包).
        /// 登录时 / 天赋表热重载后 / 客户端请求(ClientTalent包) 时调用.
        /// </summary>
        public void SendTalentInfo()
        {
            if (Connection == null) return;

            var talentPacket = new S.TalentInfo();

            //把服务端 TalentInfo 转成网络结构下发给客户端
            foreach (var talent in Envir.TalentInfoList)
            {
                var entry = new S.ClientTalentInfo
                {
                    Id = talent.Index,
                    Name = talent.Name,
                    Description = talent.Description,
                    Icon = talent.Icon,
                    Class = talent.Class,
                    RequiredLevel = talent.RequiredLevel,
                    MaxLevel = talent.MaxLevel,
                    Tier = talent.Tier,
                    Column = talent.Column,
                    SpecialEffect = talent.SpecialEffect,
                    Stats = talent.Stats,
                };

                //前置依赖转成两个平行列表(客户端显示用)
                foreach (var pre in talent.PreTalents)
                {
                    entry.PreIds.Add(pre[0]);
                    entry.PreLevels.Add(pre[1]);
                }

                talentPacket.Talents.Add(entry);
            }

            Enqueue(talentPacket);
            Enqueue(BuildPlayerTalentInfo());
        }

        /// <summary>构造"玩家天赋状态"包(剩余点数 + 已学列表)</summary>
        private S.PlayerTalentInfo BuildPlayerTalentInfo()
        {
            var packet = new S.PlayerTalentInfo { Points = Info.TalentPoints };

            foreach (var userTalent in Info.TalentList)
                packet.Talents.Add(new S.ClientUserTalent(userTalent.Id, userTalent.Level));

            return packet;
        }

        /// <summary>
        /// 学习/升级一个天赋(由客户端 LearnTalent 包触发).
        /// 校验链(顺序即提示优先级): 存在 -> 系统解锁 -> 职业 -> 等级 -> 暂未开放 -> 满级 -> 前置 -> 点数.
        /// 全部通过后扣点、记录等级、重算属性、回发 TalentChange.
        /// </summary>
        public void LearnTalent(int talentId)
        {
            var talent = GetTalentInfo(talentId);

            if (talent == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentNotFound), ChatType.Hint);
                return;
            }

            //系统整体解锁等级(Talent.ini 的 StartingLevel)
            if (Level < Settings.TalentStartingLevel)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentSystemLocked), Settings.TalentStartingLevel), ChatType.Hint);
                return;
            }

            //职业限制(Talents.txt 的职业列)
            if (!talent.MatchesClass(Class))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentWrongClass), ChatType.Hint);
                return;
            }

            //天赋自身需求等级(Talents.txt 的需求等级列)
            if (Level < talent.RequiredLevel)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentLowLevel), talent.RequiredLevel), ChatType.Hint);
                return;
            }

            //召唤类特效天赋(阶段三实现), 先禁止学习防止玩家浪费点数
            if (talent.SpecialEffect != 0)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentNotOpenYet), ChatType.Hint);
                return;
            }

            //找出已学记录(没有则临时建一个0级记录, 校验失败不会入库)
            var userTalent = GetOrCreateUserTalent(talentId);

            if (userTalent.Level >= talent.MaxLevel)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentMaxLevel), ChatType.Hint);
                return;
            }

            //前置天赋校验: 所有前置都需达到要求等级(Talents.txt 的前置列)
            foreach (var pre in talent.PreTalents)
            {
                var preUser = GetOrCreateUserTalent(pre[0]);
                if (preUser.Level < pre[1])
                {
                    var preInfo = GetTalentInfo(pre[0]);
                    var preName = preInfo != null ? preInfo.Name : pre[0].ToString();
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentPreNotMet), $"{preName} Lv{pre[1]}"), ChatType.Hint);
                    return;
                }
            }

            //点数校验与扣除(Talent.ini 的 LearnCost)
            if (Info.TalentPoints < Settings.TalentLearnCost)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TalentLowPoints), ChatType.Hint);
                return;
            }
            Info.TalentPoints -= Settings.TalentLearnCost;

            //记录新等级(新天赋才计入列表入库, 校验失败的临时记录不会写入)
            var newLevel = userTalent.Level + 1;
            userTalent.Level = newLevel;
            if (!Info.TalentList.Contains(userTalent))
                Info.TalentList.Add(userTalent);

            //属性立即生效并同步
            RefreshStats();

            Enqueue(new S.TalentChange
            {
                TalentId = talentId,
                Level = newLevel,
                Points = Info.TalentPoints,
            });

            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentLearned), talent.Name, newLevel, talent.MaxLevel), ChatType.Hint);
        }

        /// <summary>
        /// 重置天赋(洗点, 由客户端 ResetTalentPoints 包触发):
        /// 扣除 Talent.ini 配置的金币, 清空全部已学天赋并全额返还点数.
        /// </summary>
        public void ResetTalents()
        {
            //金币校验与扣除(参考 TeleportToNPC 的扣费方式)
            if (Account.Gold < Settings.TalentResetCost)
            {
                Enqueue(new S.TalentReset
                {
                    Success = false,
                    Reason = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentResetLowGold), Settings.TalentResetCost),
                });
                return;
            }

            Account.Gold -= Settings.TalentResetCost;
            Enqueue(new S.LoseGold { Gold = Settings.TalentResetCost });

            //清空已学, 点数全额回到"历史发放总量"
            Info.TalentList.Clear();
            Info.TalentPoints = Info.TalentPointsGranted;

            RefreshStats();
            Enqueue(BuildPlayerTalentInfo());

            Enqueue(new S.TalentReset
            {
                Success = true,
                Points = Info.TalentPoints,
            });

            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentResetOk), Info.TalentPoints), ChatType.Hint);
        }

        /// <summary>
        /// 取角色的已学天赋记录, 不存在则临时创建一个0级记录.
        /// 注意: 临时记录只用于查询校验, 只有学习成功才会加入 Info.TalentList 入库.
        /// </summary>
        private UserTalent GetOrCreateUserTalent(int talentId)
        {
            foreach (var userTalent in Info.TalentList)
                if (userTalent.Id == talentId)
                    return userTalent;

            return new UserTalent(talentId, 0);
        }
    }
}
