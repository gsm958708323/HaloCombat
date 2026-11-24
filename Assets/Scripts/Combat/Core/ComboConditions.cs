using System;

namespace Combat.Core
{
    public enum ComboConditionKind : byte
    {
        Tag = 1,
        Buff = 2,
        Resource = 3
    }

    public enum ComboCompareOp : byte
    {
        Gte = 1,
        Eq = 2,
        Lt = 3,
        Lte = 4
    }

    public enum ComboResourceKind : byte
    {
        Energy = 1,
        Ammo = 2
    }

    public enum ComboWindowMode : byte
    {
        Default = 0,
        Channel = 1,
        Range = 2,
        Anytime = 3
    }

    [Serializable]
    public struct ComboCondition
    {
        public ComboConditionKind Kind;
        public bool Negate;
        public int Tag;
        public int BuffRuntimeId;
        public string BuffKey;
        public ComboResourceKind Resource;
        public ComboCompareOp Op;
        public float Value;

        public static ComboCondition HasTag(int tag, bool negate = false)
            => new ComboCondition { Kind = ComboConditionKind.Tag, Tag = tag, Negate = negate };

        public static ComboCondition Buff(int runtimeId, string key, ComboCompareOp op, int stacks, bool negate = false)
            => new ComboCondition { Kind = ComboConditionKind.Buff, BuffRuntimeId = runtimeId, BuffKey = key, Op = op, Value = stacks, Negate = negate };

        public static ComboCondition CheckResource(ComboResourceKind resource, ComboCompareOp op, float value, bool negate = false)
            => new ComboCondition { Kind = ComboConditionKind.Resource, Resource = resource, Op = op, Value = value, Negate = negate };

        public bool Evaluate(in ComboEvalContext ctx)
        {
            bool raw;
            switch (Kind)
            {
                case ComboConditionKind.Tag:
                    raw = ctx.Tags != null && ctx.Tags.Has(new TagId(Tag));
                    break;
                case ComboConditionKind.Buff:
                {
                    int stacks = ctx.Buffs != null ? ctx.Buffs.StacksOf(BuffRuntimeId) : 0;
                    int expected = (int)Value;
                    raw = Op == ComboCompareOp.Eq ? stacks == expected
                        : Op == ComboCompareOp.Lt ? stacks < expected
                        : Op == ComboCompareOp.Lte ? stacks <= expected
                        : stacks >= expected;
                    break;
                }
                case ComboConditionKind.Resource:
                {
                    float current = Resource == ComboResourceKind.Ammo
                        ? (ctx.Ammo != null ? ctx.Ammo.Current : 0f)
                        : (ctx.Energy != null ? ctx.Energy.Current : 0f);
                    raw = Op == ComboCompareOp.Lte ? current <= Value + .0001f
                        : Op == ComboCompareOp.Lt ? current < Value - .0001f
                        : Op == ComboCompareOp.Eq ? Math.Abs(current - Value) <= .0001f
                        : current + .0001f >= Value;
                    break;
                }
                default:
                    raw = false;
                    break;
            }
            return Negate ? !raw : raw;
        }

        public string DescribeFailure()
        {
            switch (Kind)
            {
                case ComboConditionKind.Tag:
                    if (Tag == CommonTags.ComboConfirm.Value) return Negate ? "本次动作已命中" : "缺少本次命中确认";
                    return Negate ? $"禁止持有状态 {Tag}" : $"缺少状态 {Tag}";
                case ComboConditionKind.Buff:
                {
                    string name = string.IsNullOrEmpty(BuffKey) ? $"Buff({BuffRuntimeId})" : BuffKey;
                    string opText = Op == ComboCompareOp.Eq ? "=" : Op == ComboCompareOp.Lt ? "<" : Op == ComboCompareOp.Lte ? "<=" : ">=";
                    return Negate ? $"不可满足 {name} {opText} {(int)Value}层" : $"需要 {name} {opText} {(int)Value}层";
                }
                case ComboConditionKind.Resource:
                {
                    string name = Resource == ComboResourceKind.Ammo ? "弹药" : "能量";
                    string opText = Op == ComboCompareOp.Lte ? "<=" : Op == ComboCompareOp.Lt ? "<" : Op == ComboCompareOp.Eq ? "=" : ">=";
                    return Negate ? $"不可满足 {name} {opText} {Value:0.##}" : $"需要 {name} {opText} {Value:0.##}";
                }
                default:
                    return "条件未满足";
            }
        }
    }

    [Serializable]
    public struct ComboWindowSpec
    {
        public ComboWindowMode Mode;
        public string Channel;
        public float Start;
        public float End;

        public bool IsOpen(in ComboEvalContext ctx)
        {
            if (!ctx.IsPlaying) return true;
            switch (Mode)
            {
                case ComboWindowMode.Anytime:
                    return true;
                case ComboWindowMode.Channel:
                    return ctx.Director != null && ctx.Director.IsCancelChannelOpen(Channel);
                case ComboWindowMode.Range:
                    return ctx.ActionTime + .000001f >= Start && ctx.ActionTime - .000001f <= End;
                default:
                    return ctx.Tags != null && ctx.Tags.Has(CommonTags.Cancel);
            }
        }
    }

    public readonly struct ComboEvalContext
    {
        public readonly TagComp Tags;
        public readonly BuffComp Buffs;
        public readonly EnergyComp Energy;
        public readonly AmmoComp Ammo;
        public readonly SkillDirectorComp Director;
        public readonly bool IsPlaying;
        public readonly float ActionTime;

        public ComboEvalContext(TagComp tags, BuffComp buffs, EnergyComp energy, AmmoComp ammo, SkillDirectorComp director)
        {
            Tags = tags;
            Buffs = buffs;
            Energy = energy;
            Ammo = ammo;
            Director = director;
            IsPlaying = director != null && director.IsPlaying;
            ActionTime = director != null ? director.TimelineTime : 0f;
        }

        public static ComboEvalContext FromTagsOnly(TagComp tags)
            => new ComboEvalContext(tags, null, null, null, null);

        public static ComboEvalContext FromActor(Actor actor)
        {
            if (actor == null) return default;
            actor.TryGetComp(out TagComp tags);
            actor.TryGetComp(out BuffComp buffs);
            actor.TryGetComp(out EnergyComp energy);
            actor.TryGetComp(out AmmoComp ammo);
            actor.TryGetComp(out SkillDirectorComp director);
            return new ComboEvalContext(tags, buffs, energy, ammo, director);
        }
    }
}
