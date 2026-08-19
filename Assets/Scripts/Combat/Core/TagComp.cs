using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface ITagRead
    {
        bool Has(TagId tag);
        int Stack(TagId tag);
    }
    public interface ITagWrite : ITagRead
    {
        void Add(TagId tag, int stacks, TagSource source);
        void Remove(TagId tag, int stacks, TagSource source);
    }

    public static class CommonTags
    {
        public static readonly TagId Cancel = new TagId(1001);
        public static readonly TagId Grounded = new TagId(1002);
        public static readonly TagId Airborne = new TagId(1003);
        public static readonly TagId SuperArmor = new TagId(1004);
        public static readonly TagId Dead = new TagId(1005);
    }

    public readonly struct TagId : System.IEquatable<TagId>
    {
        public readonly int Value;

        public TagId(int value) => Value = value;

        public bool Equals(TagId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TagId t && Equals(t);
        public override int GetHashCode() => Value;
        public static bool operator ==(TagId a, TagId b) => a.Value == b.Value;
        public static bool operator !=(TagId a, TagId b) => a.Value != b.Value;
        public override string ToString() => $"Tag({Value})";
    }

    /// <summary>写来源标记，便于日志/回放，不参与玩法分支硬编码。</summary>
    public readonly struct TagSource
    {
        public readonly string Reason;

        public TagSource(string reason) => Reason = reason ?? string.Empty;

        public static TagSource StateEnter(string stateName)
            => new TagSource("StateEnter:" + stateName);

        public static TagSource StateExit(string stateName)
            => new TagSource("StateExit:" + stateName);

        public static TagSource Effect(string effectName)
            => new TagSource("Effect:" + effectName);

        public static TagSource Debug
            => new TagSource("Debug");
    }

    public sealed class TagComp : Comp
    {
        readonly Dictionary<int, int> _stacks = new Dictionary<int, int>(16);

        public bool Has(TagId tag)
            => _stacks.TryGetValue(tag.Value, out var n) && n > 0;

        public int Stack(TagId tag)
            => _stacks.TryGetValue(tag.Value, out var n) ? n : 0;

        public void Add(TagId tag, int stacks, TagSource source)
        {
            if (stacks <= 0)
                return;

            if (_stacks.TryGetValue(tag.Value, out var cur))
                _stacks[tag.Value] = cur + stacks;
            else
                _stacks[tag.Value] = stacks;

            // 需要可观测时再接 EventBus；MVP 不在这里硬依赖 World
            _ = source;
        }

        public void Remove(TagId tag, int stacks, TagSource source)
        {
            if (stacks <= 0)
                return;

            if (!_stacks.TryGetValue(tag.Value, out var cur))
                return;

            cur -= stacks;
            if (cur <= 0)
                _stacks.Remove(tag.Value);
            else
                _stacks[tag.Value] = cur;

            _ = source;
        }

        public void ClearAll()
        {
            _stacks.Clear();
        }

        protected override void OnDetach()
        {
            _stacks.Clear();
        }
    }
}
