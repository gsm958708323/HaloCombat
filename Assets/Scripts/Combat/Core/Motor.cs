using System;

namespace Combat.Core
{
    public sealed class TransformComp : Comp
    {
        public SimVec3 Position;

        // Yaw convention (frozen): 0 faces +Z (Unity's forward) and a positive angle
        // turns towards +X, so ForwardFromYaw(yaw) == Quaternion.Euler(0, yaw, 0) *
        // Vector3.forward. The old 2D polar convention (0 = +X, counter-clockwise) is
        // retired; converting an old angle is 90 - yaw.
        // Actors still spawn facing +X (the old default yaw 0), which is why the spawn
        // value is 90 instead of 0.
        public float YawDegrees = SpawnFacingYaw;

        public const float SpawnFacingYaw = 90f;
    }

    public sealed class LocomotionComp : Comp
    {
        public override bool WantsTick => false;

        // The source UnitRotate leaves rotateSpeed at 0, so an ordered rotation
        // resolves instantly. 3600 deg/s keeps mouse aiming visually instant while
        // still covering the 0.1s payload delay used by the source skill timelines.
        public const float AimTurnRateDegPerSec = 3600f;

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
        float _gravity;
        float _jumpSpeed;
        float _airSteer;
        float _groundY;
        float _stickDeadzone;
        float _aimYaw;
        bool _hasAimYaw;

        public float Yaw => _tf != null ? _tf.YawDegrees : 0f;
        public bool IsGrounded => _grounded;
        public float ClipSteer => _clipSteer;
        public SimVec3 MoveIntent => _moveIntent;

        protected override void OnAttach()
        {
            var motor = Self.World != null ? Self.World.Motor : MotorConfig.SeasonOneDefaults();
            _gravity = motor.Gravity;
            _jumpSpeed = motor.JumpSpeed;
            _airSteer = motor.AirSteer;
            _groundY = motor.GroundY;
            _stickDeadzone = motor.StickDeadzone;
            _tf = Self.GetComp<TransformComp>();
            _fsm = Self.GetComp<StateMachineComp>();
            _tags = Self.GetComp<TagComp>();
            Self.TryGetComp(out _attr);
            _grounded = _tf.Position.Y <= _groundY + 1e-4f;
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

        public void RequestAimYaw(float yaw)
        {
            _aimYaw = yaw;
            _hasAimYaw = true;
        }

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
            if (_tags.Has(CommonTags.BlockRotate) || Self.Time.IsStopped) return;
            // A live mouse-aim request outranks the move stick: the source game
            // orders the rotation from the cursor before it starts the skill
            // timeline, so a cast must never snap the body back to the last
            // movement direction.
            if (_hasAimYaw)
            {
                _pendingYaw = _aimYaw;
                _hasSnapYaw = true;
                return;
            }
            if (StickMag(_moveIntent) >= _stickDeadzone)
            {
                _pendingYaw = YawFromStick(_moveIntent);
                _hasSnapYaw = true;
            }
        }

        public void RequestSnapYawDegrees(float yaw)
        {
            if (_tags.Has(CommonTags.BlockRotate) || Self.Time.IsStopped) return;
            _pendingYaw = yaw;
            _hasSnapYaw = true;
        }

        public void SnapForCast()
        {
            RequestSnapYaw();
            if (_hasSnapYaw && !_tags.Has(CommonTags.BlockRotate)) _tf.YawDegrees = _pendingYaw;
            _hasSnapYaw = false;
        }

        public void ClearPendingMotion()
        {
            ClearFrameRequests();
            _teleport = null;
            _hasSnapYaw = _hasAimYaw = false;
            _moveIntent = SimVec3.Zero;
            _verticalVel = 0f;
        }

        public float FacingForSkillMove()
            => _hasSnapYaw && !_tags.Has(CommonTags.BlockRotate) ? _pendingYaw : (_tf != null ? _tf.YawDegrees : 0f);

        public void ImpulseJump()
        {
            if (!_grounded || _tags.Has(CommonTags.BlockMove) || Self.World.IsActorStopped(Self)) return;
            _verticalVel = _jumpSpeed;
            _grounded = false;
            WriteGroundTags(false);
        }

        public void ImpulseLaunch(float verticalSpeed)
        {
            if (verticalSpeed <= 0f) return;
            if (verticalSpeed > _verticalVel)
                _verticalVel = verticalSpeed;
            _grounded = false;
            WriteGroundTags(false);
        }

        public void SetClipSteer(float steer) => _clipSteer = steer < 0f ? 0f : steer;
        public void ClearClipSteer() => _clipSteer = 0f;
        public void ClearPendingSkill() => _skillDelta = SimVec3.Zero;

        public void Integrate(float dt)
        {
            IntegrateBeforeHitDetection(dt);
            IntegrateAfterHitDetection(dt);
        }

        public void IntegrateBeforeHitDetection(float dt)
        {
            if (Self.Time.IsStopped) return;
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

            if (_hasSnapYaw)
            {
                if (!_tags.Has(CommonTags.BlockRotate)) _tf.YawDegrees = _pendingYaw;
                _hasSnapYaw = false;
            }

            float motorScale = MotorScale();
            var delta = SimVec3.Zero;
            if (motorScale > 0f)
            {
                var walk = HorizontalMotor(dt, motorScale);
                delta.X += walk.X;
                delta.Z += walk.Z;
            }

            if (!_tags.Has(CommonTags.BlockSkillMotion))
            {
                delta.X += _skillDelta.X;
                delta.Y += _skillDelta.Y;
                delta.Z += _skillDelta.Z;
            }

            ApplyFacing(policy.Facing, dt);

            if (delta.X != 0f || delta.Y != 0f || delta.Z != 0f)
            {
                var target = _tf.Position + delta;
                target = _worldResolve(target, false, out _);
                _tf.Position = target;
            }

            _skillDelta = SimVec3.Zero;
            _hasAimYaw = false;
        }

        public void IntegrateAfterHitDetection(float dt)
        {
            if (Self.Time.IsStopped) return;
            if (_tf == null || _fsm == null)
            {
                _hitDelta = SimVec3.Zero;
                return;
            }

            var delta = SimVec3.Zero;
            if (!_tags.Has(CommonTags.BlockHitMotion))
            {
                delta.X += _hitDelta.X;
                delta.Y += _hitDelta.Y;
                delta.Z += _hitDelta.Z;
            }

            if (!_tags.Has(CommonTags.BlockGravity))
                delta.Y += IntegrateGravity(dt);
            else if (_grounded)
                _verticalVel = 0f;

            if (delta.X != 0f || delta.Y != 0f || delta.Z != 0f)
            {
                var target = _tf.Position + delta;
                target = _worldResolve(target, false, out _);
                _tf.Position = target;
            }

            _hitDelta = SimVec3.Zero;
        }

        float MotorScale()
        {
            if (_tags.Has(CommonTags.BlockMove)) return 0f;
            if (_grounded) return _clipSteer > 0f ? _clipSteer : 1f;
            return _airSteer;
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
                if (_tf.Position.Y > _groundY)
                {
                    _grounded = false;
                    WriteGroundTags(false);
                }
                else
                    return _groundY - _tf.Position.Y;
            }

            _verticalVel += _gravity * dt;
            float dy = _verticalVel * dt;
            float nextY = _tf.Position.Y + dy;
            if (nextY <= _groundY)
            {
                dy = _groundY - _tf.Position.Y;
                _verticalVel = 0f;
                _grounded = true;
                WriteGroundTags(true);
            }

            return dy;
        }

        void ApplyFacing(in FacingPolicy facing, float dt)
        {
            if (_tags.Has(CommonTags.BlockRotate)) return;
            bool stick = StickMag(_moveIntent) >= _stickDeadzone;
            float want = stick ? YawFromStick(_moveIntent) : _tf.YawDegrees;
            // Mouse aim owns the facing whenever it is requested and the activity
            // does not hard-lock rotation. The source PlayerController keeps
            // calling OrderRotateTo every FixedUpdate while a skill timeline runs
            // (SetCasterControlState(canRotate: true)), so an Attack must not gate
            // aiming behind a Move clip's steer value.
            if (_hasAimYaw && _grounded)
            {
                float turn = facing.TurnRate > 0f ? facing.TurnRate * dt : AimTurnRateDegPerSec * dt;
                _tf.YawDegrees = MoveTowardsAngle(_tf.YawDegrees, _aimYaw, turn);
                return;
            }
            switch (facing.Mode)
            {
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

        SimVec3 _worldResolve(in SimVec3 target, bool flying, out bool obstructed)
        {
            if (Self == null || Self.World == null || _tf == null)
            {
                obstructed = false;
                return target;
            }
            float radius = _attr != null ? Math.Max(0f, _attr.GetFinal(AttrId.MoveSpeed) * 0f) : 0f;
            // Character radius is stored by HitboxComp in source-style actors;
            // generic actors keep the zero-radius navigation path.
            if (Self.TryGetComp<HitboxComp>(out var hitbox) && hitbox.Radius > 0f)
                radius = hitbox.Radius;
            else if (Self.TryGetComp<CharacterRadiusComp>(out var body))
                radius = body.Radius;
            return Self.World.Movement.Resolve(_tf.Position, target, radius, flying, false, out obstructed);
        }

        static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = target - current;
            while (delta > 180f) delta -= 360f;
            while (delta < -180f) delta += 360f;
            if (Math.Abs(delta) <= maxDelta) return target;
            return current + Math.Sign(delta) * maxDelta;
        }

        public static float StickMag(in SimVec3 v)
            => (float)Math.Sqrt(v.X * v.X + v.Z * v.Z);

        public static float YawFromStick(in SimVec3 v)
            => (float)(Math.Atan2(v.X, v.Z) * (180.0 / Math.PI));

        public static SimVec3 ForwardFromYaw(float yawDeg)
        {
            double r = yawDeg * Math.PI / 180.0;
            return new SimVec3((float)Math.Sin(r), 0f, (float)Math.Cos(r));
        }
    }
}
