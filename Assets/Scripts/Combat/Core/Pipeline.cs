using System;

namespace Combat.Core
{
    public interface IEffect
    {
        void Apply(ref EffectContext ctx);
    }

    public struct EffectContext
    {
        public CombatWorld World;
        public Actor Source;
        public Actor Target;
        public float SnapshotAtk;
        public float Power;
        public int BuffStacks;
        public SimVec3 Point;
        public bool HasPoint;
        public SimVec3 Dir;
        public bool HasDir;
        public SkillOrigin Origin;
        public ProjectileHitContext ProjectileHit;
        public DeliveryContact Contact;
        public DamageResult Damage;
    }

    public readonly struct ApplyEffectsIntent
    {
        public readonly IEffect[] Effects;
        public readonly EntityId SourceId;
        public readonly EntityId TargetId;
        public readonly float SnapshotAtk;
        public readonly int BuffStacks;
        public readonly SimVec3 Point;
        public readonly bool HasPoint;
        public readonly float Power;
        public readonly SkillOrigin Origin;
        public readonly SimVec3 Direction;
        public readonly bool HasDirection;

        public ApplyEffectsIntent(
            IEffect[] effects,
            EntityId sourceId,
            EntityId targetId,
            float snapshotAtk,
            int buffStacks = 0,
            SimVec3 point = default,
            bool hasPoint = false,
            float power = 1,SkillOrigin origin=default,SimVec3 direction=default,bool hasDirection=false)
        {
            Effects = effects;
            SourceId = sourceId;
            TargetId = targetId;
            SnapshotAtk = snapshotAtk;
            BuffStacks = buffStacks;
            Point = point;
            HasPoint = hasPoint;
            Power = power;
            Origin=origin;Direction=direction;HasDirection=hasDirection;
        }
    }

    public sealed class EffectPipeline
    {
        readonly IEffectObserver _observer;
        public EffectPipeline(IEffectObserver observer=null)=>_observer=observer;
        public void Run(ref EffectContext ctx, IEffect[] effects)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
            {
                if(effects[i]==null)continue;
                int token=_observer?.Enter(effects[i],ctx)??-1;
                try{effects[i].Apply(ref ctx);}finally{_observer?.Exit(token);}
            }
        }
    }

    // Optional execution diagnostics. Implementations belong to the composition layer;
    // gameplay does not know author documents, node identities or editor positions.
    public interface IEffectObserver
    {
        int Enter(IEffect effect,in EffectContext context);
        void Exit(int token);
    }

    public sealed class CallbackEffect : IEffect
    {
        readonly Action _fn;
        public CallbackEffect(Action fn) => _fn = fn;
        public void Apply(ref EffectContext ctx) => _fn?.Invoke();
    }
}
