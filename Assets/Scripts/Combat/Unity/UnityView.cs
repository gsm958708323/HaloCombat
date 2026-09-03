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

        public void Bind(EntityId id, string blueprintId)
        {
            Id = id;
            _blueprint = blueprintId ?? string.Empty;
            _dead = false;
            gameObject.name = _blueprint + "_" + id.Index;
        }

        public void Sample(in PoseSample sample)
        {
            var p = sample.LogicPos;
            transform.position = new Vector3(p.X, p.Y, p.Z);
            transform.rotation = Quaternion.Euler(0f, 90f - sample.YawDeg, 0f);
            if (_dead) transform.rotation *= Quaternion.Euler(0f, 0f, 80f);
        }

        public void OnDead(in EvEntityDead e) => _dead = true;

        public void Release()
        {
            if (this != null) Destroy(gameObject);
        }
    }

    public sealed class UnityViewFactory : IViewFactory
    {
        public IActorView Create(string blueprintId)
        {
            PrimitiveType primitive = PrimitiveType.Capsule;
            float scale = 1f;
            if (blueprintId == "projectile") { primitive = PrimitiveType.Sphere; scale = 0.25f; }
            if (blueprintId == "aoe") { primitive = PrimitiveType.Cylinder; scale = 0.05f; }

            var gameObject = GameObject.CreatePrimitive(primitive);
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            if (blueprintId == "aoe")
                gameObject.transform.localScale = new Vector3(2.6f, 0.05f, 2.6f);
            else
                gameObject.transform.localScale = Vector3.one * scale;

            Color color = Color.white;
            if (blueprintId == "stake") color = Color.gray;
            if (blueprintId == "melee_guard" || blueprintId == "melee_ai") color = new Color(1f, 0.5f, 0.2f);
            if (blueprintId == "summon") color = Color.cyan;
            if (blueprintId == "projectile") color = Color.red;
            if (blueprintId == "aoe") color = new Color(1f, 0.4f, 0.1f, 0.35f);

            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    renderer.material = new Material(shader);
                    renderer.material.color = color;
                }
            }

            return gameObject.AddComponent<UnityActorView>();
        }
    }
}
