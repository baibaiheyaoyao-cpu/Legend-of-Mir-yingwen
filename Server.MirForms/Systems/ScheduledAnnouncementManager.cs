using System.Text.Encodings.Web;
using System.Text.Json;

namespace Server
{
    public enum ScheduledAnnouncementRepeat
    {
        Once = 0,      // 单次
        Daily = 1,     // 每天固定时间
        Interval = 2,  // 循环间隔(分钟)
    }

    public class ScheduledAnnouncement
    {
        public string Message { get; set; } = string.Empty;
        public DateTime NextRun { get; set; }
        public ScheduledAnnouncementRepeat Repeat { get; set; } = ScheduledAnnouncementRepeat.Once;
        public int IntervalMinutes { get; set; } = 10;

        public string RepeatText
        {
            get
            {
                switch (Repeat)
                {
                    case ScheduledAnnouncementRepeat.Daily: return "每天";
                    case ScheduledAnnouncementRepeat.Interval: return "每" + IntervalMinutes + "分钟";
                    default: return "单次";
                }
            }
        }
    }

    public static class ScheduledAnnouncementManager
    {
        private static string FilePath => Path.Combine(Settings.EnvirPath, "ScheduledAnnouncements.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static List<ScheduledAnnouncement> Items { get; } = new List<ScheduledAnnouncement>();

        public static void Load()
        {
            Items.Clear();

            try
            {
                if (!File.Exists(FilePath)) return;

                var list = JsonSerializer.Deserialize<List<ScheduledAnnouncement>>(File.ReadAllText(FilePath));

                if (list != null)
                    Items.AddRange(list.Where(x => !string.IsNullOrWhiteSpace(x.Message)));
            }
            catch (Exception ex)
            {
                MessageQueue.Instance.Enqueue("[定时公告] 读取配置失败: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(FilePath, JsonSerializer.Serialize(Items, JsonOptions));
            }
            catch (Exception ex)
            {
                MessageQueue.Instance.Enqueue("[定时公告] 保存配置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 检查到期的公告: 单次发送后移除, 每天/循环模式推进下次发送时间。
        /// 返回本次需要发送的公告列表。
        /// </summary>
        public static List<ScheduledAnnouncement> ProcessDue(DateTime now)
        {
            var fired = new List<ScheduledAnnouncement>();
            var changed = false;

            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];

                if (now < item.NextRun) continue;

                fired.Add(item);

                switch (item.Repeat)
                {
                    case ScheduledAnnouncementRepeat.Daily:
                        while (item.NextRun <= now)
                            item.NextRun = item.NextRun.AddDays(1);
                        break;
                    case ScheduledAnnouncementRepeat.Interval:
                        var minutes = Math.Max(1, item.IntervalMinutes);
                        while (item.NextRun <= now)
                            item.NextRun = item.NextRun.AddMinutes(minutes);
                        break;
                    default:
                        Items.RemoveAt(i);
                        break;
                }

                changed = true;
            }

            if (changed) Save();

            return fired;
        }
    }
}
