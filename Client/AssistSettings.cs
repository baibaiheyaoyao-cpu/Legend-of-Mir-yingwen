using System.IO;           // 引入 System.IO  工具

namespace Client          // 命名空间   客户端                 
{    // =========================这是 ctrl+w 设置页面的 代码.
    //辅助系统独立配置(移植自 Crystal-Monk 的 Assist 功能)   
    //与 Settings.cs  F12 设置页面.    完全解耦: 配置按角色存放在 .\Data\UserData\Assist_角色名.ini
     public static class AssistSettings     // 辅助系统配置类  static 是静态 ,  public 是公共的 assistSettings  英文辅助的意思   

    {
        private static InIReader Reader;       // 只声明，不赋值。Reader 是一个静态私有变量，类型是 InIReader，用于读取和写入 INI 文件。它用于处理辅助系统的配置文件。

        public static string CharacterName = string.Empty;   // 空的 ,角色 

        //----------- 保护(自动喝药) -----------
        public static bool SmartProtect = true;                       //总开关
        public static int ProtectPercent0 = 50;                        //规则0: HP%低于
        public static string PercentItem0 = "金疮药";                  //        使用物品
        public static int ProtectPercent1 = 30;                        //规则1: MP%低于
        public static string PercentItem1 = "魔法药";                  //        使用物品
        public static int ProtectPercent2 = 30;                        //规则2: HP%低于
        public static string PercentItem2 = "太阳水";                  //        使用物品
        public static int UseItemInterval = 1000;                      //喝药冷却(毫秒)

        //----------- 职业(自动技能) -----------
        public static bool FreeShift = false;             //免Shift攻击(玩家/保护名目标)
        public static bool SmartFireHit = false;           //自动烈火剑法   
        public static bool SmartDaMo = false;              //自动达摩棍法
        public static bool SmartSheild = false;            //自动魔法盾
        public static bool SmartElementalBarrier = false;  //自动金刚术(弓手元素盾)
        public static bool SmartChangePoison = false;      //自动毒符(施毒前自动换符)
        public static bool SpaceThrusting = false;         //隔位刺杀(目标隔2格时自动出刺杀剑气)

        //----------- 刺客(自动技能) -----------
        public static bool SmartHaste = false;             //自动体迅风(攻速buff)
        public static bool SmartLightBody = false;         //自动风身术(敏捷buff)
        public static bool SmartSwiftFeet = false;         //自动轻身步(移速buff)
        public static bool SmartMoonLight = false;         //自动月影术(隐身buff, 攻击破隐, 默认关)

        //----------- 弓手(自动技能) -----------
        public static bool SmartConcentration = false;     //自动气流术(元素蓄力buff)

        //----------- 挂机 -----------
        public static bool AutoHunt = false;               //自动打怪(找怪/寻路/攻击/拾取)  // - `= false`：初始化为 `false`，表示默认关闭

        //----------- 物品 -----------
        public static bool AutoPick = false;               //自动拾取(配合过滤表)

        //----------- 基本(显示选项, 移植自 Crystal-Monk 辅助面板基本页) -----------
        //默认值原则: 保持本次改动前的游戏画面不变(新增视觉项默认关, 原有视觉项默认开)
        public static bool ShowLevel = false;              //名牌显示等级(新增视觉, 默认关)
        public static bool ShowTransform = true;           //显示时装(变身外形, 维持现状)
        public static bool ShowGuildName = true;           //名牌显示公会名(维持现状)
        public static bool ShowGroupInfo = true;           //显示组队血条面板(维持现状)
        public static bool ShowDamage = true;              //显示伤害数字(与启动器DisplayDamage取与, 维持现状)
        public static bool ShowHeal = false;               //显示恢复数字(新增视觉, 默认关)
        public static bool HideDead = false;               //隐藏怪物尸体(维持现状)
        public static bool ShowMonsterName = false;        //怪物常显名(新增视觉, 默认关=维持悬停显名)
        public static bool HideSystem2 = false;            //隐藏掉落通知(维持现状)
        public static bool ShowPing = false;               //显示Ping(新增视觉, 默认关)
        public static bool ShowHealth = true;              //名牌显示血量(维持现状)


        // 以上 是辅助系统的配置选项，下面是加载和保存配置的方法。  也就是说上面的都是变量 ,下面是方法  

        public static void Load(string charName)       // 加载配置方法  Load 是加载的意思     load 方法接受一个字符串参数 charName，表示角色名。它用于加载指定角色的辅助系统配置。
        {
            CharacterName = charName ?? string.Empty;

            if (!Directory.Exists(Settings.UserDataPath))
                Directory.CreateDirectory(Settings.UserDataPath);          // 如果用户数据路径不存在，则创建该目录。Settings.UserDataPath 是一个静态属性，表示用户数据的存储路径。

            Reader = new InIReader(Path.Combine(Settings.UserDataPath, "Assist_" + CharacterName + ".ini"));

            //Protect 是 保护(自动喝药)  下面是读取配置文件中的各个选项的值，并赋值给对应的静态变量。  
            SmartProtect = Reader.ReadBoolean("Protect", "Enabled", SmartProtect);    // 这是 读取 "Protect" 节下的 "Enabled" 键的布尔值，如果读取失败，则使用 SmartProtect 的当前值作为默认值。
            ProtectPercent0 = Reader.ReadInt32("Protect", "Percent0", ProtectPercent0);
            PercentItem0 = Reader.ReadString("Protect", "Item0", PercentItem0);
            ProtectPercent1 = Reader.ReadInt32("Protect", "Percent1", ProtectPercent1);
            PercentItem1 = Reader.ReadString("Protect", "Item1", PercentItem1);
            ProtectPercent2 = Reader.ReadInt32("Protect", "Percent2", ProtectPercent2);
            PercentItem2 = Reader.ReadString("Protect", "Item2", PercentItem2);
            UseItemInterval = Reader.ReadInt32("Protect", "UseItemInterval", UseItemInterval);
            if (PercentItem0 == "金创药") PercentItem0 = "金疮药"; //纠正旧配置错别字(NPC商店实际卖"金疮药")

            //Class   是 职业(自动技能)  下面是读取职业相关的配置选项  Class 英文的意思是?     
            FreeShift = Reader.ReadBoolean("Class", "FreeShift", FreeShift);
            SmartFireHit = Reader.ReadBoolean("Class", "SmartFireHit", SmartFireHit);
            SmartDaMo = Reader.ReadBoolean("Class", "SmartDaMo", SmartDaMo);
            SmartSheild = Reader.ReadBoolean("Class", "SmartSheild", SmartSheild);
            SmartElementalBarrier = Reader.ReadBoolean("Class", "SmartElementalBarrier", SmartElementalBarrier);
            SmartChangePoison = Reader.ReadBoolean("Class", "SmartChangePoison", SmartChangePoison);
            SpaceThrusting = Reader.ReadBoolean("Class", "SpaceThrusting", SpaceThrusting);
            AutoHunt = Reader.ReadBoolean("Class", "AutoHunt", AutoHunt);

            //Assassin   刺客 
            SmartHaste = Reader.ReadBoolean("Class", "SmartHaste", SmartHaste);
            SmartLightBody = Reader.ReadBoolean("Class", "SmartLightBody", SmartLightBody);
            SmartSwiftFeet = Reader.ReadBoolean("Class", "SmartSwiftFeet", SmartSwiftFeet);
            SmartMoonLight = Reader.ReadBoolean("Class", "SmartMoonLight", SmartMoonLight);

            //Archer    弓箭手  
            SmartConcentration = Reader.ReadBoolean("Class", "SmartConcentration", SmartConcentration);

            //Item
            AutoPick = Reader.ReadBoolean("Item", "AutoPick", AutoPick);

            //Base(显示选项)
            ShowLevel = Reader.ReadBoolean("Base", "ShowLevel", ShowLevel);
            ShowTransform = Reader.ReadBoolean("Base", "ShowTransform", ShowTransform);
            ShowGuildName = Reader.ReadBoolean("Base", "ShowGuildName", ShowGuildName);
            ShowGroupInfo = Reader.ReadBoolean("Base", "ShowGroupInfo", ShowGroupInfo);
            ShowDamage = Reader.ReadBoolean("Base", "ShowDamage", ShowDamage);
            ShowHeal = Reader.ReadBoolean("Base", "ShowHeal", ShowHeal);
            HideDead = Reader.ReadBoolean("Base", "HideDead", HideDead);
            ShowMonsterName = Reader.ReadBoolean("Base", "ShowMonsterName", ShowMonsterName);
            HideSystem2 = Reader.ReadBoolean("Base", "HideSystem2", HideSystem2);
            ShowPing = Reader.ReadBoolean("Base", "ShowPing", ShowPing);
            ShowHealth = Reader.ReadBoolean("Base", "ShowHealth", ShowHealth);
        }

        public static void Save()    // 保存配置方法  Save 是保存的意思
        {
            if (Reader == null || string.IsNullOrEmpty(CharacterName)) return;

            //Protect
            Reader.Write("Protect", "Enabled", SmartProtect);
            Reader.Write("Protect", "Percent0", ProtectPercent0);
            Reader.Write("Protect", "Item0", PercentItem0);
            Reader.Write("Protect", "Percent1", ProtectPercent1);
            Reader.Write("Protect", "Item1", PercentItem1);
            Reader.Write("Protect", "Percent2", ProtectPercent2);
            Reader.Write("Protect", "Item2", PercentItem2);
            Reader.Write("Protect", "UseItemInterval", UseItemInterval);

            //Class
            Reader.Write("Class", "FreeShift", FreeShift);
            Reader.Write("Class", "SmartFireHit", SmartFireHit);
            Reader.Write("Class", "SmartDaMo", SmartDaMo);
            Reader.Write("Class", "SmartSheild", SmartSheild);
            Reader.Write("Class", "SmartElementalBarrier", SmartElementalBarrier);
            Reader.Write("Class", "SmartChangePoison", SmartChangePoison);
            Reader.Write("Class", "SpaceThrusting", SpaceThrusting);
            Reader.Write("Class", "AutoHunt", AutoHunt);

            //Assassin
            Reader.Write("Class", "SmartHaste", SmartHaste);
            Reader.Write("Class", "SmartLightBody", SmartLightBody);
            Reader.Write("Class", "SmartSwiftFeet", SmartSwiftFeet);
            Reader.Write("Class", "SmartMoonLight", SmartMoonLight);

            //Archer  
            Reader.Write("Class", "SmartConcentration", SmartConcentration);

            //Item
            Reader.Write("Item", "AutoPick", AutoPick);

            //Base(显示选项)
            Reader.Write("Base", "ShowLevel", ShowLevel);
            Reader.Write("Base", "ShowTransform", ShowTransform);
            Reader.Write("Base", "ShowGuildName", ShowGuildName);
            Reader.Write("Base", "ShowGroupInfo", ShowGroupInfo);
            Reader.Write("Base", "ShowDamage", ShowDamage);
            Reader.Write("Base", "ShowHeal", ShowHeal);
            Reader.Write("Base", "HideDead", HideDead);
            Reader.Write("Base", "ShowMonsterName", ShowMonsterName);
            Reader.Write("Base", "HideSystem2", HideSystem2);
            Reader.Write("Base", "ShowPing", ShowPing);
            Reader.Write("Base", "ShowHealth", ShowHealth);
        }

        //保护规则读写(AssistDialog/AssistHelper 共用)
        public static int GetProtectPercent(int index)
        {
            switch (index)
            {
                case 0: return ProtectPercent0;
                case 1: return ProtectPercent1;
                case 2: return ProtectPercent2;
            }
            return 0;
        }

        public static void SetProtectPercent(int index, int value)
        {
            switch (index)
            {
                case 0: ProtectPercent0 = value; break;
                case 1: ProtectPercent1 = value; break;
                case 2: ProtectPercent2 = value; break;
            }
        }

        public static string GetProtectItemName(int index)
        {
            switch (index)
            {
                case 0: return PercentItem0;
                case 1: return PercentItem1;
                case 2: return PercentItem2;
            }
            return string.Empty;
        }

        public static void SetProtectItemName(int index, string value)
        {
            switch (index)
            {
                case 0: PercentItem0 = value; break;
                case 1: PercentItem1 = value; break;
                case 2: PercentItem2 = value; break;
            }
        }
    }
}


