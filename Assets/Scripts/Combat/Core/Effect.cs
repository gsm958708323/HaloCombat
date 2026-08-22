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

    public readonly struct AoEBurstEffectArgs
    {
        public readonly IntentQueue Intents;
        public readonly Actor Self;
        public readonly AoESpecLibrary Library;
        public readonly int SpecValue;
        public AoEBurstEffectArgs(
            IntentQueue intents,
            Actor self,
            AoESpecLibrary library,
            int specValue)
        {
            Intents = intents;
            Self = self;
            Library = library;
            SpecValue = specValue;
        }
    }
    public sealed class AoEBurstEffect : Effect
    {
        readonly AoEBurstEffectArgs _args;
        public AoEBurstEffect(in AoEBurstEffectArgs args) => _args = args;
        public override void Enter()
        {
            if (!_args.Library.TryGet(_args.SpecValue, out var spec))
            {
                MarkFinished();
                return;
            }
            var self = _args.Self;
            var tf = self.GetComp<TransformComp>();
            int team = 0;
            if (self.TryGetComp<TeamComp>(out var teamComp))
                team = teamComp.Team;
            int skill = 0;
            if (self.TryGetComp<SkillDirectorComp>(out var director))
                skill = director.CurrentSkill.Value;
            float cx = tf.Position.X + spec.OffsetX;
            float cy = tf.Position.Y + spec.OffsetY;
            float cz = tf.Position.Z + spec.OffsetZ;
            _args.Intents.Post(new AoEIntent(
                source: self.Id,
                owner: self.Id,
                ownerTeam: team,
                shape: spec.Shape,
                cx, cy, cz,
                radius: spec.Radius,
                attackSpecValue: spec.AttackSpecValue,
                sourceSkillValue: skill,
                hitOwner: spec.HitOwner));
            MarkFinished();
        }
    }


    public readonly struct SpawnPulseZoneEffectArgs
    {
        public readonly IntentQueue Intents;
        public readonly EntityId Owner;
        public readonly int SpecValue;
        public SpawnPulseZoneEffectArgs(IntentQueue intents, EntityId owner, int specValue)
        {
            Intents = intents;
            Owner = owner;
            SpecValue = specValue;
        }
    }
    public sealed class SpawnPulseZoneEffect : Effect
    {
        readonly SpawnPulseZoneEffectArgs _args;
        public SpawnPulseZoneEffect(in SpawnPulseZoneEffectArgs args) => _args = args;
        public override void Enter()
        {
            _args.Intents.Post(new SpawnPulseZoneIntent(_args.Owner, _args.SpecValue));
            MarkFinished();
        }
    }
    public readonly struct BuffEffectArgs
    {
        public readonly BuffComp Buffs;
        public readonly BuffTypeId Type;
        public readonly int Stacks;
        public readonly float Duration;
        public readonly TagSource Source;
        public readonly bool RefreshIfExist;
        public BuffEffectArgs(BuffComp buffs, BuffTypeId type, int stacks, float duration, TagSource source, bool refreshIfExist)
        {
            Buffs = buffs;
            Type = type;
            Stacks = stacks;
            Duration = duration;
            Source = source;
            RefreshIfExist = refreshIfExist;
        }
    }
    public sealed class BuffEffect : Effect
    {
        readonly BuffEffectArgs _args;
        public BuffEffect(in BuffEffectArgs args) => _args = args;
        public override void Enter()
        {
            _args.Buffs.Apply(new BuffApplyArgs(
                _args.Type, _args.Stacks, _args.Duration, _args.Source, _args.RefreshIfExist));
        }
    }
}
