using Server.MirObjects;
using Shared;
using System.Drawing;

namespace Server.MirEnvir
{
    /// <summary>
    /// 勇猛的战场(势力战) - 纯服务端实现:
    /// 所有交互走 NPC 对话脚本(Envir\NPCs\战场\) + 聊天广播, 客户端无需任何改动.
    ///
    /// 脚本命令(NPCSegment):
    ///   #ACT 加入战场 / 退出战场 / 领取战场奖励
    ///   #IF CANGETBATTLEREWARD 1|0   (1=领胜方奖, 0=领败方奖)
    ///   文本变量: <$BATTLE(0)>战况总览  <$BATTLE(1)>红方战报  <$BATTLE(2)>蓝方战报
    ///
    /// 流程: 每日固定时间开战, 把等候室(battle_waiting)内玩家按红蓝分队传送进战场(valor);
    /// 击杀+KillScore分/被杀-DeathScore分, 死亡4秒后在己方出生点满状态复活(不掉落/不计PK);
    /// 结束后全员传回等候室, 到"勇猛的战场进入管理员"处按胜负领奖(奖励数值在脚本里).
    /// 配置: Configs\BattleField.ini (控制面板 配置→战场控制 可视化调整).
    /// </summary>
    public static class BattleField
    {
        private static Envir Envir => Envir.Main;
        private static MessageQueue MQ => MessageQueue.Instance;

        // ===== 配置(Configs\BattleField.ini) =====
        public static bool Enabled = true;
        public static int StartHour = 19;          //第一场(对齐原作者: 19:30)
        public static int StartMinute = 30;
        public static int SecondStartHour = 21;    //第二场(原作者: 21:00), 设为 -1 关闭第二场
        public static int SecondStartMinute = 0;
        public static int DurationMinutes = 30;
        public static int KillScore = 3;
        public static int DeathScore = 1;
        public static int MinLevel = 35;           //原作者: 35级参与

        // ===== 运行状态 =====
        public class Fighter
        {
            public PlayerObject Player;
            public int Team;        //0=红方 1=蓝方
            public int Kills, Deaths, Score;
            public int ResultSide = -2; //结算结果: 1=胜 0=负 -1=平局 -2=未结算
            public bool Claimed;
        }

        private static readonly List<Fighter> _fighters = new(); //战斗中+战后待领奖
        private static readonly List<KeyValuePair<long, Fighter>> _reviveQueue = new();
        private static bool _fighting;
        private static long _fightEndTime;
        private static int _redScore, _blueScore;
        private static int _lastSession1Stamp = -1; //第一场当日已开标记(yyyymmdd)
        private static int _lastSession2Stamp = -1; //第二场当日已开标记
        private static long _nextProcess;

        private static Map _waitingMap, _valorMap;
        private static Point _waitSpawn = new(95, 99);
        private static Point _redSpawn, _blueSpawn;
        private static bool _mapsResolved;
        private static bool _mapErrorLogged;

        public static bool IsFighting => _fighting;
        public static int RedScore => _redScore;
        public static int BlueScore => _blueScore;
        public static int FighterCount => _fighters.Count;

        // ==================== 配置读写 ====================

        public static void LoadSettings()
        {
            try
            {
                var reader = new InIReader(Path.Combine(Settings.ConfigPath, "BattleField.ini"));
                Enabled = reader.ReadBoolean("Config", "Enabled", Enabled);
                StartHour = Math.Max(0, Math.Min(23, reader.ReadInt32("Config", "StartHour", StartHour)));
                StartMinute = Math.Max(0, Math.Min(59, reader.ReadInt32("Config", "StartMinute", StartMinute)));
                SecondStartHour = reader.ReadInt32("Config", "SecondStartHour", SecondStartHour);
                SecondStartMinute = SecondStartHour < 0 ? 0 : Math.Max(0, Math.Min(59, reader.ReadInt32("Config", "SecondStartMinute", SecondStartMinute)));
                DurationMinutes = Math.Max(5, reader.ReadInt32("Config", "DurationMinutes", DurationMinutes));
                KillScore = Math.Max(1, reader.ReadInt32("Config", "KillScore", KillScore));
                DeathScore = Math.Max(0, reader.ReadInt32("Config", "DeathScore", DeathScore));
                MinLevel = Math.Max(1, reader.ReadInt32("Config", "MinLevel", MinLevel));
            }
            catch (Exception ex)
            {
                MQ.Enqueue("战场配置读取失败, 使用默认值: " + ex.Message);
            }
        }

        public static void SaveSettings()
        {
            var reader = new InIReader(Path.Combine(Settings.ConfigPath, "BattleField.ini"));
            reader.Write("Config", "Enabled", Enabled);
            reader.Write("Config", "StartHour", StartHour);
            reader.Write("Config", "StartMinute", StartMinute);
            reader.Write("Config", "SecondStartHour", SecondStartHour);
            reader.Write("Config", "SecondStartMinute", SecondStartMinute);
            reader.Write("Config", "DurationMinutes", DurationMinutes);
            reader.Write("Config", "KillScore", KillScore);
            reader.Write("Config", "DeathScore", DeathScore);
            reader.Write("Config", "MinLevel", MinLevel);
        }

        // ==================== 主循环驱动(游戏线程) ====================

        /// <summary>环境重启(Envir.StartEnvir)时清理残留状态, 重新解析地图.</summary>
        public static void Reset()
        {
            _fighting = false;
            _mapsResolved = false;
            _mapErrorLogged = false;
            _fighters.Clear();
            _reviveQueue.Clear();
            _redScore = 0;
            _blueScore = 0;
        }

        public static void Process()
        {
            if (Envir == null || Envir.Time < _nextProcess) return;
            _nextProcess = Envir.Time + 1000;

            try
            {
                //复活队列
                for (var i = _reviveQueue.Count - 1; i >= 0; i--)
                {
                    if (Envir.Time < _reviveQueue[i].Key) continue;
                    var f = _reviveQueue[i].Value;
                    _reviveQueue.RemoveAt(i);
                    if (!_fighting || f.ResultSide != -2) continue; //战斗已结束
                    RespawnFighter(f);
                }

                //清理掉线的参战者
                for (var i = _fighters.Count - 1; i >= 0; i--)
                {
                    var p = _fighters[i].Player;
                    if (p == null || p.Node == null) _fighters.RemoveAt(i);
                }

                if (_fighting)
                {
                    if (Envir.Time >= _fightEndTime || !Enabled) EndBattle();
                    return;
                }

                if (!Enabled) return;

                //到点自动开战(每天两场, 各自一天一开; 手动"立即开战"不受此限)
                var now = DateTime.Now;
                var stamp = now.Year * 10000 + now.Month * 100 + now.Day;
                if (InSessionWindow(now, StartHour, StartMinute) && _lastSession1Stamp != stamp)
                {
                    _lastSession1Stamp = stamp;
                    StartBattle();
                }
                else if (SecondStartHour >= 0 && InSessionWindow(now, SecondStartHour, SecondStartMinute) && _lastSession2Stamp != stamp)
                {
                    _lastSession2Stamp = stamp;
                    StartBattle();
                }
            }
            catch (Exception ex)
            {
                MQ.Enqueue("战场系统异常: " + ex);
            }
        }

        // ==================== 开战/结束 ====================

        public static void StartBattle()
        {
            if (_fighting) return;

            if (!ResolveMaps())
            {
                MQ.Enqueue("战场开战失败: 找不到 battle_waiting / valor 地图。");
                return;
            }

            var drafted = new List<PlayerObject>();
            for (var i = 0; i < Envir.Players.Count; i++)
            {
                var p = Envir.Players[i];
                if (p.CurrentMap != _waitingMap) continue;
                if (p.Level < MinLevel)
                {
                    p.ReceiveChat($"【战场】等级不足{MinLevel}级, 无法参战。", ChatType.System);
                    continue;
                }
                drafted.Add(p);
            }

            if (drafted.Count == 0)
            {
                Announce("【战场】开战时间已到, 等候室无人, 本次战场取消。");
                return;
            }

            _fighters.Clear();
            _reviveQueue.Clear();
            _redScore = 0;
            _blueScore = 0;

            for (var i = 0; i < drafted.Count; i++)
            {
                var team = i % 2; //交替分队保证人数均衡
                var f = new Fighter { Player = drafted[i], Team = team };
                _fighters.Add(f);
            }

            _fighting = true;
            _fightEndTime = Envir.Time + DurationMinutes * 60000;

            foreach (var f in _fighters)
                PlaceAt(f.Player, _valorMap, f.Team == 0 ? _redSpawn : _blueSpawn);

            Announce($"【战场】勇猛的战场开战! 红方{_fighters.Count(x => x.Team == 0)}人 对 蓝方{_fighters.Count(x => x.Team == 1)}人, 时长{DurationMinutes}分钟, 杀敌+{KillScore}分/阵亡-{DeathScore}分!");
        }

        public static void EndBattle()
        {
            if (!_fighting) return;
            _fighting = false;
            _reviveQueue.Clear();

            var winner = _redScore > _blueScore ? 0 : _blueScore > _redScore ? 1 : -1;

            var mvp = _fighters.OrderByDescending(x => x.Score).FirstOrDefault();
            var mvpText = mvp != null && mvp.Score > 0 ? $" MVP: {(mvp.Team == 0 ? "红方" : "蓝方")}{mvp.Player.Name}({mvp.Score}分)" : "";

            Announce(winner switch
            {
                0 => $"【战场】战斗结束! 红方胜利 {_redScore}:{_blueScore}。{mvpText}",
                1 => $"【战场】战斗结束! 蓝方胜利 {_blueScore}:{_redScore}。{mvpText}",
                _ => $"【战场】战斗结束, 双方战平 {_redScore}:{_blueScore}。平局无奖励。",
            });

            foreach (var f in _fighters.ToList())
            {
                f.ResultSide = winner switch { 0 => f.Team == 0 ? 1 : 0, 1 => f.Team == 1 ? 1 : 0, _ => -1 };

                var p = f.Player;
                if (p == null || p.Node == null) continue;
                PlaceAt(p, _waitingMap, _waitSpawn);
                p.ReceiveChat(winner < 0
                    ? "【战场】平局, 无奖励可领。"
                    : $"【战场】你所在阵营{(f.ResultSide == 1 ? "获胜" : "战败")}! 可找战场管理员领取对应奖励(下场开战前有效)。", ChatType.System);
            }
        }

        // ==================== 脚本命令入口(NPCSegment 调用) ====================

        public static void Join(PlayerObject player)
        {
            if (!Enabled)
            {
                player.ReceiveChat("【战场】战场系统未开放。", ChatType.System);
                return;
            }

            if (!_fighting)
            {
                player.ReceiveChat($"【战场】战场每日 {StartHour:00}:{StartMinute:00}{(SecondStartHour >= 0 ? $" 和 {SecondStartHour:00}:{SecondStartMinute:00}" : "")} 开战, 每场{DurationMinutes}分钟。开战时会自动传送等候室内所有{MinLevel}级以上的玩家。", ChatType.System);
                return;
            }

            //战斗中入场(补进人数少的一方)
            if (!ResolveMaps()) return;
            if (Find(player) != null)
            {
                player.ReceiveChat("【战场】你已在战斗中。", ChatType.System);
                return;
            }

            var team = _fighters.Count(x => x.Team == 0) <= _fighters.Count(x => x.Team == 1) ? 0 : 1;
            var f = new Fighter { Player = player, Team = team };
            _fighters.Add(f);
            PlaceAt(player, _valorMap, team == 0 ? _redSpawn : _blueSpawn);
            Announce($"【战场】{player.Name} 加入了{(team == 0 ? "红" : "蓝")}方参战!");
        }

        public static void Leave(PlayerObject player)
        {
            var f = Find(player);
            if (f == null) return;
            _fighters.Remove(f);
            player.ReceiveChat("【战场】你已退出战场, 本场无法再获得奖励。", ChatType.System);
        }

        public static bool CanClaim(PlayerObject player, bool wantWin)
        {
            var f = Find(player);
            return f != null && f.ResultSide == (wantWin ? 1 : 0) && !f.Claimed;
        }

        public static void Claim(PlayerObject player)
        {
            var f = Find(player);
            if (f != null) f.Claimed = true;
        }

        // ==================== 击杀/死亡钩子(PlayerObject.Die 调用) ====================

        /// <summary>战场击杀计分. 返回true表示这是战场击杀(调用方应跳过PK点/诅咒逻辑).</summary>
        public static bool OnPlayerKill(PlayerObject victim, PlayerObject killer)
        {
            if (!_fighting) return false;

            var vf = Find(victim);
            var kf = Find(killer);
            if (vf == null || kf == null || vf == kf) return false;

            kf.Kills++;
            kf.Score += KillScore;
            vf.Deaths++;
            vf.Score = Math.Max(0, vf.Score - DeathScore);

            if (kf.Team == 0)
            {
                _redScore += KillScore;
                _blueScore = Math.Max(0, _blueScore - DeathScore);
            }
            else
            {
                _blueScore += KillScore;
                _redScore = Math.Max(0, _redScore - DeathScore);
            }

            Announce($"【战场】{(kf.Team == 0 ? "红方" : "蓝方")}{killer.Name} 击杀了 {(vf.Team == 0 ? "红方" : "蓝方")}{victim.Name}!  (红{_redScore} : {_blueScore}蓝)");
            return true;
        }

        /// <summary>战场参战者死亡: 阵亡不掉落由Die()另行跳过, 这里安排延迟复活.</summary>
        public static void OnPlayerDeath(PlayerObject victim)
        {
            if (!_fighting) return;
            var f = Find(victim);
            if (f == null) return;
            _reviveQueue.Add(new KeyValuePair<long, Fighter>(Envir.Time + 4000, f));
            victim.ReceiveChat("【战场】4秒后将在己方出生点复活。", ChatType.Hint);
        }

        public static bool IsActiveFighter(PlayerObject player)
        {
            return _fighting && Find(player) != null;
        }

        // ==================== 文本变量 $BATTLE ====================

        public static string BattleText(string which)
        {
            switch (which)
            {
                case "1":
                    return TeamReport(0);
                case "2":
                    return TeamReport(1);
                default:
                    if (_fighting)
                    {
                        var remain = Math.Max(0, (int)(_fightEndTime - Envir.Time) / 60000);
                        return $"战场进行中, 剩余{remain}分钟 | 红方{_redScore}分 蓝方{_blueScore}分 | 参战{_fighters.Count}人";
                    }
                    return Enabled
                        ? $"战场未开战。每日 {StartHour:00}:{StartMinute:00}{(SecondStartHour >= 0 ? $" 和 {SecondStartHour:00}:{SecondStartMinute:00}" : "")} 开战(每场{DurationMinutes}分钟), 请提前到等候室等待。"
                        : "战场系统当前未开放。";
            }
        }

        private static string TeamReport(int team)
        {
            var list = _fighters.Where(x => x.Team == team).OrderByDescending(x => x.Score).Take(8).ToList();
            if (list.Count == 0) return "暂无战况";
            return string.Join(" / ", list.Select(x => $"{x.Player.Name} {x.Score}分-{x.Kills}杀-{x.Deaths}亡"));
        }

        /// <summary>面板状态快照</summary>
        public static string StatusSnapshot()
        {
            var head = _fighting
                ? $"状态: 战斗中 (剩余{Math.Max(0, (int)(_fightEndTime - Envir.Time) / 60000)}分钟)\r\n比分: 红方 {_redScore} : {_blueScore} 蓝方\r\n参战: {_fighters.Count}人"
                : $"状态: 未开战 (每日 {StartHour:00}:{StartMinute:00}{(SecondStartHour >= 0 ? $" 和 {SecondStartHour:00}:{SecondStartMinute:00}" : "")}, 每场{DurationMinutes}分钟)\r\n开关: {(Enabled ? "已启用" : "已关闭")}";
            if (_fighting)
            {
                var red = TeamReport(0);
                var blue = TeamReport(1);
                head += $"\r\n红方: {red}\r\n蓝方: {blue}";
            }
            return head;
        }

        // ==================== 内部 ====================

        private static bool InSessionWindow(DateTime now, int hour, int minute)
        {
            var start = now.Date.AddHours(hour).AddMinutes(minute);
            return now >= start && now < start.AddMinutes(DurationMinutes);
        }

        private static Fighter Find(PlayerObject player)
        {
            for (var i = 0; i < _fighters.Count; i++)
                if (ReferenceEquals(_fighters[i].Player, player)) return _fighters[i];
            return null;
        }

        private static bool ResolveMaps()
        {
            if (_mapsResolved) return true;

            _waitingMap = Envir.GetMapByNameAndInstance("BATTLE_WAITING");
            _valorMap = Envir.GetMapByNameAndInstance("VALOR");

            if (_waitingMap == null || _valorMap == null)
            {
                if (!_mapErrorLogged)
                {
                    MQ.Enqueue($"战场地图缺失: waiting={_waitingMap != null}, valor={_valorMap != null} (地图未加载或文件不存在)");
                    _mapErrorLogged = true;
                }
                return false;
            }

            _waitSpawn = FindSpawn(_waitingMap, 0.5, 0.5);
            if (_waitSpawn.X == 95 && _waitSpawn.Y == 99) { } //脚本既定出生点可用则保留
            _redSpawn = FindSpawn(_valorMap, 0.25, 0.5);
            _blueSpawn = FindSpawn(_valorMap, 0.75, 0.5);

            _mapsResolved = true;
            return true;
        }

        private static Point FindSpawn(Map map, double fx, double fy)
        {
            var cx = (int)(map.Width * fx);
            var cy = (int)(map.Height * fy);

            for (var r = 0; r < 50; r++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    for (var dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue; //只查外圈
                        var x = cx + dx;
                        var y = cy + dy;
                        if (map.ValidPoint(x, y)) return new Point(x, y);
                    }
                }
            }

            return new Point(cx, cy);
        }

        private static void PlaceAt(PlayerObject p, Map map, Point pt)
        {
            if (p.Dead) p.Revive(p.Stats[Stat.HP], false);
            p.Teleport(map, pt);
            p.ChangeHP(p.Stats[Stat.HP]); //满血(顺带兜底复活后的血量)
            p.ChangeMP(p.Stats[Stat.MP]);
        }

        private static void RespawnFighter(Fighter f)
        {
            var p = f.Player;
            if (p == null || p.Node == null) return;
            PlaceAt(p, _valorMap, f.Team == 0 ? _redSpawn : _blueSpawn);
            p.ReceiveChat("【战场】你已在出生点复活, 继续战斗!", ChatType.Hint);
        }

        private static void Announce(string msg)
        {
            MQ.Enqueue(msg);
            for (var i = 0; i < Envir.Players.Count; i++)
                Envir.Players[i].ReceiveChat(msg, ChatType.System);
        }
    }
}
