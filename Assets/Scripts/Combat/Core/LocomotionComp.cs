using System;

namespace Combat.Core
{
    public struct SimVec3
    {
        public float X, Y, Z;
        public SimVec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static SimVec3 Zero => new SimVec3(0, 0, 0);
        public static SimVec3 operator +(SimVec3 a, SimVec3 b)
            => new SimVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }
    public sealed class TransformComp : Comp
    {
        public SimVec3 Position;
        public float YawDegrees; // MVP 只要平面朝向
        public void Teleport(in SimVec3 pos) => Position = pos;
    }

    /// <summary>
    /// 位移权总闸。帧内先收轴位移，再按主状态决定是否走路，最后写 Transform。
    /// </summary>
    public sealed class LocomotionComp : Comp
    {
        readonly CombatTime _time;

        TransformComp _tf;
        StateMachineComp _fsm;
        InputBufferComp _input; // 仅 Demo：用 Jump/无方向；商用另接 MoveAxis 通道

        SimVec3 _axisDelta;     // 本帧轴位移（Attack 专用累计）
        SimVec3 _moveIntent;    // Root/Jump 方向意图（-1..1）
        float _walkSpeed = 5f;
        float _jumpSpeed = 6f;
        float _gravity = -20f;
        float _verticalVel;

        public override bool WantsTick => true;

        public LocomotionComp(CombatTime time)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
        }

        protected override void OnAttach()
        {
            _tf = Self.GetComp<TransformComp>();
            _fsm = Self.GetComp<StateMachineComp>();
            Self.TryGetComp(out _input);
        }

        protected override void OnDetach()
        {
            _tf = null;
            _fsm = null;
            _input = null;
            _axisDelta = SimVec3.Zero;
        }

        public void SetWalkSpeed(float s) => _walkSpeed = s;
        public void SetMoveIntent(float x, float z) => _moveIntent = new SimVec3(x, 0f, z);

        /// <summary>仅供轴 Effect 调用。</summary>
        public void AddAxisDelta(float x, float y, float z)
        {
            _axisDelta.X += x;
            _axisDelta.Y += y;
            _axisDelta.Z += z;
        }

        public override void Tick(float dt)
        {
            var state = _fsm.Current;
            var delta = SimVec3.Zero;

            switch (state.Value)
            {
                case 1: // Root
                    delta = IntegrateWalk(dt);
                    _verticalVel = 0f;
                    break;

                case 2: // Jump
                    delta = IntegrateWalk(dt); // 空中也可给水平（可再拆 AirControl）
                    _verticalVel += _gravity * dt;
                    delta.Y += _verticalVel * dt;
                    // 落地：简化 Y<=0
                    if (_tf.Position.Y + delta.Y <= 0f)
                    {
                        delta.Y = -_tf.Position.Y;
                        _verticalVel = 0f;
                        _fsm.NotifyActivityFinished(ActorStateId.Jump, "Land");
                    }
                    break;

                case 3: // Attack：只吃轴位移
                    delta = _axisDelta;
                    break;

                case 4: // Hit：本步不加受击位移
                case 5: // Dead
                    delta = SimVec3.Zero;
                    break;
            }

            // 轴累计每帧消费掉，防止泄漏到下一状态
            _axisDelta = SimVec3.Zero;

            if (delta.X != 0f || delta.Y != 0f || delta.Z != 0f)
                _tf.Position = _tf.Position + delta;
        }

        SimVec3 IntegrateWalk(float dt)
        {
            // 无独立摇杆通道时 Demo 用 0；测试里会 SetMoveIntent
            float lenSq = _moveIntent.X * _moveIntent.X + _moveIntent.Z * _moveIntent.Z;
            if (lenSq < 1e-6f)
                return SimVec3.Zero;

            float inv = 1f / MathF.Sqrt(lenSq);
            float x = _moveIntent.X * inv * _walkSpeed * dt;
            float z = _moveIntent.Z * inv * _walkSpeed * dt;
            return new SimVec3(x, 0f, z);
        }

        public void ImpulseJump()
        {
            _verticalVel = _jumpSpeed;
        }
    }
}
