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

        public RootState(TagComp tags, InputBufferComp input)
        {
            _tags = tags;
            _input = input;
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

        public HitState(TagComp tags, InputBufferComp input)
        {
            _tags = tags;
            _input = input;
        }
        public override void OnEnter(StateEnterArgs args)
        {
            base.OnEnter(args);
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason)
        {
            base.OnExit(reason);
        }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }

    sealed class JumpState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;
        public JumpState(TagComp tags, InputBufferComp input)
        {
            _tags = tags;
            _input = input;
        }
        public override string Name => "Jump";

        public override void OnEnter(StateEnterArgs args)
        {

            base.OnEnter(args);
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason)
        {
            base.OnExit(reason);
        }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }

    sealed class AttackState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;
        public AttackState(TagComp tags, InputBufferComp input)
        {
            _tags = tags;
            _input = input;
        }
        public override string Name => "Attack";

        public override void OnEnter(StateEnterArgs args)
        {

            base.OnEnter(args);
        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason)
        {
            base.OnExit(reason);
        }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }
    sealed class DeadState : ActorState
    {
        readonly ComboTableSO _combo;
        readonly TagComp _tags;
        readonly InputBufferComp _input;
        readonly CombatTime _time;

        public DeadState(TagComp tags, InputBufferComp input)
        {

            _tags = tags;
            _input = input;

        }
        public override string Name => "Dead";

        public override void OnEnter(StateEnterArgs args)
        {
            base.OnEnter(args);

        }

        public override void Tick(float dt) { }
        public override void OnExit(StateExitReason reason)
        {
            base.OnExit(reason);
        }
        public override bool CanEnterFrom(ActorStateId from) => true;
    }
}
