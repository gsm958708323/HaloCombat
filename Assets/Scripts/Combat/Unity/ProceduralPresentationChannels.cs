using UnityEngine;

namespace Combat.Unity.Presentation
{
    public sealed class HitSparkDirector : MonoBehaviour
    {
        public void Play(Vector3 position, Color color)
        {
            var fx = ProceduralVfxFactory.Create(transform, "hit_spark", position);
            var r = fx.GetComponentInChildren<Renderer>(); if (r != null) r.material.color = color;
            Destroy(fx, .35f);
        }
    }

    public sealed class ShockwaveVfx : MonoBehaviour
    {
        public float Lifetime = .45f;
        void Update() { transform.localScale += Vector3.one * (Time.unscaledDeltaTime * 2.4f); }
        public static ShockwaveVfx Spawn(Transform root, Vector3 position, Color color)
        {
            var go = ProceduralVfxFactory.Create(root, "shockwave", position);
            var v = go.AddComponent<ShockwaveVfx>(); var r = go.GetComponentInChildren<Renderer>(); if (r != null) r.material.color = color; Destroy(go, v.Lifetime); return v;
        }
    }

    public sealed class DeathBurstVfx : MonoBehaviour
    {
        public static void Spawn(Transform root, Vector3 position, Color color)
        {
            var go = ProceduralVfxFactory.Create(root, "death_burst", position);
            var ps = go.AddComponent<ParticleSystem>(); var main = ps.main; main.startColor = color; main.startLifetime = .5f; main.startSpeed = 3f; main.maxParticles = 24;
            var emission = ps.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 18) });
            Object.Destroy(go, .8f);
        }
    }

    public sealed class ProceduralTrailVfx : MonoBehaviour
    {
        public Transform Follow;
        Vector3 _last;
        void LateUpdate() { if (Follow == null) return; transform.position = Follow.position; if ((_last - transform.position).sqrMagnitude > .02f) { transform.localScale = new Vector3(1.1f, 1f, 1.1f); _last = transform.position; } }
    }

    public sealed class ProceduralBuffVfx : MonoBehaviour
    {
        public Color Color = new Color(.2f, .8f, 1f);
        void Update() { transform.Rotate(0f, 80f * Time.unscaledDeltaTime, 0f, Space.Self); }
    }
}
