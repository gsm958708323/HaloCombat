using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class BuffComp : Comp
    {
        readonly Dictionary<BuffTypeId, BuffStack> _buffs = new Dictionary<BuffTypeId, BuffStack>();
        readonly List<BuffTypeId> _removeQueue = new List<BuffTypeId>();
        readonly List<KeyValuePair<BuffTypeId, BuffStack>> _updateQueue =
            new List<KeyValuePair<BuffTypeId, BuffStack>>();

        public override bool WantsTick => true;

        public IReadOnlyDictionary<BuffTypeId, BuffStack> AllBuffs => _buffs;

        public void Apply(in BuffApplyArgs args)
        {
            var type = args.Type;
            if (!_buffs.TryGetValue(type, out var existing))
            {
                existing = new BuffStack(type, 0, 0f, 0f, args.Source);
            }

            int newCount = args.RefreshIfExist ? args.Stacks : existing.Count + args.Stacks;
            if (newCount <= 0)
            {
                _buffs.Remove(type);
                return;
            }

            float newDur = args.Duration;
            if (args.RefreshIfExist && existing.Count > 0)
                newDur = existing.TotalDuration; // 刷新时长不叠加

            var newStack = new BuffStack(
                type, newCount, newDur,
                existing.TotalDuration == 0f ? newDur : existing.TotalDuration,
                args.Source);

            _buffs[type] = newStack;
        }

        public void Remove(BuffTypeId type, TagSource source)
        {
            _buffs.Remove(type);
        }

        public override void Tick(float dt)
        {
            _removeQueue.Clear();
            _updateQueue.Clear();

            // Dictionary 只在遍历结束后写回，避免在 foreach 中修改集合。
            foreach (var kv in _buffs)
            {
                var stack = kv.Value;
                stack = new BuffStack(stack.Type, stack.Count, stack.DurationLeft - dt, stack.TotalDuration, stack.Source);
                if (stack.DurationLeft <= 0f)
                    _removeQueue.Add(stack.Type);
                else
                    _updateQueue.Add(new KeyValuePair<BuffTypeId, BuffStack>(kv.Key, stack));
            }

            for (int i = 0; i < _updateQueue.Count; i++)
            {
                var update = _updateQueue[i];
                _buffs[update.Key] = update.Value;
            }

            foreach (var t in _removeQueue)
                _buffs.Remove(t);
        }
    }
}
