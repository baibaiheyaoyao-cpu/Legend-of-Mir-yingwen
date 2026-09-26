using System.Collections.Generic;
using System.Drawing;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirSounds;

namespace Client.MirObjects
{
    /// <summary>
    /// CustomMagic数据驱动技能配置表(S.CustomMagicConfigs, 登录时下发).
    /// 特效段/描述/音效按原版CustomMagic INI渲染; 无配置的技能走引擎内置表现, 安全降级.
    /// </summary>
    public static class CustomMagicSettings
    {
        private static readonly Dictionary<Spell, CustomMagicConfig> Configs = new Dictionary<Spell, CustomMagicConfig>();

        public static void Set(List<CustomMagicConfig> list)
        {
            Configs.Clear();
            if (list == null) return;
            foreach (var cfg in list)
                if (cfg != null) Configs[cfg.Spell] = cfg;
        }

        public static CustomMagicConfig Get(Spell spell)
        {
            Configs.TryGetValue(spell, out var cfg);
            return cfg;
        }

        /// <summary>该技能是否有"可渲染"的自定义特效段(有则渲染优先于引擎内置硬编码; File全0=视为无配置, 回落引擎特效)</summary>
        public static bool Has(Spell spell)
        {
            CustomMagicConfig cfg = Get(spell);
            if (cfg == null) return false;

            foreach (CustomMagicSegment s in cfg.Segments)
                if (s.File > 0 && s.PlayCount > 0) return true;

            return false; //如气流术秘笈INI全File=0: 曾因此屏蔽引擎特效导致无画面
        }

        /// <summary>
        /// 原版CustomMagic库索引 → 本客户端Lib(1基, 已按帧尺寸三重交叉验证).
        /// 1=Magic 2=Magic2 3=Magic3 4=Magic4 5=Magic5 6=MagicC 7=MagicD 8=MagicMW 9=Magic_32bit(秘籍主库)
        /// 0=无特效(调用方已跳过), 其他=未收录返回null安全降级.
        /// </summary>
        public static MLibrary EffectLibrary(byte file)
        {
            switch (file)
            {
                case 1: return Libraries.Magic;
                case 2: return Libraries.Magic2;
                case 3: return Libraries.Magic3;
                case 4: return Libraries.Magic4;
                case 5: return Libraries.Magic5;
                case 6: return Libraries.MagicC;
                case 7: return Libraries.MagicD;
                case 8: return Libraries.MagicMW;
                case 9: return Libraries.Magic_32bit;
                default: return null;
            }
        }

        /// <summary>
        /// 施法/命中渲染: Self/Self2贴身(支持8方向序列), Magic脚下法阵,
        /// Fly/Explosion/Target按Delay落于目标点. 参数全部来自下发配置.
        /// 绘制模式: DrawMode=1 透明绘制(Blend), 0=普通; CalcDir=按目标方向取向.
        /// </summary>
        public static void SpawnCastEffects(PlayerObject caster, Spell spell, uint targetID, Point targetPoint)
        {
            CustomMagicConfig cfg = Get(spell);
            if (cfg == null || caster == null) return;

            if (cfg.CastSoundId > 0)
                SoundManager.PlaySound(cfg.CastSoundId);

            foreach (CustomMagicSegment seg in cfg.Segments)
            {
                if (seg.File == 0) continue; // 原版File=0=该段无特效
                MLibrary lib = EffectLibrary(seg.File);
                if (lib == null || seg.PlayCount <= 0) continue;

                int duration = Math.Max(200, (int)seg.PlayTime);
                bool blend = seg.DrawMode == 1; // 透明绘制

                // 落点三优先级: 活目标实时位置 > 施法点 > 施法者脚下(引擎惯例 PlayerObject.cs:4054)
                MapObject liveTarget = targetID != 0 ? MapControl.GetObject(targetID) : null;
                Point targetAt = caster.CurrentLocation;
                if (liveTarget != null) targetAt = liveTarget.CurrentLocation;
                else if (targetPoint.X != 0 || targetPoint.Y != 0) targetAt = targetPoint;
                bool hasTargetAt = liveTarget != null || targetPoint.X != 0 || targetPoint.Y != 0;

                // 计算方向: 勾选时朝目标取向, 否则人物朝向
                int drawDir = (int)caster.Direction;
                if (seg.CalcDir && hasTargetAt)
                    drawDir = (int)Functions.DirectionFromPoint(caster.CurrentLocation, targetAt);

                switch (seg.Kind)
                {
                    case "Self":
                    case "Self2":
                        if (seg.DirCount > 0)
                        {
                            //原版帧布局: 每方向 PlayCount+EmptyCount 帧一组, 8方向步长=组帧数
                            int stride = seg.PlayCount + seg.EmptyCount;
                            if (stride <= 0) stride = seg.DirCount;
                            caster.Effects.Add(new DirectionalEffect(lib, seg.StartIndex, seg.PlayCount,
                                duration, caster, drawDir, stride) { Blend = blend });
                        }
                        else
                            caster.Effects.Add(new Effect(lib, seg.StartIndex, seg.PlayCount, duration, caster) { Blend = blend });
                        break;

                        case "Magic": // 法阵(持续型旋风/地圈): 钉在施法点——服务端持续型AOE伤害中心=施法点固定(CastCustomMagicProfile), 特效跟目标走会与伤害区脱节
                            {
                                //落点与飞行段相反: 施法点 > 活目标 > 脚下(吸魔炎风点怪施放时, 旋风钉在施法点不跟怪跑)
                                Point magicAt = (targetPoint.X != 0 || targetPoint.Y != 0) ? targetPoint
                                    : (liveTarget != null ? liveTarget.CurrentLocation : caster.CurrentLocation);
                                var e = new Effect(lib, seg.StartIndex, seg.PlayCount, Math.Max(duration, 600), magicAt) { Blend = blend };
                                if (seg.Repeat) { e.Repeat = true; e.RepeatUntil = CMain.Time + duration * 6; }
                                MapControl.Effects.Add(e);
                            }
                            break;

                    case "Fly":
                    case "Explosion":
                    case "Target":
                        {
                            //飞行/爆炸/目标: 落在目标身上并实时跟随(目标移动跟着走, 延迟爆也炸在目标"当时"位置);
                            //目标死亡/消失则落在施法点, 再退到自己脚下. 方向序列(影子/剑弧)按 施法者→目标 方向取帧组.
                            int baseIdx = seg.StartIndex;
                            if (seg.DirCount > 0)
                            {
                                int stride = seg.PlayCount + seg.EmptyCount;
                                if (stride <= 0) stride = seg.DirCount;
                                int flyDir = hasTargetAt
                                    ? (int)Functions.DirectionFromPoint(caster.CurrentLocation, targetAt)
                                    : (int)caster.Direction;
                                baseIdx += flyDir * stride;
                            }

                            Effect e = liveTarget != null
                                ? new Effect(lib, baseIdx, seg.PlayCount, duration, liveTarget) { Blend = blend } //挂目标对象: 跟着目标飞
                                : new Effect(lib, baseIdx, seg.PlayCount, duration, targetAt) { Blend = blend };  //无目标: 施法点/脚下

                            if (seg.Delay > 0) e.SetStart(CMain.Time + seg.Delay); //SetStart同步重算NextFrame(直接改Start会让帧表在延迟期间提前走完)
                            if (seg.Repeat) { e.Repeat = true; e.RepeatUntil = CMain.Time + duration * 6; }
                            //修复列表错配: 挂Owner的特效必须加进Owner.Effects——否则Remove()删的是Owner列表(实际在MapControl列表)等于没删,
                            //特效永不消失且CurrentFrame越过Count后无限自增, 播进库里后续帧("停不下来一直往下播"根因, 带目标施放必触发)
                            if (liveTarget != null) liveTarget.Effects.Add(e);
                            else MapControl.Effects.Add(e);

                            if (seg.Kind == "Explosion" && cfg.ExplosionSoundId > 0)
                                SoundManager.PlaySound(cfg.ExplosionSoundId);
                            if (seg.Kind == "Fly" && cfg.FlySoundId > 0)
                                SoundManager.PlaySound(cfg.FlySoundId);
                        }
                        break;
                }
            }
        }
    }
}
