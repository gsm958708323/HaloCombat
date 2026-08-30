using System;

namespace Combat.Core
{
    public sealed class TransformComp : Comp
    {
        public SimVec3 Position;
        public float YawDegrees;
    }

    public sealed class LocomotionComp : Comp
    {
        public const float Gravity = -20f;
        public const float JumpSpeed = 6f;
        public const float AirSteer = 0.35f;
        public const float GroundY = 0f;
        public const float StickDeadzone = 0.25f;

        public override bool WantsTick => false;

        TransformComp _tf;
        StateMachineComp _fsm;
        TagComp _tags;
        AttributeSet _attr;

        SimVec3 _moveIntent;
        SimVec3 _skillDelta;
        SimVec3 _hitDelta;
        SimVec3? _teleport;
        float _pendingYaw;
        bool _hasSnapYaw;
        float _clipSteer;
        float _verticalVel;
        bool _grounded = true;

        public float Yaw => _tf != null ? _tf.YawDegrees : 0f;
        public bool IsGrounded => _grounded;
        public float ClipSteer => _clipSteer;
        public SimVec3 MoveIntent => _moveIntent;

        protected override void OnAttach()
        {
            _tf = Self.GetComp<TransformComp>();
            _fsm = Self.GetComp<StateMachineComp>();
            _tags = Self.GetComp<TagComp>();
            Self.TryGetComp(out _attr);
            _grounded = _tf.Position.Y <= GroundY + 1e-4f;
            WriteGroundTags(_grounded);
        }

        protected override void OnDetach()
        {
            _tf = null;
            _fsm = null;
            _tags = null;
            _attr = null;
            ClearFrameRequests();
        }

        public void RequestMoveIntent(float x, float z) => _moveIntent = new SimVec3(x, 0f, z);

        public void RequestSkillDelta(float x, float y, float z)
        {
            _skillDelta.X += x;
            _skillDelta.Y += y;
            _skillDelta.Z += z;
        }

        public void RequestHitDelta(float x, float y, float z)
        {
            _hitDelta.X += x;
            _hitDelta.Y += y;
            _hitDelta.Z += z;
        }

        public void RequestTeleport(in SimVec3 pos) => _teleport = pos;

        public void RequestSnapYaw()
        {
            if (StickMag(_moveIntent) >= StickDeadzone)
            {
                _pendingYaw = YawFromStick(_moveIntent);
                _hasSnapYaw = true;
            }
        }

        public void RequestSnapYawDegrees(float yaw)
        {
            _pendingYaw = yaw;
            _hasSnapYaw = true;
        }

        public float FacingForSkillMove()
            => _hasSnapYaw ? _pendingYaw : (_tf != null ? _tf.YawDegrees : 0f);

        public void ImpulseJump()
        {
            if (!_grounded) return;
            _verticalVel = JumpSpeed;
            _grounded = false;
            WriteGroundTags(false);
        }

        public void SetClipSteer(float steer) => _clipSteer = steer < 0f ? 0f : steer;
        public void ClearClipSteer() => _clipSteer = 0f;
        public void ClearPendingSkill() => _skillDelta = SimVec3.Zero;

        public void Integrate(float dt)
        {
            if (_tf == null || _fsm == null)
            {
                ClearFrameRequests();
                return;
            }

            if (_teleport.HasValue)
            {
                _tf.Position = _teleport.Value;
                _teleport = null;
            }

            var policy = _fsm.Motor;
            var loco = policy.Loco;

            if (_hasSnapYaw)
            {
                _tf.YawDegrees = _pendingYaw;
                _hasSnapYaw = false;
            }

            float motorScale = MotorScale(loco);
            var delta = SimVec3.Zero;
            if (motorScale > 0f)
            {
                var walk = HorizontalMotor(dt, motorScale);
                delta.X += walk.X;
                delta.Z += walk.Z;
            }

            if (loco.UseSkill)
            {
                delta.X += _skillDelta.X;
                delta.Y += _skillDelta.Y;
                delta.Z += _skillDelta.Z;
            }

            if (loco.UseHit)
            {
                delta.X += _hitDelta.X;
                delta.Y += _hitDelta.Y;
                delta.Z += _hitDelta.Z;
            }

            if (loco.ApplyGravity)
                delta.Y += IntegrateGravity(dt);
            else if (_grounded)
                _verticalVel = 0f;

            ApplyFacing(policy.Facing);

            if (delta.X != 0f || delta.Y != 0f || delta.Z != 0f)
                _tf.Position = _tf.Position + delta;

            ClearFrameRequests();
        }

        float MotorScale(in LocoProfile loco)
        {
            if (_grounded)
                return _clipSteer > 0f ? _clipSteer : loco.MotorScale;
            return AirSteer;
        }

        SimVec3 HorizontalMotor(float dt, float scale)
        {
            float mag = StickMag(_moveIntent);
            if (mag < 1e-6f) return SimVec3.Zero;
            float speed = _attr != null ? _attr.GetFinal(AttrId.MoveSpeed) : 5f;
            float inv = 1f / mag;
            float step = speed * dt * scale;
            return new SimVec3(_moveIntent.X * inv * step, 0f, _moveIntent.Z * inv * step);
        }

        float IntegrateGravity(float dt)
        {
            if (_grounded)
            {
                _verticalVel = 0f;
                if (_tf.Position.Y > GroundY)
                {
                    _grounded = false;
                    WriteGroundTags(false);
                }
                else
                    return GroundY - _tf.Position.Y;
            }

            _verticalVel += Gravity * dt;
            float dy = _verticalVel * dt;
            float nextY = _tf.Position.Y + dy;
            if (nextY <= GroundY)
            {
                dy = GroundY - _tf.Position.Y;
                _verticalVel = 0f;
                _grounded = true;
                WriteGroundTags(true);
            }

            return dy;
        }

        void ApplyFacing(in FacingPolicy facing)
        {
            bool stick = StickMag(_moveIntent) >= StickDeadzone;
            float want = stick ? YawFromStick(_moveIntent) : _tf.YawDegrees;
            switch (facing.Mode)
            {
                case FacingMode.Lock:
                    return;
                case FacingMode.FollowStickIfGrounded:
                    if (_grounded && stick) _tf.YawDegrees = want;
                    return;
                case FacingMode.SteerIfGrounded:
                    if (_grounded && _clipSteer > 0f && stick)
                        _tf.YawDegrees = want;
                    return;
            }
        }

        void WriteGroundTags(bool grounded)
        {
            if (_tags == null) return;
            if (grounded)
            {
                if (!_tags.Has(CommonTags.Grounded))
                    _tags.Add(CommonTags.Grounded, 1, TagSource.Effect("Loco.Land"));
                if (_tags.Has(CommonTags.Airborne))
                    _tags.Remove(CommonTags.Airborne, 1, TagSource.Effect("Loco.Land"));
            }
            else
            {
                if (_tags.Has(CommonTags.Grounded))
                    _tags.Remove(CommonTags.Grounded, 1, TagSource.Effect("Loco.Air"));
                if (!_tags.Has(CommonTags.Airborne))
                    _tags.Add(CommonTags.Airborne, 1, TagSource.Effect("Loco.Air"));
            }
        }

        void ClearFrameRequests()
        {
            _skillDelta = SimVec3.Zero;
            _hitDelta = SimVec3.Zero;
        }

        public static float StickMag(in SimVec3 v)
            => (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);

        public static float YawFromStick(in SimVec3 v)
            => (float)(Math.Atan2(v.Z, v.X) * (180.0 / Math.PI));

        public static SimVec3 ForwardFromYaw(float yawDeg)
        {
            double r = yawDeg * Math.PI / 180.0;
            return new SimVec3((float)Math.Cos(r), 0f, (float)Math.Sin(r));
        }
    }
}
