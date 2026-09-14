// [AI-Claude 2026-08-28] 弦月门战士AI(176): 韩服原型ChieftainSword 三种劈砍(DC/MC/SC) Image=357
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    // ============================================================
    // 弦月门战士（韩服原型：ChieftainSword 酋长剑士，AI 176）
    // 行为：近战追击 + 三种劈砍（Type 0/1/2 自动播 357.Lib 的
    //       Attack1/2/3 三套攻击动画，光效烤在帧里）
    // 伤害：0=物理劈(DC) 1=魔法劈(MC) 2=道术劈(SC)，仿ChieftainArcher三箭设计
    // 外观：表格 Image 填 357（普通怪物渲染，无Shift问题）
    // ============================================================
    public class ChieftainSword : MonsterObject
    {
        protected byte AttackRange = 1;   // 贴身1格内才算够得着

        protected internal ChieftainSword(MonsterInfo info) : base(info)
        {
        }

        protected override bool InAttackRange()
        {
            return CurrentMap == Target.CurrentMap && Functions.InRange(CurrentLocation, Target.CurrentLocation, AttackRange);
        }

        // 大脑：追不上就追（与目标重叠时随机挪开），够得着就打
        protected override void ProcessTarget()
        {
            if (Target == null) return;

            if (!InAttackRange())
            {
                if (CurrentLocation == Target.CurrentLocation)
                {
                    MirDirection direction = (MirDirection)Envir.Random.Next(8);
                    int rotation = Envir.Random.Next(2) == 0 ? 1 : -1;

                    for (int d = 0; d < 8; d++)
                    {
                        if (Walk(direction)) break;

                        direction = Functions.ShiftDirection(direction, rotation);
                    }
                }
                else
                    MoveTo(Target.CurrentLocation);
            }

            if (!CanAttack) return;

            if (InAttackRange())
            {
                Attack();
            }
        }

        // 出手：随机三种劈砍（动画+伤害类型联动）
        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;

            byte type = (byte)Envir.Random.Next(0, 3);   // 0/1/2 → 播Attack1/2/3动画

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = type });

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;

            int damage;
            DefenceType defence;
            switch (type)
            {
                case 1:
                    damage = GetAttackPower(Stats[Stat.MinMC], Stats[Stat.MaxMC]);   // 魔法劈
                    defence = DefenceType.MACAgility;
                    break;
                case 2:
                    damage = GetAttackPower(Stats[Stat.MinSC], Stats[Stat.MaxSC]);   // 道术劈
                    defence = DefenceType.MACAgility;
                    break;
                default:
                    damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);   // 物理劈
                    defence = DefenceType.ACAgility;
                    break;
            }

            if (damage == 0) return;

            DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, Target, damage, defence);
            ActionList.Add(action);
        }
    }
}