// ============================================================
// [AI-Claude 2026-08-29] 数据驱动怪物AI系统·通用执行器
// 用法: 怪物表格AI填224 + Envir\MonsterConfigs\<怪名>.ini写配方
//       无配置=普通近战(安全回退) 改配方只需重启不用编译
// 配套: MonsterProfile.cs(解析器) + case 224(注册)
// 技能模式: Single/Halfmoon/Line/Wide/AOE/Push/Pull/Summon
// 阶段: SummonAt血线召唤 / EnrageAt血线狂暴
// [AI-Claude] 重要: Anim=Range1/2/3时发远程封包(带TargetID)
//   客户端只有远程封包才更新TargetID 目标身上特效依赖它
// ============================================================
using System;
using System.Collections.Generic;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    public class ConfiguredMonster : MonsterObject
    {
        readonly MonsterProfile _profile;
        readonly long[] _cool;
        bool _summonDone, _enraged;

        protected internal ConfiguredMonster(MonsterInfo info) : base(info)
        {
            _profile = MonsterProfile.Get(info.Name);
            _cool = _profile != null ? new long[_profile.Skills.Count] : null;
        }

        protected override void ProcessAI()
        {
            if (!Dead && _profile != null)
            {
                int hpPct = Stats[Stat.HP] > 0 ? HP * 100 / Stats[Stat.HP] : 100;

                // 阶段:召唤
                if (!_summonDone && _profile.SummonAtHp >= 0 && hpPct <= _profile.SummonAtHp)
                {
                    _summonDone = true;
                    DoSummon(_profile.PhaseSummons);
                }

                // 阶段:狂暴
                if (!_enraged && _profile.EnrageAtHp >= 0 && hpPct <= _profile.EnrageAtHp)
                {
                    _enraged = true;
                    AttackSpeed = (ushort)Math.Max(400, AttackSpeed * _profile.EnragePercent / 100);
                }

                // 技能轮询: 冷却好->条件满足->几率过->释放(每周期最多一招)
                if (Target != null)
                {
                    Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

                    for (int i = 0; i < _profile.Skills.Count; i++)
                    {
                        MonsterSkillDef s = _profile.Skills[i];
                        if (Envir.Time < _cool[i]) continue;

                        if (s.MinDistance > 0 && Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) < s.MinDistance) continue;

                        if (s.MinTargets > 0 && FindAllTargets(Math.Max(1, s.Range), CurrentLocation).Count < s.MinTargets) continue;

                        if (s.Chance < 100 && Envir.Random.Next(100) >= s.Chance) continue;

                        _cool[i] = Envir.Time + s.Cooldown;
                        CastSkill(s);
                        break;
                    }
                }
            }

            base.ProcessAI();
        }

        void CastSkill(MonsterSkillDef s)
        {
            // [AI-Claude] 动画封包: 远程带TargetID(客户端特效依赖) 近战只有动作
            if (s.RangeAttack)
                Broadcast(new S.ObjectRangeAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = s.Anim, TargetID = Target.ObjectID });
            else
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = s.Anim });

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed + 500;

            // 伤害计算
            int damage = 0;
            if (s.Stat == "MC") damage = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]) * s.Rate / 100;
            else if (s.Stat == "SC") damage = GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]) * s.Rate / 100;
            else if (s.Stat != "NONE") damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]) * s.Rate / 100;

            switch (s.Mode)
            {
                case "SINGLE":
                    if (damage > 0)
                    {
                        DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, Target, damage, DefenceType.MACAgility);
                        ActionList.Add(action);
                    }
                    ApplyStatus(s, Target);
                    break;

                case "HALFMOON":
                    if (damage > 0) HalfmoonAttack(damage);
                    break;

                case "LINE":
                    if (damage > 0) LineAttack(damage, Math.Max(1, s.Range), 500, DefenceType.ACAgility, s.Push > 0);
                    break;

                case "WIDE":
                    if (damage > 0) WideLineAttack(damage, Math.Max(1, s.Range), 500, DefenceType.ACAgility, s.Push > 0, 3);
                    break;

                case "AOE":
                    {
                        List<MapObject> targets = FindAllTargets(Math.Max(1, s.Range), CurrentLocation);
                        foreach (MapObject t in targets)
                        {
                            if (damage > 0)
                            {
                                DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 400, t, damage, DefenceType.ACAgility);
                                ActionList.Add(action);
                            }
                            ApplyStatus(s, t);
                        }
                    }
                    break;

                case "PUSH":
                    {
                        List<MapObject> targets = FindAllTargets(Math.Max(1, s.Range), CurrentLocation);
                        foreach (MapObject t in targets)
                            t.Pushed(this, Functions.DirectionFromPoint(CurrentLocation, t.CurrentLocation), Math.Max(1, s.Push));
                        if (damage > 0)
                        {
                            DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, Target, damage, DefenceType.AC);
                            ActionList.Add(action);
                        }
                    }
                    break;

                case "PULL":
                    if (Envir.Random.Next(Settings.MagicResistWeight) >= Target.Stats[Stat.MagicResist])
                        Target.Teleport(CurrentMap, Functions.PointMove(CurrentLocation, Direction, 1));
                    ApplyStatus(s, Target);
                    break;

                case "SUMMON":
                    DoSummon(s.SummonList);
                    break;
            }
        }

        void ApplyStatus(MonsterSkillDef s, MapObject target)
        {
            if (target == null || string.IsNullOrEmpty(s.Status) || s.Status == "NONE") return;

            PoisonType type;
            switch (s.Status)
            {
                case "STUN": type = PoisonType.Stun; break;
                case "SLOW": type = PoisonType.Slow; break;
                case "PARALYSIS": type = PoisonType.Paralysis; break;
                case "DAZED": type = PoisonType.Dazed; break;
                case "GREEN": type = PoisonType.Green; break;
                case "RED": type = PoisonType.Red; break;
                case "BLEEDING": type = PoisonType.Bleeding; break;
                default: return;
            }

            PoisonTarget(target, s.StatusChance, s.StatusTime, type, 1000);
        }

        void DoSummon(List<KeyValuePair<string, int>> list)
        {
            foreach (KeyValuePair<string, int> pair in list)
            {
                for (int i = 0; i < pair.Value; i++)
                {
                    MonsterObject mob = GetMonster(Envir.GetMonsterInfo(pair.Key));
                    if (mob == null) continue;

                    if (!mob.Spawn(CurrentMap, Front))
                        mob.Spawn(CurrentMap, CurrentLocation);

                    mob.Target = Target;
                    mob.ActionTime = Envir.Time + 2000;
                    SlaveList.Add(mob);
                }
            }
        }
    }
}
