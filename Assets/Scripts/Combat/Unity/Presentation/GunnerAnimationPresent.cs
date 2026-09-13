using Combat.Core;
using UnityEngine;

namespace Combat.Presentation
{
    public sealed class GunnerAnimationPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;

        string _state = "Stand";
        float _progress;
        bool _dead;
        bool _hitstop;
        float _speed;

        public override void SyncLogic(CombatWorld world)
        {
            if (!Self.TryLogic(world, out var actor)) return;
            _dead = actor.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead);
            _hitstop = world.InHitstop;
            _speed = 0f;

            if (actor.TryGetComp<LocomotionComp>(out var loco))
                _speed = LocomotionComp.StickMag(loco.MoveIntent);

            if (_dead)
            {
                _state = "Dead";
                return;
            }

            if (actor.TryGetComp<StateMachineComp>(out var sm) && sm.Current == ActivityId.Hit)
            {
                _state = "Hurt0";
                return;
            }

            if (actor.TryGetComp<SkillDirectorComp>(out var director) && director.IsPlaying)
            {
                _state = string.IsNullOrEmpty(director.AnimatorState) ? "Fire" : director.AnimatorState;
                if (_state == "Roll")
                    _state = RollState(actor);
                _progress = director.CurrentDuration > 0f
                    ? Mathf.Clamp01(director.CurrentTime / director.CurrentDuration)
                    : 0f;
                return;
            }

            _progress = 0f;
            _state = _speed > .01f ? MoveState(actor) : "Stand";
        }

        public void NotifyHurt()
        {
            if (!_dead) _state = "Hurt0";
        }

        public override void LateTick(float dt)
        {
            if (!Self.TryGet<ActorViewPresent>(out var view)) return;
            var animator = view.Animator;
            if (animator == null) return;
            int hash = Animator.StringToHash(_state);
            if (animator.HasState(0, hash))
            {
                animator.speed = _hitstop ? 0f : 1f;
                animator.Play(hash, 0, _progress);
            }
        }

        string MoveState(Actor actor)
        {
            if (!actor.TryGetComp<TransformComp>(out var tf) || !actor.TryGetComp<LocomotionComp>(out var loco))
                return "Stand";
            string tail = DirectionTail(tf.YawDegrees, LocomotionComp.YawFromStick(loco.MoveIntent));
            return "Move" + tail;
        }

        string RollState(Actor actor)
        {
            if (!actor.TryGetComp<TransformComp>(out var tf) || !actor.TryGetComp<LocomotionComp>(out var loco))
                return "RollForward";
            return "Roll" + DirectionTail(tf.YawDegrees, LocomotionComp.YawFromStick(loco.MoveIntent));
        }

        static string DirectionTail(float face, float move)
        {
            float delta = move - face;
            while (delta > 180f) delta -= 360f;
            while (delta < -180f) delta += 360f;
            if (delta >= -45f && delta <= 45f) return "Forward";
            if (delta < -45f && delta >= -135f) return "Left";
            if (delta > 45f && delta <= 135f) return "Right";
            return "Back";
        }

        protected override void OnDetach()
        {
            _state = "Stand";
            _progress = 0f;
            _dead = false;
            _hitstop = false;
        }
    }

    public sealed class HealthRingPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;

        readonly Transform _root;
        GameObject _object;
        LineRenderer _line;
        float _fill = 1f;
        bool _visible;

        public HealthRingPresent(Transform root) => _root = root;

        protected override void OnAttach()
        {
            _object = new GameObject("HealthRing");
            if (_root != null) _object.transform.SetParent(_root, false);
            _line = _object.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.positionCount = 33;
            _line.startWidth = .025f;
            _line.endWidth = .025f;
            _line.startColor = new Color(.2f, .9f, .25f, .85f);
            _line.endColor = _line.startColor;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");
            if (shader != null) _line.sharedMaterial = new Material(shader);
        }

        public override void SyncLogic(CombatWorld world)
        {
            _visible = false;
            if (!Self.TryLogic(world, out var actor) || !actor.TryGetComp<AttributeSet>(out var attr)) return;
            float max = attr.GetFinal(AttrId.MaxHp);
            _fill = max <= 0f ? 0f : Mathf.Clamp01(attr.GetBase(AttrId.Hp) / max);
            _visible = true;
        }

        public override void LateTick(float dt)
        {
            if (_line == null) return;
            _line.enabled = _visible;
            if (!_visible || !Self.TryGet<PoseFollowPresent>(out var pose)) return;
            Vector3 center = new Vector3(pose.DisplayPos.X, pose.DisplayPos.Y + .025f, pose.DisplayPos.Z);
            int count = Mathf.Max(2, Mathf.CeilToInt(_fill * 32f));
            _line.positionCount = count + 1;
            for (int i = 0; i <= count; i++)
            {
                float angle = i / 32f * Mathf.PI * 2f;
                _line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * .34f, 0f, Mathf.Sin(angle) * .34f));
            }
        }

        protected override void OnDetach()
        {
            if (_line != null) _line.enabled = false;
            if (_object != null)
            {
                if (Application.isPlaying) Object.Destroy(_object);
                else Object.DestroyImmediate(_object);
            }
            _object = null;
            _line = null;
        }
    }
}
