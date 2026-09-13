using Combat.Core;
using UnityEngine;

namespace Combat.Presentation
{
    public sealed class ActorViewPresent : PresentComp
    {
        readonly GameObject _prefab;
        readonly Transform _root;
        GameObject _view;
        Animator _animator;

        public ActorViewPresent(GameObject prefab, Transform root)
        {
            _prefab = prefab;
            _root = root;
        }

        public GameObject View => _view;
        internal Animator Animator => _animator;
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;

        protected override void OnAttach()
        {
            if (_prefab == null)
                return;
            _view = Object.Instantiate(_prefab, _root);
            _animator = _view != null ? _view.GetComponentInChildren<Animator>() : null;
        }

        public override void SyncLogic(CombatWorld world)
        {
            if (_view == null || !Self.TryLogic(world, out var actor))
                return;
            if (actor.TryGetComp<TagComp>(out var tags))
                _view.SetActive(!tags.Has(CommonTags.Dead));
        }

        public override void LateTick(float dt)
        {
            if (_view == null || !Self.TryGet<PoseFollowPresent>(out var pose))
                return;
            _view.transform.position = new Vector3(
                pose.DisplayPos.X,
                pose.DisplayPos.Y,
                pose.DisplayPos.Z
            );
            _view.transform.rotation = Quaternion.Euler(0f, pose.DisplayYaw, 0f);
        }

        protected override void OnDetach()
        {
            if (_view != null)
                Destroy(_view);
            _view = null;
            _animator = null;
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
