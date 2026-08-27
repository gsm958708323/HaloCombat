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

        public ApplyEffectsIntent(
            IEffect[] effects,
            EntityId sourceId,
            EntityId targetId,
            float snapshotAtk,
            int buffStacks = 0,
            SimVec3 point = default,
            bool hasPoint = false)
        {
            Effects = effects;
            SourceId = sourceId;
            TargetId = targetId;
            SnapshotAtk = snapshotAtk;
            BuffStacks = buffStacks;
            Point = point;
            HasPoint = hasPoint;
        }
    }

    public sealed class EffectPipeline
    {
        public void Run(ref EffectContext ctx, IEffect[] effects)
        {
            if (effects == null) return;
            for (int i = 0; i < effects.Length; i++)
                effects[i]?.Apply(ref ctx);
        }
    }

    public sealed class CallbackEffect : IEffect
    {
        readonly Action _fn;
        public CallbackEffect(Action fn) => _fn = fn;
        public void Apply(ref EffectContext ctx) => _fn?.Invoke();
    }
}
