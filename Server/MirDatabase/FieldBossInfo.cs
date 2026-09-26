using System.Drawing;
using Server.MirEnvir;

namespace Server.MirDatabase
{
    /// <summary>
    /// 野外Boss系统 - 单条Boss配置.
    /// 数据来源: Envir\FieldBoss.txt (明文Tab分隔, 控制面板可编辑, 重启服务器生效).
    ///
    /// 文件格式(#开头为注释行):
    /// 启用  怪物名  地图  X  Y  每日刷新时间
    /// 示例:
    /// 1  WhiteBoneHero  3,4  -1  -1  14:00,20:00
    ///
    /// 地图列: 多个地图用逗号分隔, 每次刷新随机选一张; X/Y 填 -1 表示地图内随机刷点.
    /// 时间列: "时:分" 用逗号分隔多个时间点, 每天到点即刷(旧Boss存活则先移除).
    /// </summary>
    public class FieldBossInfo
    {
        public bool Enabled;
        public string MonsterName;
        public string MapFileName;
        public Point Location;
        public string SpawnTimes;

        public FieldBossInfo()
        {
            Enabled = true;
            MonsterName = string.Empty;
            MapFileName = string.Empty;
            Location = new Point(-1, -1);
            SpawnTimes = string.Empty;
        }

        public string ToLine()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                Enabled ? 1 : 0,
                MonsterName,
                MapFileName,
                Location.X,
                Location.Y,
                SpawnTimes);
        }

        public static List<TimeSpan> ParseTimes(string text)
        {
            List<TimeSpan> result = new List<TimeSpan>();

            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (string part in text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] ps = part.Split(':');
                if (ps.Length != 2) continue;

                int h, m;
                if (!int.TryParse(ps[0], out h) || !int.TryParse(ps[1], out m)) continue;
                if (h < 0 || h > 23 || m < 0 || m > 59) continue;

                result.Add(new TimeSpan(h, m, 0));
            }

            result.Sort();
            return result;
        }
    }

    /// <summary>
    /// 野外Boss系统 - FieldBoss.txt 加载/保存器.
    /// 解析失败只跳过该行并记录日志, 不会让整个服务端启动失败.
    /// </summary>
    public static class FieldBossLoader
    {
        private static MessageQueue MessageQueue
        {
            get { return MessageQueue.Instance; }
        }

        public static string FilePath
        {
            get { return Path.Combine(Settings.EnvirPath, "FieldBoss.txt"); }
        }

        public static List<FieldBossInfo> Load()
        {
            List<FieldBossInfo> list = new List<FieldBossInfo>();

            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath,
                    "# 野外Boss表 - Tab分隔, #开头为注释\r\n" +
                    "# 启用  怪物名  地图(逗号分隔多图随机)  X  Y  每日刷新时间(HH:mm逗号分隔)\r\n" +
                    "# 1  WhiteBoneHero  3,4  -1  -1  14:00,20:00\r\n");
                return list;
            }

            string[] lines = File.ReadAllLines(FilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                FieldBossInfo info = ParseLine(lines[i], i + 1);
                if (info != null) list.Add(info);
            }

            return list;
        }

        public static void Save(List<FieldBossInfo> list)
        {
            List<string> lines = new List<string>
            {
                "# 野外Boss表 - Tab分隔, #开头为注释",
                "# 启用  怪物名  地图(逗号分隔多图随机)  X  Y  每日刷新时间(HH:mm逗号分隔)",
                "# 1  WhiteBoneHero  3,4  -1  -1  14:00,20:00",
            };

            foreach (FieldBossInfo info in list)
                lines.Add(info.ToLine());

            File.WriteAllLines(FilePath, lines);
        }

        private static FieldBossInfo ParseLine(string line, int lineNo)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) return null;

            string[] parts = line.Split('\t');
            if (parts.Length < 6)
            {
                MessageQueue.Enqueue(string.Format("FieldBoss.txt 第{0}行格式错误(需要6列Tab分隔), 已跳过。", lineNo));
                return null;
            }

            FieldBossInfo info = new FieldBossInfo();

            if (!int.TryParse(parts[0].Trim(), out int enable))
            {
                MessageQueue.Enqueue(string.Format("FieldBoss.txt 第{0}行启用列必须为0或1, 已跳过。", lineNo));
                return null;
            }
            info.Enabled = enable != 0;

            info.MonsterName = parts[1].Trim();
            if (info.MonsterName.Length == 0)
            {
                MessageQueue.Enqueue(string.Format("FieldBoss.txt 第{0}行怪物名为空, 已跳过。", lineNo));
                return null;
            }

            info.MapFileName = parts[2].Trim();
            if (info.MapFileName.Length == 0)
            {
                MessageQueue.Enqueue(string.Format("FieldBoss.txt 第{0}行地图为空, 已跳过。", lineNo));
                return null;
            }

            int x, y;
            if (!int.TryParse(parts[3].Trim(), out x) || !int.TryParse(parts[4].Trim(), out y))
            {
                MessageQueue.Enqueue(string.Format("FieldBoss.txt 第{0}行坐标列必须为整数(-1为随机), 已跳过。", lineNo));
                return null;
            }
            info.Location = new Point(x, y);

            info.SpawnTimes = parts[5].Trim();
            if (FieldBossInfo.ParseTimes(info.SpawnTimes).Count == 0)
            {
                MessageQueue.Enqueue(string.Format("FieldBoss.txt 第{0}行没有有效时间点(如14:00,20:00), 已跳过。", lineNo));
                return null;
            }

            return info;
        }
    }
}
