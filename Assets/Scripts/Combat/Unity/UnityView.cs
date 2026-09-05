using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityActorView : MonoBehaviour, IActorView
    {
        public EntityId Id { get; private set; }
        string _blueprint;
        bool _dead;
        Transform _visual;
        Transform _core;
        Transform _ring;

        public void Bind(EntityId id, string blueprintId)
        {
            Id = id;
            _blueprint = blueprintId ?? string.Empty;
            _dead = false;
            gameObject.name = _blueprint + "_" + id.Index;
            BuildModel();
        }

        public void Sample(in PoseSample sample)
        {
            var p = sample.LogicPos;
            transform.position = new Vector3(p.X, p.Y, p.Z);
            transform.rotation = Quaternion.Euler(0f, 90f - sample.YawDeg, 0f);
            if (_dead) transform.rotation *= Quaternion.Euler(0f, 0f, 80f);
            if (_visual == null) return;
            float bob = _blueprint == "projectile" || _blueprint == "aoe"
                ? Mathf.Sin(Time.time * 7f + Id.Index) * 0.04f
                : Mathf.Sin(Time.time * 4f + Id.Index) * 0.025f;
            _visual.localPosition = new Vector3(0f, bob, 0f);
            if (_core != null) _core.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 8f) * 0.08f);
            if (_ring != null) _ring.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.05f);
        }

        public void OnDead(in EvEntityDead e)
        {
            _dead = true;
            if (_ring != null) _ring.gameObject.SetActive(false);
        }

        public void Release()
        {
            if (this != null) Destroy(gameObject);
        }

        void BuildModel()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            Color main = Palette.Main(_blueprint);
            Color accent = Palette.Accent(_blueprint);
            if (_blueprint == "projectile")
            {
                _core = Part(PrimitiveType.Sphere, "ProjectileCore", new Vector3(0f, 0.28f, 0f), Vector3.one * 0.3f, accent, true);
                var trail = _core.gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.28f; trail.startWidth = 0.22f; trail.endWidth = 0.01f;
                trail.material = Material(accent, true);
            }
            else if (_blueprint == "aoe")
            {
                _ring = Part(PrimitiveType.Cylinder, "AreaField", Vector3.zero, new Vector3(2f, 0.025f, 2f), accent, true);
                _ring.gameObject.AddComponent<LessonPulse>();
            }
            else if (_blueprint == "stake")
            {
                Part(PrimitiveType.Cylinder, "TargetColumn", new Vector3(0f, 0.7f, 0f), new Vector3(0.62f, 0.7f, 0.62f), main, false);
                _core = Part(PrimitiveType.Sphere, "TargetCore", new Vector3(0f, 1.35f, 0f), Vector3.one * 0.16f, accent, true);
                _ring = Part(PrimitiveType.Cylinder, "TargetRing", new Vector3(0f, 0.02f, 0f), new Vector3(1.3f, 0.015f, 1.3f), accent, true);
            }
            else
            {
                bool small = _blueprint == "summon";
                float scale = small ? 0.72f : 1f;
                Part(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.8f * scale, 0f), new Vector3(0.58f, 0.8f, 0.58f) * scale, main, false);
                Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.65f * scale, 0f), Vector3.one * 0.42f * scale, main, false);
                _core = Part(PrimitiveType.Sphere, "EnergyCore", new Vector3(0f, 1.03f * scale, -0.38f * scale), Vector3.one * 0.16f * scale, accent, true);
                Part(PrimitiveType.Cube, "Weapon", new Vector3(0.52f * scale, 0.75f * scale, 0f), new Vector3(0.12f, 0.72f, 0.18f) * scale, accent, true);
                _ring = Part(PrimitiveType.Cylinder, "GroundRing", new Vector3(0f, 0.02f, 0f), new Vector3(1.25f, 0.015f, 1.25f) * scale, accent, true);
                if (small) Part(PrimitiveType.Sphere, "SummonOrb", new Vector3(0f, 1.9f * scale, 0f), Vector3.one * 0.12f, accent, true);
            }
        }

        Transform Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color, bool emissive)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(_visual, false);
            go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            var renderer = go.GetComponent<Renderer>(); if (renderer != null) renderer.material = Material(color, emissive);
            return go.transform;
        }

        static Material Material(Color color, bool emissive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 2.4f);
            }
            if (color.a < 1f)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.renderQueue = 3000;
            }
            return material;
        }
    }

    public sealed class UnityViewFactory : IViewFactory
    {
        public IActorView Create(string blueprintId)
        {
            var go = new GameObject("ActorView_" + blueprintId);
            return go.AddComponent<UnityActorView>();
        }
    }

    static class Palette
    {
        public static Color Main(string id)
        {
            if (id == "stake") return new Color(0.35f, 0.42f, 0.5f);
            if (id == "melee_guard" || id == "melee_ai" || id == "melee_ai_narrow") return new Color(0.78f, 0.16f, 0.08f);
            if (id == "summon") return new Color(0.05f, 0.48f, 0.44f);
            if (id == "projectile") return new Color(0.8f, 0.12f, 0.04f);
            if (id == "aoe") return new Color(0.04f, 0.32f, 0.5f, 0.35f);
            return new Color(0.06f, 0.28f, 0.52f);
        }

        public static Color Accent(string id)
        {
            if (id == "stake") return new Color(0.9f, 0.92f, 1f);
            if (id == "melee_guard" || id == "melee_ai" || id == "melee_ai_narrow") return new Color(1f, 0.26f, 0.06f);
            if (id == "summon") return new Color(0.05f, 1f, 0.78f);
            if (id == "projectile") return new Color(1f, 0.48f, 0.06f);
            if (id == "aoe") return new Color(0.08f, 0.84f, 1f, 0.4f);
            return new Color(0.08f, 0.9f, 1f);
        }
    }

    sealed class LessonPulse : MonoBehaviour
    {
        Vector3 _base;
        void Awake() => _base = transform.localScale;
        void Update() => transform.localScale = _base * (1f + Mathf.Sin(Time.time * 5f) * 0.08f);
    }
}
