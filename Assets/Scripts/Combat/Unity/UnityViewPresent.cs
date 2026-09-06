using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityPresentFactory : Combat.Presentation.IPresentFactory
    {
        readonly ViewPrefabTable _views;
        readonly Transform _root;
        readonly Transform _vfxRoot;

        public UnityPresentFactory(ViewPrefabTable views, Transform root, Transform vfxRoot)
        {
            _views = views;
            _root = root;
            _vfxRoot = vfxRoot != null ? vfxRoot : root;
        }

        public Combat.Presentation.PresentActor Create(string blueprintId)
        {
            var actor = new Combat.Presentation.PresentActor();
            actor.Add(new PoseFollowPresent());
            var character =
                blueprintId == "fighter"
                || blueprintId == "melee_guard"
                || blueprintId == "summon"
                || blueprintId == "melee_ai"
                || blueprintId == "melee_ai_narrow";
            if (character)
            {
                actor.Add(new AnimationPresent());
                var buff = new BuffFxPresent { Port = new UnityLoopVfxPort(_vfxRoot) };
                actor.Add(buff);
            }
            if (blueprintId == "fighter")
            {
                actor.Add(new PlayerCameraPresent());
                actor.Add(new PlayerFeedbackPresent { GhostPort = new UnityLoopVfxPort(_vfxRoot) });
                actor.Add(new PlayerHudSourcePresent());
            }
            var gizmo = new HitboxGizmoPresent { Port = new UnityGizmoDrawPort(Color.red) };
            actor.Add(gizmo);
            var prefab = _views != null ? _views.Find(blueprintId) : null;
            actor.Add(new UnityViewPresent(prefab, _root, ColorFor(blueprintId)));
            return actor;
        }

        public void Release(Combat.Presentation.PresentActor actor)
        {
            actor?.Release();
        }

        static Color ColorFor(string blueprintId)
        {
            if (blueprintId == "fighter")
                return new Color(.2f, .65f, 1f);
            if (blueprintId == "melee_guard")
                return new Color(1f, .25f, .2f);
            if (blueprintId == "summon")
                return new Color(.7f, .3f, 1f);
            return new Color(.7f, .7f, .7f);
        }
    }

    public sealed class UnityViewPresent : PresentComp
    {
        readonly GameObject _prefab;
        readonly Transform _root;
        readonly Color _fallbackColor;
        GameObject _view;
        Animator _animator;

        public UnityViewPresent(GameObject prefab, Transform root, Color fallbackColor)
        {
            _prefab = prefab;
            _root = root;
            _fallbackColor = fallbackColor;
        }

        public GameObject View => _view;

        protected override void OnAttach()
        {
            _view = _prefab != null ? Object.Instantiate(_prefab, _root) : CreateFallback();
            _animator = _view != null ? _view.GetComponentInChildren<Animator>() : null;
        }

        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;

        public override void SyncLogic(Combat.Core.CombatWorld world)
        {
            if (_view == null || !Self.TryLogic(world, out var actor))
                return;
            if (actor.TryGetComp<Combat.Core.TagComp>(out var tags))
                _view.SetActive(!tags.Has(Combat.Core.CommonTags.Dead));
        }

        public override void LateTick(float dt)
        {
            if (_view == null)
                return;
            if (Self.TryGet<PoseFollowPresent>(out var pose))
            {
                _view.transform.position = new Vector3(
                    pose.DisplayPos.X,
                    pose.DisplayPos.Y,
                    pose.DisplayPos.Z
                );
                _view.transform.rotation = Quaternion.Euler(0f, pose.DisplayYaw, 0f);
            }
            if (!Self.TryGet<AnimationPresent>(out var animation) || _animator == null)
                return;
            var flags = animation.Applied;
            SetBool("Grounded", flags.Grounded);
            SetBool("InAir", flags.InAir);
            SetBool("Attack", flags.Attack);
            SetBool("Hit", flags.Hit);
            SetBool("Downed", flags.Downed);
            SetBool("Dead", flags.Dead);
            SetBool("IFrame", flags.IFrame);
            SetBool("Hitstop", flags.Hitstop);
            SetFloat("Speed", flags.Speed);
            SetInteger("SkillId", flags.SkillId);
            _animator.speed = flags.Hitstop ? 0f : 1f;
        }

        protected override void OnDetach()
        {
            if (_view != null)
                Object.Destroy(_view);
            _view = null;
            _animator = null;
        }

        void SetBool(string name, bool value)
        {
            if (_animator.HasParameter(name, AnimatorControllerParameterType.Bool))
                _animator.SetBool(name, value);
        }

        void SetFloat(string name, float value)
        {
            if (_animator.HasParameter(name, AnimatorControllerParameterType.Float))
                _animator.SetFloat(name, value);
        }

        void SetInteger(string name, int value)
        {
            if (_animator.HasParameter(name, AnimatorControllerParameterType.Int))
                _animator.SetInteger(name, value);
        }

        GameObject CreateFallback()
        {
            var primitive =
                Self.BlueprintId == "stake" ? PrimitiveType.Cube : PrimitiveType.Capsule;
            var go = GameObject.CreatePrimitive(primitive);
            go.name = "Present_" + Self.BlueprintId;
            go.transform.SetParent(_root, false);
            go.transform.localScale =
                Self.BlueprintId == "stake"
                    ? new Vector3(.45f, .7f, .45f)
                    : new Vector3(.65f, 1.1f, .65f);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = _fallbackColor;
            return go;
        }
    }

    static class AnimatorExtensions
    {
        public static bool HasParameter(
            this Animator animator,
            string name,
            AnimatorControllerParameterType type
        )
        {
            if (animator == null || animator.parameters == null)
                return false;
            var parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].name == name && parameters[i].type == type)
                    return true;
            return false;
        }
    }
}
