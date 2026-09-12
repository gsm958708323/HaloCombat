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
        readonly Dictionary<string, Stack<GameObject>> _free =
            new Dictionary<string, Stack<GameObject>>();
        readonly List<Live> _live = new List<Live>();

        struct Live
        {
            public GameObject Go;
            public string Key;
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
            _prefabs.TryGetValue(key, out var prefab);
            GameObject go;
            if (_free.TryGetValue(key, out var free) && free.Count > 0)
            {
                go = free.Pop();
                go.SetActive(true);
            }
            else
            {
                if (prefab == null)
                    return false;
                go = Object.Instantiate(prefab, _root);
            }
            go.transform.position = new Vector3(pos.X, pos.Y, pos.Z);
            _live.Add(
                new Live
                {
                    Go = go,
                    Key = key,
                    End = Time.unscaledTime + (d.LifeTime > 0 ? d.LifeTime : .5f),
                }
            );
            return true;
        }

        public void TickUnscaled(float dt)
        {
            var now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
                if (now >= _live[i].End)
                {
                    var live = _live[i];
                    if (live.Go != null)
                    {
                        live.Go.SetActive(false);
                        if (!_free.TryGetValue(live.Key, out var free))
                        {
                            free = new Stack<GameObject>();
                            _free[live.Key] = free;
                        }
                        free.Push(live.Go);
                    }
                    _live.RemoveAt(i);
                }
        }

        public void ReturnAll()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var live = _live[i];
                if (live.Go != null)
                {
                    live.Go.SetActive(false);
                    if (!_free.TryGetValue(live.Key, out var free))
                    {
                        free = new Stack<GameObject>();
                        _free[live.Key] = free;
                    }
                    free.Push(live.Go);
                }
            }
            _live.Clear();
        }
    }
}
