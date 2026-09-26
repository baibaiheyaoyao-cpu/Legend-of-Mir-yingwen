using Server.MirDatabase;
using Server.MirEnvir;

namespace Server
{
    /// <summary>
    /// GM测试街生成器: 把全服NPC按"唯一脚本"复制一份随机摆到指定地图(默认GM之家)的可走格子上, 供挨个测试.
    /// 复制体名字加"测-"前缀; 重建模式会先删除旧的"测-"NPC(幂等).
    /// 供 NPC信息管理 的"GM测试街"按钮调用, 操作编辑库(Envir.Edit)并立即写盘.
    /// </summary>
    public static class GmNpcBuilder
    {
        public static string Build(Envir envir, MapInfo target, bool rebuild)
        {
            if (target == null) return "目标地图不存在。";

            var existing = envir.NPCInfoList.Where(n => n.MapIndex == target.Index && n.Name != null && n.Name.StartsWith("测-")).ToList();
            if (existing.Count > 0)
            {
                if (!rebuild)
                    return $"地图上已有 {existing.Count} 个'测-'NPC(未重建, 如需重新生成请勾选'先删除旧的')。";

                foreach (var n in existing) envir.Remove(n);
            }

            //按唯一脚本挑选源NPC(同脚本多地摆放只复制一份; 该脚本已在本图的原生NPC不再复制)
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var onMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in envir.NPCInfoList.Where(n => n.MapIndex == target.Index))
            {
                var k = (n.FileName ?? "").Trim();
                if (k.Length > 0) onMap.Add(k);
            }

            var sources = new List<NPCInfo>();
            foreach (var n in envir.NPCInfoList)
            {
                var k = (n.FileName ?? "").Trim();
                if (k.Length == 0) continue;
                if (onMap.Contains(k)) continue;
                if (!seen.Add(k)) continue;
                sources.Add(n);
            }

            if (sources.Count == 0) return "没有可复制的NPC。";

            //随机布局: 优先用服务端同款解析(v0/v1)挑可走格子; 解析失败退化为全图随机
            var slots = LoadWalkableSlots(target.FileName, sources.Count);
            var rng = new Random();
            var used = new HashSet<long>();

            int placed = 0;
            foreach (var src in sources)
            {
                int x, y;
                if (slots.Count > 0)
                {
                    int guard = 0;
                    do
                    {
                        var p = slots[rng.Next(slots.Count)];
                        x = p.X; y = p.Y;
                        guard++;
                    } while (!used.Add((long)x << 32 | (uint)y) && guard < 50);
                }
                else
                {
                    x = 10 + rng.Next(181);
                    y = 10 + rng.Next(181);
                }

                envir.NPCInfoList.Add(new NPCInfo
                {
                    Index = ++envir.NPCIndex,
                    MapIndex = target.Index,
                    FileName = src.FileName,
                    Name = "测-" + src.Name,
                    Location = new System.Drawing.Point(x, y),
                    Image = src.Image,
                    Rate = src.Rate,
                    CanTeleportTo = false,
                    ShowOnBigMap = false,
                });
                placed++;
            }

            envir.SaveDB();

            return $"已在 '{target.Title}'({target.FileName}) 随机摆放 {placed} 个'测-'NPC" +
                   (slots.Count > 0 ? $" (基于{slots.Count}个可走格子)" : " (地图解析失败, 全图盲随机)") +
                   $"。重启服务器后生效。";
        }

        /// <summary>按服务端 Map.cs 的 v0/v1 解析逻辑读取地图可走格子(与游戏内碰撞一致).</summary>
        private static List<System.Drawing.Point> LoadWalkableSlots(string mapFile, int need)
        {
            var result = new List<System.Drawing.Point>();
            var path = Path.Combine("Maps", mapFile + ".map");
            if (!File.Exists(path)) return result;

            try
            {
                var b = File.ReadAllBytes(path);
                int w, h, cellStart, cellSize, xor = 0;

                bool v1 = b[0] == 0x10 && b[2] == 0x61 && b[7] == 0x31 && b[14] == 0x31;

                if (v1)
                {
                    xor = BitConverter.ToInt16(b, 23);
                    w = BitConverter.ToInt16(b, 21) ^ xor;
                    h = BitConverter.ToInt16(b, 25) ^ xor;
                    cellStart = 54; cellSize = 14;
                }
                else
                {
                    w = BitConverter.ToInt16(b, 0);
                    h = BitConverter.ToInt16(b, 2);
                    cellStart = 52; cellSize = 12;
                }

                if (w <= 0 || h <= 0 || cellStart + w * h * cellSize > b.Length) return result;

                for (int x = 0; x < w && result.Count < Math.Max(need * 3, 3000); x++)
                    for (int y = 0; y < h; y++)
                    {
                        int off = cellStart + (x * h + y) * cellSize;
                        bool wall = v1
                            ? ((BitConverter.ToInt32(b, off) ^ 0xAA38AA38) & 0x20000000) != 0
                              || ((BitConverter.ToInt16(b, off + 6) ^ xor) & 0x8000) != 0
                            : (BitConverter.ToInt16(b, off) & 0x8000) != 0
                              || (BitConverter.ToInt16(b, off + 2) & 0x8000) != 0
                              || (BitConverter.ToInt16(b, off + 4) & 0x8000) != 0;

                        if (!wall) result.Add(new System.Drawing.Point(x, y));
                    }
            }
            catch
            {
                result.Clear();
            }
            return result;
        }
    }
}
