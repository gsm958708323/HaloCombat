namespace Combat.Core
{
    /// <summary>
    /// Effect 基类：只留调度器需要的最小生命周期。
    /// 真正业务数据走派生类字段或 Apply 专属 args。
    /// </summary>
    public abstract class Effect
    {
        public bool IsFinished { get; protected set; }
        /// <summary>进入时调用一次。派生可 override；需要宿主数据时用专属方法。</summary>
        public virtual void Enter() { }
        public virtual void Tick(float dt) { }
        public virtual void Exit() { }
        public void MarkFinished() => IsFinished = true;
    }
    public readonly struct AddTagEffectArgs
    {
        public readonly TagComp Tags;
        public readonly TagId Tag;
        public readonly int Stacks;

        public AddTagEffectArgs(TagComp tags, TagId tag, int stacks)
        {
            Tags = tags;
            Tag = tag;
            Stacks = stacks;
        }
    }

    public sealed class AddTagEffect : Effect
    {
        readonly AddTagEffectArgs _args;

        public AddTagEffect(in AddTagEffectArgs args) => _args = args;

        public override void Enter()
        {
            _args.Tags.Add(_args.Tag, _args.Stacks, TagSource.Effect(nameof(AddTagEffect)));
            MarkFinished(); // 瞬时 Effect
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
        readonly RemoveTagEffectArgs _args;

        public RemoveTagEffect(in RemoveTagEffectArgs args) => _args = args;

        public override void Enter()
        {
            _args.Tags.Remove(_args.Tag, _args.Stacks, TagSource.Effect(nameof(RemoveTagEffect)));
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
