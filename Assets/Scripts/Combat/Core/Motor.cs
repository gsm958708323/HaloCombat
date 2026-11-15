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
        public override bool WantsTick => false;

        TransformComp _tf;
        StateMachineComp _fsm;
        TagComp _tags;
        AttributeSet _attr;

        SimVec3 _moveIntent;
        SimVec3 _skillDelta;
        string _skillCollisionKind;
        SimVec3 _hitDelta;
        SimVec3? _teleport;
        float _pendingYaw;
        bool _hasSnapYaw;
        bool _hasAimYaw;
        float _aimYaw;
        float _clipSteer;
        float _verticalVel;
        bool _grounded = true;
        float _gravity;
        float _jumpSpeed;
        float _airSteer;
        float _groundY;
        float _stickDeadzone;

        public float Yaw => _tf != null ? _tf.YawDegrees : 0f;
        public bool IsGrounded => _grounded;
        public float ClipSteer => _clipSteer;
        public SimVec3 MoveIntent => _moveIntent;
        public float CollisionRadius { get; set; } = .4f;
        internal SimVec3 MovementFrom {get;private set;}
        internal ObstacleHit MovementContact {get;private set;}

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
            _teleport=null;_hasSnapYaw=false;_moveIntent=SimVec3.Zero;_verticalVel=0;
        }

        public void RequestMoveIntent(float x, float z) => _moveIntent = new SimVec3(x, 0f, z);

        public void RequestSkillDelta(float x, float y, float z,string collisionKind=null)
        {
            _skillDelta.X += x;
            _skillDelta.Y += y;
            _skillDelta.Z += z;
            if(collisionKind!=null)_skillCollisionKind=collisionKind;
        }

        public void RequestHitDelta(float x, float y, float z)
        {
            _hitDelta.X += x;
            _hitDelta.Y += y;
            _hitDelta.Z += z;
        }

        public bool RequestTeleport(in SimVec3 pos)
        {if(Self.World!=null&&!Self.World.Collision.IsFree(pos,CollisionRadius))return false;_teleport=pos;return true;}

        // Explicit setup relocation does not advance a combat step or replace a playing action.
        // Unlike a gameplay teleport request, it also discards an older pending destination.
        public bool TryReposition(in SimVec3 position)
        {
            if(_tf==null||Self.World==null||!Self.World.Collision.IsFree(position,CollisionRadius))return false;
            _tf.Position=position;_teleport=null;MovementFrom=position;MovementContact=default;return true;
        }

        public void RequestSnapYaw()
        {
            if(_tags.Has(CommonTags.BlockRotate)||Self.World.IsActorStopped(Self))return;
            if(_hasAimYaw){RequestSnapYawDegrees(_aimYaw);return;}
            if (StickMag(_moveIntent) >= _stickDeadzone)
            {
                _pendingYaw = YawFromStick(_moveIntent);
                _hasSnapYaw = true;
            }
        }

        public void RequestSnapYawDegrees(float yaw)
        {
            if(_tags.Has(CommonTags.BlockRotate)||Self.World.IsActorStopped(Self))return;
            _pendingYaw = yaw;
            _hasSnapYaw = true;
        }
        public void RequestAimYaw(float yaw){_hasAimYaw=true;_aimYaw=yaw;}
        public void SnapForCast()
        {RequestSnapYaw();if(_hasSnapYaw&&!_tags.Has(CommonTags.BlockRotate))_tf.YawDegrees=_pendingYaw;_hasSnapYaw=false;}

        public float FacingForSkillMove()
            => _hasSnapYaw&&!_tags.Has(CommonTags.BlockRotate) ? _pendingYaw : (_tf != null ? _tf.YawDegrees : 0f);

        public void ImpulseJump()
        {
            if (!_grounded||_tags.Has(CommonTags.BlockMove)||Self.World.IsActorStopped(Self)) return;
            _verticalVel = _jumpSpeed;
            _grounded = false;
            WriteGroundTags(false);
        }
        public void ImpulseVertical(float speed)
        {
            if(speed<=0||_tags==null||_tags.Has(CommonTags.Dead))return;
            _verticalVel=speed;_grounded=false;WriteGroundTags(false);
        }

        public void SetClipSteer(float steer) => _clipSteer = steer < 0f ? 0f : steer;
        public void ClearClipSteer() => _clipSteer = 0f;
        public void ClearPendingSkill() { _skillDelta=SimVec3.Zero;_skillCollisionKind=null;_teleport=null; }

        // Only voluntary movement belongs before queries. Hit effects and gravity are
        // resolved after every attack has observed the same post-movement positions.
        public void IntegrateBeforeHitDetection(float dt)
        {
            if(Self.World.IsActorStopped(Self))return;
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
            MovementFrom=_tf.Position;

            var policy = _fsm.Motor;
            var loco = policy.Loco;

            if (_hasSnapYaw)
            {
                if(!_tags.Has(CommonTags.BlockRotate))_tf.YawDegrees = _pendingYaw;
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

            if (loco.UseSkill&&!_tags.Has(CommonTags.BlockSkillMotion))
            {
                delta.X += _skillDelta.X;
                delta.Y += _skillDelta.Y;
                delta.Z += _skillDelta.Z;
            }

            ApplyFacing(policy.Facing);
            MovementContact=Move(delta,_skillCollisionKind??"movement");
            _skillDelta=SimVec3.Zero;_skillCollisionKind=null;_hasAimYaw=false;
        }

        public void IntegrateAfterHitDetection(float dt)
        {
            if(Self.World.IsActorStopped(Self))return;
            if(_tf==null||_fsm==null){_hitDelta=SimVec3.Zero;return;}
            var loco=_fsm.Motor.Loco;
            var delta=loco.UseHit&&!_tags.Has(CommonTags.BlockHitMotion)?_hitDelta:SimVec3.Zero;
            if(loco.ApplyGravity&&!_tags.Has(CommonTags.BlockGravity))delta.Y+=IntegrateGravity(dt);
            else if(_grounded)_verticalVel=0;
            Move(delta);_hitDelta=SimVec3.Zero;
        }

        ObstacleHit Move(SimVec3 delta,string kind="movement")
        {
            if(delta.X==0&&delta.Y==0&&delta.Z==0)return default;
            var destination=_tf.Position+delta;
            _tf.Position=Self.World.Collision.Constrain(_tf.Position,destination,CollisionRadius,out var obstacle);
            if(obstacle.Blocked)Self.World.Events.Publish(new EvObstacleHit(Self.Id,Self.Id,obstacle.Id,obstacle.Point,obstacle.Normal,kind));
            return obstacle;
        }

        float MotorScale(in LocoProfile loco)
        {
            if(_tags.Has(CommonTags.BlockMove))return 0;
            if (_grounded)
                return _clipSteer > 0f ? _clipSteer : loco.MotorScale;
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

        void ApplyFacing(in FacingPolicy facing)
        {
            if(_tags.Has(CommonTags.BlockRotate))return;
            if(_hasAimYaw)
            {
                if(_grounded&&facing.Mode!=FacingMode.Lock)_tf.YawDegrees=_aimYaw;
                return;
            }
            bool stick = StickMag(_moveIntent) >= _stickDeadzone;
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
            _hasAimYaw=false;
            _skillDelta = SimVec3.Zero;
            _skillCollisionKind=null;
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
