using System.Drawing;
using Server.MirDatabase;
using Server.MirObjects;

namespace Server.MirEnvir
{
    public class FieldBossEntry
    {
        public FieldBossInfo Info;
        public MonsterObject CurrentBoss;
        public DateTime LastSpawnAt = DateTime.MinValue;
    }

    /// <summary>
    /// 野外Boss系统 - 管理多条Boss配置.
    /// 每条配置按每日时间点(HH:mm)到点刷新, 旧Boss存活则先移除; 手动"立即刷新"不影响时间表.
    /// </summary>
    public class FieldBossSystem
    {
        private const int ProcessDelay = 2000;
        private long ProcessTime;

        public List<FieldBossEntry> Entries = new List<FieldBossEntry>();

        private static Envir Envir
        {
            get { return Envir.Main; }
        }

        protected static MessageQueue MessageQueue
        {
            get { return MessageQueue.Instance; }
        }

        public FieldBossSystem(List<FieldBossInfo> infoList)
        {
            foreach (FieldBossInfo info in infoList)
                Entries.Add(CreateEntry(info));
        }

        private FieldBossEntry CreateEntry(FieldBossInfo info)
        {
            FieldBossEntry entry = new FieldBossEntry { Info = info };

            //LastSpawnAt 初始化为今天已过的最近时间点, 避免服务器重启后补刷已过时间的Boss
            List<TimeSpan> times = FieldBossInfo.ParseTimes(info.SpawnTimes);
            DateTime now = DateTime.Now;

            if (times.Count == 0)
            {
                entry.LastSpawnAt = now;
                return entry;
            }

            for (int i = times.Count - 1; i >= 0; i--)
            {
                DateTime point = now.Date + times[i];
                if (point <= now)
                {
                    entry.LastSpawnAt = point;
                    return entry;
                }
            }

            //今天的所有时间点都还没到, 设为第一个点前1分钟
            entry.LastSpawnAt = now.Date + times[0] - TimeSpan.FromMinutes(1);
            return entry;
        }

        /// <summary>面板增删配置后调用, 让运行中的系统与配置列表对齐</summary>
        public void SyncEntries(List<FieldBossInfo> infoList)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (infoList.Contains(Entries[i].Info)) continue;

                if (Entries[i].CurrentBoss != null && !Entries[i].CurrentBoss.Dead && Entries[i].CurrentBoss.Node != null)
                    Entries[i].CurrentBoss.Despawn();

                Entries.RemoveAt(i);
            }

            foreach (FieldBossInfo info in infoList)
            {
                bool exists = false;
                for (int i = 0; i < Entries.Count; i++)
                    if (Entries[i].Info == info) { exists = true; break; }

                if (!exists) Entries.Add(CreateEntry(info));
            }
        }

        public void Process()
        {
            if (Envir.Time < ProcessTime) return;
            ProcessTime = Envir.Time + ProcessDelay;

            for (int i = 0; i < Entries.Count; i++)
            {
                FieldBossEntry entry = Entries[i];

                if (entry.CurrentBoss != null && (entry.CurrentBoss.Dead || entry.CurrentBoss.Node == null))
                    entry.CurrentBoss = null;

                if (!entry.Info.Enabled) continue;

                if (IsDue(entry))
                {
                    if (!SpawnBoss(entry))
                        entry.LastSpawnAt = DateTime.Now; //刷新失败也跳过该时间点, 避免每2秒重试刷屏
                }
            }
        }

        private bool IsDue(FieldBossEntry entry)
        {
            DateTime now = DateTime.Now;

            foreach (TimeSpan t in FieldBossInfo.ParseTimes(entry.Info.SpawnTimes))
            {
                DateTime point = now.Date + t;
                if (entry.LastSpawnAt < point && now >= point) return true;
            }

            return false;
        }

        public bool SpawnNow(FieldBossEntry entry)
        {
            return SpawnBoss(entry);
        }

        public bool SpawnBoss(FieldBossEntry entry)
        {
            try
            {
                MonsterInfo info = Envir.GetMonsterInfo(entry.Info.MonsterName);
                if (info == null)
                {
                    MessageQueue.Enqueue("野外Boss刷新失败: 找不到怪物 - " + entry.Info.MonsterName);
                    return false;
                }

                string[] mapNames = entry.Info.MapFileName.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (mapNames.Length == 0)
                {
                    MessageQueue.Enqueue("野外Boss刷新失败: 未配置地图 - " + entry.Info.MonsterName);
                    return false;
                }

                Map map = null;
                for (int i = 0; i < 5; i++)
                {
                    map = Envir.GetMapByNameAndInstance(mapNames[Envir.Random.Next(mapNames.Length)].Trim());
                    if (map != null) break;
                }

                if (map == null)
                {
                    MessageQueue.Enqueue("野外Boss刷新失败: 找不到地图 - " + entry.Info.MapFileName);
                    return false;
                }

                //到点刷新时旧Boss还活着则先移除, 保证每个时间点都有新Boss
                if (entry.CurrentBoss != null && !entry.CurrentBoss.Dead && entry.CurrentBoss.Node != null)
                    entry.CurrentBoss.Despawn();

                Point location = new Point(entry.Info.Location.X, entry.Info.Location.Y);
                if (location.X < 0 || location.Y < 0)
                {
                    location = GetRandomPoint(map);
                    if (location.X < 0)
                    {
                        MessageQueue.Enqueue("野外Boss刷新失败: 地图 " + map.Info.FileName + " 未找到可用坐标。");
                        return false;
                    }
                }

                MonsterObject boss = MonsterObject.GetMonster(info);
                if (boss == null) return false;

                if (!boss.Spawn(map, location))
                {
                    location = GetRandomPoint(map);
                    if (location.X < 0 || !boss.Spawn(map, location))
                    {
                        MessageQueue.Enqueue("野外Boss刷新失败: 地图 " + map.Info.FileName + " 坐标不可用。");
                        return false;
                    }
                }

                entry.CurrentBoss = boss;
                entry.LastSpawnAt = DateTime.Now;

                MessageQueue.Enqueue(string.Format("野外Boss刷新: {0} -> {1} ({2},{3})", info.Name, map.Info.Title, boss.CurrentLocation.X, boss.CurrentLocation.Y));

                return true;
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
                return false;
            }
        }

        private Point GetRandomPoint(Map map)
        {
            for (int i = 0; i < 100; i++)
            {
                Point p = new Point(Envir.Random.Next(map.Width), Envir.Random.Next(map.Height));
                if (map.ValidPoint(p)) return p;
            }
            return new Point(-1, -1);
        }

        /// <summary>下个刷新时间点(供面板显示)</summary>
        public DateTime? GetNextSpawnAt(FieldBossEntry entry)
        {
            List<TimeSpan> times = FieldBossInfo.ParseTimes(entry.Info.SpawnTimes);
            if (times.Count == 0) return null;

            DateTime now = DateTime.Now;
            foreach (TimeSpan t in times)
            {
                DateTime point = now.Date + t;
                if (point > now) return point;
            }

            return now.Date + times[0] + TimeSpan.FromDays(1);
        }
    }
}
