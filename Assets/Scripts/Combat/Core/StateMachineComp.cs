using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public readonly struct ActorStateId
    {
        public readonly int Value;

        public ActorStateId(int value) => Value = value;

        public bool Equals(ActorStateId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ActorStateId id && Equals(id);
        public override int GetHashCode() => Value;
        public override string ToString() => Value switch
        {
            1 => nameof(Root),
            2 => nameof(Jump),
            3 => nameof(Attack),
            4 => nameof(Hit),
            5 => nameof(Dead),
            _ => $"Unknown({Value})"
        };
        public static bool operator ==(ActorStateId a, ActorStateId b) => a.Equals(b);
        public static bool operator !=(ActorStateId a, ActorStateId b) => !a.Equals(b);

        public static readonly ActorStateId None = new ActorStateId(0);
        public static readonly ActorStateId Root = new ActorStateId(1);
        public static readonly ActorStateId Jump = new ActorStateId(2);
        public static readonly ActorStateId Attack = new ActorStateId(3);
        public static readonly ActorStateId Hit = new ActorStateId(4);
        public static readonly ActorStateId Dead = new ActorStateId(5);
    }

    public sealed class StateMachineComp : Comp
    {
        InputBufferComp _input;
        SkillDirectorComp _director;
        TagComp _tags;
        ActorStateId _current = ActorStateId.Root;
        ActorState? _currentState;

        float _hitTimer;
        float _hitDuration = 0.35f;
        public ActorStateId Current => _current;
        public override bool WantsTick => true;
        public void SetHitDuration(float seconds)
            => _hitDuration = seconds > 0f ? seconds : 0.01f;
        protected override void OnAttach()
        {
            Self.TryGetComp(out _input);
            Self.TryGetComp(out _director);
            Self.TryGetComp(out _tags);
            _current = ActorStateId.Root;
            ApplyStateTags(ActorStateId.None, ActorStateId.Root);
        }
        protected override void OnDetach()
        {
            _input = null;
            _director = null;
            _tags = null;
        }
        public bool TryEnter(ActorStateId next, StateEnterArgs args)
        {
            if (next == _current)
                return true;
            if (_current == ActorStateId.Dead)
                return false;

            // 状态验证（可扩展：每个状态子类实现 CanEnterFrom）
            var targetState = GetState(next);
            if (targetState == null || !targetState.CanEnterFrom(_current))
                return false;

            // 简化：Hit 中只允许 Dead 或 Recover→Root（走 Notify/Recover）
            if (_current == ActorStateId.Hit &&
                next != ActorStateId.Dead &&
                next != ActorStateId.Root)
                return false;
            var prev = _current;
            _current = next;

            // 当前退出
            if (_currentState != null)
                _currentState.OnExit(new StateExitReason { Reason = args.Reason });
            _currentState = targetState;
            _currentState.OnEnter(args);

            if (next == ActorStateId.Hit)
                _hitTimer = _hitDuration;
            else
                _hitTimer = 0f;
            if (next == ActorStateId.Hit || next == ActorStateId.Dead)
            {
                _input?.Clear();
                _director?.Stop(next == ActorStateId.Dead
                    ? DirectorStopReason.Dead
                    : DirectorStopReason.Hit);
            }
            ApplyStateTags(prev, next);
            return true;
        }
        /// <summary>
        /// 任意非 Dead「活动结束」统一回 Root。
        /// Jump 落地、Attack 轴结束、Hit 硬直结束都走这里。
        /// </summary>
        public void NotifyActivityFinished(ActorStateId finished, string reason)
        {
            if (_current == ActorStateId.Dead)
                return;
            if (_current != finished)
                return;
            TryEnter(ActorStateId.Root, new StateEnterArgs(finished, reason));
        }
        public override void Tick(float dt)
        {
            if (_currentState != null)
                _currentState.Tick(dt);

            if (_current != ActorStateId.Hit)
                return;
            _hitTimer -= dt;
            if (_hitTimer <= 0f)
                NotifyActivityFinished(ActorStateId.Hit, "HitRecover");
        }
        void ApplyStateTags(ActorStateId prev, ActorStateId next)
        {
            if (_tags == null)
                return;
            // 粗状态 Tag：供 Condition / 移动使用
            if (prev == ActorStateId.Jump)
                _tags.Remove(CommonTags.Airborne, 1, TagSource.StateExit("Jump"));
            if (next == ActorStateId.Jump)
                _tags.Add(CommonTags.Airborne, 1, TagSource.StateEnter("Jump"));
            if (prev == ActorStateId.Root)
                _tags.Remove(CommonTags.Grounded, 1, TagSource.StateExit("Root"));
            if (next == ActorStateId.Root)
                _tags.Add(CommonTags.Grounded, 1, TagSource.StateEnter("Root"));
            if (next == ActorStateId.Dead)
                _tags.Add(CommonTags.Dead, 1, TagSource.StateEnter("Dead"));
        }

        ActorState? GetState(ActorStateId id)
        {
            return id switch
            {
                var i when i == ActorStateId.Root => new RootState(_tags, _input),
                var i when i == ActorStateId.Jump => new JumpState(_tags, _input),
                var i when i == ActorStateId.Attack => new AttackState(_tags, _input),
                var i when i == ActorStateId.Hit => new HitState( _tags, _input),
                var i when i == ActorStateId.Dead => new DeadState( _tags, _input),
                _ => null
            };
        }
    }
}
