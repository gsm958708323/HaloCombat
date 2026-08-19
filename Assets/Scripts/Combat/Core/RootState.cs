// RootState.cs（示例，其他同理）
namespace Combat.Core
{
    sealed class RootState : ActorState
    {
        public override string Name => "Root";

        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;

        public RootState(ComboTableSO combo, TagComp tags, InputBufferComp input, CombatTime time)
        {
            _combo = combo;
            _tags = tags;
            _input = input;
            _time = time;
        }

        public override bool CanEnterFrom(ActorStateId from) => true;

        public override void OnEnter(StateEnterArgs args) { }

        public override void Tick(float dt) { }

        public override void OnExit(StateExitReason reason) { }
    }

    // JumpState, AttackState, HitState, DeadState 同理（可复用清理逻辑在基类或各自 OnExit）
    // HitState 示例（重点清理）：
    sealed class HitState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;
        public override string Name => "Hit";

        public HitState(ComboTableSO combo, TagComp tags, InputBufferComp input, CombatTime time)
        {
            _combo = combo;
            _tags = tags;
            _input = input;
            _time = time;
        }
        public override void OnEnter(StateEnterArgs args)
        {
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason) { }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }

    sealed class JumpState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;
        public JumpState(ComboTableSO combo, TagComp tags, InputBufferComp input, CombatTime time)
        {
            _combo = combo;
            _tags = tags;
            _input = input;
            _time = time;
        }
        public override string Name => "Hit";

        public override void OnEnter(StateEnterArgs args)
        {
            // 清理已在 StateMachineComp.TryEnter 中统一
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason) { }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }

    sealed class AttackState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;
        public AttackState(ComboTableSO combo, TagComp tags, InputBufferComp input, CombatTime time)
        {
            _combo = combo;
            _tags = tags;
            _input = input;
            _time = time;
        }
        public override string Name => "Hit";

        public override void OnEnter(StateEnterArgs args)
        {
            // 清理已在 StateMachineComp.TryEnter 中统一
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason) { }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }
    sealed class DeadState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;

        public DeadState(ComboTableSO combo, TagComp tags, InputBufferComp input, CombatTime time)
        {
            _combo = combo;
            _tags = tags;
            _input = input;
            _time = time;
        }
        public override string Name => "Hit";

        public override void OnEnter(StateEnterArgs args)
        {
            // 清理已在 StateMachineComp.TryEnter 中统一
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason) { }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }
}
