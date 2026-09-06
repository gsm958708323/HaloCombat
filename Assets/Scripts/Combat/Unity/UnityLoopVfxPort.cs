using System.Collections.Generic;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityLoopVfxPort : ILoopVfxPort
    {
        readonly Transform _root;
        readonly Dictionary<int, GameObject> _prefabs;
        readonly Dictionary<int, Live> _live = new Dictionary<int, Live>();
        int _nextHandle = 1;

        struct Live
        {
            public GameObject View;
            public EntityId Follow;
        }

        public UnityLoopVfxPort(Transform root, Dictionary<int, GameObject> prefabs = null)
        {
            _root = root;
            _prefabs = prefabs ?? new Dictionary<int, GameObject>();
        }

        public int PlayLoop(int visualId, EntityId follow)
        {
            GameObject view = null;
            if (_prefabs.TryGetValue(visualId, out var prefab) && prefab != null)
                view = Object.Instantiate(prefab, _root);
            var handle = _nextHandle++;
            _live[handle] = new Live { View = view, Follow = follow };
            return handle;
        }

        public void SetIntensity(int handle, int stacks)
        {
            if (!_live.TryGetValue(handle, out var live) || live.View == null)
                return;
            var particle = live.View.GetComponentInChildren<ParticleSystem>();
            if (particle != null)
            {
                var emission = particle.emission;
                emission.rateOverTime = Mathf.Max(1, stacks) * 8f;
            }
        }

        public void Stop(int handle)
        {
            if (!_live.TryGetValue(handle, out var live))
                return;
            if (live.View != null)
                Object.Destroy(live.View);
            _live.Remove(handle);
        }
    }
}
