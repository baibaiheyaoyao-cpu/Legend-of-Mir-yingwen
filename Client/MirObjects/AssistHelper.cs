using Client.MirNetwork;
using Client.MirScenes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using C = ClientPackets;

namespace Client.MirObjects
{
    //辅助系统 - 核心逻辑(移植自 Crystal-Monk 的 AssistHelper, 纯逻辑无UI依赖)
    //功能: 自动喝药(保护) / 自动技能(烈火·达摩·魔法盾·金刚术·体迅风·风身术·轻身步·月影术·气流术) / 自动毒符 / 自动打怪挂机 / 物品过滤拾取
    public class ItemFilter
    {
        public string Name;
        public bool Pick = true;
        public bool Sell = false;

        public string ToText()
        {
            return Name + "|" + (Pick ? 1 : 0) + "|" + (Sell ? 1 : 0);
        }

        public static ItemFilter FromLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            string[] parts = line.Split('|');
            if (parts.Length < 1 || string.IsNullOrEmpty(parts[0])) return null;

            ItemFilter filter = new ItemFilter { Name = parts[0] };
            if (parts.Length > 1) filter.Pick = parts[1] == "1";
            if (parts.Length > 2) filter.Sell = parts[2] == "1";
            return filter;
        }
    }

    //自动技能项: 开关开启且增益状态失效时自动补施
    public class AutoSkill
    {
        public long NextSpellTime;
        public int Interval;
        public Spell Spell;
        public int FailedCount;     //连续施放后buff仍未出现的次数(防跑飞退避用)

        public AutoSkill(Spell spell, int interval)
        {
            Spell = spell;
            Interval = interval;
        }

        public bool IsToggle()
        {
            switch (Spell)
            {
                case Spell.MagicShield:
                    return AssistSettings.SmartSheild;

                case Spell.FlamingSword:
                    return AssistSettings.SmartFireHit;

                case Spell.DaMoGunFa:
                    return AssistSettings.SmartDaMo;

                case Spell.ElementalBarrier:
                    return AssistSettings.SmartElementalBarrier;

                //刺客
                case Spell.Haste:
                    return AssistSettings.SmartHaste;

                case Spell.LightBody:
                    return AssistSettings.SmartLightBody;

                case Spell.SwiftFeet:
                    return AssistSettings.SmartSwiftFeet;

                case Spell.MoonLight:
                    return AssistSettings.SmartMoonLight;

                //弓手
                case Spell.Concentration:
                    return AssistSettings.SmartConcentration;
            }

            return false;
        }

        public virtual bool CheckState()
        {
            UserObject User = GameScene.User;
            if (User == null) return true;

            switch (Spell)
            {
                case Spell.MagicShield:
                    return User.MagicShield;

                case Spell.FlamingSword:
                    return User.FlamingSword;

                case Spell.DaMoGunFa:
                    return User.DaMoGunFa;

                //buff类: 必须查 BuffsDialog(S.AddBuff 实时落地处)
                //旧代码查 User.Buffs —— 它只在进图info包赋值一次, 之后永不更新, 会导致"检测不到buff→无限重放"
                case Spell.ElementalBarrier:
                    return AssistHelper.HasBuff(BuffType.ElementalBarrier);

                //刺客
                case Spell.Haste:
                    return AssistHelper.HasBuff(BuffType.Haste);

                case Spell.LightBody:
                    return AssistHelper.HasBuff(BuffType.LightBody);

                case Spell.SwiftFeet:
                    return AssistHelper.HasBuff(BuffType.SwiftFeet);

                case Spell.MoonLight:
                    return AssistHelper.HasBuff(BuffType.MoonLight);

                //弓手
                case Spell.Concentration:
                    return AssistHelper.HasBuff(BuffType.Concentration);
            }

            return false;
        }

        public bool Process()
        {
            if (!IsToggle())
                return false;

            if (CMain.Time < NextSpellTime)
                return false;

            if (CheckState())
            {
                FailedCount = 0; //buff存在, 复位退避计数
                return false;
            }

            UserObject User = GameScene.User;
            ClientMagic magic = User.GetMagic(Spell);
            if (magic == null)
                return false;

            //与原版(Crystal-Monk)一致: 自动施法前必须过CD与蓝量检查, 否则会出现无CD连发/盲发包
            if (AssistHelper.IsMagicInCD(magic) || !AssistHelper.CheckMagicMP(User, magic))
                return false;

            //玩家手动施法尚未出队时不抢队列
            if (User.NextMagic != null)
                return false;

            NextSpellTime = CMain.Time + Interval;
            AssistHelper.UseAssistSpell(Spell);

            //防跑飞退避: 连续3次施放buff仍未出现 → 暂停1分钟, 防止任何未知的同步异常演变成无限施法
            if (++FailedCount >= 3)
            {
                FailedCount = 0;
                NextSpellTime = CMain.Time + 60000;
                AssistHelper.WarnBackoff(Spell);
            }
            return true;
        }
    }

    public class AssistHelper
    {
        private long[] UseItemTime = new long[3];
        private long PickUpCoolTime;
        private long EquipCoolTime;

        private byte useAmuletShape = 1;

        //挂机状态
        private MapObject LastTargetObject;
        private MapObject TargetObject;
        private MirDirection FindDirection;
        private long FindTargetTime;
        private long RandomDirectionTime;
        private long MaxAttackDist = 20;
        private List<Point> CurrentPath;
        private long PathRetryTime;
        private int PathFailCount;
        private long HuntTickTime;
        private long NextActionTime;
        private long PickItemTime;
        private long CellPickTime;
        private long ErrorLogTime;
        private HashSet<MapObject> BlackObject = new HashSet<MapObject>();
        private int KillCount;
        private long BeginTime;

        private string[] IgnoreMonsterName = { "变异骷髅", "大刀", "弓箭手", "神兽", "月灵", "带刀" };

        private Dictionary<string, ItemFilter> itemFilterList = new Dictionary<string, ItemFilter>();

        public List<AutoSkill> AutoList = new List<AutoSkill>();

        internal Dictionary<string, ItemFilter> ItemFilterList
        {
            get { return itemFilterList; }
            set { itemFilterList = value; }
        }

        public AssistHelper()
        {
            AutoList.Add(new AutoSkill(Spell.FlamingSword, 500));
            AutoList.Add(new AutoSkill(Spell.DaMoGunFa, 500));
            AutoList.Add(new AutoSkill(Spell.MagicShield, 500));
            AutoList.Add(new AutoSkill(Spell.ElementalBarrier, 1000));

            //刺客buff
            AutoList.Add(new AutoSkill(Spell.Haste, 500));
            AutoList.Add(new AutoSkill(Spell.LightBody, 500));
            AutoList.Add(new AutoSkill(Spell.SwiftFeet, 500));
            AutoList.Add(new AutoSkill(Spell.MoonLight, 1000));

            //弓手buff
            AutoList.Add(new AutoSkill(Spell.Concentration, 1000));
        }

        public void Process()
        {
            UserObject User = GameScene.User;
            if (User == null || GameScene.Observing || User.Dead) return;

            //健壮性: 任何辅助异常都不允许中断渲染循环(否则挂机时会整个客户端卡死)
            try
            {
                for (int i = 0; i < AutoList.Count; ++i)
                {
                    if (AutoList[i].Process())
                        break;
                }

                AutoUseItem();

                //走到物品上自动拾取(不依赖挂机, 开着自动拾取就生效)
                AutoPickupOnCell();

                //================ 挂机功能暂时停用(待完善后再启用, 其余辅助功能不受影响) ================
                //if (AssistSettings.AutoHunt)
                //{
                //    //挂机节流: 100ms一个节拍, 避免每帧全图寻路卡死客户端
                //    if (CMain.Time >= HuntTickTime)
                //    {
                //        HuntTickTime = CMain.Time + 100;
                //        ClearBlackObjects();
                //        FindTarget();
                //        ProcessTarget();
                //    }
                //}
            }
            catch (Exception ex)
            {
                if (CMain.Time > ErrorLogTime)
                {
                    ErrorLogTime = CMain.Time + 1000;
                    CMain.SaveError("AssistHelper.Process: " + ex);
                }
            }
        }

        public void ClearAttack()
        {
            TargetObject = null;
            CurrentPath = null;
            BlackObject.Clear();
            PickItemTime = 0;
            PathFailCount = 0;
        }

        #region 自动喝药(保护)
        //走到物品格子上自动拾取(移植自武僧 GameScene.UserLocation 的 AutoPick 逻辑, 每200ms检查脚下)
        private void AutoPickupOnCell()
        {
            if (!AssistSettings.AutoPick) return;
            if (CMain.Time < CellPickTime) return;

            CellPickTime = CMain.Time + 200;

            UserObject User = GameScene.User;

            MapObject[] allObjects = Client.MirScenes.MapControl.Objects.Values.ToArray(); //快照
            for (int i = 0; i < allObjects.Length; i++)
            {
                MapObject ob = allObjects[i];
                if (ob is ItemObject && ob.CurrentLocation == User.CurrentLocation && NeedPick(ob.Name))
                {
                    Network.Enqueue(new C.PickUp()); //服务端一次捡一个, 拾完为止(下个节拍继续)
                    return;
                }
            }
        }

        private void AutoUseItem()
        {
            if (!AssistSettings.SmartProtect) return;

            for (int i = 0; i < 3; i++)
            {
                AutoProtect(i);
            }
        }

        private void AutoProtect(int index)
        {
            UserObject User = GameScene.User;

            int value;
            if (index == 1)
                value = User.MP * 100 / Math.Max(1, User.Stats[Stat.MP]);
            else
                value = User.HP * 100 / Math.Max(1, User.Stats[Stat.HP]);

            if (value < AssistSettings.GetProtectPercent(index) && CMain.Time > UseItemTime[index])
            {
                string itemName = AssistSettings.GetProtectItemName(index);
                if (string.IsNullOrEmpty(itemName))
                    return;

                UseItemTime[index] = CMain.Time + AssistSettings.UseItemInterval;
                for (int i = 0; i < User.Inventory.Length; i++)
                {
                    UserItem item = User.Inventory[i];

                    if (item != null && item.Info != null && item.Info.Name.Contains(itemName))
                    {
                        //注意: 该服务端的 UseItem 封包必须带 Grid, 否则服务器不认(喝药无效)
                        Network.Enqueue(new C.UseItem { UniqueID = item.UniqueID, Grid = MirGridType.Inventory });
                        break;
                    }
                }
            }
        }
        #endregion

        #region 自动打怪(挂机)
        private void ClearBlackObjects()
        {
            BlackObject.RemoveWhere(o => { return GameScene.Scene.MapControl.FindObject(o.ObjectID, o.CurrentLocation.X, o.CurrentLocation.Y) == null; });
        }

        private void FindTarget()
        {
            if (FindTargetTime >= CMain.Time)
                return;

            if (!NeedFindTarget())
                return;

            LastTargetObject = TargetObject;
            TargetObject = null;
            PickItemTime = 0;
            UserObject User = GameScene.User;
            int MinDist = int.MaxValue;
            MapObject[] allObjects = Client.MirScenes.MapControl.Objects.Values.ToArray(); //快照, 防止枚举中被网络包修改
            for (int i = 0; i < allObjects.Length; i++)
            {
                MapObject obj = allObjects[i];

                if (!CanBeTarget(obj))
                    continue;

                int dist = Functions.MaxDistance(User.CurrentLocation, obj.CurrentLocation);
                if (dist > MaxAttackDist) //只追范围内目标, 防止选到远处不可达目标引发全图寻路
                    continue;

                if (dist < MinDist)
                {
                    TargetObject = obj;
                    MinDist = dist;
                }
            }

            if (TargetObject != null)
            {
                FindTargetTime = CMain.Time + 1000;
                if (BeginTime == 0)
                    BeginTime = CMain.Time;
                if (CurrentPath != null)
                    CurrentPath.Clear();
                Msg(string.Format("发现目标:{0} L:{1},{2} 坐标:{3},{4} 距离:{5}", TargetObject.Name,
                    User.CurrentLocation.X, User.CurrentLocation.Y, TargetObject.CurrentLocation.X, TargetObject.CurrentLocation.Y,
                    Functions.MaxDistance(User.CurrentLocation, TargetObject.CurrentLocation)),
                    "Target found");
                return;
            }

            if (CMain.Time >= RandomDirectionTime)
            {
                MapControl MapControl = GameScene.Scene.MapControl;
                Point pt = Functions.PointMove(User.CurrentLocation, FindDirection, 1);
                if (!MapControl.EmptyCell(pt))
                {
                    FindDirection = (MirDirection)CMain.Random.Next(8);
                    for (int i = 0; i < 8; i++)
                    {
                        if (MapControl.EmptyCell(pt))
                            break;

                        FindDirection = Functions.NextDir(FindDirection);
                        pt = Functions.PointMove(User.CurrentLocation, FindDirection, 1);
                    }
                }

                RandomDirectionTime = CMain.Time + 1000;
                Msg("周围没有目标，开始游荡", "No targets nearby, wandering");
            }

            Move(FindDirection);
        }

        private bool CanBeTarget(MapObject obj)
        {
            if (obj == LastTargetObject)
                return false;

            if (BlackObject.Contains(obj))
                return false;

            if (obj is MonsterObject && !obj.Dead && CheckCanAttack(obj))
                return true;

            if (AssistSettings.AutoPick && obj is ItemObject && NeedPick(obj.Name))
                return true;

            return false;
        }

        private bool CheckCanAttack(MapObject obj)
        {
            for (int i = 0; i < IgnoreMonsterName.Length; ++i)
            {
                if (obj.Name.Contains(IgnoreMonsterName[i]))
                    return false;
            }

            return true;
        }

        private bool NeedFindTarget()
        {
            if (TargetObject == null)
                return true;

            UserObject User = GameScene.User;
            if (TargetObject.Dead)
            {
                KillCount++;
                int second = (int)(CMain.Time - BeginTime) / 60000;
                Msg(string.Format("目标死亡,杀敌数:{0} 耗时:{1}分钟 平均:{2}杀/分", KillCount, second, second != 0 ? KillCount / second : 0),
                    "Target killed");
                return true;
            }

            int dist = Functions.MaxDistance(User.CurrentLocation, TargetObject.CurrentLocation);
            if (dist > MaxAttackDist)
            {
                Msg("目标超出范围", "Target out of range");
                return true;
            }

            if (GameScene.Scene.MapControl.FindObject(TargetObject.ObjectID, TargetObject.CurrentLocation.X, TargetObject.CurrentLocation.Y) == null)
            {
                Msg("找不到目标", "Target lost");
                return true;
            }

            if (PickItemTime > 0 && CMain.Time >= PickItemTime)
            {
                Msg("拾取超时", "Pickup timeout");
                BlackObject.Add(TargetObject);
                return true;
            }

            if (TargetObject is ItemObject && TargetObject.CurrentLocation != User.CurrentLocation
                && !GameScene.Scene.MapControl.EmptyCell(TargetObject.CurrentLocation))
            {
                Msg("无法到达物品位置", "Cannot reach item");
                return true;
            }

            return false;
        }

        private bool ProcessTarget()
        {
            if (TargetObject == null)
                return false;

            UserObject User = GameScene.User;
            if (User.Poison.HasFlag(PoisonType.Paralysis) || User.Poison.HasFlag(PoisonType.LRParalysis) || User.Poison.HasFlag(PoisonType.Frozen) || User.Fishing)
                return false;

            MapControl MapControl = GameScene.Scene.MapControl;
            if (Functions.InRange(TargetObject.CurrentLocation, User.CurrentLocation, 1))
            {
                if (TargetObject is MonsterObject)
                {
                    if (CMain.Time > GameScene.AttackTime && CanRideAttack() && CMain.Time > NextActionTime)
                    {
                        MapObject.TargetObject = TargetObject;

                        if (ProcessSpell())
                            return true;

                        ProcessAttack();

                        User.QueuedAction = new QueuedAction { Action = MirAction.Attack1, Direction = Functions.DirectionFromPoint(User.CurrentLocation, TargetObject.CurrentLocation), Location = User.CurrentLocation };
                        return true;
                    }
                }
                else if (TargetObject is ItemObject)
                {
                    if (User.CurrentLocation == TargetObject.CurrentLocation)
                    {
                        if (PickItemTime == 0)
                            PickItemTime = CMain.Time + 2000;

                        if (CMain.Time > PickUpCoolTime)
                        {
                            PickUpCoolTime = CMain.Time + 400;
                            Network.Enqueue(new C.PickUp());
                        }
                    }
                    else
                    {
                        MirDirection direction = Functions.DirectionFromPoint(User.CurrentLocation, TargetObject.CurrentLocation);
                        if (CanWalkDir(direction))
                            User.QueuedAction = new QueuedAction
                            {
                                Action = MirAction.Walking,
                                Direction = direction,
                                Location = Functions.PointMove(User.CurrentLocation, direction, 1)
                            };
                    }
                }
            }
            else
            {
                if (CurrentPath == null || CurrentPath.Count == 0)
                {
                    //寻路冷却: 目标不可达时避免每帧全图搜索(A*穷举会卡死客户端)
                    if (CMain.Time < PathRetryTime)
                        return true;

                    PathRetryTime = CMain.Time + 1000;

                    Point dest = TargetObject.CurrentLocation;
                    if (TargetObject is MonsterObject)
                    {
                        MirDirection direction = Functions.DirectionFromPoint(TargetObject.CurrentLocation, User.CurrentLocation);
                        Point pt = Functions.PointMove(TargetObject.CurrentLocation, direction, 1);
                        if (MapControl.EmptyCell(pt))
                            dest = pt;
                    }

                    CurrentPath = FindPathBounded(User.CurrentLocation, dest, 3000);

                    if (CurrentPath == null || CurrentPath.Count == 0)
                    {
                        CurrentPath = null;
                        PathFailCount++;
                        if (PathFailCount >= 3)
                        {
                            PathFailCount = 0;
                            Msg("连续寻路失败, 放弃目标", "Path failed, skipping target");
                            BlackObject.Add(TargetObject);
                            TargetObject = null;
                            FindTargetTime = 0;
                        }
                        return true;
                    }

                    PathFailCount = 0;
                }

                if (CurrentPath != null && CurrentPath.Count > 0)
                    Move2();
            }

            return true;
        }

        private void ProcessAttack()
        {
            UserObject User = GameScene.User;
            ClientMagic magic;

            magic = User.GetMagic(Spell.TwinDrakeBlade);
            if (magic != null && !User.TwinDrakeBlade && CheckMagicMP(User, magic))
            {
                UseAssistSpell(Spell.TwinDrakeBlade);
                return;
            }

            magic = User.GetMagic(Spell.FlashDash);
            if (magic != null && !IsMagicInCD(magic) && CheckMagicMP(User, magic))
            {
                UseAssistSpell(Spell.FlashDash, TargetObject);
                NextActionTime += 2500;
            }
        }

        private bool ProcessSpell()
        {
            UserObject User = GameScene.User;
            ClientMagic magic = User.GetMagic(Spell.PoisonSword);
            if (magic != null && !IsMagicInCD(magic) && CheckMagicMP(User, magic)
                && TargetObject != null && !TargetObject.Poison.HasFlag(PoisonType.Green) && CMain.Time > Client.MirScenes.MapControl.NextAction)
            {
                UseAssistSpell(Spell.PoisonSword, TargetObject);
                NextActionTime += 2500;
                return true;
            }

            magic = User.GetMagic(Spell.Haste);
            if (magic != null && !IsMagicInCD(magic) && CheckMagicMP(User, magic) && !HasBuff(BuffType.Haste))
            {
                UseAssistSpell(Spell.Haste);
                NextActionTime += 2500;
                return true;
            }

            magic = User.GetMagic(Spell.LightBody);
            if (magic != null && !IsMagicInCD(magic) && CheckMagicMP(User, magic) && !HasBuff(BuffType.LightBody))
            {
                UseAssistSpell(Spell.LightBody);
                NextActionTime += 2500;
                return true;
            }

            return false;
        }

        //带扩展上限的A*寻路(防卡死专用): 最多展开maxExpand个节点, 不可达时快速失败
        //不复用 PathFinder.FindPath —— 它在不可达时会穷举整个可达区域, 大地图上一次耗时数秒
        private List<Point> FindPathBounded(Point start, Point target, int maxExpand)
        {
            MapControl MapControl = GameScene.Scene.MapControl;
            if (start == target)
                return new List<Point> { target };

            PriorityQueue<Point, int> open = new PriorityQueue<Point, int>();
            Dictionary<Point, int> gCost = new Dictionary<Point, int> { [start] = 0 };
            Dictionary<Point, Point> cameFrom = new Dictionary<Point, Point>();
            HashSet<Point> closed = new HashSet<Point>();

            open.Enqueue(start, Heuristic(start, target));
            int expanded = 0;

            while (open.Count > 0 && expanded < maxExpand)
            {
                Point current = open.Dequeue();
                if (!closed.Add(current))
                    continue;

                expanded++;

                if (current == target)
                {
                    List<Point> path = new List<Point>();
                    Point node = target;
                    while (node != start)
                    {
                        path.Add(node);
                        node = cameFrom[node];
                    }
                    path.Reverse();
                    return path;
                }

                for (int i = 0; i < 8; i++)
                {
                    Point nb = Functions.PointMove(current, (MirDirection)i, 1);
                    if (nb.X < 0 || nb.Y < 0 || nb.X >= MapControl.Width || nb.Y >= MapControl.Height)
                        continue;

                    if (!MapControl.EmptyCell(nb))
                        continue;

                    int ng = gCost[current] + 1;
                    if (!gCost.TryGetValue(nb, out int og) || ng < og)
                    {
                        gCost[nb] = ng;
                        cameFrom[nb] = current;
                        open.Enqueue(nb, ng + Heuristic(nb, target));
                    }
                }
            }

            return null;
        }

        private static int Heuristic(Point a, Point b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }

        private void Move2()
        {
            UserObject User = GameScene.User;
            if (CurrentPath == null || CurrentPath.Count == 0)
                return;

            //丢弃已经走过的节点
            int idx = CurrentPath.FindLastIndex(p => p == User.CurrentLocation);
            if (idx >= 0)
                CurrentPath.RemoveRange(0, idx + 1);

            if (CurrentPath.Count == 0)
                return;

            MirDirection dir = Functions.DirectionFromPoint(User.CurrentLocation, CurrentPath[0]);

            if (!CanWalkDir(dir))
            {
                CurrentPath = null; //撞墙, 等下个节拍重新寻路(带冷却)
                return;
            }

            if (CurrentPath.Count > 1 && GameScene.CanRun && CanRunDir(dir) && CMain.Time > GameScene.NextRunTime && User.HP >= 10
                && Functions.PointMove(User.CurrentLocation, dir, 2) == CurrentPath[1])
            {
                User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, 2) };
                return;
            }

            User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = dir, Location = Functions.PointMove(User.CurrentLocation, dir, 1) };
        }

        private void Move(MirDirection direction)
        {
            UserObject User = GameScene.User;
            MapControl MapControl = GameScene.Scene.MapControl;
            if (GameScene.CanRun && CanRunDir(direction) && CMain.Time > GameScene.NextRunTime && User.HP >= 10 && (!User.Sneaking || (User.Sneaking && User.Sprint)))
            {
                int distance = User.RidingMount || User.Sprint && !User.Sneaking ? 3 : 2;
                bool fail = false;
                for (int i = 1; i <= distance; i++)
                {
                    if (!MapControl.EmptyCell(Functions.PointMove(User.CurrentLocation, direction, i)))
                        fail = true;
                }
                if (!fail)
                {
                    Point location = Functions.PointMove(User.CurrentLocation, direction, distance);
                    User.QueuedAction = new QueuedAction { Action = MirAction.Running, Direction = direction, Location = location };
                }
            }
            else
            {
                Point location = Functions.PointMove(User.CurrentLocation, direction, 1);
                if (CanWalkDir(direction))
                    User.QueuedAction = new QueuedAction { Action = MirAction.Walking, Direction = direction, Location = location };
            }
        }

        private bool CanWalkDir(MirDirection dir)
        {
            UserObject User = GameScene.User;
            return !User.InTrapRock && GameScene.Scene.MapControl.EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 1));
        }

        private bool CanRunDir(MirDirection dir)
        {
            UserObject User = GameScene.User;
            if (User.InTrapRock) return false;
            if (User.CurrentBagWeight > User.Stats[Stat.BagWeight]) return false;
            if (User.CurrentWearWeight > User.Stats[Stat.WearWeight]) return false;

            if (CanWalkDir(dir) && GameScene.Scene.MapControl.EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 2)))
            {
                if (User.RidingMount || User.Sprint && !User.Sneaking)
                {
                    return GameScene.Scene.MapControl.EmptyCell(Functions.PointMove(User.CurrentLocation, dir, 3));
                }

                return true;
            }

            return false;
        }

        private bool CanRideAttack()
        {
            UserObject User = GameScene.User;
            if (User.RidingMount)
            {
                UserItem item = User.Equipment[(int)EquipmentSlot.Mount];
                if (item == null || item.Slots.Length < 4 || item.Slots[(int)MountSlot.Bells] == null) return false;
            }

            return true;
        }
        #endregion

        #region 自动毒符
        //施放特定技能前自动更换护身符(在 MapControl.UseMagic 入口被回调)
        public void PrevSendUseMagic(ClientMagic magic)
        {
            if (!AssistSettings.SmartChangePoison)
                return;

            switch (magic.Spell)
            {
                case Spell.Poisoning:
                    {
                        bool success = AutoEquipAmulet(useAmuletShape, 1);
                        if (success)
                        {
                            if (++useAmuletShape > 2)
                                useAmuletShape = 1;
                        }
                    }
                    break;
                case Spell.PoisonCloud:
                    {
                        AutoEquipAmulet(1, 1);
                        AutoEquipAmulet(0, 1);
                    }
                    break;

                case Spell.SoulFireBall:
                case Spell.SummonSkeleton:
                case Spell.Hiding:
                case Spell.MassHiding:
                case Spell.SoulShield:
                case Spell.TrapHexagon:
                case Spell.Curse:
                case Spell.Plague:
                case Spell.UltimateEnhancer:
                case Spell.BlessedArmour:
                    {
                        AutoEquipAmulet(0, 1);
                    }
                    break;

                case Spell.SummonHolyDeva:
                    {
                        AutoEquipAmulet(0, 2);
                    }
                    break;

                case Spell.SummonShinsu:
                    {
                        AutoEquipAmulet(0, 5);
                    }
                    break;
            }
        }

        private bool CheckEquipment(int slot, ItemType itemType, int count, int shape)
        {
            UserObject User = GameScene.User;
            UserItem item = User.Equipment[slot];
            return item != null && item.Info.Type == itemType && item.Count >= count && item.Info.Shape == shape;
        }

        private bool AutoEquipAmulet(byte shape, byte count)
        {
            int slot = (int)EquipmentSlot.Amulet;
            if (CheckEquipment(slot, ItemType.Amulet, count, shape))
                return true;

            //换符冷却: 装备状态要等服务器回包才更新, 期间重复发包会造成 EquipItem 风暴(服务器会按发包过多踢线)
            if (CMain.Time < EquipCoolTime)
                return false;

            UserItem item = FindAmulet(count, shape);
            if (item == null)
                return false;

            EquipCoolTime = CMain.Time + 600;
            Network.Enqueue(new C.EquipItem { Grid = MirGridType.Inventory, UniqueID = item.UniqueID, To = slot });
            return true;
        }

        private UserItem FindAmulet(int count, int shape)
        {
            UserObject User = GameScene.User;
            for (int i = 0; i < User.Inventory.Length; i++)
            {
                UserItem item = User.Inventory[i];
                if (item != null && item.Info != null && item.Info.Type == ItemType.Amulet && item.Info.Shape == shape && item.Count >= count)
                    return item;
            }
            return null;
        }
        #endregion

        #region 物品过滤
        public bool NeedPick(string name)
        {
            name = Regex.Replace(name, @"\([\d,]+\)", string.Empty).Trim();
            if (!ItemFilterList.ContainsKey(name))
            {
                ItemFilter itemFilter = new ItemFilter() { Name = name, Pick = true, Sell = false };
                ItemFilterList[name] = itemFilter;
                return itemFilter.Pick;
            }
            else
            {
                return ItemFilterList[name].Pick;
            }
        }

        public void Init()
        {
            ItemFilterList.Clear();
            if (string.IsNullOrEmpty(AssistSettings.CharacterName))
                return;

            try
            {
                string path = Path.Combine(Settings.UserDataPath, "AssistFilter_" + AssistSettings.CharacterName + ".txt");
                if (!File.Exists(path))
                    return;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; ++i)
                {
                    ItemFilter itemFilter = ItemFilter.FromLine(lines[i]);
                    if (itemFilter != null)
                        ItemFilterList[itemFilter.Name] = itemFilter;
                }
            }
            catch
            {
            }
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(AssistSettings.CharacterName))
                return;

            try
            {
                string path = Path.Combine(Settings.UserDataPath, "AssistFilter_" + AssistSettings.CharacterName + ".txt");
                File.WriteAllLines(path, ItemFilterList.Values.Select(item => item.ToText()).ToArray());
            }
            catch
            {
            }
        }
        #endregion

        #region 公共工具
        public static bool IsMagicInCD(ClientMagic magic)
        {
            return magic != null && CMain.Time <= magic.CastTime + magic.Delay;
        }

        public static bool CheckMagicMP(UserObject user, ClientMagic magic)
        {
            return magic.BaseCost + magic.LevelCost * magic.Level <= user.MP;
        }

        //buff检测: 优先查 BuffsDialog(S.AddBuff 的实时落地处, 与原版 GameScene.Scene.Buffs 语义一致),
        //兜底 User.Buffs(仅在进图info包时赋值)。只查 User.Buffs 会漏掉所有运行期新增的buff。
        public static bool HasBuff(BuffType type)
        {
            UserObject user = GameScene.User;
            if (user != null && user.Buffs.Contains(type)) return true;

            GameScene scene = GameScene.Scene;
            if (scene != null && scene.BuffsDialog != null)
            {
                for (int i = 0; i < scene.BuffsDialog.Buffs.Count; i++)
                    if (scene.BuffsDialog.Buffs[i].Type == type) return true;
            }
            return false;
        }

        private static long WarnTime;

        //防跑飞退避提示(限频10秒一条)
        public static void WarnBackoff(Spell spell)
        {
            if (CMain.Time < WarnTime) return;
            WarnTime = CMain.Time + 10000;

            GameScene scene = GameScene.Scene;
            if (scene == null || scene.ChatDialog == null) return;
            scene.ChatDialog.ReceiveChat("自动技能[" + spell + "]未检测到增益状态, 暂停1分钟后再试", ChatType.System);
        }

        //施法入口(复刻 GameScene.UseSpell 的分流逻辑)
        //开关型技能(烈火/达摩/逐日)走 C.SpellToggle; 普通技能走 NextMagic + MapControl.UseMagic
        public static void UseAssistSpell(Spell spell, MapObject target = null)
        {
            UserObject User = GameScene.User;
            GameScene scene = GameScene.Scene;
            if (User == null || scene == null || scene.MapControl == null) return;
            if (User.Dead || User.RidingMount || User.Fishing) return;

            ClientMagic magic = User.GetMagic(spell);
            if (magic == null) return;

            if (!User.HasClassWeapon && User.Weapon >= 0) return;

            if (CMain.Time < User.BlizzardStopTime || CMain.Time < User.ReincarnationStopTime) return;

            int cost = magic.Level * magic.LevelCost + magic.BaseCost;

            switch (spell)
            {
                //开关型技能: 与 GameScene.UseSpell 手动路径一致, 走 SpellToggle 封包(服务端 HumanObject 只认这条路径)
                //必须过 ToggleTime 节流, 否则自动施法会绕过手动施法的500ms限制造成包风暴
                case Spell.FlamingSword:
                case Spell.DaMoGunFa:
                    if (CMain.Time < scene.ToggleTime) return;
                    if (cost > User.MP) return;
                    scene.ToggleTime = CMain.Time + 500;
                    Network.Enqueue(new C.SpellToggle { Spell = spell, CanUse = true });
                    break;

                case Spell.TwinDrakeBlade:
                    if (CMain.Time < scene.ToggleTime) return;
                    if (cost > User.MP || User.TwinDrakeBlade) return;
                    scene.ToggleTime = CMain.Time + 500;
                    User.TwinDrakeBlade = true;
                    Network.Enqueue(new C.SpellToggle { Spell = spell, CanUse = true });
                    break;

                //普通施法型
                default:
                    if (target != null && !target.Dead)
                    {
                        User.NextMagicObject = target;
                        User.NextMagicLocation = target.CurrentLocation;
                        User.NextMagicDirection = Functions.DirectionFromPoint(User.CurrentLocation, target.CurrentLocation);
                    }
                    else
                    {
                        User.NextMagicObject = User;
                        User.NextMagicLocation = User.CurrentLocation;
                        User.NextMagicDirection = User.Direction;
                    }
                    scene.MapControl.UseMagic(magic, User);
                    break;
            }
        }

        private static void Msg(string zh, string en)
        {
            GameScene scene = GameScene.Scene;
            if (scene == null || scene.ChatDialog == null) return;

            scene.ChatDialog.ReceiveChat(zh, ChatType.System); //固定中文(与 Crystal-Monk 原版一致)
        }
        #endregion
    }
}
