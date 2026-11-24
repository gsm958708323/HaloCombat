using System;
using System.Collections.Generic;
using Combat.Config;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Combat.Unity
{
    // Transient effects consume only new events from the current simulation generation.
    internal sealed class SkillLabFeedback : IDisposable
    {
        const int RingCapacity = 12;
        const int RingPoints = 32;
        const float Window = 5f;
        const int EventHistoryLimit = 500;
        static readonly Color Teal = new Color(.25f, 1f, .86f);
        static readonly Color Gold = new Color(1f, .73f, .38f);
        static readonly Color Orange = new Color(1f, .47f, .25f);

        readonly GameObject _root;
        readonly ParticleSystem _particles;
        readonly Material _particleMaterial;
        readonly Material _ringMaterial;
        readonly AudioSource _audio;
        readonly AudioClip _cast, _hit, _combo, _blast;
        readonly float[] _soundAfter = new float[4];
        readonly Ring[] _rings = new Ring[RingCapacity];
        readonly Dictionary<string, float> _flashUntil = new Dictionary<string, float>();
        readonly List<string> _expiredActors = new List<string>();
        readonly Queue<DamagePulse> _damage = new Queue<DamagePulse>();
        readonly SkillLabDamageNumbers _numbers;
        int _nextRing, _sequence, _particlesLeft;
        float _windowDamage, _incompleteUntil = float.NegativeInfinity;
        bool _silent;

        public float DamagePerSecond => Mathf.Max(0, _windowDamage) / Window;
        public bool DamageWindowIncomplete { get; private set; }
        public bool Muted
        {
            get => _audio.mute;
            set
            {
                if (value) _audio.Stop();
                _audio.mute = value;
            }
        }

        public SkillLabFeedback(Transform parent, Material ringMaterial, SkillLabDamageNumbers numbers)
        {
            _numbers = numbers;
            _ringMaterial = new Material(Shader.Find("Sprites/Default") ?? ringMaterial.shader) { name = "SkillLab pulse overlay" };
            _ringMaterial.SetOverrideTag("RenderType", "Transparent");
            if (_ringMaterial.HasProperty("_ZWrite")) _ringMaterial.SetFloat("_ZWrite", 0);
            _ringMaterial.renderQueue = (int)RenderQueue.Transparent;
            _root = new GameObject("SkillLab Combat Feedback");
            _root.transform.SetParent(parent, false);
            _audio = _root.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0;
            _cast = Resources.Load<AudioClip>("SkillLab/laserSmall_001");
            _hit = Resources.Load<AudioClip>("SkillLab/impactWood_medium_001");
            _combo = Resources.Load<AudioClip>("SkillLab/confirmation_001");
            _blast = Resources.Load<AudioClip>("SkillLab/explosionCrunch_001");

            var particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) return;
            _particleMaterial = new Material(particleShader) { name = "SkillLab Kenney spark", renderQueue = (int)RenderQueue.Transparent };
            _particleMaterial.SetTexture("_BaseMap", Resources.Load<Texture2D>("SkillLab/spark_01"));
            _particleMaterial.SetFloat("_Surface", 1);
            _particleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _particleMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
            _particleMaterial.SetFloat("_ZWrite", 0);
            _particleMaterial.SetOverrideTag("RenderType", "Transparent");
            _particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _particles = _root.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.gravityModifier = 1.15f;
            main.useUnscaledTime = true;
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;
            var lifetime = _particles.colorOverLifetime;
            lifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .20f), new GradientAlphaKey(0, 1) });
            lifetime.color = new ParticleSystem.MinMaxGradient(fade);
            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _particleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _particles.Play();
        }

        public void Reset()
        {
            _sequence = 0;
            _damage.Clear();
            _windowDamage = 0;
            _incompleteUntil = float.NegativeInfinity;
            DamageWindowIncomplete = false;
            ClearTransient();
        }

        public void Update(SkillSimulationSnapshot snapshot, bool pausedOrReplay, bool damageNumbers)
        {
            var now = Time.unscaledTime;
            if (pausedOrReplay && !_silent) ClearTransient();
            _silent = pausedOrReplay;
            _numbers.Tick(damageNumbers && !_silent, now);
            AgeRings(now);
            _particlesLeft = 80;
            _expiredActors.Clear();
            foreach (var flash in _flashUntil)
                if (flash.Value <= now) _expiredActors.Add(flash.Key);
            foreach (var actor in _expiredActors) _flashUntil.Remove(actor);

            // Snapshots retain a bounded event tail. A detached replay or large step can skip rows.
            // Mark the window as a lower bound until the last known gap is outside it.
            foreach (var entry in snapshot.Events)
            {
                if (entry.Sequence <= _sequence) continue;
                if (entry.Sequence > _sequence + 1 && (_sequence != 0 || snapshot.Events.Length >= EventHistoryLimit))
                    _incompleteUntil = entry.Time + Window;
                _sequence = entry.Sequence;
                TrackDamage(entry);
                if (!_silent && snapshot.Time - entry.Time <= .25f) Play(snapshot, entry, now, damageNumbers);
            }
            while (_damage.Count > 0 && _damage.Peek().Time <= snapshot.Time - Window)
                _windowDamage -= _damage.Dequeue().Amount;
            if (_damage.Count == 0) _windowDamage = 0;
            DamageWindowIncomplete = snapshot.Time < _incompleteUntil;
        }

        internal static bool IsDamage(SkillEventSnapshot entry) => entry.Kind == "damage" || entry.Kind == "critical";

        internal static float DamageAmount(SkillEventSnapshot entry) =>
            entry.Damage == null ? 0 : entry.Damage.HpLost + entry.Damage.Absorbed;

        void TrackDamage(SkillEventSnapshot entry)
        {
            if (!IsDamage(entry)) return;
            float amount = DamageAmount(entry);
            if (amount <= 0) return;
            _damage.Enqueue(new DamagePulse(entry.Time, amount));
            _windowDamage += amount;
        }

        public float Flash(string actor) => _flashUntil.TryGetValue(actor, out var end)
            ? Mathf.Clamp01((end - Time.unscaledTime) / .18f) : 0;

        void Play(SkillSimulationSnapshot snapshot, SkillEventSnapshot entry, float now, bool damageNumbers)
        {
            if (IsDamage(entry) && DamageAmount(entry) > 0 && entry.TargetX.HasValue && entry.TargetZ.HasValue)
            {
                if (damageNumbers) _numbers.Add(entry, now);
                if (entry.TargetId != null) _flashUntil[entry.TargetId] = now + .18f;
                var ground = new Vector3(entry.TargetX.Value, .035f, entry.TargetZ.Value);
                bool isCritical = entry.Kind == "critical";
                var color = isCritical ? Gold : Teal;
                Pulse(ground, color, .28f, isCritical ? .91f : .56f, .23f, now);
                Burst(ground + Vector3.up * .94f, color, isCritical ? 12 : 7, 3.3f, .19f);
                if (entry.PeriodicBuff == null) Sound(_hit, 1, now, isCritical ? .50f : .34f, .085f);
                return;
            }

            if (entry.Kind == "cast" || entry.Kind == "operation")
            {
                if (TryActorPosition(snapshot, entry.SourceId, out var source))
                {
                    Pulse(source, Teal, .18f, .75f, .24f, now);
                    Burst(source + Vector3.up * 1.04f, Teal, 10, 2.45f, .23f);
                }
                Sound(_cast, 0, now, .32f, .10f);
            }
            else if (entry.Kind == "combo-confirmation" && entry.Result == "Granted")
            {
                if (TryActorPosition(snapshot, entry.SourceId ?? FindCaster(snapshot, entry), out var source))
                    Pulse(source, Gold, .39f, .98f, .34f, now);
                Sound(_combo, 2, now, .38f, .22f);
            }
            else if (entry.Kind == "area")
            {
                foreach (var area in snapshot.Areas)
                {
                    if (area.Sequence != entry.Sequence || area.Targets == 0) continue;
                    var at = new Vector3(area.X, .035f, area.Z);
                    float radius = Mathf.Max(.25f, area.Radius);
                    Pulse(at, Orange, radius * .18f, radius, .34f, now);
                    Burst(at + Vector3.up * .24f, Orange, 18, Mathf.Clamp(radius * 2, 3, 8), .30f);
                    Sound(_blast, 3, now, .46f, .48f);
                    break;
                }
            }
            else if (entry.Kind == "collision" && entry.Result.StartsWith("projectile-bounce ->", StringComparison.Ordinal) &&
                TryActorPosition(snapshot, entry.BodyId, out var projectile))
                Burst(projectile, Gold, 6, 2.3f, .18f);
        }

        static string FindCaster(SkillSimulationSnapshot snapshot, SkillEventSnapshot entry)
        {
            foreach (var actor in snapshot.Actors)
                if (actor.Kind == "caster" && actor.Name == entry.Source) return actor.Id;
            return null;
        }

        static bool TryActorPosition(SkillSimulationSnapshot snapshot, string id, out Vector3 point)
        {
            if (id != null)
                foreach (var actor in snapshot.Actors)
                    if (actor.Id == id)
                    {
                        point = new Vector3(actor.X, actor.Y + .035f, actor.Z);
                        return true;
                    }
            point = default;
            return false;
        }

        void Sound(AudioClip clip, int channel, float now, float volume, float interval)
        {
            if (Muted || clip == null || now < _soundAfter[channel]) return;
            _soundAfter[channel] = now + interval;
            _audio.PlayOneShot(clip, volume);
        }

        void Burst(Vector3 origin, Color color, int count, float speed, float size)
        {
            if (_particles == null || _particlesLeft == 0) return;
            count = Mathf.Min(count, _particlesLeft);
            _particlesLeft -= count;
            for (int i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2 / count;
                var velocity = new Vector3(Mathf.Cos(angle), i % 2 == 0 ? .85f : .36f, Mathf.Sin(angle)) * speed;
                _particles.Emit(new ParticleSystem.EmitParams
                {
                    position = origin,
                    velocity = velocity,
                    startColor = color,
                    startSize = size * (i % 3 == 0 ? 1.5f : 1),
                    startLifetime = .22f + (i % 4) * .047f,
                    rotation = i * 49f
                }, 1);
            }
        }

        void Pulse(Vector3 at, Color color, float from, float to, float duration, float now)
        {
            ref var ring = ref _rings[_nextRing];
            _nextRing = (_nextRing + 1) % _rings.Length;
            if (ring.Line == null)
            {
                var visual = new GameObject("Combat pulse");
                visual.transform.SetParent(_root.transform, false);
                ring.Line = visual.AddComponent<LineRenderer>();
                ring.Line.sharedMaterial = _ringMaterial;
                ring.Line.loop = true;
                ring.Line.useWorldSpace = false;
                ring.Line.positionCount = RingPoints;
                ring.Line.shadowCastingMode = ShadowCastingMode.Off;
                for (int i = 0; i < RingPoints; i++)
                {
                    float angle = 2 * Mathf.PI * i / RingPoints;
                    ring.Line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)));
                }
            }
            ring.Line.transform.position = at;
            ring.Color = color;
            ring.Started = now;
            ring.Duration = duration;
            ring.From = from;
            ring.To = to;
            DrawRing(ring, 0);
            ring.Line.enabled = true;
        }

        void AgeRings(float now)
        {
            foreach (var ring in _rings)
            {
                if (ring.Line == null || !ring.Line.enabled) continue;
                float fraction = (now - ring.Started) / ring.Duration;
                if (fraction >= 1) ring.Line.enabled = false;
                else DrawRing(ring, fraction);
            }
        }

        static void DrawRing(Ring ring, float fraction)
        {
            float radius = Mathf.Lerp(ring.From, ring.To, 1 - (1 - fraction) * (1 - fraction));
            ring.Line.transform.localScale = new Vector3(radius, 1, radius);
            ring.Line.widthMultiplier = Mathf.Lerp(.105f, .007f, fraction);
            // Line widths taper with time, so transparent vertex colors are unnecessary.
            ring.Line.startColor = ring.Line.endColor = ring.Color;
        }

        void ClearTransient()
        {
            _numbers.Clear();
            _flashUntil.Clear();
            if (_audio != null) _audio.Stop();
            Array.Clear(_soundAfter, 0, _soundAfter.Length);
            if (_particles != null) _particles.Clear();
            foreach (var ring in _rings)
                if (ring.Line != null) ring.Line.enabled = false;
        }

        public void Dispose()
        {
            ClearTransient();
            Object.Destroy(_root);
            if (_particleMaterial != null) Object.Destroy(_particleMaterial);
            Object.Destroy(_ringMaterial);
        }

        struct Ring
        {
            public LineRenderer Line;
            public Color Color;
            public float Started, Duration, From, To;
        }

        readonly struct DamagePulse
        {
            public readonly float Time, Amount;
            public DamagePulse(float time, float amount) { Time = time; Amount = amount; }
        }
    }
}
