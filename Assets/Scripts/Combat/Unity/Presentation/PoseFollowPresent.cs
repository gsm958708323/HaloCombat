using Combat.Core;

namespace Combat.Unity.Presentation
{
    public sealed class PoseFollowPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public SimVec3 LogicPos { get; private set; }
        public float LogicYaw { get; private set; }
        public SimVec3 DisplayPos { get; private set; }
        public float DisplayYaw { get; private set; }
        public float LogicTime { get; private set; }
        SimVec3 _prev,
            _next;
        float _prevYaw,
            _nextYaw,
            _prevTime,
            _nextTime,
            _renderTime;
        bool _hasPrev;
        bool _stopped;

        public void SetRenderLogicTime(float t) => _renderTime = t;

        public override void SyncLogic(CombatWorld world)
        {
            if (!Self.TryLogic(world, out var a) || !a.TryGetComp<TransformComp>(out var tf))
                return;
            _stopped = world.IsActorStopped(a);
            _prev = _next;
            _prevYaw = _nextYaw;
            _prevTime = _nextTime;
            if (!_hasPrev)
            {
                _prev = tf.Position;
                _prevYaw = tf.YawDegrees;
                _prevTime = world.Time.Time;
                _hasPrev = true;
            }
            _next = tf.Position;
            _nextYaw = tf.YawDegrees;
            _nextTime = world.Time.Time;
            LogicPos = tf.Position;
            LogicYaw = tf.YawDegrees;
            LogicTime = world.Time.Time;
        }

        public override void LateTick(float dt)
        {
            if (!_hasPrev || _stopped)
            {
                DisplayPos = LogicPos;
                DisplayYaw = LogicYaw;
                return;
            }
            float u =
                _nextTime <= _prevTime + 1e-6f
                    ? 1f
                    : ((_renderTime > 0f ? _renderTime : _nextTime) - _prevTime)
                        / (_nextTime - _prevTime);
            if (u < 0f)
                u = 0f;
            if (u > 1f)
                u = 1f;
            DisplayPos = Lerp(_prev, _next, u);
            DisplayYaw = LerpAngle(_prevYaw, _nextYaw, u);
        }

        protected override void OnDetach()
        {
            _hasPrev = false;
            LogicPos = DisplayPos = SimVec3.Zero;
        }

        static SimVec3 Lerp(SimVec3 a, SimVec3 b, float u) =>
            new SimVec3(a.X + (b.X - a.X) * u, a.Y + (b.Y - a.Y) * u, a.Z + (b.Z - a.Z) * u);

        static float LerpAngle(float a, float b, float u)
        {
            float d = b - a;
            while (d > 180f)
                d -= 360f;
            while (d < -180f)
                d += 360f;
            return a + d * u;
        }
    }
}
