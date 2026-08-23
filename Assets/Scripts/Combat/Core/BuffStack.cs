using System;

namespace Combat.Core
{
    /// <summary>
    /// Buff 定义（可放到 ScriptableObject，MVP 代码内）
    /// </summary>
    public sealed class BuffDefSO
    {
        public BuffTypeId Id;
        public BuffStackPolicy StackPolicy; // RefreshDuration / AddStacks / ParallelIgnore
        public AttrModifierDef[] Modifiers; // Atk/Def/MaxHp/…
        public float DefaultDuration = 0.4f;
        public bool RefreshOnApply = true; // 刷新时长
        /// <summary>
        /// Buff 触发事件回调点
        /// </summary>
        public BuffHook[] Hooks; // 空数组 = 无回调；非空 = 会在对应事件调用 ApplyHook
    }

    public enum BuffStackPolicy
    {
        RefreshDuration,   // 刷新时长 + 叠层
        AddStacks,         // 直接加层数（时长重叠）
        ParallelIgnore     // 不同类型可并行，不互相刷新
    }
    public struct AttrModifierDef
    {
        public AttrChannel Channel; // Atk / Def / MaxHp / …
        public ModifierOp Op;
        public float Value;
    }
    public enum AttrChannel { Atk, Def, MaxHp, DamageDealMul, DamageTakenMul }
    public enum ModifierOp { Flat, PercentBase } // 后期可扩 PercentOfTotal
    public enum BuffTrigger
    {
        OnSkill,        // 播轴前/中/后
        OnHurt,         // 被命中（结算前）
        OnHurted,       // 被命中（结算后）
        OnEnterState,   // 进 Hit/Dead
        OnExitState,    // 出 Hit/Dead
        OnBuffAdded,    // Buff 自己被叠加时
        OnBuffRemoved,  // Buff 自己被移除时
    }
    public struct BuffHook
    {
        public BuffTrigger Trigger;
        public Action<BuffHookData> Callback; // 由 BuffComp 调用
        public BuffHook(BuffTrigger trigger, Action<BuffHookData> callback)
        {
            Trigger = trigger;
            Callback = callback;
        }
    }
    public struct BuffHookData
    {
        public EntityId Target;
        public BuffTypeId Type;
        public int Stacks;
        public float DurationLeft;
        public StateMachineComp State;
        public SkillDirectorComp Director;
        public TagComp Tags;
        public AttrSheet Attr;
        // 未来可加：HitIntent / AoEIntent / …
    }

    public readonly struct BuffTypeId : IEquatable<BuffTypeId>
    {
        public readonly int Value;
        public BuffTypeId(int value) => Value = value;
        public bool Equals(BuffTypeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BuffTypeId id && Equals(id);
        public override int GetHashCode() => Value;
        public static bool operator ==(BuffTypeId a, BuffTypeId b) => a.Equals(b);
        public static bool operator !=(BuffTypeId a, BuffTypeId b) => !a.Equals(b);
    }

    public readonly struct BuffStack
    {
        public readonly BuffTypeId Type;
        public readonly int Count;
        public readonly float DurationLeft; // 当前剩余秒数
        public readonly float TotalDuration; // 原配置总时长
        public readonly TagSource Source;

        public BuffStack(BuffTypeId type, int count, float durationLeft, float totalDuration, TagSource source)
        {
            Type = type;
            Count = count;
            DurationLeft = durationLeft;
            TotalDuration = totalDuration;
            Source = source;
        }
    }

    public readonly struct BuffApplyArgs
    {
        public readonly BuffTypeId Type;
        public readonly int Stacks;
        public readonly float Duration;
        public readonly TagSource Source;
        public readonly bool RefreshIfExist;
        // public readonly Func<BuffStack, BuffStack> CustomApply; // 扩展点：自定义叠加逻辑

        public BuffApplyArgs(BuffTypeId type, int stacks, float duration, TagSource source, bool refreshIfExist = false)
        {
            Type = type;
            Stacks = stacks;
            Duration = duration;
            Source = source;
            RefreshIfExist = refreshIfExist;
        }
    }
}
