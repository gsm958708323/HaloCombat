using System;

namespace Combat.Core
{
    public sealed class BehaviorTreeComp : Comp
    {
        readonly BtNode _prototype;
        readonly Action<BtBlackboard> _configure;
        BtNode _root;
        readonly BtBlackboard _board = new BtBlackboard();
        bool _homeCaptured;
        public override bool WantsTick => true;
        public bool Enabled { get; set; } = true;
        public BtBlackboard Board => _board;
        public BehaviorTreeComp(BtNode prototype, Action<BtBlackboard> configure = null)
        {
            _prototype = prototype ?? throw new ArgumentNullException(nameof(prototype));
            _configure = configure;
        }
        protected override void OnAttach()
        {
            if (!Season2Contracts.CloneTreePerActor) throw new InvalidOperationException("CloneTreePerActor required");
            _root = _prototype.Clone();
            _homeCaptured = false;
            _configure?.Invoke(_board);
        }
        protected override void OnDetach() { _root = null; _board.ClearTarget(); _board.Owner = EntityId.Invalid; _homeCaptured = false; }
        public override void Tick(float dt)
        {
            if (!Enabled || _root == null || Self.World == null) return;
            if (!_homeCaptured && Self.TryGetComp<TransformComp>(out var tf))
            {
                // EntityRegistry attaches before gameplay code assigns the spawn
                // position. Capture home on the first simulation tick instead.
                _board.Home = tf.Position;
                _homeCaptured = true;
            }
            _root.Tick(new BtTick(Self, Self.World, _board, dt));
        }

        public void SetTree(BtNode prototype)
        {
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            _root = prototype.Clone();
        }
    }
}
