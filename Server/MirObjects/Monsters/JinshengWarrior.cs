// [AI-Claude 2026-08-28] 金胜战士AI(204): 人形战士外形(SepWarrior式GetInfo伪装) + 双龙斩 + 冲锋
using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    // ============================================================
    // 金胜战士 AI（AI编号204，引擎空闲号，全库仅此怪使用）
    // 模板：SepWarrior
    // 行为：追击(可2格冲锋) + 近战平砍 + 1/3几率双龙斩(带几率眩晕)
    // 外观：人形怪——按"玩家战士"渲染(见GetInfo)，表格Image字段不生效
    // ============================================================
    public class JinshengWarrior : MonsterObject
    {
        // ------ 人形外观参数（想换造型改这两个数字）------
        // 男号素材：武器 Data\CWeapon\29.Lib  衣服 Data\CArmour\6.Lib
        // 用LibraryViewer翻 CWeapon / CArmour 文件夹挑喜欢的编号
        protected const short LookWeapon = 29;
        protected const short LookArmour = 6;

        protected byte AttackRange = 1;   // 攻击距离：贴身1格

        protected internal JinshengWarrior(MonsterInfo info) : base(info)
        {
        }

        // 够得着的定义：同地图且1格范围内
        protected override bool InAttackRange()
        {
            return CurrentMap == Target.CurrentMap && Functions.InRange(CurrentLocation, Target.CurrentLocation, AttackRange);
        }

        // 大脑：够不着就追（Walk带冲锋），够得着就打
        protected override void ProcessTarget()
        {
            if (Target == null) return;

            if (!InAttackRange())
            {
                if (CurrentLocation == Target.CurrentLocation)
                {
                    // 与目标重叠：随机方向挪开
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

        // 出手：1/3几率双龙斩，其余走基类平砍
        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;
            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (damage == 0) return;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);

            if (Envir.Random.Next(3) == 0)
            {
                // 双龙斩：客户端自动播放玩家战士同款动画(Spell.TwinDrakeBlade)
                Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Spell = Spell.TwinDrakeBlade });
                Target.Attacked(this, (int)(damage * 0.8), DefenceType.ACAgility);
                ProjectileAttack((int)(damage * 0.8), DefenceType.ACAgility);

                // 几率附加眩晕毒（等级压制+抗毒判定）
                if (((Target.Race != ObjectType.Player || Settings.PvpCanResistPoison) && (Envir.Random.Next(Settings.PoisonAttackWeight) >= Target.Stats[Stat.PoisonResist])) && (Target.Level <= Level + 8 && Envir.Random.Next(20) <= 5))
                {
                    Target.ApplyPoison(new Poison { PType = PoisonType.Stun, Duration = 5, TickSpeed = 1000 }, this);
                    Target.Broadcast(new S.ObjectEffect { ObjectID = Target.ObjectID, Effect = SpellEffect.TwinDrakeBlade });
                }
            }
            else
                base.Attack();
        }

        // 冲锋移动（SepWarrior原版逻辑）：先尝试一次跑2格，被挡退回走1格
        public bool Walk(MirDirection dir, bool br = false)
        {
            if (!CanMove) return false;

            var temploc = Functions.PointMove(CurrentLocation, dir, 1);

            if (!CurrentMap.ValidPoint(temploc)) return false;

            var cell = CurrentMap.GetCell(temploc);

            if (cell.Objects != null)
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (!ob.Blocking) continue;
                    return false;
                }

            Point location = Functions.PointMove(CurrentLocation, dir, 2);

            if (!CurrentMap.ValidPoint(location)) return false;

            cell = CurrentMap.GetCell(location);

            bool isBreak = br;

            if (cell.Objects != null)
                for (int i = 0; i < cell.Objects.Count; i++)
                {
                    MapObject ob = cell.Objects[i];
                    if (!ob.Blocking) continue;
                    isBreak = true;
                    break;
                }

            if (isBreak)
            {
                location = Functions.PointMove(CurrentLocation, dir, 1);

                if (!CurrentMap.ValidPoint(location)) return false;

                cell = CurrentMap.GetCell(location);

                if (cell.Objects != null)
                    for (int i = 0; i < cell.Objects.Count; i++)
                    {
                        MapObject ob = cell.Objects[i];
                        if (!ob.Blocking) continue;
                        return false;
                    }
            }

            CurrentMap.GetCell(CurrentLocation).Remove(this);

            Direction = dir;
            RemoveObjects(dir, 1);
            CurrentLocation = location;
            CurrentMap.GetCell(CurrentLocation).Add(this);
            AddObjects(dir, 1);

            if (Hidden)
            {
                Hidden = false;

                for (int i = 0; i < Buffs.Count; i++)
                {
                    if (Buffs[i].Type != BuffType.Hiding) continue;

                    Buffs[i].ExpireTime = 0;
                    break;
                }
            }

            CellTime = Envir.Time + 500;
            ActionTime = Envir.Time + 300;
            MoveTime = Envir.Time + MoveSpeed;
            if (MoveTime > AttackTime)
                AttackTime = MoveTime;

            InSafeZone = CurrentMap.GetSafeZone(CurrentLocation) != null;

            if (isBreak)
                Broadcast(new S.ObjectWalk { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });
            else
                Broadcast(new S.ObjectRun { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            cell = CurrentMap.GetCell(CurrentLocation);

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                if (cell.Objects[i].Race != ObjectType.Spell) continue;
                SpellObject ob = (SpellObject)cell.Objects[i];

                ob.ProcessSpell(this);
            }

            return true;
        }

        // 人形怪死亡：尸体立即消失不留骨架（SepWarrior原版）
        public override void Die()
        {
            if (Dead) return;

            HP = 0;
            Dead = true;

            DeadTime = 0;

            Broadcast(new S.ObjectDied { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation, Type = (byte)(Master != null ? 1 : 0) });

            if (EXPOwner != null && Master == null && EXPOwner.Race == ObjectType.Player)
                EXPOwner.WinExp(Experience, Level);

            if (Respawn != null)
                Respawn.Count--;

            if (Master == null)
                Drop();

            Master = null;

            PoisonList.Clear();
            Envir.MonsterCount--;

            if (CurrentMap != null)
                CurrentMap.MonsterCount--;
        }

        // ★人形怪核心：出生电报换成"玩家"封包 → 客户端按战士玩家外形渲染
        public override Packet GetInfo()
        {
            return new S.ObjectPlayer
            {
                ObjectID = ObjectID,
                Name = Name,
                NameColour = NameColour,
                Class = MirClass.Warrior,
                Gender = MirGender.Male,   // 固定男性
                Location = CurrentLocation,
                Direction = Direction,
                Hair = 1,                  // 发型号(Data\CHair)
                Weapon = LookWeapon,
                Armour = LookArmour,
                Light = Light,
                Poison = CurrentPoison,
                Dead = Dead,
                Hidden = Hidden,
                Effect = SpellEffect.None,
                WingEffect = 0,
                Extra = false,
                TransformType = -1,
            };
        }
    }
}