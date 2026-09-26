using Server.MirDatabase;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    // [AI 201] 幻灵: 616模型宠物(召唤幻灵Hling=109)
    // 近身爆发: 自身7x7(Fullmoon distance=3)范围MAC伤害
    // 素材: kehux\Data\Monster\616.Lib 帧表自带攻击动作+特效层(370-379) 客户端零特殊代码
    public class PhantomSpiritPet : MonsterObject
    {
        protected internal PhantomSpiritPet(MonsterInfo info)
            : base(info)
        {
        }

        protected override bool InAttackRange()
        {
            return CurrentMap == Target.CurrentMap && Functions.InRange(CurrentLocation, Target.CurrentLocation, 3);
        }

        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;
            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Broadcast(new S.ObjectRangeAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, TargetID = Target.ObjectID, Type = 0 });

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (damage == 0) return;

            //7x7: 自身±3 范围MAC伤害
            FullmoonAttack(damage, 500, DefenceType.MAC, -1, 3);
        }

        protected override void ProcessTarget()
        {
            if (Target == null || !CanAttack) return;

            if (InAttackRange())
            {
                Attack();
                return;
            }

            if (Envir.Time < ShockTime)
            {
                Target = null;
                return;
            }

            if (Master != null && Functions.MaxDistance(CurrentLocation, Master.CurrentLocation) > 5)
                MoveTo(Master.CurrentLocation);
            else
                MoveTo(Target.CurrentLocation);
        }
    }
}
