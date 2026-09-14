using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    // [AI 200] 风灵: 615模型宠物(召唤风灵Yling=108)
    // 远程弹道攻击, 命中点5x5范围MAC伤害(伤害在目标位置=匹配客户端弹道+512x512绽放特效)
    // 素材: kehux\Data\Monster\615.Lib 弹道520-597(8方向x8帧) 命中特效710-727(18帧)
    public class WindSpiritPet : MonsterObject
    {
        public byte AttackRange = 7;

        protected internal WindSpiritPet(MonsterInfo info)
            : base(info)
        {
            Direction = MirDirection.DownLeft;
        }

        protected override bool InAttackRange()
        {
            return CurrentMap == Target.CurrentMap && Functions.InRange(CurrentLocation, Target.CurrentLocation, AttackRange);
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

            //5x5: 命中点(目标位置)±2 全体MAC伤害 (弹道视觉=客户端615专属case)
            Point hitCenter = Target.CurrentLocation;
            for (int y = hitCenter.Y - 2; y <= hitCenter.Y + 2; y++)
            {
                for (int x = hitCenter.X - 2; x <= hitCenter.X + 2; x++)
                {
                    Point p = new Point(x, y);
                    if (!CurrentMap.ValidPoint(p)) continue;

                    Cell cell = CurrentMap.GetCell(p);
                    if (cell == null || cell.Objects == null) continue;

                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (ob.Race != ObjectType.Player && ob.Race != ObjectType.Monster && ob.Race != ObjectType.Hero) continue;
                        if (ob.Dead || !ob.IsAttackTarget(this)) continue;

                        DelayedAction action = new DelayedAction(DelayedType.RangeDamage, Envir.Time + 800, ob, damage, DefenceType.MAC, false);
                        ActionList.Add(action);
                    }
                }
            }
        }

        protected override void ProcessTarget()
        {
            if (Target == null || !CanAttack) return;

            if (Master != null)
                MoveTo(Master.CurrentLocation);

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

            MoveTo(Target.CurrentLocation);
        }
    }
}
