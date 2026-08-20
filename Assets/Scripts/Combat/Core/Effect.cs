namespace Combat.Core
{
    /// <summary>
    /// Effect 基类：只留调度器需要的最小生命周期。
    /// 真正业务数据走派生类字段或 Apply 专属 args。
    /// </summary>
    public abstract class Effect
    {
        public bool IsFinished { get; private set; }
        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }
        public void MarkFinished() => IsFinished = true;
    }

    public sealed class MoveOffsetEffect : Effect
    {
        readonly LocomotionComp _loco;
        readonly float _x, _y, _z;
        readonly bool _asVelocity;
        readonly bool _interval;
        readonly float _duration;
        bool _instantApplied;
        public MoveOffsetEffect(
            LocomotionComp loco,
            float x, float y, float z,
            bool asVelocity,
            bool interval,
            float duration)
        {
            _loco = loco;
            _x = x; _y = y; _z = z;
            _asVelocity = asVelocity;
            _interval = interval;
            _duration = duration > 1e-5f ? duration : 1e-5f;
        }
        public override void Enter()
        {
            if (_interval)
                return;
            // 瞬时：一次性位移
            _loco.AddAxisDelta(_x, _y, _z);
            _instantApplied = true;
            MarkFinished();
        }
        public override void Tick(float dt)
        {
            if (!_interval)
                return;
            if (_asVelocity)
            {
                _loco.AddAxisDelta(_x * dt, _y * dt, _z * dt);
            }
            else
            {
                // 总位移在区间内均分
                _loco.AddAxisDelta(
                    _x * (dt / _duration),
                    _y * (dt / _duration),
                    _z * (dt / _duration));
            }
        }
        public override void Exit()
        {
            // 区间结束无需额外处理；累计已在 Tick 完成
        }
    }

    public sealed class AddTagEffect : Effect
    {
        readonly TagComp _tags;
        readonly TagId _tag;
        readonly int _stacks;
        readonly bool _interval;
        public AddTagEffect(TagComp tags, TagId tag, int stacks, bool interval)
        {
            _tags = tags;
            _tag = tag;
            _stacks = stacks < 1 ? 1 : stacks;
            _interval = interval;
        }
        public override void Enter()
        {
            _tags.Add(_tag, _stacks, TagSource.Effect(nameof(AddTagEffect)));
            if (!_interval)
                MarkFinished();
        }
        public override void Exit()
        {
            // 区间结束自动移除；瞬时已在 Enter 结束
            if (_interval)
                _tags.Remove(_tag, _stacks, TagSource.Effect(nameof(AddTagEffect) + ".Exit"));
        }
    }

    public readonly struct RemoveTagEffectArgs
    {
        public readonly TagComp Tags;
        public readonly TagId Tag;
        public readonly int Stacks;

        public RemoveTagEffectArgs(TagComp tags, TagId tag, int stacks)
        {
            Tags = tags;
            Tag = tag;
            Stacks = stacks;
        }
    }

    public sealed class RemoveTagEffect : Effect
    {
        readonly TagComp _tags;
        readonly TagId _tag;
        readonly int _stacks;
        public RemoveTagEffect(TagComp tags, TagId tag, int stacks)
        {
            _tags = tags;
            _tag = tag;
            _stacks = stacks < 1 ? 1 : stacks;
        }
        public override void Enter()
        {
            _tags.Remove(_tag, _stacks, TagSource.Effect(nameof(RemoveTagEffect)));
            MarkFinished();
        }
    }

    public readonly struct AnimSignalEffectArgs
    {
        public readonly IntentQueue Intents;
        public readonly EntityId Self;
        public readonly string Signal;

        public AnimSignalEffectArgs(IntentQueue intents, EntityId self, string signal)
        {
            Intents = intents;
            Self = self;
            Signal = signal;
        }
    }

    /// <summary>表现向信号：投递 Intent/事件，逻辑不依赖是否有人听。</summary>
    public sealed class AnimSignalEffect : Effect
    {
        readonly AnimSignalEffectArgs _args;

        public AnimSignalEffect(in AnimSignalEffectArgs args) => _args = args;

        public override void Enter()
        {
            _args.Intents.Post(new AnimSignalIntent(_args.Self, _args.Signal));
            MarkFinished();
        }
    }

    public readonly struct SpawnProjectileEffectArgs
    {
        public readonly IntentQueue Intents;
        public readonly EntityId Owner;
        public readonly int SpecValue;

        public SpawnProjectileEffectArgs(IntentQueue intents, EntityId owner, int specValue)
        {
            Intents = intents;
            Owner = owner;
            SpecValue = specValue;
        }
    }

    public sealed class SpawnProjectileEffect : Effect
    {
        readonly SpawnProjectileEffectArgs _args;

        public SpawnProjectileEffect(in SpawnProjectileEffectArgs args) => _args = args;

        public override void Enter()
        {
            _args.Intents.Post(new SpawnProjectileIntent(_args.Owner, _args.SpecValue));
            MarkFinished();
        }
    }
}
