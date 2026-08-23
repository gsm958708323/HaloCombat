using System;

namespace Combat.Core
{
    public struct SimVec3
    {
        public float X, Y, Z;

        public SimVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SimVec3 Zero => new SimVec3(0f, 0f, 0f);

        public static SimVec3 operator +(SimVec3 a, SimVec3 b)
            => new SimVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public sealed class TransformComp : Comp
    {
        public SimVec3 Position;
        // 0 degrees faces +X. Skill data uses this same local forward axis.
        public float YawDegrees;

        public void Teleport(in SimVec3 pos) => Position = pos;

        public SimVec3 LocalToWorld(float x, float y, float z)
        {
            float radians = YawDegrees * (MathF.PI / 180f);
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new SimVec3(
                x * cos - z * sin,
                y,
                x * sin + z * cos);
        }
    }

    /// <summary>
    /// Owns per-frame movement. Timeline offsets are accumulated first, then
    /// the current state decides whether walking and/or airborne motion applies.
    /// </summary>
    public sealed class LocomotionComp : Comp
    {
        readonly CombatTime _time;

        TransformComp _tf;
        StateMachineComp _fsm;
        InputBufferComp _input;

        SimVec3 _axisDelta;
        SimVec3 _moveIntent;
        float _walkSpeed = 5f;
        float _jumpSpeed = 6f;
        float _gravity = -20f;
        float _verticalVel;

        const float GroundEpsilon = 1e-5f;

        public override bool WantsTick => true;
        public bool IsGrounded => _tf == null ||
            (_tf.Position.Y <= GroundEpsilon && _verticalVel <= 0f);

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
            _moveIntent = SimVec3.Zero;
            _verticalVel = 0f;
        }

        public void SetWalkSpeed(float speed) => _walkSpeed = speed;

        public void SetMoveIntent(float x, float z)
        {
            _moveIntent = new SimVec3(x, 0f, z);

            // Keep the last facing while an action is playing. This makes a
            // dash/projectile use the direction at the time the action began.
            float lenSq = x * x + z * z;
            if (lenSq >= GroundEpsilon &&
                (_fsm == null || (_fsm.Current != ActorStateId.Attack &&
                                  _fsm.Current != ActorStateId.Hit &&
                                  _fsm.Current != ActorStateId.Dead)))
            {
                _tf.YawDegrees = MathF.Atan2(z, x) * (180f / MathF.PI);
            }
        }

        // World-space delta API retained for callers that already provide world coordinates.
        public void AddAxisDelta(float x, float y, float z)
        {
            _axisDelta.X += x;
            _axisDelta.Y += y;
            _axisDelta.Z += z;
        }

        public void AddLocalAxisDelta(float x, float y, float z)
        {
            var world = _tf.LocalToWorld(x, y, z);
            AddAxisDelta(world.X, world.Y, world.Z);
        }

        public override void Tick(float dt)
        {
            var state = _fsm.Current;
            var delta = SimVec3.Zero;

            switch (state.Value)
            {
                case 1: // Root
                    delta = IntegrateWalk(dt);
                    if (!IsGrounded)
                        delta = IntegrateAirborne(delta, dt);
                    else
                        _verticalVel = 0f;
                    break;

                case 2: // Jump
                    delta = IntegrateAirborne(IntegrateWalk(dt), dt);
                    break;

                case 3: // Attack
                    delta = _axisDelta;
                    if (!IsGrounded)
                        delta = IntegrateAirborne(delta, dt);
                    break;

                case 4: // Hit
                case 5: // Dead
                    delta = !IsGrounded
                        ? IntegrateAirborne(SimVec3.Zero, dt)
                        : SimVec3.Zero;
                    break;
            }

            _axisDelta = SimVec3.Zero;

            if (delta.X != 0f || delta.Y != 0f || delta.Z != 0f)
                _tf.Position = _tf.Position + delta;
        }

        SimVec3 IntegrateAirborne(SimVec3 delta, float dt)
        {
            _verticalVel += _gravity * dt;
            delta.Y += _verticalVel * dt;

            if (_tf.Position.Y + delta.Y > 0f)
                return delta;

            delta.Y = -_tf.Position.Y;
            _verticalVel = 0f;
            if (_fsm.Current == ActorStateId.Jump)
                _fsm.NotifyActivityFinished(ActorStateId.Jump, "Land");
            else
                _fsm.NotifyLanded();
            return delta;
        }

        SimVec3 IntegrateWalk(float dt)
        {
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
