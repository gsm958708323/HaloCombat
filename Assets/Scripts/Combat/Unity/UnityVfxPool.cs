using System.Collections.Generic;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityVfxPool : IVfxPool
    {
        readonly Transform _root;
        readonly Dictionary<string, GameObject> _prefabs;
        readonly List<Live> _live = new List<Live>();

        struct Live
        {
            public GameObject Go;
            public float End;
        }

        public UnityVfxPool(Transform root, Dictionary<string, GameObject> prefabs)
        {
            _root = root;
            _prefabs = prefabs ?? new Dictionary<string, GameObject>();
        }

        public bool TryPlay(in CueDef d, SimVec3 pos, EntityId source)
        {
            var key = string.IsNullOrEmpty(d.PrefabKey) ? "cue_" + d.CueId : d.PrefabKey;
            if (!_prefabs.TryGetValue(key, out var prefab) || prefab == null)
                return true;
            var go = Object.Instantiate(prefab, _root);
            go.transform.position = new Vector3(pos.X, pos.Y, pos.Z);
            _live.Add(
                new Live { Go = go, End = Time.unscaledTime + (d.LifeTime > 0 ? d.LifeTime : .5f) }
            );
            return true;
        }

        public void TickUnscaled(float dt)
        {
            var now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (now >= _live[i].End)
                {
                    if (_live[i].Go != null)
                        Object.Destroy(_live[i].Go);
                    _live.RemoveAt(i);
                }
        }

        public void ReturnAll()
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].Go != null)
                    Object.Destroy(_live[i].Go);
            _live.Clear();
        }
    }
}
