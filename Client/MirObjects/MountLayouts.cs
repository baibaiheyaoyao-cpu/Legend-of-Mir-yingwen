using System.Collections.Generic;

namespace Client.MirObjects
{
    /// <summary>
    /// 外来坐骑Lib帧布局适配表。
    /// 引擎默认按Crystal规范从Lib取帧: 站立0-31(8方向x4) 走32-95(8x8) 跑96-143(8x6) 被击144-167(8x3) 攻击168-215(8x6)。
    /// 部分坐骑Lib拷自其它客户端, 内部排帧不同(实帧间夹空帧、每方向帧数不同), 导致骑乘方向错位/闪隐。
    /// 此处为这类坐骑登记档案, 把引擎帧号换算成该Lib的实际帧号; 未登记的坐骑走恒等映射(原行为不变)。
    /// </summary>
    internal static class MountLayouts
    {
        private class Profile
        {
            /// <summary>各动作段实帧基址(方向d的实帧起点 = 基址 + Stride*d), 以及段内步长</summary>
            public readonly int StandBase, WalkBase, RunBase, StruckBase, AttackBase, Stride;

            public Profile(int standBase, int walkBase, int runBase, int struckBase, int attackBase, int stride)
            {
                StandBase = standBase;
                WalkBase = walkBase;
                RunBase = runBase;
                StruckBase = struckBase;
                AttackBase = attackBase;
                Stride = stride;
            }
        }

        // 白象(36.Lib/37.Lib, 409帧): 每方向占8槽(实帧+空帧间隔)
        // 站立@0(4实) 走@64(6实) 跑@128(6实) 被击@192(3实) 攻击@256(6实)
        private static readonly Profile Elephant = new Profile(0, 64, 128, 192, 256, 8);

        private static readonly Dictionary<int, Profile> Profiles = new Dictionary<int, Profile>
        {
            { 36, Elephant },
            { 37, Elephant },
        };

        /// <summary>把引擎动作帧号(0-215)换算为Lib实际帧号; 无档案的坐骑原样返回</summary>
        public static int Translate(int mountType, int frameIndex)
        {
            Profile p;
            if (!Profiles.TryGetValue(mountType, out p)) return frameIndex;

            int dir, f;
            if (frameIndex < 32)              // 站立 8方向x4
            {
                dir = frameIndex >> 2;
                f = frameIndex & 3;
                return p.StandBase + p.Stride * dir + f;
            }
            if (frameIndex < 96)              // 走路 8方向x8(源只有6帧, 末尾乒乓复用4/3补齐)
            {
                dir = (frameIndex - 32) >> 3;
                f = (frameIndex - 32) & 7;
                if (f == 6) f = 4;
                else if (f == 7) f = 3;
                return p.WalkBase + p.Stride * dir + f;
            }
            if (frameIndex < 144)             // 跑步 8方向x6
            {
                dir = (frameIndex - 96) / 6;
                f = (frameIndex - 96) % 6;
                return p.RunBase + p.Stride * dir + f;
            }
            if (frameIndex < 168)             // 被击 8方向x3
            {
                dir = (frameIndex - 144) / 3;
                f = (frameIndex - 144) % 3;
                return p.StruckBase + p.Stride * dir + f;
            }
            if (frameIndex < 216)             // 攻击 8方向x6
            {
                dir = (frameIndex - 168) / 6;
                f = (frameIndex - 168) % 6;
                return p.AttackBase + p.Stride * dir + f;
            }
            return frameIndex;
        }
    }
}
