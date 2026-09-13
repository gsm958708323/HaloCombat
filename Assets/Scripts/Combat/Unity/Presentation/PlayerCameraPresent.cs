using Combat.Core;

namespace Combat.Presentation
{
    public struct CameraFocus
    {
        public SimVec3 Logic,
            Spring;
        public float EyeHeight,
            YawHint,
            Impulse;
        public bool Hitstop,
            Downed,
            Dead,
            Paused;
    }

    public sealed class PlayerCameraPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public CameraFocus Focus { get; private set; }
        public bool HasFocus { get; private set; }
        public float StandEye = 1.6f,
            DownedEye = .55f,
            SpringHz = 8f;
        PoseFollowPresent _pose;
        bool _paused,
            _init;

        public void SetPaused(bool p) => _paused = p;

        public void NotifyImpulse()
        {
            var f = Focus;
            f.Impulse = 1f;
            Focus = f;
        }

        protected override void OnAttach() => Self.TryGet(out _pose);

        protected override void OnDetach()
        {
            _pose = null;
            _init = false;
            HasFocus = false;
        }

        public override void SyncLogic(CombatWorld w)
        {
            var f = Focus;
            f.Paused = _paused;
            f.Hitstop = w != null && w.InHitstop;
            if (Self.TryLogic(w, out var a))
            {
                f.Logic = _pose != null ? _pose.LogicPos : a.GetComp<TransformComp>().Position;
                f.YawHint = _pose != null ? _pose.LogicYaw : a.GetComp<TransformComp>().YawDegrees;
                var t = a.GetComp<TagComp>();
                f.Downed = t.Has(CommonTags.Downed);
                f.Dead = t.Has(CommonTags.Dead);
                f.EyeHeight = f.Downed || f.Dead ? DownedEye : StandEye;
                HasFocus = true;
            }
            Focus = f;
        }

        public override void LateTick(float dt)
        {
            if (!HasFocus)
                return;
            var f = Focus;
            var target = new SimVec3(f.Logic.X, f.Logic.Y + f.EyeHeight, f.Logic.Z);
            if (!_init)
            {
                _spring = target;
                _init = true;
            }
            else
            {
                var k = 1f - (float)System.Math.Exp(-SpringHz * (dt < 0 ? 0 : dt));
                _spring = new SimVec3(
                    _spring.X + (target.X - _spring.X) * k,
                    _spring.Y + (target.Y - _spring.Y) * k,
                    _spring.Z + (target.Z - _spring.Z) * k
                );
            }
            if (f.Impulse > 0)
            {
                f.Impulse -= dt < 0 ? 0 : dt;
                if (f.Impulse < 0)
                    f.Impulse = 0;
            }
            f.Spring = _spring;
            Focus = f;
        }

        SimVec3 _spring;
    }
}
