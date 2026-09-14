// [AI-Claude 2026-08-28] 阳龙王AI(205): 韩服技能复刻v4 三阶段BOSS(召唤60%/狂暴30%) 六技能
using System.Collections.Generic;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    // 阳龙王 v4：三阶段BOSS + 特效绑定（客户端case配合）
    // Type绑定：0=普攻金剑气 | 1=烈风月形波 | 2=狂暴剑法(血≤30%) | 3=技能蓝爆
    // 远程Type0=气功波地面火墙
    public class MasterYangDragon : MonsterObject
    {
        private const byte SummonHPPercent = 60;   // 阶段2：召唤
        private const byte EnrageHPPercent = 30;   // 阶段3：狂暴
        private static readonly string[] SummonNames = { "弦月门战士", "弦月门法师", "弦月门道士", "弦月门弓手", "弦月门刺客" };

        private bool _summoned;
        private bool _enraged;
        private long _roarTime, _waveTime, _trapTime, _bindTime, _confuseTime, _pullTime;

        protected internal MasterYangDragon(MonsterInfo info) : base(info)
        {
        }

        protected override void ProcessAI()
        {
            if (Dead) return;

            // 阶段2：60%血 召唤玄月团五职业
            if (!_summoned && Stats[Stat.HP] > 0 && HP * 100 / Stats[Stat.HP] <= SummonHPPercent)
            {
                _summoned = true;
                SpawnSlaves();
            }

            // 阶段3：30%血 狂暴（攻速+20%，普攻切狂暴剑法特效；不想要攻速删这行）
            if (!_enraged && Stats[Stat.HP] > 0 && HP * 100 / Stats[Stat.HP] <= EnrageHPPercent)
            {
                _enraged = true;
                AttackSpeed = (ushort)System.Math.Max(400, AttackSpeed * 80 / 100);
            }

            if (Target != null)
            {
                Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

                // 狮子吼：15秒CD，5格全体眩晕
                if (Envir.Time >= _roarTime)
                {
                    List<MapObject> targets = FindAllTargets(5, CurrentLocation);
                    if (targets.Count > 0)
                    {
                        _roarTime = Envir.Time + 15000;
                        CastAnim(3);
                        foreach (MapObject t in targets)
                            PoisonTarget(t, 3, 3, PoisonType.Stun, 1000);
                    }
                }

                // 气功波：身边3格≥2人 → 远程动作0（客户端画地面火墙）+ 推离
                if (Envir.Time >= _waveTime)
                {
                    List<MapObject> near = FindAllTargets(3, CurrentLocation);
                    if (near.Count >= 2)
                    {
                        _waveTime = Envir.Time + 12000;
                        Broadcast(new S.ObjectRangeAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = 0, TargetID = Target.ObjectID });
                        ActionTime = Envir.Time + 300;
                        AttackTime = Envir.Time + AttackSpeed + 500;
                        int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
                        foreach (MapObject t in near)
                            t.Pushed(this, Functions.DirectionFromPoint(CurrentLocation, t.CurrentLocation), 2);
                        SinglePushAttack(damage);
                    }
                }

                // 捕绳剑：目标>4格拉到身边
                if (Envir.Time >= _pullTime && Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) > 4)
                {
                    _pullTime = Envir.Time + 15000;
                    CastAnim(3);
                    if (Envir.Random.Next(Settings.MagicResistWeight) >= Target.Stats[Stat.MagicResist])
                        Target.Teleport(CurrentMap, Functions.PointMove(CurrentLocation, Direction, 1));
                }

                // 困魔咒：麻痹
                if (Envir.Time >= _trapTime)
                {
                    _trapTime = Envir.Time + 20000;
                    CastAnim(3);
                    PoisonTarget(Target, 2, 5, PoisonType.Paralysis, 2000);
                }

                // 捕缚术：迟缓
                if (Envir.Time >= _bindTime)
                {
                    _bindTime = Envir.Time + 18000;
                    CastAnim(3);
                    PoisonTarget(Target, 2, 6, PoisonType.Slow, 2000);
                }

                // 迷魂术：茫然乱走
                if (Envir.Time >= _confuseTime)
                {
                    _confuseTime = Envir.Time + 22000;
                    CastAnim(3);
                    PoisonTarget(Target, 2, 5, PoisonType.Dazed, 2000);
                }

                // 冲锋：目标≥3格
                if (Functions.MaxDistance(CurrentLocation, Target.CurrentLocation) >= 3 && CanMove)
                    Walk(Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation));
            }

            base.ProcessAI();
        }

        // 出手：Type固定绑定特效
        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (damage == 0) return;

            if (Envir.Random.Next(4) == 0)
            {
                // 烈风击：Type1 → 月形冲击波 + 半月AOE
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = 1 });
                HalfmoonAttack(damage);
            }
            else if (_enraged)
            {
                // 狂暴劈砍：Type2 → 狂暴剑法特效
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = 2 });
                DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, Target, damage, DefenceType.MACAgility);
                ActionList.Add(action);
            }
            else
            {
                // 普通劈砍：Type0 → 金剑气弹道
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = 0 });
                DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, Target, damage, DefenceType.MACAgility);
                ActionList.Add(action);
            }

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;
        }

        // 施法动画（固定Type）
        private void CastAnim(byte type)
        {
            Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = type });
            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed + 500;
        }

        private void SpawnSlaves()
        {
            foreach (string name in SummonNames)
            {
                MonsterObject mob = GetMonster(Envir.GetMonsterInfo(name));
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