using System;

namespace Combat.Core
{
    public sealed class SpawnSpeedCurve
    {
        public int Base,RandomRange;
        public float Scale,Divisor,Offset;
    }
    public sealed class ActorSpawnStats
    {
        public float HpPerSpawn,AttackPerSpawn;
        public int AttackRandomRange;
        public SpawnSpeedCurve MoveSpeedCurve;
        public void Apply(Actor actor,RuntimeActorDefinition definition,int ordinal,IRandom random)
        {
            if(ordinal<0)throw new ArgumentOutOfRangeException(nameof(ordinal));
            var attributes=actor.GetComp<AttributeSet>();float hp=definition.Hp+ordinal*HpPerSpawn;
            attributes.SetBase(AttrId.MaxHp,hp);attributes.SetBase(AttrId.Hp,hp);
            // A declared profile consumes its attack draw even at range zero. This keeps
            // seeded spawn/wander/fire draws in their original deterministic order.
            attributes.SetBase(AttrId.Atk,definition.Attack+Sample(random,AttackRandomRange)+ordinal*AttackPerSpawn);
            if(MoveSpeedCurve!=null)
            {
                var curve=MoveSpeedCurve;float value=curve.Base+Sample(random,curve.RandomRange);
                attributes.SetBase(AttrId.MoveSpeed,value*curve.Scale/(value+curve.Divisor)+curve.Offset);
            }
        }
        static int Sample(IRandom random,int range)
        {float value=random.Next01();return range<=0?0:Math.Max(0,Math.Min(range-1,(int)(value*range)));}
    }
}
