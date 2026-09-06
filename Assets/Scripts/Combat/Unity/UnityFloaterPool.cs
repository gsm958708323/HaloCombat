using System.Collections.Generic;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class UnityFloaterPool : IFloaterPool
    {
        readonly Camera _camera;
        readonly Transform _root;
        readonly List<Live> _live = new List<Live>();

        struct Live
        {
            public GameObject View;
            public float End;
            public Vector3 Start;
        }

        public UnityFloaterPool(Transform root, Camera camera)
        {
            _root = root;
            _camera = camera;
        }

        public bool TryPlay(in FloaterRequest request)
        {
            var go = new GameObject(
                request.Immune ? "Immune"
                : request.Heal ? "Heal"
                : "Damage"
            );
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(
                request.LogicPos.X,
                request.LogicPos.Y,
                request.LogicPos.Z
            );
            var text = go.AddComponent<TextMesh>();
            text.text =
                request.Immune ? "IMMUNE"
                : request.Heal ? "+" + request.Amount.ToString("0")
                : request.Amount.ToString("0");
            text.fontSize = request.Crit ? 64 : 48;
            text.color =
                request.Immune ? Color.gray
                : request.Heal ? Color.green
                : Color.red;
            _live.Add(
                new Live
                {
                    View = go,
                    End = Time.unscaledTime + 0.8f,
                    Start = go.transform.position,
                }
            );
            return true;
        }

        public void TickUnscaled(float dt)
        {
            var now = Time.unscaledTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                if (live.View == null || now >= live.End)
                {
                    if (live.View != null)
                        Object.Destroy(live.View);
                    _live.RemoveAt(i);
                    continue;
                }
                var t = Mathf.Clamp01(1f - (live.End - now) / 0.8f);
                live.View.transform.position = live.Start + Vector3.up * (t * .6f);
                if (_camera != null)
                    live.View.transform.LookAt(
                        live.View.transform.position + _camera.transform.rotation * Vector3.forward
                    );
            }
        }

        public void ReturnAll()
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].View != null)
                    Object.Destroy(_live[i].View);
            _live.Clear();
        }
    }
}
