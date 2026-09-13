using Combat.Core;
using UnityEngine;

namespace Combat.Presentation
{
    public sealed class GunnerAnimationPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;

        string _state = "Stand";
        string _appliedState;
        bool _wasCasting;
        float _progress;
        bool _dead;
        bool _casting;
        bool _hitstop;
        float _speed;

        public override void SyncLogic(CombatWorld world)
        {
            if (!Self.TryLogic(world, out var actor)) return;
            _wasCasting = _casting;
            _dead = actor.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead);
            _hitstop = world.InHitstop;
            _speed = 0f;

            if (actor.TryGetComp<LocomotionComp>(out var loco))
                _speed = LocomotionComp.StickMag(loco.MoveIntent);

            _casting = false;

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
                _casting = true;
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
            int hash = ResolveState(animator, _state);
            if (hash == 0) return;

            animator.speed = _hitstop ? 0f : 1f;

            // Play() every frame reset looping clips to frame 0, which is why walking
            // never cycled. CrossFadeInFixedTime silently no-ops on this controller
            // (state stayed on Idle), so Play is used but only on a real state change.
            bool becameCasting = _casting && !_wasCasting;
            if (_appliedState == _state)
            {
                // Casting timelines own the clip phase; looping states run free.
                if (_casting && _progress > 0f)
                    animator.Play(hash, 0, _progress);
                return;
            }

            _appliedState = _state;
            animator.Play(hash, 0, becameCasting ? _progress : 0f);
        }

        static int ResolveState(Animator animator, string state)
        {
            switch (state)
            {
                case "Stand":
                    return FirstState(animator, "Stand", "StandAndRun");
                case "Fire":
                    return FirstState(animator, "Fire", "0Fire", "1RapidFire");
                case "Reload":
                    return FirstState(animator, "Reload", "2Reload");
                case "Hurt0":
                case "Hurt1":
                    return FirstState(animator, state, "Hurt");
                case "MoveForward":
                case "MoveBack":
                case "MoveLeft":
                case "MoveRight":
                    return FirstState(animator, state, "StandAndRun");
                case "RollForward":
                case "RollBack":
                case "RollLeft":
                case "RollRight":
                    return FirstState(animator, state, "Roll", "StepAndRoll");
                default:
                    return FirstState(animator, state);
            }
        }

        static int FirstState(Animator animator, params string[] states)
        {
            for (int i = 0; i < states.Length; i++)
            {
                int hash = Animator.StringToHash(states[i]);
                if (animator.HasState(0, hash)) return hash;
            }
            return 0;
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
            _appliedState = null;
            _progress = 0f;
            _dead = false;
            _casting = false;
            _wasCasting = false;
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
