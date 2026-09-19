using System;
using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityVfxPool : IAdvancedVfxPool
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
            public EntityId AnchorId;
            public string AnchorKey;
            public string InstanceKey;
            public bool Loop;
        }
        readonly Func<EntityId, string, SimVec3?> _anchorResolver;

        public UnityVfxPool(Transform root, Dictionary<string, GameObject> prefabs,
            Func<EntityId, string, SimVec3?> anchorResolver = null)
        {
            _root = root;
            _prefabs = prefabs ?? new Dictionary<string, GameObject>();
            _anchorResolver = anchorResolver;
        }

        public bool TryPlay(in CueDef d, SimVec3 pos, EntityId source)
        {
            return Spawn(d, pos, source, string.Empty, string.Empty, false, false);
        }

        public bool TryPlay(in CueDef d, in EvCue cue, SimVec3 pos)
        {
            if (cue.Stop)
            {
                Stop(cue.InstanceKey, cue.Source);
                return true;
            }

            EntityId anchorId = cue.Target.IsValid ? cue.Target : cue.Source;
            return Spawn(d, pos, anchorId, cue.AnchorKey, cue.InstanceKey, cue.Loop, cue.HasPoint);
        }

        bool Spawn(in CueDef d, SimVec3 pos, EntityId anchorId, string anchorKey,
            string instanceKey, bool loop, bool fixedPoint)
        {
            var key = string.IsNullOrEmpty(d.PrefabKey) ? "cue_" + d.CueId : d.PrefabKey;
            if (loop && !string.IsNullOrEmpty(instanceKey) && HasInstance(instanceKey, anchorId))
                return true;
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
                go = UnityEngine.Object.Instantiate(prefab, _root);
                Combat.Unity.Game.BuffArenaRenderUtility.Normalize(go);
            }
            go.transform.position = new Vector3(pos.X, pos.Y, pos.Z);
            _live.Add(
                new Live
                {
                    Go = go,
                    Key = key,
                    End = loop ? float.PositiveInfinity : Time.unscaledTime + (d.LifeTime > 0 ? d.LifeTime : .5f),
                    AnchorId = fixedPoint ? EntityId.Invalid : anchorId,
                    AnchorKey = fixedPoint ? string.Empty : (anchorKey ?? string.Empty),
                    InstanceKey = instanceKey ?? string.Empty,
                    Loop = loop,
                }
            );
            return true;
        }

        bool HasInstance(string instanceKey, EntityId anchorId)
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].InstanceKey == instanceKey && _live[i].AnchorId == anchorId)
                    return true;
            return false;
        }

        void Stop(string instanceKey, EntityId anchorId)
        {
            if (string.IsNullOrEmpty(instanceKey)) return;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].InstanceKey != instanceKey || _live[i].AnchorId != anchorId) continue;
                RecycleAt(i);
            }
        }

        public void TickUnscaled(float dt)
        {
            var now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                if (live.AnchorId.IsValid && !string.IsNullOrEmpty(live.AnchorKey) && _anchorResolver != null)
                {
                    var pos = _anchorResolver(live.AnchorId, live.AnchorKey);
                    if (pos.HasValue)
                        live.Go.transform.position = new Vector3(pos.Value.X, pos.Value.Y, pos.Value.Z);
                }
                if (!live.Loop && now >= live.End)
                    RecycleAt(i);
            }
        }

        void RecycleAt(int index)
        {
            var live = _live[index];
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
            _live.RemoveAt(index);
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
