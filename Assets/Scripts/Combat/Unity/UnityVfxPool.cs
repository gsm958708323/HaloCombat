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
            public int AnchorMissFrames;
        }
        readonly Func<EntityId, string, SimVec3?> _anchorResolver;

        /// <summary>锚点连续解析失败超过这么多帧才回收，避免 view 尚未建立时误杀。</summary>
        const int AnchorMissGraceFrames = 30;

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
                Stop(cue.InstanceKey, AnchorOf(cue));
                return true;
            }

            return Spawn(d, pos, AnchorOf(cue), cue.AnchorKey, cue.InstanceKey, cue.Loop, cue.HasPoint);
        }

        /// <summary>
        /// 锚点解析与 CueDirector.OnCue 保持一致：优先取目标，缺省回落施法者。
        /// 生成与停播必须用同一个算法，否则「施法者 ≠ 持有者」的 loop（例如玩家给敌人挂的 debuff）会挂得上却停不掉。
        /// </summary>
        static EntityId AnchorOf(in EvCue cue) => cue.Target.IsValid ? cue.Target : cue.Source;

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
                    {
                        live.Go.transform.position = new Vector3(pos.Value.X, pos.Value.Y, pos.Value.Z);
                        live.AnchorMissFrames = 0;
                    }
                    else if (++live.AnchorMissFrames > AnchorMissGraceFrames)
                    {
                        // 持有者已卸载/回收：Buff 的静默清理不会派发 OnExpire，这里兜底回收，避免残留幽灵特效。
                        RecycleAt(i);
                        continue;
                    }

                    _live[i] = live;
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
