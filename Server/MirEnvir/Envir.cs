using ClientPackets;
using Server.Library.MirDatabase;
using Server.Library.Utils;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using S = ServerPackets;

namespace Server.MirEnvir
{
    public class MobThread
    {
        public int Id = 0;
        public long LastRunTime = 0;
        public long StartTime = 0;
        public long EndTime = 0;
        public LinkedList<MapObject> ObjectsList = new LinkedList<MapObject>();
        public LinkedListNode<MapObject> _current = null;
        public bool Stop = false;
    }
    public class RandomProvider
    {
        private static int seed = Environment.TickCount;
        private static readonly ThreadLocal<Random> RandomWrapper = new ThreadLocal<Random>(() => new Random(Interlocked.Increment(ref seed)));

        public static Random GetThreadRandom() =>
            RandomWrapper.Value;

        public int Next() =>
            RandomWrapper.Value.Next();
        public int Next(int maxValue) =>
            RandomWrapper.Value.Next(maxValue);
        public int Next(int minValue, int maxValue) =>
            RandomWrapper.Value.Next(minValue, maxValue);
    }

    public class Envir
    {
        public static Envir Main { get; } = new Envir();

        public static Envir Edit { get; } = new Envir();

        protected static MessageQueue MessageQueue => MessageQueue.Instance;

        public static object AccountLock = new object();
        public static object LoadLock = new object();

        public const int MinVersion = 60;
        //版本119: CharacterInfo 新增天赋存档字段(TalentList/TalentPoints/TalentPointsGranted)
        //注意: 版本118已被罗汉棍法(LuoHanGunFa)字段占用, 旧库按版本判断自动兼容
        //版本120: RespawnInfo 新增"无人不刷怪"字段(NoPlayerNoSpawn), 旧库按版本判断默认False
        //版本121: MapInfo 新增地图级"无人不刷怪"字段(NoPlayerNoSpawn), 整张地图无玩家时不补怪, 旧库按版本判断默认False
        public const int Version = 121;
        //自定义版本1: UserItem 的 BreakthroughCount 字段(装备突破系统已移除, 字段仅为旧档读写兼容保留, 恒为0)
        public const int CustomVersion = 1;
        public static readonly string DatabasePath = Path.Combine(".", "Server.MirDB");
        public static readonly string AccountPath = Path.Combine(".", "Server.MirADB");
        public static readonly string BackUpPath = Path.Combine(".", "Back Up");
        public static readonly string AccountsBackUpPath = Path.Combine(".", "Back Up", "Accounts");
        public static readonly string DatabaseBackUpPath = Path.Combine(".", "Back Up", "Database");
        public static readonly string ArchivePath = Path.Combine(".", "Archive");
        public bool ResetGS = false;
        public bool GuildRefreshNeeded;

        /// <summary>
        /// 物品热同步: 编辑器(Envir.Edit)保存时排队的同步动作, 由运行库(Main)的 WorkLoop
        /// 在服务器线程内执行, 保证与周期存盘(SaveDB)串行无竞争.
        /// </summary>
        private Action _pendingItemSync;
        private Action _pendingMainAction; //通用主线程任务队列(控制面板等UI线程投递)

        /// <summary>
        /// 物品防回滚: 编辑器里已删除、但运行库内存中仍可能被在线玩家引用的物品编号.
        /// SaveDB 落盘时跳过这些编号, 避免运行中的服务器把已删物品"写活";
        /// 重新 LoadDB(重启/重启服务)后自动清空.
        /// </summary>
        public readonly HashSet<int> SuppressedItemIndexes = new HashSet<int>();

        private static readonly Regex AccountIDReg, PasswordReg, EMailReg, CharacterReg;

        public static int LoadVersion;
        public static int LoadCustomVersion;

        private readonly DateTime _startTime = DateTime.UtcNow;
        public readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        public long Time { get; private set; }
        public RespawnTimer RespawnTick = new RespawnTimer();

        private static List<string> DisabledCharNames = new List<string>();
        private static List<string> LineMessages = new List<string>();

        public static ConcurrentDictionary<string, DateTime> IPBlocks = new ConcurrentDictionary<string, DateTime>();

        public static ConcurrentDictionary<string, MirConnectionLog> ConnectionLogs = new ConcurrentDictionary<string, MirConnectionLog>();

        public DateTime Now =>
            _startTime.AddMilliseconds(Time);

        public bool Running { get; private set; }


        private static uint _objectID;
        public uint ObjectID => ++_objectID;

        public static int _playerCount;
        public int PlayerCount => Players.Count;

        public int[] OnlineRankingCount = new int[6];
        public int HeroCount => Heroes.Count;

        public RandomProvider Random = new RandomProvider();

        private Thread _thread;
        private TcpListener _listener;
        private bool StatusPortEnabled = true;
        public List<MirStatusConnection> StatusConnections = new List<MirStatusConnection>();
        private TcpListener _StatusPort;
        private int _sessionID;
        public List<MirConnection> Connections = new List<MirConnection>();

        //Server DB
        public int MapIndex, ItemIndex, MonsterIndex, NPCIndex, QuestIndex, GameshopIndex, ConquestIndex, RespawnIndex, ScriptIndex;
        public List<MapInfo> MapInfoList = new List<MapInfo>();
        public List<ItemInfo> ItemInfoList = new List<ItemInfo>();
        public List<MonsterInfo> MonsterInfoList = new List<MonsterInfo>();
        public List<MagicInfo> MagicInfoList = new List<MagicInfo>();
        public List<NPCInfo> NPCInfoList = new List<NPCInfo>();
        public DragonInfo DragonInfo = new DragonInfo();
        public List<FieldBossInfo> FieldBossInfoList = new List<FieldBossInfo>();
        public List<QuestInfo> QuestInfoList = new List<QuestInfo>();
        public List<GameShopItem> GameShopList = new List<GameShopItem>();
        public List<RecipeInfo> RecipeInfoList = new List<RecipeInfo>();
        //天赋系统 - 全局天赋表(Envir\Talents.txt)
        public List<Server.MirDatabase.TalentInfo> TalentInfoList = new List<Server.MirDatabase.TalentInfo>();
        public List<BuffInfo> BuffInfoList = new List<BuffInfo>();
        public List<ConquestInfo> ConquestInfoList = new List<ConquestInfo>();
        public List<GTMap> GTMapList = new List<GTMap>();

        //User DB
        public int NextAccountID, NextCharacterID, NextGuildID, NextHeroID;
        public ulong NextUserItemID, NextAuctionID, NextMailID, NextRecipeID;
        public List<AccountInfo> AccountList = new List<AccountInfo>();
        public List<CharacterInfo> CharacterList = new List<CharacterInfo>();
        public List<GuildInfo> GuildList = new List<GuildInfo>();
        public LinkedList<AuctionInfo> Auctions = new LinkedList<AuctionInfo>();
        public List<ConquestGuildInfo> ConquestList = new List<ConquestGuildInfo>();
        public Dictionary<int, int> GameshopLog = new Dictionary<int, int>();
        public List<HeroInfo> HeroList = new List<HeroInfo>();

        public int GuildCount; //This shouldn't be needed?? -> remove in the future

        //Live Info
        public bool Saving = false;
        public List<Map> MapList = new List<Map>();
        public List<Map> MapUnloadQueue = new List<Map>();
        public List<SafeZoneInfo> StartPoints = new List<SafeZoneInfo>();
        public List<ItemInfo> StartItems = new List<ItemInfo>();

        public List<PlayerObject> Players = new List<PlayerObject>();
        public List<SpellObject> Spells = new List<SpellObject>();
        public List<NPCObject> NPCs = new List<NPCObject>();
        public List<GuildObject> Guilds = new List<GuildObject>();
        public List<ConquestObject> Conquests = new List<ConquestObject>();
        public List<HeroObject> Heroes = new List<HeroObject>();

        public LightSetting Lights;
        public LinkedList<MapObject> Objects = new LinkedList<MapObject>();
        public Dictionary<int, NPCScript> Scripts = new Dictionary<int, NPCScript>();
        public Dictionary<string, Timer> Timers = new Dictionary<string, Timer>();

        //multithread vars
        readonly object _locker = new object();
        public MobThread[] MobThreads = new MobThread[Math.Max(2, Settings.ThreadLimit)];
        private Thread[] MobThreading = new Thread[Math.Max(2, Settings.ThreadLimit)];
        public int SpawnMultiplier = 1;//set this to 2 if you want double spawns (warning this can easily lag your server far beyond what you imagine)

        public List<string> CustomCommands = new List<string>();

        public Dragon DragonSystem;
        public FieldBossSystem FieldBossSystem;
        public NPCScript DefaultNPC, MonsterNPC, RobotNPC;

        public List<DropInfo> FishingDrops = new List<DropInfo>();
        public List<DropInfo> AwakeningDrops = new List<DropInfo>();

        public List<DropInfo> StrongboxDrops = new List<DropInfo>();
        public List<DropInfo> BlackstoneDrops = new List<DropInfo>();

        public List<GuildAtWar> GuildsAtWar = new List<GuildAtWar>();
        public List<MapRespawn> SavedSpawns = new List<MapRespawn>();

        public List<RankCharacterInfo> RankTop = new List<RankCharacterInfo>();
        public List<RankCharacterInfo>[] RankClass = new List<RankCharacterInfo>[6];

        static HttpServer http;

        static Envir()
        {
            AccountIDReg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");
            PasswordReg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");
            EMailReg = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*");
            CharacterReg = new Regex(@"^[\u4e00-\u9fa5_A-Za-z0-9]{" + Globals.MinCharacterNameLength + "," + Globals.MaxCharacterNameLength + "}$");
        }

        public static bool IsPasswordValid(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;

            return PasswordReg.IsMatch(password);
        }

        public static int LastCount = 0, LastRealCount = 0;
        public static long LastRunTime = 0;
        public int MonsterCount;

        private long warTime, guildTime, conquestTime, rentalItemsTime, auctionTime, spawnTime, robotTime, timerTime;
        private int dailyTime = DateTime.UtcNow.Day;
        private bool MagicExists(Spell spell)
        {
            for (var i = 0; i < MagicInfoList.Count; i++)
            {
                if (MagicInfoList[i].Spell == spell) return true;
            }
            return false;
        }

        private void UpdateMagicInfo()
        {
            for (var i = 0; i < MagicInfoList.Count; i++)
            {
                switch (MagicInfoList[i].Spell)
                {
                    //warrior
                    case Spell.Thrusting:
                        MagicInfoList[i].MultiplierBase = 0.25f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    case Spell.HalfMoon:
                        MagicInfoList[i].MultiplierBase = 0.3f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.ShoulderDash:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    case Spell.TwinDrakeBlade:
                        MagicInfoList[i].MultiplierBase = 0.8f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.FlamingSword:
                        MagicInfoList[i].MultiplierBase = 1.4f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                    case Spell.CrossHalfMoon:
                        MagicInfoList[i].MultiplierBase = 0.4f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.BladeAvalanche:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                    case Spell.SlashingBurst:
                        MagicInfoList[i].MultiplierBase = 3.25f;
                        MagicInfoList[i].MultiplierBonus = 0.25f;
                        break;
                    //wiz
                    case Spell.Repulsion:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    //tao
                    case Spell.Poisoning:
                        MagicInfoList[i].MPowerBase = 0;
                        break;
                    case Spell.Curse:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.Plague:
                        MagicInfoList[i].MPowerBase = 0;
                        MagicInfoList[i].PowerBase = 0;
                        break;
                    //sin
                    case Spell.FatalSword:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.DoubleSlash:
                        MagicInfoList[i].MultiplierBase = 0.8f;
                        MagicInfoList[i].MultiplierBonus = 0.1f;
                        break;
                    case Spell.FireBurst:
                        MagicInfoList[i].MPowerBase = 4;
                        break;
                    case Spell.MoonLight:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.DarkBody:
                        MagicInfoList[i].MPowerBase = 20;
                        break;
                    case Spell.Hemorrhage:
                        MagicInfoList[i].MultiplierBase = 0.2f;
                        MagicInfoList[i].MultiplierBonus = 0.05f;
                        break;
                    case Spell.CrescentSlash:
                        MagicInfoList[i].MultiplierBase = 1f;
                        MagicInfoList[i].MultiplierBonus = 0.4f;
                        break;
                }
            }
        }

        private void FillMagicInfoList()
        {
            // ==== 技能书补全计划: 秘籍系注册(数值取自参考源码/技能书体系, 图标可用GUI再调) ====
            //战士秘籍
            if (!MagicExists(Spell.ImmortalSkinRare))
                MagicInfoList.Add(new MagicInfo { Name = "金刚不坏-秘籍", Spell = Spell.ImmortalSkinRare, Icon = 84, Level1 = 62, Level2 = 64, Level3 = 66, Need1 = 1000, Need2 = 1560, Need3 = 2200, BaseCost = 10, LevelCost = 4, DelayBase = 600000, DelayReduction = 120000, Range = 0 });
            if (!MagicExists(Spell.EntrapmentRare))
                MagicInfoList.Add(new MagicInfo { Name = "捕绳剑-秘籍", Spell = Spell.EntrapmentRare, Icon = 107, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 10000, Need2 = 13000, Need3 = 16000, BaseCost = 15, LevelCost = 3, DelayBase = 15000, DelayReduction = 3000, Range = 9 });
            if (!MagicExists(Spell.LionRoarRare))
                MagicInfoList.Add(new MagicInfo { Name = "狮子吼-秘籍", Spell = Spell.LionRoarRare, Icon = 112, Level1 = 95, Level2 = 97, Level3 = 102, Need1 = 8900, Need2 = 15000, Need3 = 21600, BaseCost = 14, LevelCost = 4, DelayBase = 30000, DelayReduction = 5000, Range = 0 });
            if (!MagicExists(Spell.DimensionalSword))
                MagicInfoList.Add(new MagicInfo { Name = "时空剑", Spell = Spell.DimensionalSword, Icon = 117, Level1 = 90, Level2 = 92, Level3 = 94, Need1 = 3800, Need2 = 6300, Need3 = 9300, BaseCost = 32, LevelCost = 4, MPowerBase = 1, PowerBase = 3, DelayBase = 14000, DelayReduction = 4000, Range = 2, MultiplierBase = 1f, MultiplierBonus = 0.25f });
            if (!MagicExists(Spell.DimensionalSwordRare))
                MagicInfoList.Add(new MagicInfo { Name = "时空剑-秘籍", Spell = Spell.DimensionalSwordRare, Icon = 122, Level1 = 100, Level2 = 105, Level3 = 110, Need1 = 8800, Need2 = 13000, Need3 = 21600, BaseCost = 32, LevelCost = 4, MPowerBase = 1, PowerBase = 3, DelayBase = 14000, DelayReduction = 4000, Range = 3, MultiplierBase = 1f, MultiplierBonus = 0.25f });
            //道士秘籍
            if (!MagicExists(Spell.HealingRare))
                MagicInfoList.Add(new MagicInfo { Name = "治愈术-秘籍", Spell = Spell.HealingRare, Icon = 109, Level1 = 55, Level2 = 60, Level3 = 65, Need1 = 17000, Need2 = 22000, Need3 = 27000, BaseCost = 3, LevelCost = 2, MPowerBase = 14, DelayBase = 3000, DelayReduction = 500, Range = 9 });
            if (!MagicExists(Spell.PetEnhancerRare))
                MagicInfoList.Add(new MagicInfo { Name = "血龙水-秘籍", Spell = Spell.PetEnhancerRare, Icon = 115, Level1 = 95, Level2 = 97, Level3 = 102, Need1 = 23600, Need2 = 38900, Need3 = 57900, BaseCost = 12, LevelCost = 4, DelayBase = 60000, DelayReduction = 10000, Range = 0 });
            //刺客秘籍
            if (!MagicExists(Spell.FlashDashRare))
                MagicInfoList.Add(new MagicInfo { Name = "拔刀术-秘籍", Spell = Spell.FlashDashRare, Icon = 94, Level1 = 70, Level2 = 72, Level3 = 74, Need1 = 12000, Need2 = 18000, Need3 = 26000, BaseCost = 12, LevelCost = 3, DelayBase = 11000, DelayReduction = 2000, Range = 3 });
            if (!MagicExists(Spell.MoonMistRare))
                MagicInfoList.Add(new MagicInfo { Name = "月影雾-秘籍", Spell = Spell.MoonMistRare, Icon = 106, Level1 = 60, Level2 = 62, Level3 = 64, Need1 = 14000, Need2 = 21000, Need3 = 30000, BaseCost = 22, LevelCost = 3, DelayBase = 20000, DelayReduction = 4000, Range = 0 });
            if (!MagicExists(Spell.CrescentSlashRare))
                MagicInfoList.Add(new MagicInfo { Name = "月华乱舞-秘籍", Spell = Spell.CrescentSlashRare, Icon = 113, Level1 = 95, Level2 = 97, Level3 = 102, Need1 = 23600, Need2 = 38900, Need3 = 57600, BaseCost = 19, LevelCost = 3, DelayBase = 13000, DelayReduction = 3000, Range = 0 });
            if (!MagicExists(Spell.ShadowCombo))
                MagicInfoList.Add(new MagicInfo { Name = "闪影连击", Spell = Spell.ShadowCombo, Icon = 105, Level1 = 85, Level2 = 88, Level3 = 91, Need1 = 15000, Need2 = 24000, Need3 = 36000, BaseCost = 14, LevelCost = 3, DelayBase = 13000, DelayReduction = 3000, Range = 3 });
            if (!MagicExists(Spell.ShadowComboRare))
                MagicInfoList.Add(new MagicInfo { Name = "闪影连击-秘籍", Spell = Spell.ShadowComboRare, Icon = 105, Level1 = 95, Level2 = 98, Level3 = 101, Need1 = 24000, Need2 = 39000, Need3 = 57600, BaseCost = 18, LevelCost = 3, DelayBase = 13000, DelayReduction = 3000, Range = 3 });
            //弓手秘籍
            if (!MagicExists(Spell.DelayedExplosionRare))
                MagicInfoList.Add(new MagicInfo { Name = "爆闪-秘籍", Spell = Spell.DelayedExplosionRare, Icon = 125, Level1 = 95, Level2 = 97, Level3 = 100, Need1 = 20000, Need2 = 32000, Need3 = 46000, BaseCost = 18, LevelCost = 3, DelayBase = 12000, DelayReduction = 3000, Range = 9 });
            if (!MagicExists(Spell.ConcentrationRare))
                MagicInfoList.Add(new MagicInfo { Name = "气流术-秘籍", Spell = Spell.ConcentrationRare, Icon = 129, Level1 = 60, Level2 = 62, Level3 = 64, Need1 = 12000, Need2 = 19000, Need3 = 28000, BaseCost = 30, LevelCost = 5, DelayBase = 30000, DelayReduction = 5000, Range = 0 });
            if (!MagicExists(Spell.ThunderStrike))
                MagicInfoList.Add(new MagicInfo { Name = "落雷击", Spell = Spell.ThunderStrike, Icon = 140, Level1 = 85, Level2 = 88, Level3 = 91, Need1 = 18000, Need2 = 29000, Need3 = 42000, BaseCost = 25, LevelCost = 4, DelayBase = 12000, DelayReduction = 3000, Range = 9 });
            if (!MagicExists(Spell.ThunderStrikeRare))
                MagicInfoList.Add(new MagicInfo { Name = "落雷击-秘籍", Spell = Spell.ThunderStrikeRare, Icon = 140, Level1 = 95, Level2 = 98, Level3 = 101, Need1 = 24000, Need2 = 39000, Need3 = 57600, BaseCost = 32, LevelCost = 4, DelayBase = 12000, DelayReduction = 3000, Range = 9 });

            //Warrior
            if (!MagicExists(Spell.Fencing))
                MagicInfoList.Add(new MagicInfo { Name = "Fencing", Spell = Spell.Fencing, Icon = 2, Level1 = 7, Level2 = 9, Level3 = 12, Need1 = 270, Need2 = 600, Need3 = 1300, Range = 0 });
            if (!MagicExists(Spell.Slaying))
                MagicInfoList.Add(new MagicInfo { Name = "Slaying", Spell = Spell.Slaying, Icon = 6, Level1 = 15, Level2 = 17, Level3 = 20, Need1 = 500, Need2 = 1100, Need3 = 1800, Range = 0 });
            if (!MagicExists(Spell.Thrusting))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Thrusting",
                    Spell = Spell.Thrusting,
                    Icon = 11,
                    Level1 = 22,
                    Level2 = 24,
                    Level3 = 27,
                    Need1 = 2000,
                    Need2 = 3500,
                    Need3 = 6000,
                    Range = 0,
                    MultiplierBase = 0.25f,
                    MultiplierBonus = 0.25f
                });
            if (!MagicExists(Spell.HalfMoon))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "HalfMoon",
                    Spell = Spell.HalfMoon,
                    Icon = 24,
                    Level1 = 26,
                    Level2 = 28,
                    Level3 = 31,
                    Need1 = 5000,
                    Need2 = 8000,
                    Need3 = 14000,
                    BaseCost = 3,
                    Range = 0,
                    MultiplierBase = 0.3f,
                    MultiplierBonus = 0.1f
                });
            if (!MagicExists(Spell.ShoulderDash))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ShoulderDash",
                    Spell = Spell.ShoulderDash,
                    Icon = 26,
                    Level1 = 30,
                    Level2 = 32,
                    Level3 = 34,
                    Need1 = 3000,
                    Need2 = 4000,
                    Need3 = 6000,
                    BaseCost = 4,
                    LevelCost = 4,
                    DelayBase = 2500,
                    Range = 0,
                    MPowerBase = 4
                });
            if (!MagicExists(Spell.TwinDrakeBlade))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "TwinDrakeBlade",
                    Spell = Spell.TwinDrakeBlade,
                    Icon = 37,
                    Level1 = 32,
                    Level2 = 34,
                    Level3 = 37,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 10000,
                    BaseCost = 10,
                    Range = 0,
                    MultiplierBase = 0.8f,
                    MultiplierBonus = 0.1f
                });
            if (!MagicExists(Spell.Entrapment))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Entrapment",
                    Spell = Spell.Entrapment,
                    Icon = 46,
                    Level1 = 32,
                    Level2 = 35,
                    Level3 = 37,
                    Need1 = 2000,
                    Need2 = 3500,
                    Need3 = 5500,
                    BaseCost = 15,
                    LevelCost = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.FlamingSword))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FlamingSword",
                    Spell = Spell.FlamingSword,
                    Icon = 25,
                    Level1 = 35,
                    Level2 = 37,
                    Level3 = 40,
                    Need1 = 2000,
                    Need2 = 4000,
                    Need3 = 6000,
                    BaseCost = 7,
                    Range = 0,
                    MultiplierBase = 1.4f,
                    MultiplierBonus = 0.4f
                });
            if (!MagicExists(Spell.BloodDragon))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "血龙震",
                    Spell = Spell.BloodDragon,
                    Icon = 37,
                    Level1 = 35,
                    Level2 = 37,
                    Level3 = 40,
                    Need1 = 2000,
                    Need2 = 4000,
                    Need3 = 6000,
                    BaseCost = 7,
                    Range = 0,
                    MultiplierBase = 1.4f,
                    MultiplierBonus = 0.4f
                });
            if (!MagicExists(Spell.LionRoar))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "LionRoar",
                    Spell = Spell.LionRoar,
                    Icon = 42,
                    Level1 = 36,
                    Level2 = 39,
                    Level3 = 41,
                    Need1 = 5000,
                    Need2 = 8000,
                    Need3 = 12000,
                    BaseCost = 14,
                    LevelCost = 4,
                    Range = 0
                });
            if (!MagicExists(Spell.CrossHalfMoon))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "CrossHalfMoon",
                    Spell = Spell.CrossHalfMoon,
                    Icon = 33,
                    Level1 = 38,
                    Level2 = 40,
                    Level3 = 42,
                    Need1 = 7000,
                    Need2 = 11000,
                    Need3 = 16000,
                    BaseCost = 6,
                    Range = 0,
                    MultiplierBase = 0.4f,
                    MultiplierBonus = 0.1f
                });
            if (!MagicExists(Spell.BladeAvalanche))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "BladeAvalanche",
                    Spell = Spell.BladeAvalanche,
                    Icon = 43,
                    Level1 = 38,
                    Level2 = 41,
                    Level3 = 43,
                    Need1 = 5000,
                    Need2 = 8000,
                    Need3 = 12000,
                    BaseCost = 14,
                    LevelCost = 4,
                    Range = 0,
                    MultiplierBonus = 0.3f
                });
            if (!MagicExists(Spell.ProtectionField))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ProtectionField",
                    Spell = Spell.ProtectionField,
                    Icon = 50,
                    Level1 = 39,
                    Level2 = 42,
                    Level3 = 45,
                    Need1 = 6000,
                    Need2 = 12000,
                    Need3 = 18000,
                    BaseCost = 23,
                    LevelCost = 6,
                    Range = 0
                });
            if (!MagicExists(Spell.Rage))
                MagicInfoList.Add(new MagicInfo
                { Name = "Rage", Spell = Spell.Rage, Icon = 49, Level1 = 44, Level2 = 47, Level3 = 50, Need1 = 8000, Need2 = 14000, Need3 = 20000, BaseCost = 20, LevelCost = 5, Range = 0 });
            if (!MagicExists(Spell.CounterAttack))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "CounterAttack",
                    Spell = Spell.CounterAttack,
                    Icon = 72,
                    Level1 = 47,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 7000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 12,
                    LevelCost = 4,
                    DelayBase = 24000,
                    Range = 0,
                    MultiplierBonus = 0.4f
                });
            if (!MagicExists(Spell.SlashingBurst))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SlashingBurst",
                    Spell = Spell.SlashingBurst,
                    Icon = 55,
                    Level1 = 50,
                    Level2 = 53,
                    Level3 = 56,
                    Need1 = 10000,
                    Need2 = 16000,
                    Need3 = 24000,
                    BaseCost = 25,
                    LevelCost = 4,
                    MPowerBase = 1,
                    PowerBase = 3,
                    DelayBase = 14000,
                    DelayReduction = 4000,
                    Range = 0,
                    MultiplierBase = 3.25f,
                    MultiplierBonus = 0.25f
                });
            if (!MagicExists(Spell.Fury))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Fury",
                    Spell = Spell.Fury,
                    Icon = 76,
                    Level1 = 45,
                    Level2 = 48,
                    Level3 = 51,
                    Need1 = 8000,
                    Need2 = 14000,
                    Need3 = 20000,
                    BaseCost = 10,
                    LevelCost = 4,
                    DelayBase = 600000,
                    DelayReduction = 120000,
                    Range = 0
                });
            if (!MagicExists(Spell.ImmortalSkin))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ImmortalSkin",
                    Spell = Spell.ImmortalSkin,
                    Icon = 80,
                    Level1 = 60,
                    Level2 = 61,
                    Level3 = 62,
                    Need1 = 1560,
                    Need2 = 2200,
                    Need3 = 3000,
                    BaseCost = 10,
                    LevelCost = 4,
                    DelayBase = 600000,
                    DelayReduction = 120000,
                    Range = 0
                });

            //Wizard
            if (!MagicExists(Spell.FireBall))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FireBall",
                    Spell = Spell.FireBall,
                    Icon = 0,
                    Level1 = 7,
                    Level2 = 9,
                    Level3 = 11,
                    Need1 = 200,
                    Need2 = 350,
                    Need3 = 700,
                    BaseCost = 3,
                    LevelCost = 2,
                    MPowerBase = 8,
                    PowerBase = 2,
                    Range = 9
                });
            if (!MagicExists(Spell.Repulsion))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Repulsion",
                    Spell = Spell.Repulsion,
                    Icon = 7,
                    Level1 = 12,
                    Level2 = 15,
                    Level3 = 19,
                    Need1 = 500,
                    Need2 = 1300,
                    Need3 = 2200,
                    BaseCost = 2,
                    LevelCost = 2,
                    Range = 0,
                    MPowerBase = 4
                });
            if (!MagicExists(Spell.ElectricShock))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ElectricShock",
                    Spell = Spell.ElectricShock,
                    Icon = 19,
                    Level1 = 13,
                    Level2 = 18,
                    Level3 = 24,
                    Need1 = 530,
                    Need2 = 1100,
                    Need3 = 2200,
                    BaseCost = 3,
                    LevelCost = 1,
                    Range = 9
                });
            if (!MagicExists(Spell.GreatFireBall))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "GreatFireBall",
                    Spell = Spell.GreatFireBall,
                    Icon = 4,
                    Level1 = 15,
                    Level2 = 18,
                    Level3 = 21,
                    Need1 = 2000,
                    Need2 = 2700,
                    Need3 = 3500,
                    BaseCost = 5,
                    LevelCost = 1,
                    MPowerBase = 6,
                    PowerBase = 10,
                    Range = 9
                });
            if (!MagicExists(Spell.HellFire))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "HellFire",
                    Spell = Spell.HellFire,
                    Icon = 8,
                    Level1 = 16,
                    Level2 = 20,
                    Level3 = 24,
                    Need1 = 700,
                    Need2 = 2700,
                    Need3 = 3500,
                    BaseCost = 10,
                    LevelCost = 3,
                    MPowerBase = 14,
                    PowerBase = 6,
                    Range = 0
                });
            if (!MagicExists(Spell.ThunderBolt))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ThunderBolt",
                    Spell = Spell.ThunderBolt,
                    Icon = 10,
                    Level1 = 17,
                    Level2 = 20,
                    Level3 = 23,
                    Need1 = 500,
                    Need2 = 2000,
                    Need3 = 3500,
                    BaseCost = 9,
                    LevelCost = 2,
                    MPowerBase = 8,
                    MPowerBonus = 20,
                    PowerBase = 9,
                    Range = 9
                });
            if (!MagicExists(Spell.Teleport))
                MagicInfoList.Add(new MagicInfo
                { Name = "Teleport", Spell = Spell.Teleport, Icon = 20, Level1 = 19, Level2 = 22, Level3 = 25, Need1 = 350, Need2 = 1000, Need3 = 2000, BaseCost = 10, LevelCost = 3, Range = 0 });
            if (!MagicExists(Spell.FireBang))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FireBang",
                    Spell = Spell.FireBang,
                    Icon = 22,
                    Level1 = 22,
                    Level2 = 25,
                    Level3 = 28,
                    Need1 = 3000,
                    Need2 = 5000,
                    Need3 = 10000,
                    BaseCost = 14,
                    LevelCost = 4,
                    MPowerBase = 8,
                    PowerBase = 8,
                    Range = 9
                });
            if (!MagicExists(Spell.FireWall))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FireWall",
                    Spell = Spell.FireWall,
                    Icon = 21,
                    Level1 = 24,
                    Level2 = 28,
                    Level3 = 33,
                    Need1 = 4000,
                    Need2 = 10000,
                    Need3 = 20000,
                    BaseCost = 30,
                    LevelCost = 5,
                    MPowerBase = 3,
                    PowerBase = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.Lightning))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Lightning",
                    Spell = Spell.Lightning,
                    Icon = 9,
                    Level1 = 26,
                    Level2 = 29,
                    Level3 = 32,
                    Need1 = 3000,
                    Need2 = 6000,
                    Need3 = 12000,
                    BaseCost = 38,
                    LevelCost = 7,
                    MPowerBase = 12,
                    PowerBase = 12,
                    Range = 0
                });
            if (!MagicExists(Spell.FrostCrunch))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FrostCrunch",
                    Spell = Spell.FrostCrunch,
                    Icon = 38,
                    Level1 = 28,
                    Level2 = 30,
                    Level3 = 33,
                    Need1 = 3000,
                    Need2 = 5000,
                    Need3 = 8000,
                    BaseCost = 15,
                    LevelCost = 3,
                    MPowerBase = 12,
                    PowerBase = 12,
                    Range = 9
                });
            if (!MagicExists(Spell.ThunderStorm))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ThunderStorm",
                    Spell = Spell.ThunderStorm,
                    Icon = 23,
                    Level1 = 30,
                    Level2 = 32,
                    Level3 = 34,
                    Need1 = 4000,
                    Need2 = 8000,
                    Need3 = 12000,
                    BaseCost = 29,
                    LevelCost = 9,
                    MPowerBase = 10,
                    MPowerBonus = 20,
                    PowerBase = 10,
                    PowerBonus = 20,
                    Range = 0
                });
            if (!MagicExists(Spell.MagicShield))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MagicShield",
                    Spell = Spell.MagicShield,
                    Icon = 30,
                    Level1 = 31,
                    Level2 = 34,
                    Level3 = 38,
                    Need1 = 3000,
                    Need2 = 7000,
                    Need3 = 10000,
                    BaseCost = 35,
                    LevelCost = 5,
                    Range = 0
                });
            if (!MagicExists(Spell.TurnUndead))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "TurnUndead",
                    Spell = Spell.TurnUndead,
                    Icon = 31,
                    Level1 = 32,
                    Level2 = 35,
                    Level3 = 39,
                    Need1 = 3000,
                    Need2 = 7000,
                    Need3 = 10000,
                    BaseCost = 52,
                    LevelCost = 13,
                    Range = 9
                });
            if (!MagicExists(Spell.Vampirism))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Vampirism",
                    Spell = Spell.Vampirism,
                    Icon = 47,
                    Level1 = 33,
                    Level2 = 36,
                    Level3 = 40,
                    Need1 = 3000,
                    Need2 = 5000,
                    Need3 = 8000,
                    BaseCost = 26,
                    LevelCost = 13,
                    MPowerBase = 12,
                    PowerBase = 12,
                    Range = 9
                });
            if (!MagicExists(Spell.IceStorm))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "IceStorm",
                    Spell = Spell.IceStorm,
                    Icon = 32,
                    Level1 = 35,
                    Level2 = 37,
                    Level3 = 40,
                    Need1 = 4000,
                    Need2 = 8000,
                    Need3 = 12000,
                    BaseCost = 33,
                    LevelCost = 3,
                    MPowerBase = 12,
                    PowerBase = 14,
                    Range = 9
                });
            if (!MagicExists(Spell.FlameDisruptor))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FlameDisruptor",
                    Spell = Spell.FlameDisruptor,
                    Icon = 34,
                    Level1 = 38,
                    Level2 = 40,
                    Level3 = 42,
                    Need1 = 5000,
                    Need2 = 9000,
                    Need3 = 14000,
                    BaseCost = 28,
                    LevelCost = 3,
                    MPowerBase = 15,
                    MPowerBonus = 20,
                    PowerBase = 9,
                    Range = 9
                });
            if (!MagicExists(Spell.Mirroring))
                MagicInfoList.Add(new MagicInfo
                { Name = "Mirroring", Spell = Spell.Mirroring, Icon = 41, Level1 = 41, Level2 = 43, Level3 = 45, Need1 = 6000, Need2 = 11000, Need3 = 16000, BaseCost = 21, Range = 0 });
            if (!MagicExists(Spell.FlameField))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FlameField",
                    Spell = Spell.FlameField,
                    Icon = 44,
                    Level1 = 42,
                    Level2 = 43,
                    Level3 = 45,
                    Need1 = 6000,
                    Need2 = 11000,
                    Need3 = 16000,
                    BaseCost = 45,
                    LevelCost = 8,
                    MPowerBase = 100,
                    PowerBase = 25,
                    Range = 9
                });
            if (!MagicExists(Spell.Blizzard))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Blizzard",
                    Spell = Spell.Blizzard,
                    Icon = 51,
                    Level1 = 44,
                    Level2 = 47,
                    Level3 = 50,
                    Need1 = 8000,
                    Need2 = 16000,
                    Need3 = 24000,
                    BaseCost = 65,
                    LevelCost = 10,
                    MPowerBase = 30,
                    MPowerBonus = 10,
                    PowerBase = 20,
                    PowerBonus = 5,
                    Range = 9
                });
            if (!MagicExists(Spell.MagicBooster))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MagicBooster",
                    Spell = Spell.MagicBooster,
                    Icon = 73,
                    Level1 = 47,
                    Level2 = 49,
                    Level3 = 52,
                    Need1 = 12000,
                    Need2 = 18000,
                    Need3 = 24000,
                    BaseCost = 150,
                    LevelCost = 15,
                    Range = 0
                });
            if (!MagicExists(Spell.MeteorStrike))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MeteorStrike",
                    Spell = Spell.MeteorStrike,
                    Icon = 52,
                    Level1 = 49,
                    Level2 = 52,
                    Level3 = 55,
                    Need1 = 15000,
                    Need2 = 20000,
                    Need3 = 25000,
                    BaseCost = 115,
                    LevelCost = 17,
                    MPowerBase = 40,
                    MPowerBonus = 10,
                    PowerBase = 20,
                    PowerBonus = 15,
                    Range = 9
                });
            if (!MagicExists(Spell.IceThrust))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "IceThrust",
                    Spell = Spell.IceThrust,
                    Icon = 56,
                    Level1 = 53,
                    Level2 = 56,
                    Level3 = 59,
                    Need1 = 17000,
                    Need2 = 22000,
                    Need3 = 27000,
                    BaseCost = 100,
                    LevelCost = 20,
                    MPowerBase = 100,
                    PowerBase = 50,
                    Range = 0
                });
            if (!MagicExists(Spell.Blink))
                MagicInfoList.Add(new MagicInfo
                { Name = "Blink", Spell = Spell.Blink, Icon = 20, Level1 = 19, Level2 = 22, Level3 = 25, Need1 = 350, Need2 = 1000, Need3 = 2000, BaseCost = 10, LevelCost = 3, Range = 9 });
            //if (!MagicExists(Spell.FastMove)) MagicInfoList.Add(new MagicInfo { Name = "FastMove", Spell = Spell.ImmortalSkin, Icon = ?, Level1 = ?, Level2 = ?, Level3 = ?, Need1 = ?, Need2 = ?, Need3 = ?, BaseCost = ?, LevelCost = ?, DelayBase = ?, DelayReduction = ? });
            if (!MagicExists(Spell.StormEscape))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "StormEscape",
                    Spell = Spell.StormEscape,
                    Icon = 23,
                    Level1 = 60,
                    Level2 = 61,
                    Level3 = 62,
                    Need1 = 2200,
                    Need2 = 3300,
                    Need3 = 4400,
                    BaseCost = 65,
                    LevelCost = 8,
                    MPowerBase = 12,
                    PowerBase = 4,
                    Range = 9
                });


            //Taoist
            if (!MagicExists(Spell.Healing))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Healing",
                    Spell = Spell.Healing,
                    Icon = 1,
                    Level1 = 7,
                    Level2 = 11,
                    Level3 = 14,
                    Need1 = 150,
                    Need2 = 350,
                    Need3 = 700,
                    BaseCost = 3,
                    LevelCost = 2,
                    MPowerBase = 14,
                    Range = 9
                });
            if (!MagicExists(Spell.SpiritSword))
                MagicInfoList.Add(new MagicInfo
                { Name = "SpiritSword", Spell = Spell.SpiritSword, Icon = 3, Level1 = 9, Level2 = 12, Level3 = 15, Need1 = 350, Need2 = 1300, Need3 = 2700, Range = 0 });
            if (!MagicExists(Spell.Poisoning))
                MagicInfoList.Add(new MagicInfo
                { Name = "Poisoning", Spell = Spell.Poisoning, Icon = 5, Level1 = 14, Level2 = 17, Level3 = 20, Need1 = 700, Need2 = 1300, Need3 = 2700, BaseCost = 2, LevelCost = 1, Range = 9 });
            if (!MagicExists(Spell.SoulFireBall))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SoulFireBall",
                    Spell = Spell.SoulFireBall,
                    Icon = 12,
                    Level1 = 18,
                    Level2 = 21,
                    Level3 = 24,
                    Need1 = 1300,
                    Need2 = 2700,
                    Need3 = 4000,
                    BaseCost = 3,
                    LevelCost = 1,
                    MPowerBase = 8,
                    PowerBase = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.SummonSkeleton))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SummonSkeleton",
                    Spell = Spell.SummonSkeleton,
                    Icon = 16,
                    Level1 = 19,
                    Level2 = 22,
                    Level3 = 26,
                    Need1 = 1000,
                    Need2 = 2000,
                    Need3 = 3500,
                    BaseCost = 12,
                    LevelCost = 4,
                    Range = 0
                });
            if (!MagicExists(Spell.Hiding))
                MagicInfoList.Add(new MagicInfo
                { Name = "Hiding", Spell = Spell.Hiding, Icon = 17, Level1 = 20, Level2 = 23, Level3 = 26, Need1 = 1300, Need2 = 2700, Need3 = 5300, BaseCost = 1, LevelCost = 1, Range = 0 });
            if (!MagicExists(Spell.MassHiding))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MassHiding",
                    Spell = Spell.MassHiding,
                    Icon = 18,
                    Level1 = 21,
                    Level2 = 25,
                    Level3 = 29,
                    Need1 = 1300,
                    Need2 = 2700,
                    Need3 = 5300,
                    BaseCost = 2,
                    LevelCost = 2,
                    Range = 9
                });
            if (!MagicExists(Spell.SoulShield))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SoulShield",
                    Spell = Spell.SoulShield,
                    Icon = 13,
                    Level1 = 22,
                    Level2 = 24,
                    Level3 = 26,
                    Need1 = 2000,
                    Need2 = 3500,
                    Need3 = 7000,
                    BaseCost = 2,
                    LevelCost = 2,
                    Range = 9
                });
            if (!MagicExists(Spell.Revelation))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Revelation",
                    Spell = Spell.Revelation,
                    Icon = 27,
                    Level1 = 23,
                    Level2 = 25,
                    Level3 = 28,
                    Need1 = 1500,
                    Need2 = 2500,
                    Need3 = 4000,
                    BaseCost = 4,
                    LevelCost = 4,
                    Range = 9
                });
            if (!MagicExists(Spell.BlessedArmour))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "BlessedArmour",
                    Spell = Spell.BlessedArmour,
                    Icon = 14,
                    Level1 = 25,
                    Level2 = 27,
                    Level3 = 29,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 10000,
                    BaseCost = 2,
                    LevelCost = 2,
                    Range = 9
                });
            if (!MagicExists(Spell.EnergyRepulsor))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "EnergyRepulsor",
                    Spell = Spell.EnergyRepulsor,
                    Icon = 36,
                    Level1 = 27,
                    Level2 = 29,
                    Level3 = 31,
                    Need1 = 1800,
                    Need2 = 2400,
                    Need3 = 3200,
                    BaseCost = 2,
                    LevelCost = 2,
                    Range = 0,
                    MPowerBase = 4
                });
            if (!MagicExists(Spell.TrapHexagon))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "TrapHexagon",
                    Spell = Spell.TrapHexagon,
                    Icon = 15,
                    Level1 = 28,
                    Level2 = 30,
                    Level3 = 32,
                    Need1 = 2500,
                    Need2 = 5000,
                    Need3 = 10000,
                    BaseCost = 7,
                    LevelCost = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.Purification))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Purification",
                    Spell = Spell.Purification,
                    Icon = 39,
                    Level1 = 30,
                    Level2 = 32,
                    Level3 = 35,
                    Need1 = 3000,
                    Need2 = 5000,
                    Need3 = 8000,
                    BaseCost = 14,
                    LevelCost = 2,
                    Range = 9
                });
            if (!MagicExists(Spell.MassHealing))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MassHealing",
                    Spell = Spell.MassHealing,
                    Icon = 28,
                    Level1 = 31,
                    Level2 = 33,
                    Level3 = 36,
                    Need1 = 2000,
                    Need2 = 4000,
                    Need3 = 8000,
                    BaseCost = 28,
                    LevelCost = 3,
                    MPowerBase = 10,
                    PowerBase = 4,
                    Range = 9
                });
            if (!MagicExists(Spell.Hallucination))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Hallucination",
                    Spell = Spell.Hallucination,
                    Icon = 48,
                    Level1 = 31,
                    Level2 = 34,
                    Level3 = 36,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 9000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 9
                });
            if (!MagicExists(Spell.UltimateEnhancer))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "UltimateEnchancer",
                    Spell = Spell.UltimateEnhancer,
                    Icon = 35,
                    Level1 = 33,
                    Level2 = 35,
                    Level3 = 38,
                    Need1 = 5000,
                    Need2 = 7000,
                    Need3 = 10000,
                    BaseCost = 28,
                    LevelCost = 4,
                    Range = 9
                });
            if (!MagicExists(Spell.SummonShinsu))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SummonShinsu",
                    Spell = Spell.SummonShinsu,
                    Icon = 29,
                    Level1 = 35,
                    Level2 = 37,
                    Level3 = 40,
                    Need1 = 2000,
                    Need2 = 4000,
                    Need3 = 6000,
                    BaseCost = 28,
                    LevelCost = 4,
                    Range = 0
                });
            if (!MagicExists(Spell.Reincarnation))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Reincarnation",
                    Spell = Spell.Reincarnation,
                    Icon = 53,
                    Level1 = 37,
                    Level2 = 39,
                    Level3 = 41,
                    Need1 = 2000,
                    Need2 = 6000,
                    Need3 = 10000,
                    BaseCost = 125,
                    LevelCost = 17,
                    Range = 9
                });
            if (!MagicExists(Spell.SummonHolyDeva))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SummonHolyDeva",
                    Spell = Spell.SummonHolyDeva,
                    Icon = 40,
                    Level1 = 38,
                    Level2 = 41,
                    Level3 = 43,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 9000,
                    BaseCost = 28,
                    LevelCost = 4,
                    Range = 0
                });
            if (!MagicExists(Spell.Curse))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Curse",
                    Spell = Spell.Curse,
                    Icon = 45,
                    Level1 = 40,
                    Level2 = 42,
                    Level3 = 44,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 9000,
                    BaseCost = 17,
                    LevelCost = 3,
                    Range = 9,
                    MPowerBase = 20
                });
            if (!MagicExists(Spell.Plague))
                MagicInfoList.Add(new MagicInfo
                { Name = "Plague", Spell = Spell.Plague, Icon = 74, Level1 = 42, Level2 = 44, Level3 = 47, Need1 = 5000, Need2 = 9000, Need3 = 13000, BaseCost = 20, LevelCost = 5, Range = 9 });
            if (!MagicExists(Spell.PoisonCloud))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "PoisonCloud",
                    Spell = Spell.PoisonCloud,
                    Icon = 54,
                    Level1 = 43,
                    Level2 = 45,
                    Level3 = 48,
                    Need1 = 4000,
                    Need2 = 8000,
                    Need3 = 12000,
                    BaseCost = 30,
                    LevelCost = 5,
                    MPowerBase = 40,
                    PowerBase = 20,
                    DelayBase = 18000,
                    DelayReduction = 2000,
                    Range = 9
                });

            //万效符(87): 护符投掷 7x7友方4Buff 数值移植自angelk727
            //注意用"找到即覆写"— 清理DB里实验残留的同Spell旧记录
            MagicInfo wxInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.WanXiaoFu);
            if (wxInfo == null) MagicInfoList.Add(wxInfo = new MagicInfo { Spell = Spell.WanXiaoFu });
            wxInfo.Name = "万效符";
            wxInfo.Icon = 120;
            wxInfo.Level1 = 90; wxInfo.Level2 = 92; wxInfo.Level3 = 94;
            wxInfo.Need1 = 18500; wxInfo.Need2 = 29900; wxInfo.Need3 = 43200;
            wxInfo.BaseCost = 2; wxInfo.LevelCost = 2;
            wxInfo.DelayBase = 3000; wxInfo.DelayReduction = 250;
            wxInfo.Range = 9;

            //万效符秘笈(88)
            MagicInfo wxrInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.WanXiaoFuRare);
            if (wxrInfo == null) MagicInfoList.Add(wxrInfo = new MagicInfo { Spell = Spell.WanXiaoFuRare });
            wxrInfo.Name = "万效符秘笈";
            wxrInfo.Icon = 125;
            wxrInfo.Level1 = 100; wxrInfo.Level2 = 105; wxrInfo.Level3 = 110;
            wxrInfo.Need1 = 23600; wxrInfo.Need2 = 38900; wxrInfo.Need3 = 57600;
            wxrInfo.BaseCost = 2; wxrInfo.LevelCost = 2;
            wxrInfo.DelayBase = 3000; wxrInfo.DelayReduction = 250;
            wxrInfo.Range = 9;

            //---- 法师奥义x6 (数值移植自angelk727, 找到即覆写) ----
            MagicInfo hsInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.HeavenlySecrets);
            if (hsInfo == null) MagicInfoList.Add(hsInfo = new MagicInfo { Spell = Spell.HeavenlySecrets });
            hsInfo.Name = "天上秘术"; hsInfo.Icon = 77;
            hsInfo.Level1 = 50; hsInfo.Level2 = 63; hsInfo.Level3 = 56;
            hsInfo.Need1 = 1000; hsInfo.Need2 = 2000; hsInfo.Need3 = 3500;
            hsInfo.BaseCost = 28; hsInfo.LevelCost = 2;
            hsInfo.DelayBase = 600000; hsInfo.DelayReduction = 100000;
            hsInfo.Range = 0;

            MagicInfo gfbrInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.GreatFireBallRare);
            if (gfbrInfo == null) MagicInfoList.Add(gfbrInfo = new MagicInfo { Spell = Spell.GreatFireBallRare });
            gfbrInfo.Name = "大火球秘籍"; gfbrInfo.Icon = 108;
            gfbrInfo.Level1 = 55; gfbrInfo.Level2 = 60; gfbrInfo.Level3 = 65;
            gfbrInfo.Need1 = 17000; gfbrInfo.Need2 = 22000; gfbrInfo.Need3 = 27000;
            gfbrInfo.BaseCost = 5; gfbrInfo.LevelCost = 1;
            gfbrInfo.MPowerBase = 15; gfbrInfo.PowerBase = 18;
            gfbrInfo.DelayBase = 5000; gfbrInfo.DelayReduction = 1000;
            gfbrInfo.Range = 9;

            MagicInfo tbrInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.ThunderBoltRare);
            if (tbrInfo == null) MagicInfoList.Add(tbrInfo = new MagicInfo { Spell = Spell.ThunderBoltRare });
            tbrInfo.Name = "强击秘籍"; tbrInfo.Icon = 114;
            tbrInfo.Level1 = 95; tbrInfo.Level2 = 97; tbrInfo.Level3 = 102;
            tbrInfo.Need1 = 7410; tbrInfo.Need2 = 12540; tbrInfo.Need3 = 19200;
            tbrInfo.BaseCost = 9; tbrInfo.LevelCost = 2;
            tbrInfo.MPowerBase = 8; tbrInfo.MPowerBonus = 20; tbrInfo.PowerBase = 9;
            tbrInfo.DelayBase = 8000; tbrInfo.DelayReduction = 2000;
            tbrInfo.Range = 9;

            MagicInfo serInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.StormEscapeRare);
            if (serInfo == null) MagicInfoList.Add(serInfo = new MagicInfo { Spell = Spell.StormEscapeRare });
            serInfo.Name = "雷仙风秘籍"; serInfo.Icon = 85;
            serInfo.Level1 = 62; serInfo.Level2 = 64; serInfo.Level3 = 66;
            serInfo.Need1 = 2200; serInfo.Need2 = 3300; serInfo.Need3 = 4400;
            serInfo.BaseCost = 65; serInfo.LevelCost = 8;
            serInfo.MPowerBase = 30; serInfo.PowerBase = 10;
            serInfo.DelayBase = 300000; serInfo.DelayReduction = 40000;
            serInfo.Range = 9;

            MagicInfo sfsInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.SoulflameSiphon);
            if (sfsInfo == null) MagicInfoList.Add(sfsInfo = new MagicInfo { Spell = Spell.SoulflameSiphon });
            sfsInfo.Name = "吸魔炎风"; sfsInfo.Icon = 119;
            sfsInfo.Level1 = 90; sfsInfo.Level2 = 92; sfsInfo.Level3 = 94;
            sfsInfo.Need1 = 6300; sfsInfo.Need2 = 9300; sfsInfo.Need3 = 15200;
            sfsInfo.BaseCost = 30; sfsInfo.LevelCost = 5;
            sfsInfo.MPowerBase = 3; sfsInfo.PowerBase = 3;
            sfsInfo.DelayBase = 12000; sfsInfo.DelayReduction = 3000;
            sfsInfo.Range = 9;

            MagicInfo sfsrInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.SoulflameSiphonRare);
            if (sfsrInfo == null) MagicInfoList.Add(sfsrInfo = new MagicInfo { Spell = Spell.SoulflameSiphonRare });
            sfsrInfo.Name = "吸魔炎风秘籍"; sfsrInfo.Icon = 124;
            sfsrInfo.Level1 = 100; sfsrInfo.Level2 = 105; sfsrInfo.Level3 = 110;
            sfsrInfo.Need1 = 8800; sfsrInfo.Need2 = 13000; sfsrInfo.Need3 = 21600;
            sfsrInfo.BaseCost = 30; sfsrInfo.LevelCost = 5;
            sfsrInfo.MPowerBase = 3; sfsrInfo.PowerBase = 3;
            sfsrInfo.DelayBase = 12000; sfsrInfo.DelayReduction = 3000;
            sfsrInfo.Range = 9;

            //---- 道士宠物召唤: 风灵(615)/幻灵(616) [AI-Claude 2026-08-30] ----
            //宠物MonsterInfo: find-or-create + 每次启动覆写可调参数(DB会持久化旧记录 直接改创建代码不生效)
            MonsterInfo windInfo = MonsterInfoList.FirstOrDefault(t => t.Name == "风灵" && t.AI == 230);
            if (windInfo == null)
            {
                MonsterInfoList.Add(windInfo = new MonsterInfo { Index = ++MonsterIndex, Name = "风灵", Image = (Monster)615, AI = 230 });
                MessageQueue.Instance.Enqueue("[宠物注册] 风灵(615) MonsterInfo已创建 AI=230");
            }
            windInfo.Level = 30;
            windInfo.ViewRange = 7;
            windInfo.CanTame = false;
            windInfo.CanRecall = true;
            windInfo.AttackSpeed = 1600;
            windInfo.MoveSpeed = 500;
            windInfo.Stats = new Stats { [Stat.HP] = 600, [Stat.MinDC] = 30, [Stat.MaxDC] = 45, [Stat.MinAC] = 10, [Stat.MaxAC] = 20, [Stat.MinMAC] = 10, [Stat.MaxMAC] = 20 };

            MonsterInfo phantomInfo = MonsterInfoList.FirstOrDefault(t => t.Name == "幻灵" && t.AI == 231);
            if (phantomInfo == null)
            {
                MonsterInfoList.Add(phantomInfo = new MonsterInfo { Index = ++MonsterIndex, Name = "幻灵", Image = (Monster)616, AI = 231 });
                MessageQueue.Instance.Enqueue("[宠物注册] 幻灵(616) MonsterInfo已创建 AI=231");
            }
            phantomInfo.Level = 60;
            phantomInfo.ViewRange = 7;
            phantomInfo.CanTame = false;
            phantomInfo.CanRecall = true;
            phantomInfo.AttackSpeed = 2000;
            phantomInfo.MoveSpeed = 600;
            phantomInfo.Stats = new Stats { [Stat.HP] = 1200, [Stat.MinDC] = 60, [Stat.MaxDC] = 90, [Stat.MinAC] = 20, [Stat.MaxAC] = 40, [Stat.MinMAC] = 20, [Stat.MaxMAC] = 40 };

            //上古神谕(415): 远程弹道 复用风灵AI(232)
            MonsterInfo oracleInfo = MonsterInfoList.FirstOrDefault(t => t.Name == "上古神谕" && t.AI == 232);
            if (oracleInfo == null)
            {
                MonsterInfoList.Add(oracleInfo = new MonsterInfo { Index = ++MonsterIndex, Name = "上古神谕", Image = (Monster)415, AI = 232 });
                MessageQueue.Instance.Enqueue("[宠物注册] 上古神谕(415) MonsterInfo已创建 AI=232");
            }
            oracleInfo.Level = 55;
            oracleInfo.ViewRange = 7;
            oracleInfo.CanTame = false;
            oracleInfo.CanRecall = true;
            oracleInfo.AttackSpeed = 1600;
            oracleInfo.MoveSpeed = 500;
            oracleInfo.Stats = new Stats { [Stat.HP] = 1200, [Stat.MinDC] = 40, [Stat.MaxDC] = 60, [Stat.MinAC] = 15, [Stat.MaxAC] = 30, [Stat.MinMAC] = 15, [Stat.MaxMAC] = 30 };

            //技能注册: 召唤风灵(108) / 召唤幻灵(109)
            MagicInfo ylInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.Yling);
            if (ylInfo == null) MagicInfoList.Add(ylInfo = new MagicInfo { Spell = Spell.Yling });
            ylInfo.Name = "召唤风灵"; ylInfo.Icon = 116;
            ylInfo.Level1 = 45; ylInfo.Level2 = 47; ylInfo.Level3 = 50;
            ylInfo.Need1 = 8000; ylInfo.Need2 = 13000; ylInfo.Need3 = 20000;
            ylInfo.BaseCost = 25; ylInfo.LevelCost = 5;
            ylInfo.DelayBase = 3000; ylInfo.DelayReduction = 250;
            ylInfo.Range = 0;

            MagicInfo hlInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.Hling);
            if (hlInfo == null) MagicInfoList.Add(hlInfo = new MagicInfo { Spell = Spell.Hling });
            hlInfo.Name = "召唤幻灵"; hlInfo.Icon = 117;
            hlInfo.Level1 = 75; hlInfo.Level2 = 77; hlInfo.Level3 = 80;
            hlInfo.Need1 = 20000; hlInfo.Need2 = 30000; hlInfo.Need3 = 40000;
            hlInfo.BaseCost = 40; hlInfo.LevelCost = 5;
            hlInfo.DelayBase = 3000; hlInfo.DelayReduction = 250;
            hlInfo.Range = 0;

            MagicInfo aoInfo = MagicInfoList.FirstOrDefault(t => t.Spell == Spell.AncientOracle);
            if (aoInfo == null) MagicInfoList.Add(aoInfo = new MagicInfo { Spell = Spell.AncientOracle });
            aoInfo.Name = "召唤上古神谕"; aoInfo.Icon = 118;
            aoInfo.Level1 = 55; aoInfo.Level2 = 57; aoInfo.Level3 = 60;
            aoInfo.Need1 = 12000; aoInfo.Need2 = 20000; aoInfo.Need3 = 30000;
            aoInfo.BaseCost = 30; aoInfo.LevelCost = 5;
            aoInfo.DelayBase = 3000; aoInfo.DelayReduction = 250;
            aoInfo.Range = 0;

            if (!MagicExists(Spell.EnergyShield))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "EnergyShield",
                    Spell = Spell.EnergyShield,
                    Icon = 57,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 54,
                    Need1 = 5000,
                    Need2 = 9000,
                    Need3 = 13000,
                    BaseCost = 50,
                    LevelCost = 20,
                    Range = 9
                });
            if (!MagicExists(Spell.PetEnhancer))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "PetEnhancer",
                    Spell = Spell.PetEnhancer,
                    Icon = 78,
                    Level1 = 45,
                    Level2 = 48,
                    Level3 = 51,
                    Need1 = 4000,
                    Need2 = 8000,
                    Need3 = 12000,
                    BaseCost = 30,
                    LevelCost = 40,
                    Range = 0
                });
            if (!MagicExists(Spell.HealingCircle))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "HealingCircle",
                    Spell = Spell.HealingCircle,
                    Icon = 82,
                    Level1 = 39,
                    Level2 = 41,
                    Level3 = 43,
                    Need1 = 7000,
                    Need2 = 12000,
                    Need3 = 15000,
                    BaseCost = 10,
                    LevelCost = 100
                });
            //Assassin
            if (!MagicExists(Spell.FatalSword))
                MagicInfoList.Add(new MagicInfo { Name = "FatalSword", Spell = Spell.FatalSword, Icon = 58, Level1 = 7, Level2 = 9, Level3 = 12, Need1 = 500, Need2 = 1000, Need3 = 2300, Range = 0 });
            if (!MagicExists(Spell.DoubleSlash))
                MagicInfoList.Add(new MagicInfo
                { Name = "DoubleSlash", Spell = Spell.DoubleSlash, Icon = 59, Level1 = 15, Level2 = 17, Level3 = 19, Need1 = 700, Need2 = 1500, Need3 = 2200, BaseCost = 2, LevelCost = 1 });
            if (!MagicExists(Spell.Haste))
                MagicInfoList.Add(new MagicInfo
                { Name = "Haste", Spell = Spell.Haste, Icon = 60, Level1 = 20, Level2 = 22, Level3 = 25, Need1 = 2000, Need2 = 3000, Need3 = 6000, BaseCost = 3, LevelCost = 2, Range = 0 });
            if (!MagicExists(Spell.FlashDash))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FlashDash",
                    Spell = Spell.FlashDash,
                    Icon = 61,
                    Level1 = 25,
                    Level2 = 27,
                    Level3 = 30,
                    Need1 = 4000,
                    Need2 = 7000,
                    Need3 = 9000,
                    BaseCost = 12,
                    LevelCost = 2,
                    DelayBase = 200,
                    Range = 0
                });
            if (!MagicExists(Spell.LightBody))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "LightBody",
                    Spell = Spell.LightBody,
                    Icon = 68,
                    Level1 = 27,
                    Level2 = 29,
                    Level3 = 32,
                    Need1 = 5000,
                    Need2 = 7000,
                    Need3 = 10000,
                    BaseCost = 11,
                    LevelCost = 2,
                    Range = 0
                });
            if (!MagicExists(Spell.HeavenlySword))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "HeavenlySword",
                    Spell = Spell.HeavenlySword,
                    Icon = 62,
                    Level1 = 30,
                    Level2 = 32,
                    Level3 = 35,
                    Need1 = 4000,
                    Need2 = 8000,
                    Need3 = 10000,
                    BaseCost = 13,
                    LevelCost = 2,
                    MPowerBase = 8,
                    Range = 0
                });
            if (!MagicExists(Spell.FireBurst))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FireBurst",
                    Spell = Spell.FireBurst,
                    Icon = 63,
                    Level1 = 33,
                    Level2 = 35,
                    Level3 = 38,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 8000,
                    BaseCost = 10,
                    LevelCost = 1,
                    Range = 0
                });
            if (!MagicExists(Spell.Trap))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Trap",
                    Spell = Spell.Trap,
                    Icon = 64,
                    Level1 = 33,
                    Level2 = 35,
                    Level3 = 38,
                    Need1 = 2000,
                    Need2 = 4000,
                    Need3 = 6000,
                    BaseCost = 14,
                    LevelCost = 2,
                    DelayBase = 60000,
                    DelayReduction = 15000,
                    Range = 9
                });
            if (!MagicExists(Spell.PoisonSword))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "PoisonSword",
                    Spell = Spell.PoisonSword,
                    Icon = 69,
                    Level1 = 34,
                    Level2 = 36,
                    Level3 = 39,
                    Need1 = 5000,
                    Need2 = 8000,
                    Need3 = 11000,
                    BaseCost = 14,
                    LevelCost = 3,
                    Range = 0
                });
            if (!MagicExists(Spell.MoonLight))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MoonLight",
                    Spell = Spell.MoonLight,
                    Icon = 65,
                    Level1 = 36,
                    Level2 = 39,
                    Level3 = 42,
                    Need1 = 3000,
                    Need2 = 5000,
                    Need3 = 8000,
                    BaseCost = 36,
                    LevelCost = 3,
                    Range = 0
                });
            if (!MagicExists(Spell.MPEater))
                MagicInfoList.Add(new MagicInfo { Name = "MPEater", Spell = Spell.MPEater, Icon = 66, Level1 = 38, Level2 = 41, Level3 = 44, Need1 = 5000, Need2 = 8000, Need3 = 11000, Range = 0 });
            if (!MagicExists(Spell.SwiftFeet))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SwiftFeet",
                    Spell = Spell.SwiftFeet,
                    Icon = 67,
                    Level1 = 40,
                    Level2 = 43,
                    Level3 = 46,
                    Need1 = 4000,
                    Need2 = 6000,
                    Need3 = 9000,
                    BaseCost = 17,
                    LevelCost = 5,
                    DelayBase = 210000,
                    DelayReduction = 40000,
                    Range = 0
                });
            if (!MagicExists(Spell.DarkBody))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "DarkBody",
                    Spell = Spell.DarkBody,
                    Icon = 70,
                    Level1 = 46,
                    Level2 = 49,
                    Level3 = 52,
                    Need1 = 6000,
                    Need2 = 10000,
                    Need3 = 14000,
                    BaseCost = 40,
                    LevelCost = 7,
                    Range = 0
                });
            if (!MagicExists(Spell.Hemorrhage))
                MagicInfoList.Add(new MagicInfo
                { Name = "Hemorrhage", Spell = Spell.Hemorrhage, Icon = 75, Level1 = 47, Level2 = 51, Level3 = 55, Need1 = 9000, Need2 = 15000, Need3 = 21000, Range = 0 });
            if (!MagicExists(Spell.CrescentSlash))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "CresentSlash",
                    Spell = Spell.CrescentSlash,
                    Icon = 71,
                    Level1 = 50,
                    Level2 = 53,
                    Level3 = 56,
                    Need1 = 12000,
                    Need2 = 16000,
                    Need3 = 24000,
                    BaseCost = 19,
                    LevelCost = 5,
                    Range = 0
                });
            if (!MagicExists(Spell.MoonMist))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MoonMist",
                    Spell = Spell.MoonMist,
                    Icon = 83,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 56,
                    Need1 = 10,
                    Need2 = 20,
                    Need3 = 30,
                    BaseCost = 30,
                    LevelCost = 5,
                    DelayBase = 20000,
                    DelayReduction = 2000
                });
            if (!MagicExists(Spell.CatTongue))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "CatTongue",
                    Spell = Spell.CatTongue,
                    Icon = 79,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 56,
                    Need1 = 10,
                    Need2 = 20,
                    Need3 = 30,
                    BaseCost = 30,
                    LevelCost = 5,
                    DelayBase = 20000,
                    DelayReduction = 2000
                });

            //Archer
            if (!MagicExists(Spell.Focus))
                MagicInfoList.Add(new MagicInfo { Name = "Focus", Spell = Spell.Focus, Icon = 88, Level1 = 7, Level2 = 13, Level3 = 17, Need1 = 270, Need2 = 600, Need3 = 1300, Range = 0 });
            if (!MagicExists(Spell.StraightShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "StraightShot",
                    Spell = Spell.StraightShot,
                    Icon = 89,
                    Level1 = 9,
                    Level2 = 12,
                    Level3 = 16,
                    Need1 = 350,
                    Need2 = 750,
                    Need3 = 1400,
                    BaseCost = 3,
                    LevelCost = 2,
                    MPowerBase = 8,
                    PowerBase = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.DoubleShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "DoubleShot",
                    Spell = Spell.DoubleShot,
                    Icon = 90,
                    Level1 = 14,
                    Level2 = 18,
                    Level3 = 21,
                    Need1 = 700,
                    Need2 = 1500,
                    Need3 = 2100,
                    BaseCost = 3,
                    LevelCost = 2,
                    MPowerBase = 6,
                    PowerBase = 2,
                    Range = 9
                });
            if (!MagicExists(Spell.ExplosiveTrap))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ExplosiveTrap",
                    Spell = Spell.ExplosiveTrap,
                    Icon = 91,
                    Level1 = 22,
                    Level2 = 25,
                    Level3 = 30,
                    Need1 = 2000,
                    Need2 = 3500,
                    Need3 = 5000,
                    BaseCost = 10,
                    LevelCost = 3,
                    MPowerBase = 15,
                    PowerBase = 15,
                    Range = 0
                });
            if (!MagicExists(Spell.DelayedExplosion))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "DelayedExplosion",
                    Spell = Spell.DelayedExplosion,
                    Icon = 92,
                    Level1 = 31,
                    Level2 = 34,
                    Level3 = 39,
                    Need1 = 3000,
                    Need2 = 7000,
                    Need3 = 10000,
                    BaseCost = 8,
                    LevelCost = 2,
                    MPowerBase = 30,
                    PowerBase = 15,
                    Range = 9
                });
            if (!MagicExists(Spell.Meditation))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Meditation",
                    Spell = Spell.Meditation,
                    Icon = 93,
                    Level1 = 19,
                    Level2 = 24,
                    Level3 = 29,
                    Need1 = 1800,
                    Need2 = 2600,
                    Need3 = 5600,
                    BaseCost = 8,
                    LevelCost = 2,
                    Range = 0
                });
            if (!MagicExists(Spell.ElementalShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ElementalShot",
                    Spell = Spell.ElementalShot,
                    Icon = 94,
                    Level1 = 20,
                    Level2 = 25,
                    Level3 = 31,
                    Need1 = 1800,
                    Need2 = 2700,
                    Need3 = 6000,
                    BaseCost = 8,
                    LevelCost = 2,
                    MPowerBase = 6,
                    PowerBase = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.Concentration))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Concentration",
                    Spell = Spell.Concentration,
                    Icon = 96,
                    Level1 = 23,
                    Level2 = 27,
                    Level3 = 32,
                    Need1 = 2100,
                    Need2 = 3800,
                    Need3 = 6500,
                    BaseCost = 8,
                    LevelCost = 2,
                    Range = 0
                });
            if (!MagicExists(Spell.ElementalBarrier))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ElementalBarrier",
                    Spell = Spell.ElementalBarrier,
                    Icon = 98,
                    Level1 = 33,
                    Level2 = 38,
                    Level3 = 44,
                    Need1 = 3000,
                    Need2 = 7000,
                    Need3 = 10000,
                    BaseCost = 10,
                    LevelCost = 2,
                    MPowerBase = 15,
                    PowerBase = 5,
                    Range = 0
                });
            if (!MagicExists(Spell.BackStep))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "BackStep",
                    Spell = Spell.BackStep,
                    Icon = 95,
                    Level1 = 30,
                    Level2 = 34,
                    Level3 = 38,
                    Need1 = 2400,
                    Need2 = 3000,
                    Need3 = 6000,
                    BaseCost = 12,
                    LevelCost = 2,
                    DelayBase = 2500,
                    Range = 0
                });
            if (!MagicExists(Spell.BindingShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "BindingShot",
                    Spell = Spell.BindingShot,
                    Icon = 97,
                    Level1 = 35,
                    Level2 = 39,
                    Level3 = 42,
                    Need1 = 400,
                    Need2 = 7000,
                    Need3 = 9500,
                    BaseCost = 7,
                    LevelCost = 3,
                    Range = 9
                });
            if (!MagicExists(Spell.Stonetrap))
                MagicInfoList.Add(new MagicInfo
                { Name = "Stonetrap", Spell = Spell.Stonetrap, Icon = 97, Level1 = 40, Level2 = 43, Level3 = 46, Need1 = 4900, Need2 = 9800, Need3 = 141, BaseCost = 7, LevelCost = 3, Range = 9 });
            if (!MagicExists(Spell.SummonVampire))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SummonVampire",
                    Spell = Spell.SummonVampire,
                    Icon = 99,
                    Level1 = 28,
                    Level2 = 33,
                    Level3 = 41,
                    Need1 = 2000,
                    Need2 = 2700,
                    Need3 = 7500,
                    BaseCost = 10,
                    LevelCost = 5,
                    Range = 9
                });
            if (!MagicExists(Spell.VampireShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "VampireShot",
                    Spell = Spell.VampireShot,
                    Icon = 100,
                    Level1 = 26,
                    Level2 = 32,
                    Level3 = 36,
                    Need1 = 3000,
                    Need2 = 6000,
                    Need3 = 12000,
                    BaseCost = 12,
                    LevelCost = 3,
                    MPowerBase = 10,
                    PowerBase = 7,
                    Range = 9
                });
            if (!MagicExists(Spell.SummonToad))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SummonToad",
                    Spell = Spell.SummonToad,
                    Icon = 101,
                    Level1 = 37,
                    Level2 = 43,
                    Level3 = 47,
                    Need1 = 5800,
                    Need2 = 10000,
                    Need3 = 13000,
                    BaseCost = 10,
                    LevelCost = 5,
                    Range = 9
                });
            if (!MagicExists(Spell.PoisonShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "PoisonShot",
                    Spell = Spell.PoisonShot,
                    Icon = 102,
                    Level1 = 40,
                    Level2 = 45,
                    Level3 = 49,
                    Need1 = 6000,
                    Need2 = 14000,
                    Need3 = 16000,
                    BaseCost = 10,
                    LevelCost = 4,
                    MPowerBase = 10,
                    PowerBase = 10,
                    Range = 9
                });
            if (!MagicExists(Spell.CrippleShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "CrippleShot",
                    Spell = Spell.CrippleShot,
                    Icon = 103,
                    Level1 = 43,
                    Level2 = 47,
                    Level3 = 50,
                    Need1 = 12000,
                    Need2 = 15000,
                    Need3 = 18000,
                    BaseCost = 15,
                    LevelCost = 3,
                    MPowerBase = 10,
                    MPowerBonus = 20,
                    PowerBase = 10,
                    Range = 9
                });
            if (!MagicExists(Spell.SummonSnakes))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "SummonSnakes",
                    Spell = Spell.SummonSnakes,
                    Icon = 104,
                    Level1 = 46,
                    Level2 = 51,
                    Level3 = 54,
                    Need1 = 14000,
                    Need2 = 17000,
                    Need3 = 20000,
                    BaseCost = 10,
                    LevelCost = 5,
                    Range = 9
                });
            if (!MagicExists(Spell.NapalmShot))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "NapalmShot",
                    Spell = Spell.NapalmShot,
                    Icon = 105,
                    Level1 = 48,
                    Level2 = 52,
                    Level3 = 55,
                    Need1 = 15000,
                    Need2 = 18000,
                    Need3 = 21000,
                    BaseCost = 40,
                    LevelCost = 10,
                    MPowerBase = 25,
                    MPowerBonus = 25,
                    PowerBase = 25,
                    Range = 9
                });
            if (!MagicExists(Spell.OneWithNature))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "OneWithNature",
                    Spell = Spell.OneWithNature,
                    Icon = 106,
                    Level1 = 50,
                    Level2 = 53,
                    Level3 = 56,
                    Need1 = 17000,
                    Need2 = 19000,
                    Need3 = 24000,
                    BaseCost = 80,
                    LevelCost = 15,
                    MPowerBase = 75,
                    MPowerBonus = 35,
                    PowerBase = 30,
                    PowerBonus = 20,
                    Range = 9
                });
            if (!MagicExists(Spell.MentalState))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MentalState",
                    Spell = Spell.MentalState,
                    Icon = 81,
                    Level1 = 11,
                    Level2 = 15,
                    Level3 = 22,
                    Need1 = 500,
                    Need2 = 900,
                    Need3 = 1800,
                    BaseCost = 1,
                    LevelCost = 1,
                    Range = 0
                });

            //Custom
            if (!MagicExists(Spell.Portal))
                MagicInfoList.Add(new MagicInfo
                { Name = "Portal", Spell = Spell.Portal, Icon = 1, Level1 = 7, Level2 = 11, Level3 = 14, Need1 = 150, Need2 = 350, Need3 = 700, BaseCost = 3, LevelCost = 2, Range = 9 });
            if (!MagicExists(Spell.BattleCry))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "BattleCry",
                    Spell = Spell.BattleCry,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.FireBounce))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "FireBounce",
                    Spell = Spell.FireBounce,
                    Icon = 4,
                    Level1 = 15,
                    Level2 = 18,
                    Level3 = 21,
                    Need1 = 2000,
                    Need2 = 2700,
                    Need3 = 3500,
                    BaseCost = 5,
                    LevelCost = 1,
                    MPowerBase = 6,
                    PowerBase = 10,
                    Range = 9
                });
            if (!MagicExists(Spell.MeteorShower))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "MeteorShower",
                    Spell = Spell.MeteorShower,
                    Icon = 4,
                    Level1 = 15,
                    Level2 = 18,
                    Level3 = 21,
                    Need1 = 2000,
                    Need2 = 2700,
                    Need3 = 3500,
                    BaseCost = 5,
                    LevelCost = 1,
                    MPowerBase = 6,
                    PowerBase = 10,
                    Range = 9
                });

            //Monk
            if (!MagicExists(Spell.JiBenGunFa))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "JiBenGunFa",
                    Spell = Spell.JiBenGunFa,
                    Icon = 42,
                    Level1 = 1,
                    Level2 = 1,
                    Level3 = 5,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.LuoHanGunFa))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "LuoHanGunFa",
                    Spell = Spell.LuoHanGunFa,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.JinGangGunFa))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "JinGangGunFa",
                    Spell = Spell.JinGangGunFa,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.DaMoGunFa))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "DaMoGunFa",
                    Spell = Spell.DaMoGunFa,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.XiangLongGunFa))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "XiangLongGunFa",
                    Spell = Spell.XiangLongGunFa,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.Taunt))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "Taunt",
                    Spell = Spell.Taunt,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.TianLeiZhen))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "TianLeiZhen",
                    Spell = Spell.TianLeiZhen,
                    Icon = 42,
                    Level1 = 48,
                    Level2 = 51,
                    Level3 = 55,
                    Need1 = 8000,
                    Need2 = 11000,
                    Need3 = 15000,
                    BaseCost = 22,
                    LevelCost = 10,
                    Range = 0
                });
            if (!MagicExists(Spell.ShiBuYiSha))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "ShiBuYiSha",
                    Spell = Spell.ShiBuYiSha,
                    Icon = 23,
                    Level1 = 60,
                    Level2 = 61,
                    Level3 = 62,
                    Need1 = 2200,
                    Need2 = 3300,
                    Need3 = 4400,
                    BaseCost = 65,
                    LevelCost = 8,
                    MPowerBase = 12,
                    PowerBase = 4,
                    Range = 9
                });
            if (!MagicExists(Spell.LuoHanZhen))
                MagicInfoList.Add(new MagicInfo
                {
                    Name = "LuoHanZhen",
                    Spell = Spell.LuoHanZhen,
                    Icon = 23,
                    Level1 = 60,
                    Level2 = 61,
                    Level3 = 62,
                    Need1 = 2200,
                    Need2 = 3300,
                    Need3 = 4400,
                    BaseCost = 65,
                    LevelCost = 8,
                    MPowerBase = 12,
                    PowerBase = 4,
                    Range = 9
                });

            //==== 秘籍技能CD强制同步(找到即覆写) ====
            //原因: 上面的注册是"缺则添加"式, DB里已存在的旧记录(无CD/默认1800ms)会优先于代码,
            //导致高级技能无CD. 此处启动时强制覆写, 下次SaveDB即持久化为正确值.
            SyncRareSkillCooldowns();
        }

        /// <summary>
        /// 秘籍/高级技能冷却强制同步: 不管DB里是什么值, 启动时一律按此表覆写.
        /// </summary>
        private void SyncRareSkillCooldowns()
        {
            var delayTable = new Dictionary<Spell, KeyValuePair<uint, uint>>
            {
                //战士
                { Spell.EntrapmentRare,        new KeyValuePair<uint, uint>(15000, 3000) },  //捕绳剑-秘籍
                { Spell.LionRoarRare,          new KeyValuePair<uint, uint>(30000, 5000) },  //狮子吼-秘籍
                //道士
                { Spell.HealingRare,           new KeyValuePair<uint, uint>(3000, 500) },    //治愈术-秘籍
                { Spell.PetEnhancerRare,       new KeyValuePair<uint, uint>(60000, 10000) }, //血龙水-秘籍
                //刺客
                { Spell.MoonMistRare,          new KeyValuePair<uint, uint>(20000, 4000) },  //月影雾-秘籍
                { Spell.CrescentSlashRare,     new KeyValuePair<uint, uint>(13000, 3000) },  //月华乱舞-秘籍
                //弓手
                { Spell.DelayedExplosionRare,  new KeyValuePair<uint, uint>(12000, 3000) },  //爆闪-秘籍
            };

            foreach (var pair in delayTable)
            {
                MagicInfo info = MagicInfoList.FirstOrDefault(t => t.Spell == pair.Key);
                if (info == null) continue;
                info.DelayBase = pair.Value.Key;
                info.DelayReduction = pair.Value.Value;
            }
        }

        /// <summary>
        /// 自定义技能槽(242-255)MagicInfo同步: 缺则播种, 有则按面板配置刷新名称/图标/耗蓝/冷却.
        /// 服务器启动(LoadDB后)与"自定义技能"面板保存时调用.
        /// </summary>
        public void SyncCustomSkillMagicInfo()
        {
            foreach (var cfg in Settings.CustomSkills)
            {
                if (!cfg.Enabled) continue;

                MagicInfo info = null;
                for (int i = 0; i < MagicInfoList.Count; i++)
                    if (MagicInfoList[i].Spell == cfg.Spell) { info = MagicInfoList[i]; break; }

                if (info == null)
                {
                    MagicInfoList.Add(new MagicInfo
                    {
                        Name = string.IsNullOrEmpty(cfg.Name) ? cfg.Spell.ToString() : cfg.Name,
                        Spell = cfg.Spell,
                        Icon = cfg.Icon,
                        Level1 = 35,
                        Level2 = 37,
                        Level3 = 40,
                        Need1 = 2000,
                        Need2 = 4000,
                        Need3 = 6000,
                        BaseCost = (byte)Math.Min(255, (int)cfg.BaseCost),
                        Range = 9,
                        DelayBase = Math.Max(500, cfg.DelayMs),
                        MultiplierBase = 1.0f,
                        MultiplierBonus = 0.1f
                    });
                }
                else
                {
                    info.Name = string.IsNullOrEmpty(cfg.Name) ? cfg.Spell.ToString() : cfg.Name;
                    info.Icon = cfg.Icon;
                    info.BaseCost = (byte)Math.Min(255, (int)cfg.BaseCost);
                    info.DelayBase = Math.Max(500, cfg.DelayMs);
                }
            }
        }

        private string CanStartEnvir()
        {
            if (StartPoints.Count == 0) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMapAndStartPoint);

            if (Settings.EnforceDBChecks)
            {
                if (GetMonsterInfo(Settings.SkeletonName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.SkeletonName;
                if (GetMonsterInfo(Settings.ShinsuName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.ShinsuName;
                if (GetMonsterInfo(Settings.BugBatName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BugBatName;
                if (GetMonsterInfo(Settings.Zuma1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma1;
                if (GetMonsterInfo(Settings.Zuma2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma2;
                if (GetMonsterInfo(Settings.Zuma3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma3;
                if (GetMonsterInfo(Settings.Zuma4, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma4;
                if (GetMonsterInfo(Settings.Zuma5, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma5;
                if (GetMonsterInfo(Settings.Zuma6, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma6;
                if (GetMonsterInfo(Settings.Zuma7, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Zuma7;
                if (GetMonsterInfo(Settings.Turtle1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Turtle1;
                if (GetMonsterInfo(Settings.Turtle2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Turtle2;
                if (GetMonsterInfo(Settings.Turtle3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Turtle3;
                if (GetMonsterInfo(Settings.Turtle4, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Turtle4;
                if (GetMonsterInfo(Settings.Turtle5, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.Turtle5;
                if (GetMonsterInfo(Settings.BoneMonster1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BoneMonster1;
                if (GetMonsterInfo(Settings.BoneMonster2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BoneMonster2;
                if (GetMonsterInfo(Settings.BoneMonster3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BoneMonster3;
                if (GetMonsterInfo(Settings.BoneMonster4, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BoneMonster4;
                if (GetMonsterInfo(Settings.BehemothMonster1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BehemothMonster1;
                if (GetMonsterInfo(Settings.BehemothMonster2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BehemothMonster2;
                if (GetMonsterInfo(Settings.BehemothMonster3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BehemothMonster3;
                if (GetMonsterInfo(Settings.HellKnight1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellKnight1;
                if (GetMonsterInfo(Settings.HellKnight2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellKnight2;
                if (GetMonsterInfo(Settings.HellKnight3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellKnight3;
                if (GetMonsterInfo(Settings.HellKnight4, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellKnight4;
                if (GetMonsterInfo(Settings.HellBomb1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellBomb1;
                if (GetMonsterInfo(Settings.HellBomb2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellBomb2;
                if (GetMonsterInfo(Settings.HellBomb3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HellBomb3;
                if (GetMonsterInfo(Settings.WhiteSnake, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.WhiteSnake;
                if (GetMonsterInfo(Settings.AngelName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.AngelName;
                if (GetMonsterInfo(Settings.BombSpiderName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.BombSpiderName;
                if (GetMonsterInfo(Settings.CloneName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.CloneName;
                if (GetMonsterInfo(Settings.AssassinCloneName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.AssassinCloneName;
                if (GetMonsterInfo(Settings.VampireName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.VampireName;
                if (GetMonsterInfo(Settings.ToadName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.ToadName;
                if (GetMonsterInfo(Settings.SnakeTotemName, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.SnakeTotemName;
                if (GetMonsterInfo(Settings.FishingMonster, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.FishingMonster;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.GeneralMeowMeowMob1;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.GeneralMeowMeowMob2;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.GeneralMeowMeowMob3;
                if (GetMonsterInfo(Settings.GeneralMeowMeowMob4, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.GeneralMeowMeowMob4;
                if (GetMonsterInfo(Settings.KingHydraxMob, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.KingHydraxMob;
                if (GetMonsterInfo(Settings.HornedCommanderMob, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HornedCommanderMob;
                if (GetMonsterInfo(Settings.HornedCommanderBombMob, true) == null)
                    return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.HornedCommanderBombMob;
                if (GetMonsterInfo(Settings.SnowWolfKingMob, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.SnowWolfKingMob;
                if (GetMonsterInfo(Settings.ScrollMob1, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.ScrollMob1;
                if (GetMonsterInfo(Settings.ScrollMob2, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.ScrollMob2;
                if (GetMonsterInfo(Settings.ScrollMob3, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.ScrollMob3;
                if (GetMonsterInfo(Settings.ScrollMob4, true) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutMob) + Settings.ScrollMob4;

                if (GetItemInfo(Settings.RefineOreName) == null) return GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotStartServerWithoutItem) + Settings.RefineOreName;
            }

            WorldMapIcon wmi = ValidateWorldMap();
            if (wmi != null)
                return GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.InvalidWorldmapIndex), wmi.MapIndex, wmi.Title);


            //add intelligent creature checks?

            return "true";
        }

        private void WorkLoop()
        {
            try
            {
                Time = Stopwatch.ElapsedMilliseconds;

                var conTime = Time;
                var saveTime = Time + Settings.SaveDelay * Settings.Minute;
                var userTime = Time + Settings.Minute * 5;
                var lineMessageTime = Time + Settings.Minute * Settings.LineMessageTimer;
                var mapUnloadTime = Time + 30 * Settings.Second;
                var mapProcessTime = Time + 100;
                var processTime = Time + 1000;
                var startTime = Time;

                var processCount = 0;
                var processRealCount = 0;

                LinkedListNode<MapObject> current = null;

                if (Settings.Multithreaded)
                {
                    //每次启动按当前配置重建怪物线程数组(最低2: 仅1条时主循环每圈只处理1只怪=全体冻结);
                    //此前数组仅在进程构造时分配一次, "保存并重启"重读ThreadLimit后新旧值错位,
                    //地图Random.Next(新值)分到不存在的线程→Spawned越界/怪物冻结, 此处根治
                    var threadLimit = Math.Max(2, Settings.ThreadLimit);
                    MobThreads = new MobThread[threadLimit];
                    MobThreading = new Thread[threadLimit];
                    for (var j = 0; j < MobThreads.Length; j++)
                    {
                        MobThreads[j] = new MobThread();
                        MobThreads[j].Id = j;
                    }
                }

                StartEnvir();
                var canstartserver = CanStartEnvir();
                if (canstartserver != "true")
                {
                    LastStartError = canstartserver; //SMain 面板轮询此值弹窗, 启动失败不再只写一行日志
                    MessageQueue.Enqueue(canstartserver);
                    StopEnvir();
                    _thread = null;
                    Stop();
                    return;
                }
                LastStartError = null;

                if (Settings.Multithreaded)
                {
                    for (var j = 0; j < MobThreads.Length; j++)
                    {
                        var Info = MobThreads[j];
                        if (j <= 0) continue;
                        MobThreading[j] = new Thread(() => ThreadLoop(Info)) { IsBackground = true };
                        MobThreading[j].Start();
                    }
                }

                StartNetwork();
                if (Settings.StartHTTPService)
                {
                    http = new HttpServer();
                    http.Start();
                }
                try
                {
                    while (Running)
                    {
                        Time = Stopwatch.ElapsedMilliseconds;

                        //物品热同步: 执行编辑器排队过来的同步动作(与下方周期存盘同线程, 串行安全)
                        var pendingItemSync = Interlocked.Exchange(ref _pendingItemSync, null);
                        if (pendingItemSync != null)
                        {
                            try
                            {
                                pendingItemSync();
                            }
                            catch (Exception syncEx)
                            {
                                MessageQueue.Enqueue("物品热同步失败: " + syncEx);
                            }
                        }

                        //通用主线程任务(控制面板投递, 如战场手动开战/结束)
                        var mainAction = Interlocked.Exchange(ref _pendingMainAction, null);
                        if (mainAction != null)
                        {
                            try
                            {
                                mainAction();
                            }
                            catch (Exception mainEx)
                            {
                                MessageQueue.Enqueue("主线程任务失败: " + mainEx);
                            }
                        }

                        if (Time >= processTime)
                        {
                            LastCount = processCount;
                            LastRealCount = processRealCount;
                            processCount = 0;
                            processRealCount = 0;
                            processTime = Time + 1000;
                        }

                        if (conTime != Time)
                        {
                            conTime = Time;

                            AdjustLights();

                            lock (Connections)
                            {
                                for (var i = Connections.Count - 1; i >= 0; i--)
                                {
                                    Connections[i].Process();
                                }
                            }

                            lock (StatusConnections)
                            {
                                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                                {
                                    StatusConnections[i].Process();
                                }
                            }
                        }


                        if (current == null)
                            current = Objects.First;

                        if (current == Objects.First)
                        {
                            LastRunTime = Time - startTime;
                            startTime = Time;
                        }

                        if (Settings.Multithreaded)
                        {
                            for (var j = 1; j < MobThreads.Length; j++)
                            {
                                var Info = MobThreads[j];

                                if (!Info.Stop) continue;
                                Info.EndTime = Time + 10;
                                Info.Stop = false;
                            }
                            lock (_locker)
                            {
                                Monitor.PulseAll(_locker); //changing a blocking condition. (this makes the threads wake up!)
                            }
                            //run the first loop in the main thread so the main thread automaticaly 'halts' until the other threads are finished
                            ThreadLoop(MobThreads[0]);
                        }

                        var TheEnd = false;
                        var Start = Stopwatch.ElapsedMilliseconds;
                        while (!TheEnd && Stopwatch.ElapsedMilliseconds - Start < 20)
                        {
                            if (current == null)
                            {
                                TheEnd = true;
                                break;
                            }

                            var next = current.Next;
                            if (!Settings.Multithreaded || current.Value.Race != ObjectType.Monster || current.Value.Master != null)
                            {
                                if (Time > current.Value.OperateTime)
                                {
                                    current.Value.Process();
                                    current.Value.SetOperateTime();
                                }
                                processCount++;
                            }
                            current = next;
                        }

                        //地图处理节流: 刷怪/门/地图特效按100ms一轮(原本每圈空转都全图扫, 803图白烧CPU)
                        if (Time >= mapProcessTime)
                        {
                            mapProcessTime = Time + 100;
                            for (var i = 0; i < MapList.Count; i++)
                                MapList[i].Process();
                        }

                        DragonSystem?.Process();
                        FieldBossSystem?.Process();

                        BattleField.Process(); //战场系统(每秒节流)

                        Process();

                        if (Time >= saveTime)
                        {
                            saveTime = Time + Settings.SaveDelay * Settings.Minute;
                            BeginSaveAccounts();
                            SaveDB();
                            SaveGuilds();
                            SaveGoods();
                            SaveConquests();
                        }

                        if (Time >= userTime)
                        {
                            userTime = Time + Settings.Minute * 5;
                            Broadcast(new S.Chat
                            {
                                Message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.OnlinePlayers), Players.Count),
                                Type = ChatType.Hint
                            });
                        }

                        if (LineMessages.Count > 0 && Time >= lineMessageTime)
                        {
                            lineMessageTime = Time + Settings.Minute * Settings.LineMessageTimer;
                            Broadcast(new S.Chat
                            {
                                Message = LineMessages[Random.Next(LineMessages.Count)],
                                Type = ChatType.LineMessage
                            });
                        }

                        if (Settings.MapUnloadEnabled && Time >= mapUnloadTime)
                        {
                            mapUnloadTime = Time + 30 * Settings.Second;
                            UnloadIdleMaps();
                        }

                        //主循环节流: 每圈睡1ms, 避免满速空转烧满一个核
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    lock (Connections)
                    {
                        for (var i = Connections.Count - 1; i >= 0; i--)
                            Connections[i].SendDisconnect(3);
                    }

                    // Get stack trace for the exception with source file information
                    var st = new StackTrace(ex, true);
                    // Get the top stack frame
                    var frame = st.GetFrame(0);
                    // Get the line number from the stack frame
                    var line = frame.GetFileLineNumber();

                    MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.InnerWorkloopErrorLine), line, ex));
                }

                StopNetwork();
                StopEnvir();
                SaveAccounts();
                SaveGuilds(true);
                SaveConquests(true);
            }
            catch (Exception ex)
            {
                // Get stack trace for the exception with source file information
                var st = new StackTrace(ex, true);
                // Get the top stack frame
                var frame = st.GetFrame(0);
                // Get the line number from the stack frame
                var line = frame.GetFileLineNumber();

                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.OuterWorkloopErrorLine), line, ex));
            }

            _thread = null;
        }

        private void ThreadLoop(MobThread Info)
        {
            Info.Stop = false;

            try
            {
                var stopping = false;
                if (Info._current == null)
                    Info._current = Info.ObjectsList.First;
                stopping = Info._current == null;

                while (Running)
                {
                    if (Info._current == null)
                        Info._current = Info.ObjectsList.First;
                    else
                    {
                        var next = Info._current.Next;

                        //if we reach the end of our list > go back to the top (since we are running threaded, we dont want the system to sit there for xxms doing nothing)
                        if (Info._current == Info.ObjectsList.Last)
                        {
                            next = Info.ObjectsList.First;
                            Info.LastRunTime = (Info.LastRunTime + (Time - Info.StartTime)) / 2;
                            //Info.LastRunTime = (Time - Info.StartTime) /*> 0 ? (Time - Info.StartTime) : Info.LastRunTime */;
                            Info.StartTime = Time;
                        }
                        if (Time > Info._current.Value.OperateTime)
                        {
                            if (Info._current.Value.Master == null) //since we are running multithreaded, dont allow pets to be processed (unless you constantly move pets into their map appropriate thead)
                            {
                                Info._current.Value.Process();
                                Info._current.Value.SetOperateTime();
                            }
                        }
                        Info._current = next;
                    }

                    //if it's the main thread > make it loop till the subthreads are done, else make it stop after 'endtime'
                    if (Info.Id == 0)
                    {
                        stopping = true;
                        for (var x = 1; x < MobThreads.Length; x++)
                        {
                            if (MobThreads[x].Stop == false)
                            {
                                stopping = false;
                            }
                        }
                        if (!stopping)
                        {
                            Thread.Sleep(0); //仅让出时间片立即返回, 等待期间继续全速处理本线程怪物(Sleep(1)实际睡15ms导致怪物冻结)
                            continue;
                        }
                        Info.Stop = stopping;
                        return;
                    }

                    if (Stopwatch.ElapsedMilliseconds <= Info.EndTime || !Running) continue;
                    Info.Stop = true;
                    lock (_locker)
                    {
                        while (Info.Stop) Monitor.Wait(_locker);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is ThreadInterruptedException) return;

                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ThreadLoopError), ex));
            }
        }

        private void AdjustLights()
        {
            var oldLights = Lights;

            var hours = Now.Hour * 2 % 24;
            if (hours == 6 || hours == 7)
                Lights = LightSetting.Dawn;
            else if (hours >= 8 && hours <= 15)
                Lights = LightSetting.Day;
            else if (hours == 16 || hours == 17)
                Lights = LightSetting.Evening;
            else
                Lights = LightSetting.Night;

            if (oldLights == Lights) return;

            Broadcast(new S.TimeOfDay { Lights = Lights });
        }

        public void Process()
        {
            if (Now.Day != dailyTime)
            {
                dailyTime = Now.Day;
                ProcessNewDay();
            }

            if (Time >= warTime)
            {
                warTime = Time + Settings.Minute;
                for (var i = GuildsAtWar.Count - 1; i >= 0; i--)
                {
                    GuildsAtWar[i].TimeRemaining -= Settings.Minute;

                    if (GuildsAtWar[i].TimeRemaining >= 0) continue;
                    GuildsAtWar[i].EndWar();
                    GuildsAtWar.RemoveAt(i);
                }
            }

            if (Time >= guildTime)
            {
                guildTime = Time + Settings.Minute;
                for (var i = 0; i < Guilds.Count; i++)
                {
                    Guilds[i].Process();
                }
            }

            if (Time >= conquestTime)
            {
                conquestTime = Time + Settings.Second * 10;
                for (var i = 0; i < Conquests.Count; i++)
                {
                    Conquests[i].Process();
                }
            }

            if (Time >= rentalItemsTime)
            {
                rentalItemsTime = Time + Settings.Minute * 5;
                ProcessRentedItems();
            }

            if (Time >= auctionTime)
            {
                auctionTime = Time + Settings.Minute * 10;
                ProcessAuction();
            }

            if (Time >= spawnTime)
            {
                spawnTime = Time + Settings.Second * 10;
                Main.RespawnTick.Process();
            }

            if (Time >= robotTime)
            {
                robotTime = Time + Settings.Minute;
                Robot.Process(RobotNPC);
            }

            if (Time >= timerTime)
            {
                timerTime = Time + Settings.Second;

                string[] keys = Timers.Keys.ToArray();

                foreach (var key in keys)
                {
                    if (Timers[key].RelativeTime <= Time)
                    {
                        Timers.Remove(key);
                    }
                }
            }
        }

        private void ProcessAuction()
        {
            LinkedListNode<AuctionInfo> current = Auctions.First;

            while (current != null)
            {
                AuctionInfo info = current.Value;

                if (!info.Expired && !info.Sold && Now >= info.ConsignmentDate.AddDays(Globals.ConsignmentLength))
                {
                    if (info.ItemType == MarketItemType.Auction && info.CurrentBid > info.Price)
                    {
                        string message = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouWonForGold), info.Item.FriendlyName, info.CurrentBid);

                        info.Sold = true;
                        MailCharacter(info.CurrentBuyerInfo, item: info.Item, customMessage: message);

                        MessageAccount(info.CurrentBuyerInfo.AccountInfo, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouBoughtForGold), info.Item.FriendlyName, info.CurrentBid),
                            ChatType.Hint);
                        MessageAccount(info.SellerInfo.AccountInfo, GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.YouSoldForGold), info.Item.FriendlyName, info.CurrentBid),
                            ChatType.Hint);
                    }
                    else
                    {
                        info.Expired = true;
                    }
                }

                current = current.Next;
            }
        }

        public void Broadcast(Packet p)
        {
            for (var i = 0; i < Players.Count; i++) Players[i].Enqueue(p);
        }

        public void RequiresBaseStatUpdate()
        {
            for (var i = 0; i < Players.Count; i++) Players[i].HasUpdatedBaseStats = false;
        }

        public void RequiresHeroBaseStatUpdate()
        {
            for (var i = 0; i < Heroes.Count; i++)
            {
                Heroes[i].HasUpdatedBaseStats = false;
                Heroes[i].RefreshStats();
            }
        }

        private long _lastDbBackupTime;
        private int _dbIoRunning;

        public void SaveDB(bool sync = false)
        {
            //序列化留主线程(集合非线程安全); 磁盘IO挪后台+临时文件原子替换, 备份降为每小时1次
            byte[] data;
            var mStream = new MemoryStream();
            using (var writer = new BinaryWriter(mStream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(MapIndex);
                writer.Write(ItemIndex);
                writer.Write(MonsterIndex);
                writer.Write(NPCIndex);
                writer.Write(QuestIndex);
                writer.Write(GameshopIndex);
                writer.Write(ConquestIndex);
                writer.Write(RespawnIndex);

                writer.Write(MapInfoList.Count);
                for (var i = 0; i < MapInfoList.Count; i++)
                    MapInfoList[i].Save(writer);

                var itemCount = 0;
                for (var i = 0; i < ItemInfoList.Count; i++)
                    if (!SuppressedItemIndexes.Contains(ItemInfoList[i].Index)) itemCount++;

                writer.Write(itemCount);
                for (var i = 0; i < ItemInfoList.Count; i++)
                {
                    if (SuppressedItemIndexes.Contains(ItemInfoList[i].Index)) continue;
                    ItemInfoList[i].Save(writer);
                }

                writer.Write(MonsterInfoList.Count);
                for (var i = 0; i < MonsterInfoList.Count; i++)
                    MonsterInfoList[i].Save(writer);

                writer.Write(NPCInfoList.Count);
                for (var i = 0; i < NPCInfoList.Count; i++)
                    NPCInfoList[i].Save(writer);

                writer.Write(QuestInfoList.Count);
                for (var i = 0; i < QuestInfoList.Count; i++)
                    QuestInfoList[i].Save(writer);

                DragonInfo.Save(writer);
                writer.Write(MagicInfoList.Count);
                for (var i = 0; i < MagicInfoList.Count; i++)
                    MagicInfoList[i].Save(writer);

                writer.Write(GameShopList.Count);
                for (var i = 0; i < GameShopList.Count; i++)
                    GameShopList[i].Save(writer);

                writer.Write(ConquestInfoList.Count);
                for (var i = 0; i < ConquestInfoList.Count; i++)
                    ConquestInfoList[i].Save(writer);

                RespawnTick.Save(writer);

                writer.Write(GTMapList.Count);
                for (var i = 0; i < GTMapList.Count; i++)
                    GTMapList[i].Save(writer);
            }
            data = mStream.ToArray();

            bool doBackup = false;
            string backupFile = null;
            if (File.Exists(DatabasePath) && Time - _lastDbBackupTime >= Settings.Hour)
            {
                _lastDbBackupTime = Time;
                doBackup = true;
                if (!Directory.Exists(DatabaseBackUpPath)) Directory.CreateDirectory(DatabaseBackUpPath);
                var fileName =
                    $"Database {Now.Year:0000}-{Now.Month:00}-{Now.Day:00} {Now.Hour:00}-{Now.Minute:00}-{Now.Second:00}.bak";
                backupFile = Path.Combine(DatabaseBackUpPath, fileName);
            }

            if (!sync && Interlocked.CompareExchange(ref _dbIoRunning, 1, 0) != 0)
                return; //上一轮后台落盘未完成, 跳过本轮(下个存盘周期再写)

            Action ioWork = () =>
            {
                try
                {
                    if (doBackup && backupFile != null)
                    {
                        if (File.Exists(backupFile)) File.Delete(backupFile);
                        File.Copy(DatabasePath, backupFile, true);
                    }

                    //先写临时文件, 再原子替换, 任意时刻被杀都不会损坏主库
                    var tmpPath = DatabasePath + "n";
                    using (var fStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        fStream.Write(data, 0, data.Length);

                    var oldPath = DatabasePath + "o";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    if (File.Exists(DatabasePath)) File.Move(DatabasePath, oldPath);
                    File.Move(tmpPath, DatabasePath);
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue("存盘DB失败: " + ex.Message);
                }
                finally
                {
                    if (!sync) Interlocked.Exchange(ref _dbIoRunning, 0);
                }
            };

            if (sync)
                ioWork();
            else
                Task.Run(ioWork);
        }


        public CharacterInfo GetArchivedCharacter(string name)
        {
            DirectoryInfo dir = new DirectoryInfo(ArchivePath);
            FileInfo[] files = dir.GetFiles($"{name}*.MirCA");

            if (files.Length != 1)
            {
                return null;
            }

            var fileInfo = files[0];

            CharacterInfo info = null;

            using (var stream = fileInfo.OpenRead())
            {
                using var reader = new BinaryReader(stream);

                var version = reader.ReadInt32();
                var customVersion = reader.ReadInt32();

                info = new CharacterInfo(reader, version, customVersion);
            }

            return info;
        }

        public void SaveArchivedCharacter(CharacterInfo info)
        {
            if (!Directory.Exists(ArchivePath)) Directory.CreateDirectory(ArchivePath);

            using var stream = File.Create(Path.Combine(ArchivePath, @$"{info.Name}{Now:_MMddyyyy_HHmmss}.MirCA"));
            using var writer = new BinaryWriter(stream);

            writer.Write(Version);
            writer.Write(CustomVersion);

            info.Save(writer);
        }

        public void SaveAccounts()
        {
            while (Saving)
                Thread.Sleep(1);

            try
            {
                using (var stream = File.Create(AccountPath + "n"))
                    SaveAccounts(stream);
                if (File.Exists(AccountPath))
                    File.Move(AccountPath, AccountPath + "o");
                File.Move(AccountPath + "n", AccountPath);
                if (File.Exists(AccountPath + "o"))
                    File.Delete(AccountPath + "o");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
        }

        private void SaveAccounts(Stream stream)
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(NextAccountID);
                writer.Write(NextCharacterID);
                writer.Write(NextUserItemID);
                writer.Write(NextHeroID);

                writer.Write(GuildList.Count);
                writer.Write(NextGuildID);
                writer.Write(HeroList.Count);
                for (var i = 0; i < HeroList.Count; i++)
                    HeroList[i].Save(writer);
                writer.Write(AccountList.Count);
                for (var i = 0; i < AccountList.Count; i++)
                    AccountList[i].Save(writer);

                writer.Write(NextAuctionID);
                writer.Write(Auctions.Count);
                foreach (var auction in Auctions)
                    auction.Save(writer);

                writer.Write(NextMailID);

                writer.Write(GameshopLog.Count);
                foreach (var item in GameshopLog)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }

                writer.Write(SavedSpawns.Count);
                foreach (var Spawn in SavedSpawns)
                {
                    var Save = new RespawnSave { RespawnIndex = Spawn.Info.RespawnIndex, NextSpawnTick = Spawn.NextSpawnTick, Spawned = Spawn.Count >= Spawn.Info.Count * SpawnMultiplier };
                    Save.Save(writer);
                }
            }
        }

        private void SaveGuilds(bool forced = false)
        {
            if (!Directory.Exists(Settings.GuildPath)) Directory.CreateDirectory(Settings.GuildPath);

            if (GuildRefreshNeeded == true) //deletes guild files and resaves with new indexing if a guild is deleted.
            {
                foreach (var guildfile in Directory.GetFiles(Settings.GuildPath, "*.mgd"))
                {
                    File.Delete(guildfile);
                }

                GuildRefreshNeeded = false;
                forced = true; //triggers a full resave of all guilds
            }

            for (var i = 0; i < GuildList.Count; i++)
            {
                if (GuildList[i].NeedSave || forced)
                {
                    GuildList[i].NeedSave = false;

					GuildObject liveGuild = Guilds.Find(g => g.Guildindex == GuildList[i].GuildIndex);
					if (liveGuild != null)
					{
						GuildList[i] = liveGuild.Info;
					}

					var mStream = new MemoryStream();
                    var writer = new BinaryWriter(mStream);
					GuildList[i].Save(writer);
                    var fStream = new FileStream(Path.Combine(Settings.GuildPath, i + ".mgdn"), FileMode.Create);
                    var data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGuildsAsync, fStream);
                }
            }
        }
        private void EndSaveGuildsAsync(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        private void SaveGoods(bool forced = false)
        {
            if (!Directory.Exists(Settings.GoodsPath)) Directory.CreateDirectory(Settings.GoodsPath);

            for (var i = 0; i < MapList.Count; i++)
            {
                SaveGoodsForMap(MapList[i], forced);
            }
        }

        public void SaveGoodsForMap(Map map, bool forced = false)
        {
            if (map == null || map.NPCs.Count == 0) return;

            for (var j = 0; j < map.NPCs.Count; j++)
            {
                var npc = map.NPCs[j];

                if (forced)
                {
                    npc.ProcessGoods(forced);
                }

                if (!npc.NeedSave) continue;

                var path = Path.Combine(Settings.GoodsPath, npc.Info.Index + ".msdn");

                var mStream = new MemoryStream();
                var writer = new BinaryWriter(mStream);
                var Temp = 9999;
                writer.Write(Temp);
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(npc.UsedGoods.Count);

                for (var k = 0; k < npc.UsedGoods.Count; k++)
                {
                    npc.UsedGoods[k].Save(writer);
                }

                var fStream = new FileStream(path, FileMode.Create);
                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveGoodsAsync, fStream);
            }
        }
        private void EndSaveGoodsAsync(IAsyncResult result)
        {
            try
            {
                var fStream = result.AsyncState as FileStream;
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        private void SaveConquests(bool forced = false)
        {
            if (!Directory.Exists(Settings.ConquestsPath)) Directory.CreateDirectory(Settings.ConquestsPath);
            for (var i = 0; i < ConquestList.Count; i++)
            {
                if (!ConquestList[i].NeedSave && !forced) continue;
                ConquestList[i].NeedSave = false;
                var mStream = new MemoryStream();
                var writer = new BinaryWriter(mStream);
                ConquestList[i].Save(writer);
                var fStream = new FileStream(Path.Combine(Settings.ConquestsPath, ConquestList[i].Info.Index + ".mcdn"), FileMode.Create);
                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveConquestsAsync, fStream);
            }
        }
        private void EndSaveConquestsAsync(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        public void BeginSaveAccounts()
        {
            if (Saving) return;

            Saving = true;


            using (var mStream = new MemoryStream())
            {
                if (File.Exists(AccountPath))
                {
                    if (!Directory.Exists(AccountsBackUpPath)) Directory.CreateDirectory(AccountsBackUpPath);
                    var fileName =
                        $"Accounts {Now.Year:0000}-{Now.Month:00}-{Now.Day:00} {Now.Hour:00}-{Now.Minute:00}-{Now.Second:00}.bak";
                    if (File.Exists(Path.Combine(AccountsBackUpPath, fileName))) File.Delete(Path.Combine(AccountsBackUpPath, fileName));
                    File.Move(AccountPath, Path.Combine(AccountsBackUpPath, fileName));
                }

                SaveAccounts(mStream);
                var fStream = new FileStream(AccountPath + "n", FileMode.Create);

                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveAccounts, fStream);
            }
        }

        private void EndSaveAccounts(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream != null)
                {
                    var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    var newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception)
            {
            }

            Saving = false;
        }

        public bool LoadDB()
        {
            lock (LoadLock)
            {
                if (!File.Exists(DatabasePath))
                {
                    SaveDB(true);
                }

                using (var stream = File.OpenRead(DatabasePath))
                using (var reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    LoadCustomVersion = reader.ReadInt32();

                    if (LoadVersion < MinVersion)
                    {
                        MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotLoadDatabaseMinSupported), LoadVersion, MinVersion));
                        return false;
                    }
                    else if (LoadVersion > Version)
                    {
                        MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotLoadDatabaseMaxSupported), LoadVersion, Version));
                        return false;
                    }

                    MapIndex = reader.ReadInt32();
                    ItemIndex = reader.ReadInt32();
                    MonsterIndex = reader.ReadInt32();

                    NPCIndex = reader.ReadInt32();
                    QuestIndex = reader.ReadInt32();

                    if (LoadVersion >= 63)
                    {
                        GameshopIndex = reader.ReadInt32();
                    }

                    if (LoadVersion >= 66)
                    {
                        ConquestIndex = reader.ReadInt32();
                    }

                    if (LoadVersion >= 68)
                        RespawnIndex = reader.ReadInt32();


                    var count = reader.ReadInt32();
                    MapInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        MapInfoList.Add(new MapInfo(reader));

                    count = reader.ReadInt32();
                    ItemInfoList.Clear();
                    for (var i = 0; i < count; i++)
                    {
                        ItemInfoList.Add(new ItemInfo(reader, LoadVersion, LoadCustomVersion));
                        if (ItemInfoList[i] != null && ItemInfoList[i].RandomStatsId < Settings.RandomItemStatsList.Count)
                        {
                            ItemInfoList[i].RandomStats = Settings.RandomItemStatsList[ItemInfoList[i].RandomStatsId];
                        }
                    }

                    //数据库文件是最新口径(已删物品不在其中), 清空防回滚抑制表
                    SuppressedItemIndexes.Clear();

                    count = reader.ReadInt32();
                    MonsterInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        MonsterInfoList.Add(new MonsterInfo(reader));

                    EnsureMonkCloneExists();

                    count = reader.ReadInt32();
                    NPCInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        NPCInfoList.Add(new NPCInfo(reader));

                    count = reader.ReadInt32();
                    QuestInfoList.Clear();
                    for (var i = 0; i < count; i++)
                        QuestInfoList.Add(new QuestInfo(reader));

                    DragonInfo = new DragonInfo(reader);
                    count = reader.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var m = new MagicInfo(reader, LoadVersion, LoadCustomVersion);
                        if (!MagicExists(m.Spell))
                            MagicInfoList.Add(m);
                    }

                    FillMagicInfoList();
                    SyncCustomSkillMagicInfo();
                    if (LoadVersion <= 70)
                        UpdateMagicInfo();

                    if (LoadVersion >= 63)
                    {
                        count = reader.ReadInt32();
                        GameShopList.Clear();
                        for (var i = 0; i < count; i++)
                        {
                            var item = new GameShopItem(reader, LoadVersion, LoadCustomVersion);
                            if (Main.BindGameShop(item))
                            {
                                GameShopList.Add(item);
                            }
                        }
                    }

                    if (LoadVersion >= 66)
                    {
                        ConquestInfoList.Clear();
                        count = reader.ReadInt32();
                        for (var i = 0; i < count; i++)
                        {
                            ConquestInfoList.Add(new ConquestInfo(reader));
                        }
                    }

                    if (LoadVersion > 67)
                        RespawnTick = new RespawnTimer(reader);
                }
                Settings.LinkGuildCreationItems(ItemInfoList);

                //计数器兜底: 历史批量导入会让头部计数器落后于列表实际最大值, 不同步则新建条目会撞号
                //(商城GIndex重复的根源; 物品/怪物等计数器存在同样的隐患)
                if (MapInfoList.Count > 0) MapIndex = Math.Max(MapIndex, MapInfoList.Max(x => x.Index));
                if (ItemInfoList.Count > 0) ItemIndex = Math.Max(ItemIndex, ItemInfoList.Max(x => x.Index));
                if (MonsterInfoList.Count > 0) MonsterIndex = Math.Max(MonsterIndex, MonsterInfoList.Max(x => x.Index));
                if (NPCInfoList.Count > 0) NPCIndex = Math.Max(NPCIndex, NPCInfoList.Max(x => x.Index));
                if (QuestInfoList.Count > 0) QuestIndex = Math.Max(QuestIndex, QuestInfoList.Max(x => x.Index));
                if (GameShopList.Count > 0) GameshopIndex = Math.Max(GameshopIndex, GameShopList.Max(x => x.GIndex));
            }

            return true;
        }

        public void LoadAccounts()
        {
            //reset ranking
            for (var i = 0; i < RankClass.Count(); i++)
            {
                if (RankClass[i] != null)
                {
                    RankClass[i].Clear();
                }
                else
                {
                    RankClass[i] = new List<RankCharacterInfo>();
                }
            }

            RankTop.Clear();

            lock (LoadLock)
            {
                if (!File.Exists(AccountPath))
                {
                    SaveAccounts();
                }

                using (var stream = File.OpenRead(AccountPath))
                using (var reader = new BinaryReader(stream))
                {
                    LoadVersion = reader.ReadInt32();
                    LoadCustomVersion = reader.ReadInt32();
                    NextAccountID = reader.ReadInt32();
                    NextCharacterID = reader.ReadInt32();
                    NextUserItemID = reader.ReadUInt64();
                    if (LoadVersion > 98)
                        NextHeroID = reader.ReadInt32();

                    GuildCount = reader.ReadInt32();
                    NextGuildID = reader.ReadInt32();

                    int count;
                    if (LoadVersion > 102)
                    {
                        count = reader.ReadInt32();

                        HeroList.Clear();

                        for (var i = 0; i < count; i++)
                            HeroList.Add(new HeroInfo(reader, LoadVersion, LoadCustomVersion));
                    }

                    count = reader.ReadInt32();

                    AccountList.Clear();
                    CharacterList.Clear();

                    int TrueAccount = 0;
                    for (var i = 0; i < count; i++)
                    {
                        AccountInfo NextAccount = new AccountInfo(reader);
                        if (i > 0 && NextAccount.Characters.Count == 0)
                            continue;
                        AccountList.Add(NextAccount);
                        CharacterList.AddRange(AccountList[TrueAccount].Characters);
                        if (LoadVersion > 98 && LoadVersion < 103)
                            AccountList[TrueAccount].Characters.ForEach(character => HeroList.AddRange(character.Heroes));
                        TrueAccount++;
                    }

                    foreach (var auction in Auctions)
                    {
                        auction.SellerInfo.AccountInfo.Auctions.Remove(auction);
                    }

                    Auctions.Clear();

                    NextAuctionID = reader.ReadUInt64();

                    count = reader.ReadInt32();
                    for (var i = 0; i < count; i++)
                    {
                        var auction = new AuctionInfo(reader, LoadVersion, LoadCustomVersion);

                        if (!BindItem(auction.Item) || !BindCharacter(auction)) continue;

                        Auctions.AddLast(auction);
                        auction.SellerInfo.AccountInfo.Auctions.AddLast(auction);
                    }

                    NextMailID = reader.ReadUInt64();

                    if (LoadVersion <= 80)
                    {
                        count = reader.ReadInt32();
                        for (var i = 0; i < count; i++)
                        {
                            var mail = new MailInfo(reader, LoadVersion, LoadCustomVersion);

                            mail.RecipientInfo = GetCharacterInfo(mail.RecipientIndex);

                            if (mail.RecipientInfo != null)
                            {
                                mail.RecipientInfo.Mail.Add(mail); //add to players inbox
                            }
                        }
                    }

                    if (LoadVersion >= 63)
                    {
                        var logCount = reader.ReadInt32();
                        for (var i = 0; i < logCount; i++)
                        {
                            GameshopLog.Add(reader.ReadInt32(), reader.ReadInt32());
                        }

                        if (ResetGS) ClearGameshopLog();
                    }

                        if (LoadVersion >= 68)
                        {
                            var saveCount = reader.ReadInt32();
                            for (var i = 0; i < saveCount; i++)
                            {
                                var saved = new RespawnSave(reader);
                                foreach (var respawn in SavedSpawns)
                                {
                                    if (respawn.Info.RespawnIndex != saved.RespawnIndex) continue;

                                    respawn.NextSpawnTick = saved.NextSpawnTick;

                                    if (!saved.Spawned || respawn.Info.Count * SpawnMultiplier <= respawn.Count)
                                    {
                                        continue;
                                    }

                                    var mobcount = respawn.Info.Count * SpawnMultiplier - respawn.Count;
                                    for (var j = 0; j < mobcount; j++)
                                    {
                                        respawn.Spawn();
                                    }
                                }
                            }
                        }
                    }

                FixDuplicateMonsterIndexes();
            }
        }

        //修复重复的怪物Index(如重导入怪物表后,代码注册的宠物条目与原有条目撞号):
        //保留每组第一条(刷怪点/地图引用先出现的条目),后面的条目换新号,并同步重映射角色存档里的宠物
        private void FixDuplicateMonsterIndexes()
        {
            if (MonsterInfoList.Count == 0) return;

            MonsterIndex = Math.Max(MonsterIndex, MonsterInfoList.Max(x => x.Index));

            var seen = new HashSet<int>();
            var remap = new Dictionary<int, int>();

            foreach (var info in MonsterInfoList)
            {
                if (seen.Add(info.Index)) continue;

                var oldIndex = info.Index;
                info.Index = ++MonsterIndex;
                remap[oldIndex] = info.Index;
                MessageQueue.Instance.Enqueue($"[怪物Index修复] {info.Name}: {oldIndex} -> {info.Index} (与前面的条目撞号)");
            }

            if (remap.Count == 0) return;

            var fixedPets = 0;
            foreach (var character in CharacterList)
            {
                foreach (var pet in character.Pets)
                {
                    if (!remap.TryGetValue(pet.MonsterIndex, out var newIndex)) continue;

                    pet.MonsterIndex = newIndex;
                    fixedPets++;
                }
            }

            MessageQueue.Instance.Enqueue($"[怪物Index修复] 共重分配 {remap.Count} 条怪物,重映射 {fixedPets} 条存档宠物");
        }

        public void LoadGuilds()
        {
            lock (LoadLock)
            {
                var count = 0;

                GuildList.Clear();

                for (var i = 0; i < GuildCount; i++)
                {
                    GuildInfo guildInfo;
                    if (!File.Exists(Path.Combine(Settings.GuildPath, i + ".mgd"))) continue;

                    using (var stream = File.OpenRead(Path.Combine(Settings.GuildPath, i + ".mgd")))
                    {
                        using var reader = new BinaryReader(stream);
                        guildInfo = new GuildInfo(reader);
                    }

                    GuildList.Add(guildInfo);

                    new GuildObject(guildInfo);

                    count++;
                }

                if (count != GuildCount) GuildCount = count;
            }
        }

        public void LoadConquests()
        {
            lock (LoadLock)
            {
                Conquests.Clear();
                ConquestList.Clear();

                for (var i = 0; i < ConquestInfoList.Count; i++)
                {
                    ConquestObject newConquest;
                    ConquestGuildInfo conquestGuildInfo;
                    var tempMap = GetMap(ConquestInfoList[i].MapIndex);

                    if (tempMap == null) continue;

                    if (File.Exists(Path.Combine(Settings.ConquestsPath, ConquestInfoList[i].Index + ".mcd")))
                    {
                        using (var stream = File.OpenRead(Path.Combine(Settings.ConquestsPath, ConquestInfoList[i].Index + ".mcd")))
                        {
                            using var reader = new BinaryReader(stream);
                            conquestGuildInfo = new ConquestGuildInfo(reader) { Info = ConquestInfoList[i] };
                        }

                        newConquest = new ConquestObject(conquestGuildInfo)
                        {
                            ConquestMap = tempMap
                        };

                        for (var k = 0; k < Guilds.Count; k++)
                        {
                            if (conquestGuildInfo.Owner != Guilds[k].Guildindex) continue;
                            newConquest.Guild = Guilds[k];
                            Guilds[k].Conquest = newConquest;
                        }
                    }
                    else
                    {
                        conquestGuildInfo = new ConquestGuildInfo { Info = ConquestInfoList[i], NeedSave = true };
                        newConquest = new ConquestObject(conquestGuildInfo)
                        {
                            ConquestMap = tempMap
                        };
                    }

                    ConquestList.Add(conquestGuildInfo);
                    Conquests.Add(newConquest);
                    tempMap.Conquest.Add(newConquest);

                    newConquest.Bind();
                }
            }
        }

        private void LoadGTInfo()
        {
            foreach (var gt in GTMapList)
            {
                var Guild = GuildList.FirstOrDefault(x => x.GTIndex == gt.Index);
                if (Guild != null)
                {
                    gt.Owner = Guild.Name;
                    if (Guild.Ranks.Count > 0 && Guild.Ranks[0] != null && Guild.Ranks[0].Members.Count > 0 && Guild.Ranks[0].Members[0] != null)
                        gt.Leader = Guild.Ranks[0].Members[0].Name;
                    gt.Price = 0;
                    gt.Days = (Now - Guild.GTRent).Days;
                }
            }
        }

        public void LoadDisabledChars()
        {
            DisabledCharNames.Clear();

            var path = Path.Combine(Settings.EnvirPath, "DisabledChars.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    DisabledCharNames.Add(lines[i].ToUpper());
                }
            }
        }

        public void LoadLineMessages()
        {
            LineMessages.Clear();

            var path = Path.Combine(Settings.EnvirPath, "LineMessage.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    LineMessages.Add(lines[i]);
                }
            }
        }

        private bool BindCharacter(AuctionInfo auction)
        {
            bool bound = false;

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (CharacterList[i].Index == auction.SellerIndex)
                {
                    auction.SellerInfo = CharacterList[i];
                    bound = true;
                }

                else if (CharacterList[i].Index == auction.CurrentBuyerIndex)
                {
                    auction.CurrentBuyerInfo = CharacterList[i];
                    bound = true;
                }
            }

            return bound;
        }

        //最近一次启动失败原因(CanStartEnvir), null=启动成功或从未失败; SMain面板轮询后弹窗展示
        public string LastStartError;

        public void Start()
        {
            if (Running || _thread != null) return;

            Running = true;

            _thread = new Thread(WorkLoop) { IsBackground = true };
            _thread.Start();
        }

        public void Stop()
        {
            Running = false;

            lock (_locker)
            {
                // changing a blocking condition. (this makes the threads wake up!)
                Monitor.PulseAll(_locker);
            }

            //simply intterupt all the mob threads if they are running (will give an invisible error on them but fastest way of getting rid of them on shutdowns)
            for (var i = 1; i < MobThreading.Length; i++)
            {
                if (MobThreads[i] != null)
                {
                    MobThreads[i].EndTime = Time + 9999;
                }
                if (MobThreading[i] != null &&
                    MobThreading[i].ThreadState != System.Threading.ThreadState.Stopped && MobThreading[i].ThreadState != System.Threading.ThreadState.Unstarted)
                {
                    MobThreading[i].Interrupt();
                }
            }

            http?.Stop();

            while (_thread != null)
                Thread.Sleep(1);
        }

        public void Reboot()
        {
            new Thread(() =>
            {
                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.ServerRebooting));
                Stop();

                try
                {
                    //重启 = 完整重读 Setup.ini(语言/倍率/开关/端口等立即生效), 不再需要关闭整个程序
                    Settings.Load();
                    MessageQueue.Enqueue("配置(Setup.ini)已重新加载。");
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue("配置重读失败, 沿用当前配置: " + ex.Message);
                }

                Start();
            }).Start();
        }

        public void UpdateIPBlock(string ipAddress, TimeSpan value)
        {
            IPBlocks[ipAddress] = Now.Add(value);
        }

        private void StartEnvir()
        {
            //战场系统: 重启环境时重置状态并读取配置(Configs\BattleField.ini)
            BattleField.Reset();
            BattleField.LoadSettings();

            Players.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            MapList.Clear();
            MapUnloadQueue.Clear();
            GTMapList.Clear();
            GameshopLog.Clear();
            CustomCommands.Clear();
            Heroes.Clear();
            MonsterCount = 0;

            LoadDB();

            BuffInfoList.Clear();
            foreach (var buff in BuffInfo.Load())
            {
                BuffInfoList.Add(buff);
            }

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.BuffsLoaded), BuffInfoList.Count));

            RecipeInfoList.Clear();
            foreach (var recipe in Directory.GetFiles(Settings.RecipePath, "*.txt")
                         .Select(path => Path.GetFileNameWithoutExtension(path))
                         .ToArray())
            {
                RecipeInfoList.Add(new RecipeInfo(recipe));
            }

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.RecipesLoaded), RecipeInfoList.Count));

            for (var i = 0; i < MapInfoList.Count; i++)
            {
                for (var j = 0; j < MapInfoList[i].SafeZones.Count; j++)
                {
                    if (MapInfoList[i].SafeZones[j].StartPoint)
                        StartPoints.Add(MapInfoList[i].SafeZones[j]);
                }
            }

            for (var i = 0; i < MapInfoList.Count; i++)
            {
                MapInfo info = MapInfoList[i];

                if (Settings.MapUnloadEnabled && !info.GT && !info.SafeZones.Any(sz => sz.StartPoint))
                    continue;

                // Call CreateMap(), which adds the map to Envir.MapList
                info.CreateMap();

                // Fetch the created map from Envir.MapList
                Map map = MapList.FirstOrDefault(m => m.Info == info);

                if (map != null)
                {
                    if (info.GT)
                    {
                        GTMap gt = GTMapList.FirstOrDefault(x => x.Index == info.GTIndex);
                        if (gt != null)
                        {
                            gt.Maps.Add(map);
                        }
                        else
                        {
                            var GT = new GTMap()
                            {
                                Index = info.GTIndex,
                                Name = info.Title,
                                Price = Settings.BuyGTGold,
                                Days = 0,
                                Begin = 0,
                                Leader = "None",
                                Owner = "None",
                            };
                            GT.Maps.Add(map);

                            GTMapList.Add(GT);
                        }
                    }
                }
            }

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.MapsLoaded), MapList.Count));

            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i].StartItem)
                {
                    StartItems.Add(ItemInfoList[i]);
                }
            }

            ReloadDrops();

            ReloadTalents();

            LoadDisabledChars();
            LoadLineMessages();

            if (DragonInfo.Enabled)
            {
                DragonSystem = new Dragon(DragonInfo);
                if (DragonSystem != null)
                {
                    if (DragonSystem.Load()) DragonSystem.Info.LoadDrops();
                }

                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.DragonLoaded));
            }

            FieldBossInfoList = FieldBossLoader.Load();

            if (FieldBossInfoList.Count > 0)
            {
                FieldBossSystem = new FieldBossSystem(FieldBossInfoList);
                MessageQueue.Enqueue(string.Format("野外Boss系统已加载, 共{0}条配置。", FieldBossInfoList.Count));
            }
            else
            {
                FieldBossSystem = null;
            }

            DefaultNPC = NPCScript.GetOrAdd((uint)Random.Next(1000000, 1999999), Settings.DefaultNPCFilename, NPCScriptType.AutoPlayer);
            MonsterNPC = NPCScript.GetOrAdd((uint)Random.Next(2000000, 2999999), Settings.MonsterNPCFilename, NPCScriptType.AutoMonster);
            RobotNPC = NPCScript.GetOrAdd((uint)Random.Next(3000000, 3999999), Settings.RobotNPCFilename, NPCScriptType.Robot);

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.EnvirStarted));           

        }

        
        private void StartNetwork()
        {
            Connections.Clear();

            LoadAccounts();

            LoadGuilds();

            LoadConquests();
            LoadGTInfo();

            _listener = new TcpListener(IPAddress.Parse(Settings.IPAddress), Settings.Port);
            _listener.Start();
            _listener.BeginAcceptTcpClient(Connection, null);

            if (StatusPortEnabled)
            {
                _StatusPort = new TcpListener(IPAddress.Parse(Settings.IPAddress), 3000);
                _StatusPort.Start();
                _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NetworkStarted));
        }

        private void StopEnvir()
        {
            SaveGoods(true);

            MapList.Clear();
            StartPoints.Clear();
            StartItems.Clear();
            Objects.Clear();
            Players.Clear();
            Heroes.Clear();
            GTMapList.Clear();

            CleanUp();

            GC.Collect();

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.EnvirStopped));
        }
        private void StopNetwork()
        {
            _listener.Stop();
            lock (Connections)
            {
                for (var i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].SendDisconnect(0);
            }

            if (StatusPortEnabled)
            {
                _StatusPort.Stop();
                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].SendDisconnect();
            }

            var expire = Time + 5000;

            while (Connections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (var i = Connections.Count - 1; i >= 0; i--)
                    Connections[i].Process();

                Thread.Sleep(1);
            }


            Connections.Clear();

            expire = Time + 10000;
            while (StatusConnections.Count != 0 && Stopwatch.ElapsedMilliseconds < expire)
            {
                Time = Stopwatch.ElapsedMilliseconds;

                for (var i = StatusConnections.Count - 1; i >= 0; i--)
                    StatusConnections[i].Process();

                Thread.Sleep(1);
            }


            StatusConnections.Clear();
            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NetworkStopped));
        }

        public void QueueForFree(Map map)
        {
            MapUnloadQueue.Add(map);
        }

        private void FreeQueuedMaps()
        {
            if (MapUnloadQueue.Count == 0) return;

            for (var i = 0; i < MapUnloadQueue.Count; i++)
                MapUnloadQueue[i].FreeMemory();

            MapUnloadQueue.Clear();
        }

        private bool MapIsProtected(Map map)
        {
            if (map.Info.GT) return true;
            if (map.Info.SafeZones.Any(sz => sz.StartPoint)) return true;

            for (var i = 0; i < ConquestInfoList.Count; i++)
            {
                var info = ConquestInfoList[i];
                if (info.MapIndex == map.Info.Index || info.PalaceIndex == map.Info.Index) return true;
                if (info.ExtraMaps.Contains(map.Info.Index)) return true;
            }

            if (DragonSystem != null && DragonSystem.Info != null &&
                string.Equals(DragonSystem.Info.MapFileName, map.Info.FileName, StringComparison.CurrentCultureIgnoreCase))
                return true;

            return false;
        }

        private void UnloadIdleMaps()
        {
            FreeQueuedMaps();

            long threshold = Time - (long)Settings.MapUnloadDelay * Settings.Minute;

            for (var i = MapList.Count - 1; i >= 0; i--)
            {
                Map map = MapList[i];

                if (MapIsProtected(map)) continue;
                if (map.Players.Count > 0 || map.Heroes.Count > 0) continue;
                if (map.LastActiveTime > threshold) continue;

                map.Unload();
            }
        }

        private void CleanUp()
        {
            for (var i = 0; i < CharacterList.Count; i++)
            {                var info = CharacterList[i];

                if (info.Deleted)
                {
                    #region Mentor Cleanup
                    if (info.Mentor > 0)
                    {
                        var mentor = GetCharacterInfo(info.Mentor);

                        if (mentor != null)
                        {
                            mentor.Mentor = 0;
                            mentor.MentorExp = 0;
                            mentor.IsMentor = false;
                        }

                        info.Mentor = 0;
                        info.MentorExp = 0;
                        info.IsMentor = false;
                    }
                    #endregion

                    #region Marriage Cleanup
                    if (info.Married > 0)
                    {
                        var Lover = GetCharacterInfo(info.Married);

                        info.Married = 0;
                        info.MarriedDate = Now;

                        Lover.Married = 0;
                        Lover.MarriedDate = Now;
                        if (Lover.Equipment[(int)EquipmentSlot.RingL] != null)
                            Lover.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;
                    }
                    #endregion
                }

                if (info.Mail.Count > Settings.MailCapacity)
                {
                    for (var j = info.Mail.Count - 1 - (int)Settings.MailCapacity; j >= 0; j--)
                    {
                        if (info.Mail[j].DateOpened > Now && info.Mail[j].Collected && info.Mail[j].Items.Count == 0 && info.Mail[j].Gold == 0)
                        {
                            info.Mail.Remove(info.Mail[j]);
                        }
                    }
                }
            }
        }

        private void Connection(IAsyncResult result)
        {
            try
            {
                if (!Running || !_listener.Server.IsBound) return;
            }
            catch (Exception e)
            {
                MessageQueue.Enqueue(e.ToString());
            }

            try
            {
                var tempTcpClient = _listener.EndAcceptTcpClient(result);

                bool connected = false;
                var ipAddress = tempTcpClient.Client.RemoteEndPoint.ToString().Split(':')[0];

                if (!IPBlocks.TryGetValue(ipAddress, out DateTime banDate) || banDate < Now)
                {
                    int count = 0;

                    for (int i = 0; i < Connections.Count; i++)
                    {
                        var connection = Connections[i];

                        if (!connection.Connected || connection.IPAddress != ipAddress)
                            continue;

                        count++;
                    }

                    if (count >= Settings.MaxIP)
                    {
                        UpdateIPBlock(ipAddress, TimeSpan.FromSeconds(Settings.IPBlockSeconds));

                        MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.IpAddressDisconnectedTooManyConnections), ipAddress));
                    }
                    else
                    {
                        var tempConnection = new MirConnection(++_sessionID, tempTcpClient);
                        if (tempConnection.Connected)
                        {
                            connected = true;
                            lock (Connections)
                                Connections.Add(tempConnection);
                        }
                    }
                }

                if (!connected)
                    tempTcpClient.Close();
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
            finally
            {
                while (Connections.Count >= Settings.MaxUser)
                    Thread.Sleep(1);

                if (Running && _listener.Server.IsBound)
                    _listener.BeginAcceptTcpClient(Connection, null);
            }
        }

        private void StatusConnection(IAsyncResult result)
        {
            if (!Running || !_StatusPort.Server.IsBound) return;

            try
            {
                var tempTcpClient = _StatusPort.EndAcceptTcpClient(result);
                lock (StatusConnections)
                    StatusConnections.Add(new MirStatusConnection(tempTcpClient));
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
            finally
            {
                while (StatusConnections.Count >= 5) //dont allow to many status port connections it's just an abuse thing
                    Thread.Sleep(1);

                if (Running && _StatusPort.Server.IsBound)
                    _StatusPort.BeginAcceptTcpClient(StatusConnection, null);
            }
        }

        public void NewAccount(ClientPackets.NewAccount p, MirConnection c)
        {
            if (!Settings.AllowNewAccount)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 0 });
                return;
            }


            if (ConnectionLogs.TryGetValue(c.IPAddress, out MirConnectionLog currentlog))
            {
                if (currentlog.AccountsMade.Count > 2)
                {
                    IPBlocks[c.IPAddress] = Now.AddHours(24);
                    c.Enqueue(new ServerPackets.NewAccount { Result = 0 });
                    return;
                }
                currentlog.AccountsMade.Add(Time);
                for (int i = 0; i < currentlog.AccountsMade.Count; i++)
                {
                    if ((currentlog.AccountsMade[i] + 60 * 60 * 1000) < Time)
                    {
                        currentlog.AccountsMade.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                ConnectionLogs[c.IPAddress] = new MirConnectionLog() { IPAddress = c.IPAddress };
            }


            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 2 });
                return;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 3 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 4 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 5 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 6 });
                return;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    c.Enqueue(new ServerPackets.NewAccount { Result = 7 });
                    return;
                }

                AccountList.Add(new AccountInfo(p) { Index = ++NextAccountID, CreationIP = c.IPAddress });


                c.Enqueue(new ServerPackets.NewAccount { Result = 8 });
            }
        }

        public int HTTPNewAccount(ClientPackets.NewAccount p, string ip)
        {
            if (!Settings.AllowNewAccount)
            {
                return 0;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                return 1;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                return 2;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                return 3;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                return 4;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                return 5;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                return 6;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    return 7;
                }

                AccountList.Add(new AccountInfo(p) { Index = ++NextAccountID, CreationIP = ip });
                return 8;
            }
        }

        public void ChangePassword(ClientPackets.ChangePassword p, MirConnection c)
        {
            if (!Settings.AllowChangePassword)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.CurrentPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 2 });
                return;
            }

            if (!PasswordReg.IsMatch(p.NewPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 3 });
                return;
            }

            var account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 4 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.ChangePasswordBanned { Reason = account.BanReason, ExpiryDate = account.ExpiryDate });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            p.CurrentPassword = Utils.Crypto.HashPassword(p.CurrentPassword, account.Salt);
            if (string.CompareOrdinal(account.Password, p.CurrentPassword) != 0)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 5 });
                return;
            }

            account.Password = p.NewPassword;
            account.RequirePasswordChange = false;
            c.Enqueue(new ServerPackets.ChangePassword { Result = 6 });
        }
        public void Login(ClientPackets.Login p, MirConnection c)
        {
            if (!Settings.AllowLogin)
            {
                c.Enqueue(new ServerPackets.Login { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.Login { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.Login { Result = 2 });
                return;
            }
            var account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.Login { Result = 3 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            p.Password = Utils.Crypto.HashPassword(p.Password, account.Salt);

            if (string.CompareOrdinal(account.Password, p.Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TooManyWrongLoginAttempts);
                    account.ExpiryDate = Now.AddMinutes(2);

                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }

                c.Enqueue(new ServerPackets.Login { Result = 4 });
                return;
            }
            account.WrongPasswordCount = 0;

            if (account.RequirePasswordChange)
            {
                c.Enqueue(new ServerPackets.Login { Result = 5 });
                return;
            }

            lock (AccountLock)
            {
                account.Connection?.SendDisconnect(1);

                account.Connection = c;
            }

            c.Account = account;
            c.Stage = GameStage.Select;

            account.LastDate = Now;
            account.LastIP = c.IPAddress;

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.UserLoggedIn), account.Connection.SessionID, account.Connection.IPAddress));
            c.Enqueue(new ServerPackets.LoginSuccess { Characters = account.GetSelectInfo() });
            c.Enqueue(new ServerPackets.ClassAvailability
            {
                AllowedClasses = (byte)((Settings.AllowCreateAssassin ? 1 << (byte)MirClass.Assassin : 0) |
                                        (Settings.AllowCreateArcher ? 1 << (byte)MirClass.Archer : 0) |
                                        (Settings.AllowCreateMonk ? 1 << (byte)MirClass.Monk : 0) |
                                        (1 << (byte)MirClass.Warrior) | (1 << (byte)MirClass.Wizard) | (1 << (byte)MirClass.Taoist))
            });
        }

        public int HTTPLogin(string AccountID, string Password)
        {
            if (!Settings.AllowLogin)
            {
                return 0;
            }

            if (!AccountIDReg.IsMatch(AccountID))
            {
                return 1;
            }

            if (!PasswordReg.IsMatch(Password))
            {
                return 2;
            }

            var account = GetAccount(AccountID);

            if (account == null)
            {
                return 3;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    return 4;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;
            if (string.CompareOrdinal(account.Password, Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TooManyWrongLoginAttempts);
                    account.ExpiryDate = Now.AddMinutes(2);
                    return 5;
                }
                return 6;
            }
            account.WrongPasswordCount = 0;
            return 7;
        }

        public void NewCharacter(ClientPackets.NewCharacter p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewCharacter)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 0 });
                return;
            }

            if (ConnectionLogs.TryGetValue(c.IPAddress, out MirConnectionLog currentlog))
            {
                if (currentlog.CharactersMade.Count > 4)
                {
                    IPBlocks[c.IPAddress] = Now.AddHours(24);
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 0 });
                    return;
                }
                currentlog.CharactersMade.Add(Time);
                for (int i = 0; i < currentlog.CharactersMade.Count; i++)
                {
                    if ((currentlog.CharactersMade[i] + 60 * 60 * 1000) < Time)
                    {
                        currentlog.CharactersMade.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                ConnectionLogs[c.IPAddress] = new MirConnectionLog() { IPAddress = c.IPAddress };
            }


            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (!IsGm && DisabledCharNames.Contains(p.Name.ToUpper()))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (p.Gender != MirGender.Male && p.Gender != MirGender.Female)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 2 });
                return;
            }

            if (p.Class != MirClass.Warrior && p.Class != MirClass.Wizard && p.Class != MirClass.Taoist &&
                p.Class != MirClass.Assassin && p.Class != MirClass.Archer && p.Class != MirClass.Monk)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            if (p.Class == MirClass.Assassin && !Settings.AllowCreateAssassin ||
                p.Class == MirClass.Archer && !Settings.AllowCreateArcher ||
                p.Class == MirClass.Monk && !Settings.AllowCreateMonk)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            if (p.Class == MirClass.Monk && p.Gender == MirGender.Female)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 2 });
                return;
            }

            var count = 0;

            for (var i = 0; i < c.Account.Characters.Count; i++)
            {
                if (c.Account.Characters[i].Deleted) continue;

                if (++count >= Globals.MaxCharacterCount)
                {
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 4 });
                    return;
                }
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 5 });
                    return;
                }

                var info = new CharacterInfo(p, c) { Index = ++NextCharacterID, AccountInfo = c.Account };

                c.Account.Characters.Add(info);
                CharacterList.Add(info);

                c.Enqueue(new ServerPackets.NewCharacterSuccess { CharInfo = info.ToSelectInfo() });
            }
        }

        public bool CanCreateHero(ClientPackets.NewHero p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewHero)
            {
                c.Enqueue(new S.NewHero { Result = 0 });
                return false;
            }

            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new S.NewHero { Result = 1 });
                return false;
            }

            if (!IsGm && DisabledCharNames.Contains(p.Name.ToUpper()))
            {
                c.Enqueue(new S.NewHero { Result = 1 });
                return false;
            }

            if (p.Gender != MirGender.Male && p.Gender != MirGender.Female)
            {
                c.Enqueue(new S.NewHero { Result = 2 });
                return false;
            }

            if (p.Class != MirClass.Warrior && p.Class != MirClass.Wizard && p.Class != MirClass.Taoist && p.Class != MirClass.Assassin && p.Class != MirClass.Archer)
            {
                c.Enqueue(new S.NewHero { Result = 3 });
                return false;
            }

            if (p.Class == MirClass.Warrior && !Settings.Hero_CanCreateClass[0] || p.Class == MirClass.Wizard && !Settings.Hero_CanCreateClass[1] || p.Class == MirClass.Taoist && !Settings.Hero_CanCreateClass[2] || p.Class == MirClass.Assassin && !Settings.Hero_CanCreateClass[3] || p.Class == MirClass.Archer && !Settings.Hero_CanCreateClass[4])
            {
                c.Enqueue(new S.NewHero { Result = 3 });
                return false;
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new S.NewHero { Result = 5 });
                    return false;
                }

                for (int i = 0; i < HeroList.Count; i++)
                {
                    if (HeroList[i].Deleted) continue;
                    if (string.Equals(HeroList[i].Name, p.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        c.Enqueue(new S.NewHero { Result = 5 });
                        return false;
                    }
                }
            }

            return true;
        }

        public bool AccountExists(string accountID)
        {
            for (var i = 0; i < AccountList.Count; i++)
                if (string.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public bool CharacterExists(string name)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (string.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public List<CharacterInfo> MatchPlayer(string PlayerID, bool match = false)
        {
            if (string.IsNullOrEmpty(PlayerID)) return new List<CharacterInfo>(CharacterList);

            List<CharacterInfo> list = new List<CharacterInfo>();

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (match)
                {
                    if (CharacterList[i].Name.Equals(PlayerID, StringComparison.OrdinalIgnoreCase))
                        list.Add(CharacterList[i]);
                }
                else
                {
                    if (CharacterList[i].Name.IndexOf(PlayerID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(CharacterList[i]);
                }
            }

            return list;
        }
        public List<CharacterInfo> MatchPlayerbyItem(string itemIdentifier, bool match = false)
        {
            List<CharacterInfo> list = new List<CharacterInfo>();

            bool isNumeric = ulong.TryParse(itemIdentifier, out ulong itemId);

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (match)
                {
                    foreach (var item in CharacterList[i].Inventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].AccountInfo.Storage)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].QuestInventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].Equipment)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var mail in CharacterList[i].Mail)
                        foreach (var item in mail.Items)
                            if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                                list.Add(CharacterList[i]);
                }
                else
                {
                    foreach (var item in CharacterList[i].Inventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].QuestInventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].Equipment)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].AccountInfo.Storage)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);
                }
            }

            return list;
        }

        public AccountInfo GetAccount(string accountID)
        {
            for (var i = 0; i < AccountList.Count; i++)
                if (string.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                    return AccountList[i];

            return null;
        }

        public AccountInfo GetAccountByCharacter(string name)
        {
            for (var i = 0; i < AccountList.Count; i++)
            {
                for (int j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (string.Compare(AccountList[i].Characters[j].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                        return AccountList[i];
                }
            }

            return null;
        }

        public List<AccountInfo> MatchAccounts(string accountID, bool match = false)
        {
            if (string.IsNullOrEmpty(accountID)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                if (match)
                {
                    if (AccountList[i].AccountID.Equals(accountID, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (AccountList[i].AccountID.IndexOf(accountID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByPlayer(string playerName, bool match = false)
        {
            if (string.IsNullOrEmpty(playerName)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                for (var j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (match)
                    {
                        if (AccountList[i].Characters[j].Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                            list.Add(AccountList[i]);
                    }
                    else
                    {
                        if (AccountList[i].Characters[j].Name.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0)
                            list.Add(AccountList[i]);
                    }
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByIP(string ipAddress, bool matchLastIP = false, bool match = false)
        {
            if (string.IsNullOrEmpty(ipAddress)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                string ipToMatch = matchLastIP ? AccountList[i].LastIP : AccountList[i].CreationIP;

                if (match)
                {
                    if (ipToMatch.Equals(ipAddress, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (ipToMatch.IndexOf(ipAddress, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }


        public void CreateAccountInfo()
        {
            AccountList.Add(new AccountInfo { Index = ++NextAccountID });
        }
        public void CreateMapInfo()
        {
            MapInfoList.Add(new MapInfo { Index = ++MapIndex });
        }
        public void CreateItemInfo(ItemType type = ItemType.Nothing)
        {
            ItemInfoList.Add(new ItemInfo { Index = ++ItemIndex, Type = type, RandomStatsId = 255 });
        }
        public void CreateMonsterInfo()
        {
            MonsterInfoList.Add(new MonsterInfo { Index = ++MonsterIndex });
        }
        public void CreateNPCInfo()
        {
            NPCInfoList.Add(new NPCInfo { Index = ++NPCIndex });
        }
        public void CreateQuestInfo()
        {
            QuestInfoList.Add(new QuestInfo { Index = ++QuestIndex });
        }

        public void AddToGameShop(ItemInfo Info)
        {
            GameShopList.Add(new GameShopItem
            {
                GIndex = ++GameshopIndex,
                GoldPrice = (uint)(1000 * Settings.CredxGold),
                CreditPrice = 1000,
                ItemIndex = Info.Index,
                Info = Info,
                Date = Now,
                Class = "All",
                Category = Info.Type.ToString(),
                CanBuyCredit = true, //默认两种货币都可购买, 否则上架后无人能买(可在商城编辑器里再调整)
                CanBuyGold = true
            });
        }

        public void Remove(MapInfo info)
        {
            MapInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(ItemInfo info)
        {
            ItemInfoList.Remove(info);
        }
        public void Remove(MonsterInfo info)
        {
            MonsterInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(NPCInfo info)
        {
            NPCInfoList.Remove(info);
            //Desync all objects\
        }
        public void Remove(QuestInfo info)
        {
            QuestInfoList.Remove(info);
            //Desync all objects\
        }

        public void Remove(GameShopItem info)
        {
            GameShopList.Remove(info);

            if (GameShopList.Count == 0)
            {
                GameshopIndex = 0;
            }

            //Desync all objects\
        }

        public UserItem CreateFreshItem(ItemInfo info)
        {
            var item = new UserItem(info)
            {
                UniqueID = ++NextUserItemID,
                CurrentDura = info.Durability,
                MaxDura = info.Durability
            };

            UpdateItemExpiry(item);

            return item;
        }
        public UserItem CreateDropItem(int index)
        {
            return CreateDropItem(GetItemInfo(index));
        }
        public UserItem CreateDropItem(ItemInfo info)
        {
            if (info == null) return null;

            var item = new UserItem(info)
            {
                UniqueID = ++NextUserItemID,
                MaxDura = info.Durability,
                CurrentDura = (ushort)Math.Min(info.Durability, Random.Next(info.Durability) + 1000)
            };

            UpgradeItem(item);

            UpdateItemExpiry(item);

            if (!info.NeedIdentify) item.Identified = true;
            return item;
        }

        public UserItem CreateShopItem(ItemInfo info, ulong id)
        {
            if (info == null) return null;

            var item = new UserItem(info)
            {
                UniqueID = id,
                CurrentDura = info.Durability,
                MaxDura = info.Durability,
                IsShopItem = true,
            };

            return item;
        }

        public void UpdateItemExpiry(UserItem item)
        {
            var expiryInfo = new ExpireInfo();

            var r = new Regex(@"\[(.*?)\]");
            var expiryMatch = r.Match(item.Info.Name);

            if (expiryMatch.Success)
            {
                var parameter = expiryMatch.Groups[1].Captures[0].Value;

                var numAlpha = new Regex("(?<Numeric>[0-9]*)(?<Alpha>[a-zA-Z]*)");
                var match = numAlpha.Match(parameter);

                var alpha = match.Groups["Alpha"].Value;
                var num = 0;

                int.TryParse(match.Groups["Numeric"].Value, out num);

                switch (alpha)
                {
                    case "m":
                        expiryInfo.ExpiryDate = Now.AddMinutes(num);
                        break;
                    case "h":
                        expiryInfo.ExpiryDate = Now.AddHours(num);
                        break;
                    case "d":
                        expiryInfo.ExpiryDate = Now.AddDays(num);
                        break;
                    case "M":
                        expiryInfo.ExpiryDate = Now.AddMonths(num);
                        break;
                    case "y":
                        expiryInfo.ExpiryDate = Now.AddYears(num);
                        break;
                    default:
                        expiryInfo.ExpiryDate = DateTime.MaxValue;
                        break;
                }

                item.ExpireInfo = expiryInfo;
            }
        }

        public void UpgradeItem(UserItem item)
        {
            if (item.Info.RandomStats == null) return;
            var stat = item.Info.RandomStats;
            if (stat.MaxDuraChance > 0 && Random.Next(stat.MaxDuraChance) == 0)
            {
                var dura = RandomomRange(stat.MaxDuraMaxStat, stat.MaxDuraStatChance);
                item.MaxDura = (ushort)Math.Min(ushort.MaxValue, item.MaxDura + dura * 1000);
                item.CurrentDura = (ushort)Math.Min(ushort.MaxValue, item.CurrentDura + dura * 1000);
            }

            if (stat.MaxAcChance > 0 && Random.Next(stat.MaxAcChance) == 0) item.AddedStats[Stat.MaxAC] = (byte)(RandomomRange(stat.MaxAcMaxStat - 1, stat.MaxAcStatChance) + 1);
            if (stat.MaxMacChance > 0 && Random.Next(stat.MaxMacChance) == 0) item.AddedStats[Stat.MaxMAC] = (byte)(RandomomRange(stat.MaxMacMaxStat - 1, stat.MaxMacStatChance) + 1);
            if (stat.MaxDcChance > 0 && Random.Next(stat.MaxDcChance) == 0) item.AddedStats[Stat.MaxDC] = (byte)(RandomomRange(stat.MaxDcMaxStat - 1, stat.MaxDcStatChance) + 1);
            if (stat.MaxMcChance > 0 && Random.Next(stat.MaxMcChance) == 0) item.AddedStats[Stat.MaxMC] = (byte)(RandomomRange(stat.MaxMcMaxStat - 1, stat.MaxMcStatChance) + 1);
            if (stat.MaxScChance > 0 && Random.Next(stat.MaxScChance) == 0) item.AddedStats[Stat.MaxSC] = (byte)(RandomomRange(stat.MaxScMaxStat - 1, stat.MaxScStatChance) + 1);
            if (stat.AccuracyChance > 0 && Random.Next(stat.AccuracyChance) == 0) item.AddedStats[Stat.Accuracy] = (byte)(RandomomRange(stat.AccuracyMaxStat - 1, stat.AccuracyStatChance) + 1);
            if (stat.AgilityChance > 0 && Random.Next(stat.AgilityChance) == 0) item.AddedStats[Stat.Agility] = (byte)(RandomomRange(stat.AgilityMaxStat - 1, stat.AgilityStatChance) + 1);
            if (stat.HpChance > 0 && Random.Next(stat.HpChance) == 0) item.AddedStats[Stat.HP] = (byte)(RandomomRange(stat.HpMaxStat - 1, stat.HpStatChance) + 1);
            if (stat.MpChance > 0 && Random.Next(stat.MpChance) == 0) item.AddedStats[Stat.MP] = (byte)(RandomomRange(stat.MpMaxStat - 1, stat.MpStatChance) + 1);
            if (stat.StrongChance > 0 && Random.Next(stat.StrongChance) == 0) item.AddedStats[Stat.Strong] = (byte)(RandomomRange(stat.StrongMaxStat - 1, stat.StrongStatChance) + 1);
            if (stat.MagicResistChance > 0 && Random.Next(stat.MagicResistChance) == 0) item.AddedStats[Stat.MagicResist] = (byte)(RandomomRange(stat.MagicResistMaxStat - 1, stat.MagicResistStatChance) + 1);
            if (stat.PoisonResistChance > 0 && Random.Next(stat.PoisonResistChance) == 0) item.AddedStats[Stat.PoisonResist] = (byte)(RandomomRange(stat.PoisonResistMaxStat - 1, stat.PoisonResistStatChance) + 1);
            if (stat.HpRecovChance > 0 && Random.Next(stat.HpRecovChance) == 0) item.AddedStats[Stat.HealthRecovery] = (byte)(RandomomRange(stat.HpRecovMaxStat - 1, stat.HpRecovStatChance) + 1);
            if (stat.MpRecovChance > 0 && Random.Next(stat.MpRecovChance) == 0) item.AddedStats[Stat.SpellRecovery] = (byte)(RandomomRange(stat.MpRecovMaxStat - 1, stat.MpRecovStatChance) + 1);
            if (stat.PoisonRecovChance > 0 && Random.Next(stat.PoisonRecovChance) == 0) item.AddedStats[Stat.PoisonRecovery] = (byte)(RandomomRange(stat.PoisonRecovMaxStat - 1, stat.PoisonRecovStatChance) + 1);
            if (stat.CriticalRateChance > 0 && Random.Next(stat.CriticalRateChance) == 0) item.AddedStats[Stat.CriticalRate] = (byte)(RandomomRange(stat.CriticalRateMaxStat - 1, stat.CriticalRateStatChance) + 1);
            if (stat.CriticalDamageChance > 0 && Random.Next(stat.CriticalDamageChance) == 0) item.AddedStats[Stat.CriticalDamage] = (byte)(RandomomRange(stat.CriticalDamageMaxStat - 1, stat.CriticalDamageStatChance) + 1);
            if (stat.FreezeChance > 0 && Random.Next(stat.FreezeChance) == 0) item.AddedStats[Stat.Freezing] = (byte)(RandomomRange(stat.FreezeMaxStat - 1, stat.FreezeStatChance) + 1);
            if (stat.PoisonAttackChance > 0 && Random.Next(stat.PoisonAttackChance) == 0) item.AddedStats[Stat.PoisonAttack] = (byte)(RandomomRange(stat.PoisonAttackMaxStat - 1, stat.PoisonAttackStatChance) + 1);
            if (stat.AttackSpeedChance > 0 && Random.Next(stat.AttackSpeedChance) == 0) item.AddedStats[Stat.AttackSpeed] = (sbyte)(RandomomRange(stat.AttackSpeedMaxStat - 1, stat.AttackSpeedStatChance) + 1);
            if (stat.LuckChance > 0 && Random.Next(stat.LuckChance) == 0) item.AddedStats[Stat.Luck] = (sbyte)(RandomomRange(stat.LuckMaxStat - 1, stat.LuckStatChance) + 1);
            if (stat.CurseChance > 0 && Random.Next(100) <= stat.CurseChance) item.Cursed = true;

            if (stat.SlotChance > 0 && Random.Next(stat.SlotChance) == 0)
            {
                var slot = (byte)(RandomomRange(stat.SlotMaxStat - 1, stat.SlotStatChance) + 1);

                if (slot > item.Info.Slots)
                {
                    item.SetSlotSize(slot);
                }
            }
        }

        public int RandomomRange(int count, int rate)
        {
            var x = 0;
            for (var i = 0; i < count; i++) if (Random.Next(rate) == 0) x++;
            return x;
        }
        public bool BindItem(UserItem item)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return BindSlotItems(item);
            }
            return false;
        }

        public bool BindGameShop(GameShopItem item, bool editEnvir = true)
        {
            for (var i = 0; i < Edit.ItemInfoList.Count; i++)
            {
                var info = Edit.ItemInfoList[i];
                if (info.Index != item.ItemIndex) continue;
                item.Info = info;

                return true;
            }
            return false;
        }

        public bool BindSlotItems(UserItem item)
        {
            for (var i = 0; i < item.Slots.Length; i++)
            {
                if (item.Slots[i] == null) continue;

                if (!BindItem(item.Slots[i])) return false;
            }

            return true;
        }

        public bool BindQuest(QuestProgressInfo quest)
        {
            for (var i = 0; i < QuestInfoList.Count; i++)
            {
                var info = QuestInfoList[i];
                if (info.Index != quest.Index) continue;
                quest.Info = info;
                return true;
            }
            return false;
        }

        public Map LoadMap(MapInfo info)
        {
            if (info == null) return null;

            Map map = MapList.FirstOrDefault(m => m.Info == info);
            if (map != null) return map;

            info.CreateMap();

            map = MapList.FirstOrDefault(m => m.Info == info);
            if (map != null && info.GT)
            {
                GTMap gt = GTMapList.FirstOrDefault(x => x.Index == info.GTIndex);
                if (gt != null)
                {
                    gt.Maps.Add(map);
                }
                else
                {
                    var GT = new GTMap()
                    {
                        Index = info.GTIndex,
                        Name = info.Title,
                        Price = Settings.BuyGTGold,
                        Days = 0,
                        Begin = 0,
                        Leader = "None",
                        Owner = "None",
                    };
                    GT.Maps.Add(map);
                    GTMapList.Add(GT);
                }
            }

            return map;
        }

        public Map GetMap(int index)
        {
            Map map = MapList.FirstOrDefault(t => t.Info.Index == index);
            if (map == null && Settings.MapUnloadEnabled)
                map = LoadMap(GetMapInfo(index));
            return map;
        }

        public Map GetMap(string name, bool strict = true)
        {
            Map map = MapList.FirstOrDefault(t => strict ? string.Equals(t.Info.Title, name, StringComparison.CurrentCultureIgnoreCase) : t.Info.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase));
            if (map == null && Settings.MapUnloadEnabled)
            {
                MapInfo info = MapInfoList.FirstOrDefault(t => strict ? string.Equals(t.Title, name, StringComparison.CurrentCultureIgnoreCase) : t.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase));
                map = LoadMap(info);
            }
            return map;
        }

        public Map GetWorldMap(string name)
        {
            Map map = MapList.FirstOrDefault(t => t.Info.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.Info.BigMap > 0);
            if (map == null && Settings.MapUnloadEnabled)
            {
                MapInfo info = MapInfoList.FirstOrDefault(t => t.Title.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.BigMap > 0);
                map = LoadMap(info);
            }
            return map;
        }

        public MapInfo GetMapInfo(int index)
        {
            return MapInfoList.FirstOrDefault(t => t.Index == index);
        }

        public Map GetMapByNameAndInstance(string name, int instanceValue = 0)
        {
            if (instanceValue < 0) instanceValue = 0;
            if (instanceValue > 0) instanceValue--;

            var instanceMapList = MapList.Where(t => string.Equals(t.Info.FileName, name, StringComparison.CurrentCultureIgnoreCase)).ToList();
            if (instanceValue < instanceMapList.Count) return instanceMapList[instanceValue];

            if (Settings.MapUnloadEnabled)
            {
                MapInfo info = MapInfoList.FirstOrDefault(t => string.Equals(t.FileName, name, StringComparison.CurrentCultureIgnoreCase));
                return LoadMap(info);
            }

            return null;
        }

        public MapObject GetObject(uint objectID)
        {
            return Objects.FirstOrDefault(e => e.ObjectID == objectID);
        }

        public List<MapObject> GetObjects(int map, ObjectType race)
        {
            return Objects.Where(x => x.CurrentMapIndex == map && x.Race == race).ToList();
        }

        public MonsterInfo GetMonsterInfo(int index)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].Index == index) return MonsterInfoList[i];

            return null;
        }

        public NPCInfo GetNPCInfo(int index)
        {
            for (var i = 0; i < NPCInfoList.Count; i++)
            {
                if (NPCInfoList[i].Index == index)
                    return NPCInfoList[i];
            }

            return null;
        }

        public MonsterInfo GetMonsterInfo(int ai, int effect = -1)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
                if (MonsterInfoList[i].AI == ai && (MonsterInfoList[i].Effect == effect || effect < 0)) return MonsterInfoList[i];

            return null;
        }

        public NPCObject GetNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.Name == name)).FirstOrDefault();
        }

        public NPCObject GetWorldMapNPC(string name)
        {
            return MapList.SelectMany(t1 => t1.NPCs.Where(t => t.Info.GameName.StartsWith(name, StringComparison.CurrentCultureIgnoreCase) && t.Info.ShowOnBigMap)).FirstOrDefault();
        }

        public MonsterInfo GetMonsterInfo(int id, bool strict = false)
        {
            String monsterName = MonsterInfoList.FirstOrDefault(x => x.Index == id)?.Name;

            if (monsterName == null)
            {
                return null;
            }
            else
            {
                return (GetMonsterInfo(monsterName, strict));
            }
        }

        public MonsterInfo GetMonsterInfo(string name, bool Strict = false)
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
            {
                var info = MonsterInfoList[i];
                if (Strict)
                {
                    if (info.Name != name) continue;
                    return info;
                }
                else
                {
                    if (string.Compare(info.Name, name, StringComparison.OrdinalIgnoreCase) != 0 &&
                        string.Compare(info.Name.Replace(" ", string.Empty), name.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase) != 0) continue;
                    return info;
                }
            }
            return null;
        }

        private void EnsureMonkCloneExists()
        {
            if (GetMonsterInfo(Settings.MonkCloneName, true) != null) return;

            MonsterInfo info = new MonsterInfo
            {
                Index = ++MonsterIndex,
                Name = Settings.MonkCloneName,
                Image = Monster.BlueMonk,
                AI = 23,            //BoneFamiliar melee pet AI
                Effect = 0,
                Level = 55,
                ViewRange = 8,
                CoolEye = 0,
                Light = 0,
                AttackSpeed = 1400,
                MoveSpeed = 1400,
                Experience = 0,
                CanTame = false,
                CanPush = false,
                AutoRev = false,
                Undead = false,
                CanRecall = true,
                IsBoss = false
            };
            info.Stats = new Stats();
            info.Stats[Stat.HP] = Settings.MonkCloneHP;
            info.Stats[Stat.MinAC] = 15;
            info.Stats[Stat.MaxAC] = 25;
            info.Stats[Stat.MinMAC] = 15;
            info.Stats[Stat.MaxMAC] = 25;
            info.Stats[Stat.MinDC] = Settings.MonkCloneMinDC;
            info.Stats[Stat.MaxDC] = Settings.MonkCloneMaxDC;
            info.Stats[Stat.Accuracy] = 20;
            info.Stats[Stat.Agility] = 20;

            MonsterInfoList.Add(info);
            MessageQueue.Enqueue("MonkClone monster created automatically (LuoHanZhen summon).");
        }

        private void EnsureMonkSkillBooks()
        {
            Spell[] monkSpells = { Spell.JiBenGunFa, Spell.LuoHanGunFa, Spell.JinGangGunFa, Spell.DaMoGunFa, Spell.XiangLongGunFa, Spell.Taunt, Spell.TianLeiZhen, Spell.ShiBuYiSha, Spell.LuoHanZhen };
            byte[] reqLevels = { 1, 48, 48, 48, 48, 48, 48, 60, 60 };

            ushort bookImage = 631;
            ItemInfo template = ItemInfoList.Find(i => i.Type == ItemType.Book);
            if (template != null) bookImage = template.Image;

            int created = 0;
            for (int s = 0; s < monkSpells.Length; s++)
            {
                Spell spell = monkSpells[s];

                bool exists = false;
                for (int i = 0; i < ItemInfoList.Count; i++)
                    if (ItemInfoList[i].Type == ItemType.Book && ItemInfoList[i].Shape == (short)spell)
                    {
                        exists = true;
                        break;
                    }
                if (exists) continue;

                ItemInfo info = new ItemInfo
                {
                    Index = ++ItemIndex,
                    Name = spell.ToString() + " Book",
                    Type = ItemType.Book,
                    Grade = ItemGrade.None,
                    RequiredType = RequiredType.Level,
                    RequiredClass = RequiredClass.Monk,
                    RequiredGender = RequiredGender.None,
                    Shape = (short)spell,
                    Weight = 1,
                    RequiredAmount = reqLevels[s],
                    Image = bookImage,
                    StackSize = 1,
                    Price = 5000
                };
                ItemInfoList.Add(info);
                created++;
            }

            if (created > 0)
                MessageQueue.Enqueue(created + " Monk skill books created automatically.");
        }

        private void EnsureMonkEquipment()
        {
            //Weapon: Shape 300+i -> client MonkWeapon lib 00+i
            //Armour: Shape 12..17 -> client MonkArmour lib 9..14
            int created = 0;

            ItemInfo weaponTemplate = ItemInfoList.Find(i => i.Type == ItemType.Weapon && i.Name.ToLower().Contains("staff"));
            if (weaponTemplate == null) weaponTemplate = ItemInfoList.Find(i => i.Type == ItemType.Weapon);
            ItemInfo armourTemplate = ItemInfoList.Find(i => i.Type == ItemType.Armour);

            string[] weaponNames = { "Monk Wooden Staff", "Monk Iron Staff", "Monk Steel Staff", "Monk Silver Staff", "Monk Golden Staff", "Monk Dragon Staff" };
            byte[] weaponLevels = { 1, 11, 22, 35, 48, 60 };
            byte[] weaponWeight = { 13, 18, 25, 33, 40, 45 };
            int[,] weaponStats =
            {
                { 2, 5, 2, 5 },
                { 4, 9, 4, 9 },
                { 8, 15, 8, 15 },
                { 15, 24, 15, 24 },
                { 25, 35, 25, 35 },
                { 35, 50, 35, 50 },
            };
            uint[] weaponPrice = { 2000, 8000, 30000, 90000, 250000, 600000 };

            for (int w = 0; w < weaponNames.Length; w++)
            {
                short shape = (short)(300 + w);
                if (ItemInfoList.Any(i => i.Type == ItemType.Weapon && i.Shape == shape)) continue;

                ItemInfo info = new ItemInfo
                {
                    Index = ++ItemIndex,
                    Name = weaponNames[w],
                    Type = ItemType.Weapon,
                    Grade = w >= 4 ? ItemGrade.Rare : ItemGrade.Common,
                    RequiredType = RequiredType.Level,
                    RequiredClass = RequiredClass.Monk,
                    RequiredGender = RequiredGender.Male,
                    Shape = shape,
                    Weight = weaponWeight[w],
                    RequiredAmount = weaponLevels[w],
                    Image = weaponTemplate != null ? weaponTemplate.Image : (ushort)0,
                    Durability = (ushort)(20 + w * 5),
                    StackSize = 1,
                    Price = weaponPrice[w]
                };
                info.Stats = new Stats();
                info.Stats[Stat.MinDC] = weaponStats[w, 0];
                info.Stats[Stat.MaxDC] = weaponStats[w, 1];
                info.Stats[Stat.MinSC] = weaponStats[w, 2];
                info.Stats[Stat.MaxSC] = weaponStats[w, 3];
                info.Stats[Stat.Accuracy] = w + 1;
                ItemInfoList.Add(info);
                created++;
            }

            string[] armourNames = { "Monk Cloth Robe", "Monk Leather Robe", "Monk Iron Robe", "Monk Silver Robe", "Monk Golden Robe", "Monk Dragon Robe" };
            byte[] armourLevels = { 1, 14, 28, 40, 52, 65 };
            byte[] armourWeight = { 8, 13, 20, 27, 33, 38 };
            int[,] armourStats =
            {
                { 1, 3, 1, 3 },
                { 3, 6, 3, 5 },
                { 5, 9, 5, 8 },
                { 7, 12, 7, 11 },
                { 10, 16, 9, 14 },
                { 13, 20, 12, 18 },
            };
            uint[] armourPrice = { 1500, 6000, 25000, 80000, 200000, 500000 };

            for (int a = 0; a < armourNames.Length; a++)
            {
                short shape = (short)(12 + a);
                if (ItemInfoList.Any(i => i.Type == ItemType.Armour && i.Shape == shape && i.RequiredClass == RequiredClass.Monk)) continue;

                ItemInfo info = new ItemInfo
                {
                    Index = ++ItemIndex,
                    Name = armourNames[a],
                    Type = ItemType.Armour,
                    Grade = a >= 4 ? ItemGrade.Rare : ItemGrade.Common,
                    RequiredType = RequiredType.Level,
                    RequiredClass = RequiredClass.Monk,
                    RequiredGender = RequiredGender.Male,
                    Shape = shape,
                    Weight = armourWeight[a],
                    RequiredAmount = armourLevels[a],
                    Image = armourTemplate != null ? armourTemplate.Image : (ushort)0,
                    Durability = (ushort)(20 + a * 5),
                    StackSize = 1,
                    Price = armourPrice[a]
                };
                info.Stats = new Stats();
                info.Stats[Stat.MinAC] = armourStats[a, 0];
                info.Stats[Stat.MaxAC] = armourStats[a, 1];
                info.Stats[Stat.MinMAC] = armourStats[a, 2];
                info.Stats[Stat.MaxMAC] = armourStats[a, 3];
                ItemInfoList.Add(info);
                created++;
            }

            if (created > 0)
                MessageQueue.Enqueue(created + " Monk equipment items created automatically.");
        }

        private void EnsureMonkGameShop()
        {
            int added = 0;

            Spell[] monkSpells = { Spell.JiBenGunFa, Spell.LuoHanGunFa, Spell.JinGangGunFa, Spell.DaMoGunFa, Spell.XiangLongGunFa, Spell.Taunt, Spell.TianLeiZhen, Spell.ShiBuYiSha, Spell.LuoHanZhen };
            uint[] bookCredit = { 100, 300, 300, 300, 300, 300, 500, 1000, 1000 };

            for (int s = 0; s < monkSpells.Length; s++)
            {
                ItemInfo book = null;
                for (int i = 0; i < ItemInfoList.Count; i++)
                    if (ItemInfoList[i].Type == ItemType.Book && ItemInfoList[i].Shape == (short)monkSpells[s])
                    {
                        book = ItemInfoList[i];
                        break;
                    }
                if (book == null) continue;
                if (GameShopList.Any(g => g.ItemIndex == book.Index)) continue;

                GameShopList.Add(new GameShopItem
                {
                    GIndex = ++GameshopIndex,
                    ItemIndex = book.Index,
                    Info = book,
                    GoldPrice = (uint)(bookCredit[s] * Settings.CredxGold),
                    CreditPrice = bookCredit[s],
                    Count = 1,
                    Class = "Monk",
                    Category = "Book",
                    Date = Now,
                    CanBuyCredit = true,
                    CanBuyGold = true
                });
                added++;
            }

            //starter weapon + robe for convenience
            string[] starterItems = { "Monk Wooden Staff", "Monk Cloth Robe" };
            foreach (string name in starterItems)
            {
                ItemInfo item = ItemInfoList.Find(i => i.Name == name);
                if (item == null) continue;
                if (GameShopList.Any(g => g.ItemIndex == item.Index)) continue;

                GameShopList.Add(new GameShopItem
                {
                    GIndex = ++GameshopIndex,
                    ItemIndex = item.Index,
                    Info = item,
                    GoldPrice = (uint)(100 * Settings.CredxGold),
                    CreditPrice = 100,
                    Count = 1,
                    Class = "Monk",
                    Category = item.Type.ToString(),
                    Date = Now,
                    CanBuyCredit = true,
                    CanBuyGold = true
                });
                added++;
            }

            if (added > 0)
                MessageQueue.Enqueue(added + " Monk items stocked in GameShop automatically.");
        }

        public PlayerObject GetPlayer(string name)
        {
            for (var i = 0; i < Players.Count; i++)
                if (string.Compare(Players[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return Players[i];

            return null;
        }
        public PlayerObject GetPlayer(uint PlayerId)
        {
            for (var i = 0; i < Players.Count; i++)
                if (Players[i].Info.Index == PlayerId)
                    return Players[i];

            return null;
        }
        public CharacterInfo GetCharacterInfo(string name)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (string.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return CharacterList[i];

            return null;
        }

        public CharacterInfo GetCharacterInfo(int index)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (CharacterList[i].Index == index)
                    return CharacterList[i];

            return null;
        }
        public HeroInfo GetHeroInfo(int index)
        {
            return HeroList.FirstOrDefault(x => x.Index == index && !x.Deleted);
        }
        public bool HeroIsOwnedByOther(CharacterInfo character, HeroInfo hero)
        {
            for (int i = 0; i < CharacterList.Count; i++)
            {
                CharacterInfo c = CharacterList[i];
                if (c == character || c.Deleted || c.Heroes == null) continue;

                for (int j = 0; j < c.Heroes.Length; j++)
                    if (c.Heroes[j] == hero) return true;
            }
            return false;
        }

        public ItemInfo GetItemInfo(int index)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Index != index) continue;
                return info;
            }
            return null;
        }

        public ItemInfo GetItemInfo(string name)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (string.Compare(info.Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                return info;
            }
            return null;
        }

        public QuestInfo GetQuestInfo(int index)
        {
            return QuestInfoList.FirstOrDefault(info => info.Index == index);
        }

        public ItemInfo GetBook(short Skill)
        {
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var info = ItemInfoList[i];
                if (info.Type != ItemType.Book || info.Shape != Skill) continue;
                return info;
            }
            return null;
        }

        public BuffInfo GetBuffInfo(BuffType type)
        {
            for (int i = 0; i < BuffInfoList.Count; i++)
            {
                var info = BuffInfoList[i];
                if (info.Type != type) continue;

                return info;
            }

            throw new NotImplementedException($"{type} has not been implemented.");
        }

        public void MessageAccount(AccountInfo account, string message, ChatType type)
        {
            if (account?.Characters == null) return;

            for (var i = 0; i < account.Characters.Count; i++)
            {
                if (account.Characters[i].Player == null) continue;
                account.Characters[i].Player.ReceiveChat(message, type);
                return;
            }
        }


        public void MailCharacter(CharacterInfo info, UserItem item = null, uint gold = 0, int reason = 0, string customMessage = null)
        {
            string sender = "Bichon Administrator";

            string message = "You have been mailed due to the following reason:\r\n\r\n";

            switch (reason)
            {
                case 1:
                    message += "Could not return item to bag after trade.";
                    break;
                case 99:
                    message += "Code didn't correctly handle checking inventory space.";
                    break;
                default:
                    message += customMessage ?? "No reason provided.";
                    break;
            }

            MailInfo mail = new MailInfo(info.Index)
            {
                Sender = sender,
                Message = message,
                Gold = gold
            };

            if (item != null)
            {
                mail.Items.Add(item);
            }

            mail.Send();
        }

        public GuildObject GetGuild(string name)
        {
            for (var i = 0; i < Guilds.Count; i++)
            {
                if (string.Compare(Guilds[i].Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase) != 0) continue;

                return Guilds[i];
            }

            return null;
        }
        public GuildObject GetGuild(int index)
        {
            for (var i = 0; i < Guilds.Count; i++)
            {
                if (Guilds[i].Guildindex == index)
                {
                    return Guilds[i];
                }
            }

            return null;
        }

        public void ProcessNewDay()
        {
            foreach (var c in CharacterList)
            {
                ClearDailyQuests(c);

                c.NewDay = true;

                c.Player?.CallDefaultNPC(DefaultNPCType.Daily);
            }
        }

        private void ProcessRentedItems()
        {
            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItems.Count <= 0)
                {
                    continue;
                }

                foreach (var rentedItemInfo in characterInfo.RentedItems)
                {
                    if (rentedItemInfo.ItemReturnDate >= Now)
                        continue;

                    var rentingPlayer = GetCharacterInfo(rentedItemInfo.RentingPlayerName);

                    for (var i = 0; i < rentingPlayer.Inventory.Length; i++)
                    {
                        if (rentedItemInfo.ItemId != rentingPlayer?.Inventory[i]?.UniqueID)
                        {
                            continue;
                        }

                        var item = rentingPlayer.Inventory[i];

                        if (item?.RentalInformation == null)
                        {
                            continue;
                        }

                        if (Now <= item.RentalInformation.ExpiryDate)
                        {
                            continue;
                        }

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer, false);
                        rentingPlayer.Inventory[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                        {
                            continue;
                        }

                        rentingPlayer.Player.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemExpiredFromInventory), item.Info.FriendlyName), ChatType.Hint);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }

                    for (var i = 0; i < rentingPlayer.Equipment.Length; i++)
                    {
                        var item = rentingPlayer.Equipment[i];

                        if (item?.RentalInformation == null)
                        {
                            continue;
                        }

                        if (Now <= item.RentalInformation.ExpiryDate)
                        {
                            continue;
                        }

                        ReturnRentalItem(item, item.RentalInformation.OwnerName, rentingPlayer, false);
                        rentingPlayer.Equipment[i] = null;
                        rentingPlayer.HasRentedItem = false;

                        if (rentingPlayer.Player == null)
                        {
                            continue;
                        }

                        rentingPlayer.Player.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.ItemExpiredInventory), item.Info.FriendlyName), ChatType.Hint);
                        rentingPlayer.Player.Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                        rentingPlayer.Player.RefreshStats();
                    }
                }
            }

            foreach (var characterInfo in CharacterList)
            {
                if (characterInfo.RentedItemsToRemove.Count <= 0)
                {
                    continue;
                }

                foreach (var rentalInformationToRemove in characterInfo.RentedItemsToRemove)
                {
                    characterInfo.RentedItems.Remove(rentalInformationToRemove);
                }

                characterInfo.RentedItemsToRemove.Clear();
            }
        }

        public bool ReturnRentalItem(UserItem rentedItem, string ownerName, CharacterInfo rentingCharacterInfo, bool removeNow = true)
        {
            if (rentedItem.RentalInformation == null)
            {
                return false;
            }

            var owner = GetCharacterInfo(ownerName);
            var returnItems = new List<UserItem>();

            foreach (var rentalInformation in owner.RentedItems)
            {
                if (rentalInformation.ItemId == rentedItem.UniqueID)
                {
                    owner.RentedItemsToRemove.Add(rentalInformation);
                }
            }

            rentedItem.RentalInformation.BindingFlags = BindMode.None;
            rentedItem.RentalInformation.RentalLocked = true;
            rentedItem.RentalInformation.ExpiryDate = rentedItem.RentalInformation.ExpiryDate.AddDays(1);

            returnItems.Add(rentedItem);

            var mail = new MailInfo(owner.Index, true)
            {
                Sender = rentingCharacterInfo.Name,
                Message = rentedItem.Info.FriendlyName,
                Items = returnItems
            };

            mail.Send();

            if (removeNow)
            {
                foreach (var rentalInformationToRemove in owner.RentedItemsToRemove)
                {
                    owner.RentedItems.Remove(rentalInformationToRemove);
                }

                owner.RentedItemsToRemove.Clear();
            }

            return true;
        }

        private void ClearDailyQuests(CharacterInfo info)
        {
            foreach (var quest in QuestInfoList)
            {
                if (quest.Type != QuestType.Daily) continue;

                //2026-09-18 修复: 倒序遍历, 原正序遍历中RemoveAt会跳过后一项, 导致多个每日任务时漏删(次日无法重接)
                for (var i = info.CompletedQuests.Count - 1; i >= 0; i--)
                {
                    if (info.CompletedQuests[i] != quest.Index) continue;

                    info.CompletedQuests.RemoveAt(i);
                }
            }

            info.Player?.GetCompletedQuests();
        }

        public GuildBuffInfo FindGuildBuffInfo(int Id)
        {
            for (var i = 0; i < Settings.Guild_BuffList.Count; i++)
            {
                if (Settings.Guild_BuffList[i].Id == Id)
                {
                    return Settings.Guild_BuffList[i];
                }
            }

            return null;
        }

        public void ClearGameshopLog()
        {
            Main.GameshopLog.Clear();

            for (var i = 0; i < AccountList.Count; i++)
            {
                for (var f = 0; f < AccountList[i].Characters.Count; f++)
                {
                    AccountList[i].Characters[f].GSpurchases.Clear();
                }
            }

            ResetGS = false;
            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.GameshopPurchaseLogsCleared));
        }

        public void Inspect(MirConnection con, uint id)
        {
            if (ObjectID == id) return;

            PlayerObject player = Players.SingleOrDefault(x => x.ObjectID == id || x.Pets.Count(y => y.ObjectID == id && y is HumanWizard) > 0);

            if (player == null) return;
            Inspect(con, player.Info.Index);
        }

        public void Inspect(MirConnection con, int id)
        {
            if (ObjectID == id) return;

            CharacterInfo player = GetCharacterInfo(id);
            if (player == null) return;

            CharacterInfo Lover = null;
            string loverName = "";

            if (player.Married != 0) Lover = GetCharacterInfo(player.Married);

            if (Lover != null)
            {
                loverName = Lover.Name;
            }

            for (int i = 0; i < player.Equipment.Length; i++)
            {
                UserItem u = player.Equipment[i];
                if (u == null) continue;

                con.CheckItem(u);
            }

            string guildname = "";
            string guildrank = "";
            GuildObject guild = null;
            GuildRank guildRank = null;
            if (player.GuildIndex != -1)
            {
                guild = GetGuild(player.GuildIndex);
                if (guild != null)
                {
                    guildRank = guild.FindRank(player.Name);
                    if (guildRank == null)
                    {
                        guild = null;
                    }
                    else
                    {
                        guildname = guild.Name;
                        guildrank = guildRank.Name;
                    }
                }
            }

            con.Enqueue(new S.PlayerInspect
            {
                Name = player.Name,
                Equipment = player.Equipment,
                GuildName = guildname,
                GuildRank = guildrank,
                Hair = player.Hair,
                Gender = player.Gender,
                Class = player.Class,
                Level = player.Level,
                LoverName = loverName,
                AllowObserve = player.AllowObserve && Settings.AllowObserve
            });
        }

        public void InspectHero(MirConnection con, int id)
        {
            if (ObjectID == id)
            {
                return;
            }

            HeroObject heroObject = Heroes.SingleOrDefault(h => h.ObjectID == id);

            if (heroObject == null)
            {
                return;
            }

            HeroInfo heroInfo = GetHeroInfo(heroObject.Info.Index);

            if (heroInfo == null)
            {
                return;
            }

            for (int i = 0; i < heroInfo.Equipment.Length; i++)
            {
                UserItem u = heroInfo.Equipment[i];

                if (u == null)
                {
                    continue;
                }

                con.CheckItem(u);
            }

            var ownerName = heroObject.Owner.Name;

            con.Enqueue(new S.PlayerInspect
            {
                Name = GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerHero), ownerName),
                Equipment = heroInfo.Equipment,
                GuildName = String.Empty,
                GuildRank = String.Empty,
                Hair = heroInfo.Hair,
                Gender = heroInfo.Gender,
                Class = heroInfo.Class,
                Level = heroInfo.Level,
                LoverName = String.Empty,
                AllowObserve = false,
                IsHero = true
            });
        }

        public void Observe(MirConnection con, string Name)
        {
            var player = GetPlayer(Name);

            if (player == null) return;
            if (!player.AllowObserve || !Settings.AllowObserve) return;

            player.AddObserver(con);
        }

        public void GetRanking(MirConnection con, byte RankType, int RankIndex, bool OnlineOnly)
        {
            if (RankType > 6) return;
            List<RankCharacterInfo> listings = RankType == 0 ? RankTop : RankClass[RankType - 1];

            if (RankIndex >= listings.Count || RankIndex < 0) return;

            S.Rankings p = new S.Rankings
            {
                RankType = RankType,
                Count = OnlineOnly ? OnlineRankingCount[RankType] : listings.Count
            };

            if (con.Player != null)
            {
                if (RankType == 0)
                    p.MyRank = con.Player.Info.Rank[0];
                else
                    p.MyRank = (byte)con.Player.Class == (RankType - 1) ? con.Player.Info.Rank[1] : 0;
            }

            int c = 0;
            for (int i = RankIndex; i < listings.Count; i++)
            {
                if (OnlineOnly && GetPlayer(listings[i].Name) == null) continue;

                if (!CheckListing(con, listings[i]))
                    p.ListingDetails.Add(listings[i]);
                p.Listings.Add(listings[i].PlayerId);
                c++;

                if (c > 19 || c >= p.Count) break;
            }

            con.Enqueue(p);
        }

        private bool CheckListing(MirConnection con, RankCharacterInfo listing)
        {
            if (!con.SentRankings.ContainsKey(listing.PlayerId))
            {
                con.SentRankings.Add(listing.PlayerId, listing.LastUpdated);
                return false;
            }

            DateTime lastUpdated = con.SentRankings[listing.PlayerId];
            if (lastUpdated != listing.LastUpdated)
            {
                con.SentRankings[listing.PlayerId] = lastUpdated;
                return false;
            }

            return true;
        }

        public int InsertRank(List<RankCharacterInfo> Ranking, RankCharacterInfo NewRank)
        {
            if (Ranking.Count == 0)
            {
                Ranking.Add(NewRank);
                return Ranking.Count;
            }

            for (var i = 0; i < Ranking.Count; i++)
            {
                //if level is lower
                if (Ranking[i].level < NewRank.level)
                {
                    Ranking.Insert(i, NewRank);
                    return i + 1;
                }

                //if exp is lower but level = same
                if (Ranking[i].level == NewRank.level && Ranking[i].Experience < NewRank.Experience)
                {
                    Ranking.Insert(i, NewRank);
                    return i + 1;
                }
            }

            Ranking.Add(NewRank);
            return Ranking.Count;
        }

        public bool TryAddRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var NewRank = new RankCharacterInfo() { Name = info.Name, Class = info.Class, Experience = info.Experience, level = info.Level, PlayerId = info.Index, info = info, LastUpdated = Now };
            var NewRankIndex = InsertRank(Ranking, NewRank);
            if (NewRankIndex == 0) return false;
            for (var i = NewRankIndex; i < Ranking.Count; i++)
            {
                SetNewRank(Ranking[i], i + 1, type);
            }
            info.Rank[type] = NewRankIndex;
            return true;
        }

        public int FindRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var startindex = info.Rank[type];
            if (startindex > 0) //if there's a previously known rank then the user can only have gone down in the ranking (or stayed the same)
            {
                for (var i = startindex - 1; i < Ranking.Count; i++)
                {
                    if (Ranking[i].Name == info.Name)
                        return i;
                }
                info.Rank[type] = 0;//set the rank to 0 to tell future searches it's not there anymore
            }
            return -1;//index can be 0
        }

        public bool UpdateRank(List<RankCharacterInfo> Ranking, CharacterInfo info, byte type)
        {
            var CurrentRank = FindRank(Ranking, info, type);
            if (CurrentRank == -1) return false;//not in ranking list atm

            var NewRank = CurrentRank;
            //next find our updated rank
            for (var i = CurrentRank - 1; i >= 0; i--)
            {
                if (Ranking[i].level > info.Level || Ranking[i].level == info.Level && Ranking[i].Experience > info.Experience) break;
                NewRank = i;
            }

            Ranking[CurrentRank].level = info.Level;
            Ranking[CurrentRank].Experience = info.Experience;
            Ranking[CurrentRank].LastUpdated = Now;

            if (NewRank < CurrentRank)
            {//if we gained any ranks
                Ranking.Insert(NewRank, Ranking[CurrentRank]);
                Ranking.RemoveAt(CurrentRank + 1);
                for (var i = NewRank + 1; i < Math.Min(Ranking.Count, CurrentRank + 1); i++)
                {
                    SetNewRank(Ranking[i], i + 1, type);
                }
            }
            info.Rank[type] = NewRank + 1;

            return true;
        }

        public void SetNewRank(RankCharacterInfo Rank, int Index, byte type)
        {
            Rank.LastUpdated = Now;
            if (!(Rank.info is CharacterInfo Player)) return;
            Player.Rank[type] = Index;
        }

        public void RemoveRank(CharacterInfo info)
        {
            List<RankCharacterInfo> Ranking;
            var Rankindex = -1;
            //first check overall top           
            Ranking = RankTop;
            Rankindex = FindRank(Ranking, info, 0);
            if (Rankindex >= 0)
            {
                Ranking.RemoveAt(Rankindex);
                for (var i = Rankindex; i < Ranking.Count(); i++)
                {
                    SetNewRank(Ranking[i], i, 0);
                }
            }

            //next class based top
            Ranking = RankTop;
            Rankindex = FindRank(Ranking, info, 1);
            if (Rankindex >= 0)
            {
                Ranking.RemoveAt(Rankindex);
                for (var i = Rankindex; i < Ranking.Count(); i++)
                {
                    SetNewRank(Ranking[i], i, 1);
                }
            }
        }

        public void CheckRankUpdate(CharacterInfo info)
        {
            List<RankCharacterInfo> Ranking;

            //first check overall top           

            Ranking = RankTop;
            if (!UpdateRank(Ranking, info, 0))
            {
                TryAddRank(Ranking, info, 0);
            }

            //now check class top

            Ranking = RankClass[(byte)info.Class];
            if (!UpdateRank(Ranking, info, 1))
            {
                TryAddRank(Ranking, info, 1);
            }
        }


        public void ReloadNPCs()
        {
            SaveGoods(true);

            Robot.Clear();

            var keys = Scripts.Keys;

            foreach (var key in keys)
            {
                Scripts[key].Load();
            }

            RefreshPlayersQuestInfo();

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NpcScriptsReloaded));
        }

        // 脚本重载会重新绑定任务的接取/交付NPC(以及任务内容), 但在线客户端的任务信息缓存不会自动刷新,
        // 旧的表现是玩家必须完整重启客户端(建立新连接)才能看到新的可接/可交状态。
        // 这里对每个在线玩家重发任务信息, 并刷新进行中任务的进度与完成状态。
        public void RefreshPlayersQuestInfo()
        {
            var players = Players.ToArray(); //遍历快照, 避免中途上下线导致集合变动

            foreach (var player in players)
            {
                if (player == null || player.Node == null || player.Connection == null) continue;

                try
                {
                    player.Connection.SentQuestInfo.Clear();
                    player.GetQuestInfo();

                    for (int i = 0; i < player.CurrentQuests.Count; i++)
                    {
                        //必须用 Update: 客户端对 Add 是无条件插入会重复, Update 按任务Id替换
                        player.SendUpdateQuest(player.CurrentQuests[i], QuestState.Update);
                    }
                }
                catch (Exception ex)
                {
                    MessageQueue.Enqueue(string.Format("刷新玩家[{0}]任务信息失败: {1}", player.Name, ex.Message));
                }
            }
        }

        public void ReloadDrops()
        {
            for (var i = 0; i < MonsterInfoList.Count; i++)
            {
                string path = Path.Combine(Settings.DropPath, MonsterInfoList[i].Name + ".txt");

                if (!string.IsNullOrEmpty(MonsterInfoList[i].DropPath))
                {
                    path = Path.Combine(Settings.DropPath, MonsterInfoList[i].DropPath + ".txt");
                }

                MonsterInfoList[i].Drops.Clear();

                DropInfo.Load(MonsterInfoList[i].Drops, MonsterInfoList[i].Name, path, 0, true);
            }

            FishingDrops.Clear();
            for (int i = 0; i < 19; i++)
            {
                var path = Path.Combine(Settings.DropPath, Settings.FishingDropFilename + ".txt");
                path = path.Replace("00", i.ToString("D2"));

                DropInfo.Load(FishingDrops, $"Fishing {i}", path, (byte)i, i < 2);
            }

            AwakeningDrops.Clear();
            DropInfo.Load(AwakeningDrops, "Awakening", Path.Combine(Settings.DropPath, Settings.AwakeningDropFilename + ".txt"));

            StrongboxDrops.Clear();
            DropInfo.Load(StrongboxDrops, "StrongBox", Path.Combine(Settings.DropPath, Settings.StrongboxDropFilename + ".txt"));

            BlackstoneDrops.Clear();
            DropInfo.Load(BlackstoneDrops, "Blackstone", Path.Combine(Settings.DropPath, Settings.BlackstoneDropFilename + ".txt"));

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.DropsLoaded));
        }

        public void ReloadLineMessages()
        {
            LineMessages.Clear();

            var path = Path.Combine(Settings.EnvirPath, "LineMessage.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    LineMessages.Add(lines[i]);
                }

                MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.LineMessagesReloaded));
            }
        }

        /// <summary>
        /// 天赋系统 - 加载(或热重载)天赋表 Envir\Talents.txt.
        /// 重载后需要刷新在线玩家的属性, 使已删/改天赋的属性加成立即生效.
        /// </summary>
        public void ReloadTalents()
        {
            TalentInfoList = Server.MirDatabase.TalentLoader.Load();

            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.TalentsLoaded), TalentInfoList.Count));

            //通知在线玩家重发天赋表(客户端会刷新天赋窗口), 并重算属性
            for (var i = 0; i < Players.Count; i++)
            {
                Players[i].RefreshStats();
                Players[i].SendTalentInfo();
            }
        }

        /// <summary>
        /// 商城 - 热重载商城表: 把DB工具(Envir.Edit)里最新编辑的商城表同步到运行中的表,
        /// 并给所有在线玩家重发完整商城数据(客户端按GIndex去重替换, 无需重启/重登).
        /// 注意: 请先在DB工具里编辑商城并保存, 再执行 @ReloadGameShop.
        /// </summary>
        public void ReloadGameShop()
        {
            GameShopList = new List<GameShopItem>(Envir.Edit.GameShopList);

            MessageQueue.Enqueue(string.Format("商城表已热重载, 共 {0} 条商品, 已通知 {1} 名在线玩家刷新.", GameShopList.Count, Players.Count));

            for (var i = 0; i < Players.Count; i++)
            {
                Players[i].GetGameShop();
            }
        }

        /// <summary>
        /// 物品管理器 - 排队热同步: 由编辑器在保存后调用.
        /// 同步动作会在运行库(通常是 Envir.Main)的服务器线程内执行,
        /// 与 WorkLoop 里的周期存盘串行, 不存在文件覆盖竞争.
        /// </summary>
        public void QueueItemSync(List<int> deletedItemIndexes)
        {
            var deleted = deletedItemIndexes ?? new List<int>();

            Interlocked.Exchange(ref _pendingItemSync, () =>
            {
                foreach (var idx in deleted)
                    SuppressedItemIndexes.Add(idx);

                MessageQueue.Enqueue(SyncItemsFromEdit());
            });
        }

        /// <summary>
        /// 把任意任务投递到游戏主线程执行(线程安全).
        /// 控制面板等UI线程需要触碰玩家/地图等运行时数据时必须走这里, 避免和游戏循环抢数据.
        /// </summary>
        public void QueueMainAction(Action action)
        {
            if (action == null) return;
            Interlocked.Exchange(ref _pendingMainAction, action);
        }

        /// <summary>
        /// 物品管理器 - 热同步执行体(务必在服务器线程内调用):
        /// 把编辑库(Envir.Edit)的最新物品表同步到本实例:
        /// 1. 按 Index 原地复制字段(在线玩家 UserItem 引用不断链);
        /// 2. 编辑器新增的物品克隆追加;
        /// 3. 已删物品靠 SuppressedItemIndexes 防止周期存盘复活(下次重启彻底消失);
        /// 4. 全体在线玩家刷新属性, 使攻防变化立即生效.
        /// </summary>
        public string SyncItemsFromEdit()
        {
            if (ReferenceEquals(this, Edit))
                return "物品热同步跳过: 不能在编辑实例(Envir.Edit)上执行。";

            int updated = 0, added = 0;

            var editById = new Dictionary<int, ItemInfo>(Edit.ItemInfoList.Count);
            foreach (var e in Edit.ItemInfoList)
                editById[e.Index] = e;

            var known = new HashSet<int>(ItemInfoList.Count);
            for (var i = 0; i < ItemInfoList.Count; i++)
            {
                var item = ItemInfoList[i];
                known.Add(item.Index);

                if (editById.TryGetValue(item.Index, out var src))
                {
                    if (!ReferenceEquals(item, src))
                        item.CopyFieldsFrom(src);
                    updated++;
                }
            }

            foreach (var e in Edit.ItemInfoList)
            {
                if (known.Contains(e.Index)) continue;
                ItemInfoList.Add(e.CloneItemInfo());
                added++;
            }

            for (var i = 0; i < Players.Count; i++)
                Players[i].RefreshStats();

            return $"物品热同步完成: 更新 {updated} 件, 新增 {added} 件, 删除标记 {SuppressedItemIndexes.Count} 件, 已刷新 {Players.Count} 名在线玩家属性。";
        }

        private WorldMapIcon ValidateWorldMap()
        {
            foreach (WorldMapIcon wmi in Settings.WorldMapSetup.Icons)
            {
                MapInfo info = GetMapInfo(wmi.MapIndex);

                if (info == null)
                    return wmi;
            }
            return null;
        }

        public void DeleteGuild(GuildObject guild)
        {
            Guilds.Remove(guild);
            GuildList.Remove(guild.Info);

            GuildRefreshNeeded = true;
            MessageQueue.Enqueue(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.GuildWillBeDeletedFromServer), guild.Info.Name));
        }
    }
}