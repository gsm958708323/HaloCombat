using System;

namespace Combat.Core
{
    /// <summary>
    /// 一个效果包。结算路径只有这一条：效果只读自己的配置 + 写 ctx 指向的目标/世界。
    /// 效果实例可能在多条内容之间共享，所以实现必须无状态。
    /// </summary>
    public interface IEffect
    {
        void Apply(ref EffectContext ctx);
    }

    /// <summary>
    /// 受击过滤钩子：由「目标」组件实现，让通用伤害公式不必认识具体产品组件（例如 Arena 的木桶）。
    /// 只在 DamageEffect 的 DirectDamage 为 true 时调用；反伤等路径保持绕过。
    /// 返回值替换 raw 伤害，护盾 / 暴击 / 事件 / 死亡等其余步骤不受影响。
    /// </summary>
    public interface IIncomingDamage
    {
        float Filter(Actor source, float amount, in EffectContext ctx);
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
