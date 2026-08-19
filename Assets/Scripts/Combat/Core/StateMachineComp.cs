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

        public static readonly ActorStateId Root = new ActorStateId(1);
        public static readonly ActorStateId Jump = new ActorStateId(2);
        public static readonly ActorStateId Attack = new ActorStateId(3);
        public static readonly ActorStateId Hit = new ActorStateId(4);
        public static readonly ActorStateId Dead = new ActorStateId(5);
    }

    public sealed class StateMachineComp : Comp
    {
        readonly ComboTableSO _comboTable;
        readonly TagComp _tags;
        InputBufferComp _input;
        CombatTime _time;
        ActorState? _currentState;
        ActorStateId _current = ActorStateId.Root;
        ActorStateId _targetId = ActorStateId.Root;

        public ActorStateId Current => _current;

        public StateMachineComp(ComboTableSO comboTable, TagComp tags, InputBufferComp input, CombatTime time)
        {
            _comboTable = comboTable ?? throw new ArgumentNullException(nameof(comboTable));
            _tags = tags ?? throw new ArgumentNullException(nameof(tags));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _time = time ?? throw new ArgumentNullException(nameof(time));
        }

        public override bool WantsTick => false; // 主状态切换事件驱动；需要状态内 tick 再开
        protected override void OnAttach()
        {
            _input = Self.GetComp<InputBufferComp>();
            _current = ActorStateId.Root;
        }
        protected override void OnDetach()
        {
            if (_currentState != null)
                _currentState.OnExit(new StateExitReason { Reason = "Detach" });
            _currentState = null;
        }

        public bool TryEnter(ActorStateId next, StateEnterArgs args)
        {
            if (next == _current)
                return true;

            // MVP：Dead 不可退出；Hit 可被 Dead 覆盖
            if (_current == ActorStateId.Dead)
                return false;
            if (_current == ActorStateId.Hit && next != ActorStateId.Dead && next != ActorStateId.Root)
                return false; // 简化硬直：Hit 只能回 Root 或进 Dead（可按受击表再扩）

            // 状态验证（可扩展：每个状态子类实现 CanEnterFrom）
            var targetState = GetState(next);
            if (targetState == null || !targetState.CanEnterFrom(_current))
                return false;

            // 当前退出
            if (_currentState != null)
                _currentState.OnExit(new StateExitReason { Reason = args.Reason });

            // 新进入
            _current = next;
            _currentState = targetState;
            _currentState.OnEnter(args);

            // Hit/Dead 自动清理（你锁定编排）
            if (next == ActorStateId.Hit || next == ActorStateId.Dead)
            {
                _input.Clear();
                if (Self.TryGetComp<SkillDirectorComp>(out var director))
                    director.Stop(next == ActorStateId.Dead ? DirectorStopReason.Dead : DirectorStopReason.Hit);
            }

            return true;
        }

        /// <summary>硬直结束回 Root（后续可由 Hit 状态时长驱动调用）。</summary>
        public void RecoverFromHit()
        {
            if (_current == ActorStateId.Hit)
                TryEnter(ActorStateId.Root, new StateEnterArgs(ActorStateId.Hit, "Recover"));
        }

        public void Tick(float dt)
        {
            if (_currentState != null)
                _currentState.Tick(dt);
        }

        ActorState? GetState(ActorStateId id)
        {
            return id switch
            {
                var i when i == ActorStateId.Root => new RootState(_comboTable, _tags, _input, _time),
                var i when i == ActorStateId.Jump => new JumpState(_comboTable, _tags, _input, _time),
                var i when i == ActorStateId.Attack => new AttackState(_comboTable, _tags, _input, _time),
                var i when i == ActorStateId.Hit => new HitState(_comboTable, _tags, _input, _time),
                var i when i == ActorStateId.Dead => new DeadState(_comboTable, _tags, _input, _time),
                _ => null
            };
        }
    }
}
