using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    [RequireComponent(typeof(CombatRunner))]
    public sealed class CombatLessonVfx : MonoBehaviour
    {
        CombatRunner _runner;
        LineRenderer _ownerLine;
        Material _lineMaterial;
        Camera _camera;
        float _shake;
        CombatWorld _boundWorld;

        void Awake()
        {
            _runner = GetComponent<CombatRunner>();
            _camera = Camera.main;
            _lineMaterial = Material(new Color(0.05f, 0.95f, 0.75f, 0.9f), true);
        }

        void Start()
        {
            BindWorld();
            _ownerLine = new GameObject("OwnerLink").AddComponent<LineRenderer>();
            _ownerLine.transform.SetParent(transform, false);
            _ownerLine.positionCount = 2; _ownerLine.widthMultiplier = 0.035f; _ownerLine.material = _lineMaterial;
            _ownerLine.enabled = false;
        }

        void OnDestroy()
        {
            UnbindWorld();
        }

        void Update()
        {
            BindWorld();
            if (_shake > 0f)
            {
                _shake -= Time.unscaledDeltaTime;
                if (_camera != null) _camera.transform.localPosition = new Vector3(Random.Range(-1f, 1f) * _shake * 0.15f, 0f, 0f);
            }
            UpdateOwnerLine();
        }

        void BindWorld()
        {
            if (_boundWorld == _runner.World) return;
            UnbindWorld();
            _boundWorld = _runner.World;
            if (_boundWorld == null) return;
            _boundWorld.Events.Subscribe<EvCue>(OnCue);
            _boundWorld.Events.Subscribe<EvDamage>(OnDamage);
            _boundWorld.Events.Subscribe<EvHitstop>(OnHitstop);
        }

        void UnbindWorld()
        {
            if (_boundWorld == null) return;
            _boundWorld.Events.Unsubscribe<EvCue>(OnCue);
            _boundWorld.Events.Unsubscribe<EvDamage>(OnDamage);
            _boundWorld.Events.Unsubscribe<EvHitstop>(OnHitstop);
            _boundWorld = null;
        }

        void UpdateOwnerLine()
        {
            if (_runner == null || _runner.World == null || _ownerLine == null) return;
            var actors = _runner.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].TryGetComp<SummonComp>(out var summon) ||
                    !_runner.World.TryGetActor(summon.OwnerId, out var owner) ||
                    !actors[i].TryGetComp<TransformComp>(out var a) ||
                    !owner.TryGetComp<TransformComp>(out var b)) continue;
                _ownerLine.enabled = true;
                _ownerLine.SetPosition(0, new Vector3(a.Position.X, 1f, a.Position.Z));
                _ownerLine.SetPosition(1, new Vector3(b.Position.X, 1f, b.Position.Z));
                return;
            }
            _ownerLine.enabled = false;
        }

        void OnCue(EvCue e)
        {
            if (!_runner.World.TryGetActor(e.Source, out var source) || !source.TryGetComp<TransformComp>(out var tf)) return;
            Color color = e.CueId == 101 || e.CueId == 102 ? new Color(0.1f, 0.9f, 1f) : new Color(1f, 0.32f, 0.05f);
            var fx = Burst("Cue_" + e.CueId, new Vector3(tf.Position.X, 1f, tf.Position.Z), color, e.CueId == 101 ? 1.4f : 1f);
            if (e.CueId == 101 || e.CueId == 102) MakeSlash(fx.transform.position, color);
        }

        void OnDamage(EvDamage e)
        {
            if (!_runner.World.TryGetActor(e.Target, out var target) || !target.TryGetComp<TransformComp>(out var tf)) return;
            var p = new Vector3(tf.Position.X, 1.6f, tf.Position.Z);
            Burst("Damage_" + e.Target.Index, p, e.IsCrit ? Color.white : new Color(1f, 0.18f, 0.06f), 0.65f);
            _shake = e.IsCrit ? 0.18f : 0.08f;
        }

        void OnHitstop(EvHitstop e) { _shake = Mathf.Max(_shake, 0.14f); }

        GameObject Burst(string name, Vector3 position, Color color, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.transform.position = position; go.transform.localScale = Vector3.one * 0.12f;
            var c = go.GetComponent<Collider>(); if (c != null) Destroy(c);
            var r = go.GetComponent<Renderer>(); if (r != null) r.material = Material(color, true);
            var pulse = go.AddComponent<LessonBurst>(); pulse.Configure(size);
            return go;
        }

        void MakeSlash(Vector3 position, Color color)
        {
            var go = new GameObject("SlashArc");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 16; line.widthMultiplier = 0.08f; line.material = Material(color, true);
            for (int i = 0; i < 16; i++)
            {
                float t = i / 15f;
                float a = Mathf.Lerp(-0.9f, 0.9f, t);
                line.SetPosition(i, position + new Vector3(Mathf.Cos(a) * 0.9f, Mathf.Sin(t * Mathf.PI) * 0.9f, Mathf.Sin(a) * 0.25f));
            }
            Destroy(go, 0.28f);
        }

        static Material Material(Color color, bool emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            if (emission) { material.EnableKeyword("_EMISSION"); if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2f); }
            return material;
        }
    }

    sealed class LessonBurst : MonoBehaviour
    {
        float _life = 0.38f;
        float _size = 1f;
        Vector3 _initial;
        public void Configure(float size) { _size = size; _initial = transform.localScale; }
        void Update()
        {
            _life -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(1f - _life / 0.38f);
            transform.localScale = _initial * Mathf.Lerp(1f, _size, t);
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}

