namespace Combat.Core
{
    public enum ActivityId : byte
    {
        None = 0,
        Root = 1,
        Attack = 3,
        Hit = 4,
        Dead = 5,
        Knockdown = 6
    }

    public struct LocoProfile
    {
        public float MotorScale;
        public bool UseSkill;
        public bool UseHit;
        public bool ApplyGravity;
    }

    public enum FacingMode : byte
    {
        FollowStickIfGrounded = 0,
        Lock = 1,
        SteerIfGrounded = 2
    }

    public struct FacingPolicy
    {
        public FacingMode Mode;
        public float TurnRate;
    }

    public struct ActivityMotorPolicy
    {
        public LocoProfile Loco;
        public FacingPolicy Facing;
    }

    public struct ActivityEnterArgs
    {
        public ActivityId From;
        public string Reason;
        public float HitDuration;
        public float IFrameDuration;
        public EntityId Killer;
    }

    public readonly struct ActivityContext
    {
        public readonly Actor Self;
        public readonly TagComp Tags;
        public readonly SkillDirectorComp Director;
        public readonly InputBufferComp Input;
        public readonly LocomotionComp Loco;

        public ActivityContext(Actor self, TagComp tags, SkillDirectorComp director, InputBufferComp input, LocomotionComp loco)
        {
            Self = self;
            Tags = tags;
            Director = director;
            Input = input;
            Loco = loco;
        }
    }

    public interface IActivity
    {
        ActivityId Id { get; }
        ActivityMotorPolicy Motor { get; }
        bool CanEnter(ActivityId from);
        void Enter(in ActivityContext ctx, in ActivityEnterArgs args);
        void Exit(in ActivityContext ctx, in ActivityEnterArgs toNext);
        bool Tick(in ActivityContext ctx, float dt);
    }

    public sealed class RootActivity : IActivity
    {
        public ActivityId Id => ActivityId.Root;
        public ActivityMotorPolicy Motor { get; } = new ActivityMotorPolicy
        {
            Loco = new LocoProfile { MotorScale = 1f, ApplyGravity = true },
            Facing = new FacingPolicy { Mode = FacingMode.FollowStickIfGrounded }
        };

        public bool CanEnter(ActivityId from) => from != ActivityId.Dead;
        public void Enter(in ActivityContext ctx, in ActivityEnterArgs args) { }
        public void Exit(in ActivityContext ctx, in ActivityEnterArgs toNext) { }
        public bool Tick(in ActivityContext ctx, float dt) => false;
    }

    public sealed class AttackActivity : IActivity
    {
        public ActivityId Id => ActivityId.Attack;
        public ActivityMotorPolicy Motor { get; } = new ActivityMotorPolicy
        {
            Loco = new LocoProfile { MotorScale = 0f, UseSkill = true, ApplyGravity = true },
            Facing = new FacingPolicy { Mode = FacingMode.SteerIfGrounded }
        };

        public bool CanEnter(ActivityId from) => from != ActivityId.Dead && from != ActivityId.Hit;

        public void Enter(in ActivityContext ctx, in ActivityEnterArgs args)
        {
            ctx.Tags.Add(CommonTags.Casting, 1, TagSource.StateEnter("Attack"));
        }

        public void Exit(in ActivityContext ctx, in ActivityEnterArgs toNext)
        {
            ctx.Tags.Remove(CommonTags.Casting, 1, TagSource.StateExit("Attack"));
            ctx.Loco?.ClearClipSteer();
        }

        public bool Tick(in ActivityContext ctx, float dt) => false;
    }

    public sealed class HitActivity : IActivity
    {
        float _timer;
        public ActivityId Id => ActivityId.Hit;
        public ActivityMotorPolicy Motor { get; } = new ActivityMotorPolicy
        {
            Loco = new LocoProfile { UseHit = true, ApplyGravity = true },
            Facing = new FacingPolicy { Mode = FacingMode.Lock }
        };

        public bool CanEnter(ActivityId from) => from != ActivityId.Dead;

        public void Enter(in ActivityContext ctx, in ActivityEnterArgs args)
            => ApplyHit(ctx, args, false);

        public void Refresh(in ActivityContext ctx, in ActivityEnterArgs args)
            => ApplyHit(ctx, args, true);

        void ApplyHit(in ActivityContext ctx, in ActivityEnterArgs args, bool isRefresh)
        {
            _timer = args.HitDuration > 0f ? args.HitDuration : 0.35f;
            ctx.Input?.Clear();
            ctx.Director?.Stop(DirectorStopReason.Hit);
            ctx.Loco?.ClearClipSteer();
            ctx.Loco?.ClearPendingSkill();
            if (!isRefresh)
                ctx.Tags.Add(CommonTags.Stunned, 1, TagSource.StateEnter("Hit"));
            if (args.IFrameDuration > 0f && ctx.Self.TryGetComp<HealthComp>(out var hp))
                hp.BeginIFrame(args.IFrameDuration);
        }

        public void Exit(in ActivityContext ctx, in ActivityEnterArgs toNext)
        {
            ctx.Tags.Remove(CommonTags.Stunned, 1, TagSource.StateExit("Hit"));
            _timer = 0f;
        }

        public bool Tick(in ActivityContext ctx, float dt)
        {
            _timer -= dt;
            return _timer <= 0f;
        }
    }

    public sealed class DeadActivity : IActivity
    {
        public ActivityId Id => ActivityId.Dead;
        public ActivityMotorPolicy Motor { get; } = new ActivityMotorPolicy
        {
            Facing = new FacingPolicy { Mode = FacingMode.Lock }
        };

        public bool CanEnter(ActivityId from) => true;

        public void Enter(in ActivityContext ctx, in ActivityEnterArgs args)
        {
            ctx.Input?.Clear();
            ctx.Director?.Stop(DirectorStopReason.Dead);
            ctx.Loco?.ClearClipSteer();
            ctx.Loco?.ClearPendingSkill();
            ctx.Tags.Add(CommonTags.Dead, 1, TagSource.StateEnter("Dead"));
            if (ctx.Self.TryGetComp<BuffComp>(out var buffs))
                buffs.ClearAllWithExpire();
            var world = ctx.Self.World;
            if (world != null)
            {
                world.CleanupByOwner(ctx.Self.Id);
                world.Events.Publish(new EvEntityDead(ctx.Self.Id, args.Killer));
            }
        }

        public void Exit(in ActivityContext ctx, in ActivityEnterArgs toNext)
        {
            ctx.Tags.Remove(CommonTags.Dead, 1, TagSource.StateExit("Dead"));
        }

        public bool Tick(in ActivityContext ctx, float dt) => false;
    }

    public sealed class KnockdownActivity : IActivity
    {
        float _timer;

        public ActivityId Id => ActivityId.Knockdown;
        public ActivityMotorPolicy Motor { get; } = new ActivityMotorPolicy
        {
            Loco = new LocoProfile
            {
                MotorScale = 0f,
                UseSkill = false,
                UseHit = true,
                ApplyGravity = true
            },
            Facing = new FacingPolicy { Mode = FacingMode.Lock }
        };

        public bool CanEnter(ActivityId from) => from != ActivityId.Dead;

        public void Enter(in ActivityContext ctx, in ActivityEnterArgs args)
            => Apply(ctx, args, false);

        public void Refresh(in ActivityContext ctx, in ActivityEnterArgs args)
            => Apply(ctx, args, true);

        void Apply(in ActivityContext ctx, in ActivityEnterArgs args, bool refresh)
        {
            _timer = args.HitDuration > 0f ? args.HitDuration : 0.80f;
            ctx.Input?.Clear();
            ctx.Director?.Stop(DirectorStopReason.Knockdown);
            ctx.Loco?.ClearClipSteer();
            ctx.Loco?.ClearPendingSkill();
            if (!refresh)
                ctx.Tags.Add(CommonTags.Downed, 1, TagSource.StateEnter("Knockdown"));
        }

        public void Exit(in ActivityContext ctx, in ActivityEnterArgs toNext)
        {
            ctx.Tags.Remove(CommonTags.Downed, 1, TagSource.StateExit("Knockdown"));
            _timer = 0f;
        }

        public bool Tick(in ActivityContext ctx, float dt)
        {
            _timer -= dt;
            return _timer <= 0f;
        }
    }

    public sealed class StateMachineComp : Comp
    {
        readonly RootActivity _root = new RootActivity();
        readonly AttackActivity _attack = new AttackActivity();
        readonly HitActivity _hit = new HitActivity();
        readonly DeadActivity _dead = new DeadActivity();
        readonly KnockdownActivity _knockdown = new KnockdownActivity();

        TagComp _tags;
        SkillDirectorComp _director;
        InputBufferComp _input;
        LocomotionComp _loco;
        IActivity _current;
        ActivityId _currentId = ActivityId.Root;

        public ActivityId Current => _currentId;
        public IActivity CurrentActivity => _current;
        public ActivityMotorPolicy Motor => _current != null ? _current.Motor : _root.Motor;
        public override bool WantsTick => true;

        protected override void OnAttach()
        {
            _tags = Self.GetComp<TagComp>();
            Self.TryGetComp(out _director);
            Self.TryGetComp(out _input);
            Self.TryGetComp(out _loco);
            _current = _root;
            _currentId = ActivityId.Root;
            _current.Enter(MakeCtx(), new ActivityEnterArgs { From = ActivityId.None, Reason = "Spawn" });
        }

        protected override void OnDetach()
        {
            _current = null;
            _tags = null;
            _director = null;
            _input = null;
            _loco = null;
        }

        public bool TryEnter(ActivityId next, ActivityEnterArgs args)
        {
            if (_currentId == ActivityId.Dead)
                return false;

            if (_currentId == ActivityId.Hit && next == ActivityId.Hit)
            {
                args.From = ActivityId.Hit;
                _hit.Refresh(MakeCtx(), args);
                return true;
            }

            if (_currentId == ActivityId.Knockdown && next == ActivityId.Knockdown)
            {
                args.From = ActivityId.Knockdown;
                _knockdown.Refresh(MakeCtx(), args);
                return true;
            }

            if (next == _currentId)
                return true;

            if (_currentId == ActivityId.Hit && next != ActivityId.Dead && next != ActivityId.Root && next != ActivityId.Knockdown)
                return false;

            if (_currentId == ActivityId.Knockdown && next != ActivityId.Dead && next != ActivityId.Root)
                return false;

            var target = Get(next);
            if (target == null || !target.CanEnter(_currentId))
                return false;

            var ctx = MakeCtx();
            args.From = _currentId;
            _current.Exit(ctx, args);
            _current = target;
            _currentId = next;
            _current.Enter(ctx, args);
            return true;
        }

        public void NotifyActivityFinished(ActivityId finished, string reason)
        {
            if (_currentId == ActivityId.Dead) return;
            if (_currentId != finished) return;
            TryEnter(ActivityId.Root, new ActivityEnterArgs { From = finished, Reason = reason ?? "Finished" });
        }

        public override void Tick(float dt)
        {
            if (_current == null) return;
            if (_current.Tick(MakeCtx(), dt))
                NotifyActivityFinished(_currentId, "ActivityTick");
        }

        IActivity Get(ActivityId id)
        {
            switch (id)
            {
                case ActivityId.Root: return _root;
                case ActivityId.Attack: return _attack;
                case ActivityId.Hit: return _hit;
                case ActivityId.Knockdown: return _knockdown;
                case ActivityId.Dead: return _dead;
                default: return null;
            }
        }

        ActivityContext MakeCtx()
        {
            // Components may be attached in the state-machine-first order used by
            // enemy/AI blueprints. Resolve optional peers lazily once they exist.
            if (_director == null) Self.TryGetComp(out _director);
            if (_input == null) Self.TryGetComp(out _input);
            if (_loco == null) Self.TryGetComp(out _loco);
            return new ActivityContext(Self, _tags, _director, _input, _loco);
        }
    }
}
