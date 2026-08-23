using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class CombatRunner : MonoBehaviour
    {
        [SerializeField] float _maxDt = 0.05f;
        [SerializeField] bool _spawnViews = true;

        DemoCombatSession _session;
        UnityInputAdapter _input;
        CombatDebugHUD _hud;

        public DemoCombatSession Session => _session;

        void Awake()
        {
            _session = new DemoCombatSession();
            _session.SpawnDemoActors();

            _input = gameObject.AddComponent<UnityInputAdapter>();
            _input.Bind(_session);

            _hud = gameObject.AddComponent<CombatDebugHUD>();
            _hud.Bind(_session);

            if (_spawnViews)
                ActorViewSpawner.SpawnAll(_session);

            _session.Events.Subscribe<DamageAppliedEvent>(OnDamage);
            _session.Events.Subscribe<AnimSignalIntent>(_ => { /* 可接动画 */ });
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, _maxDt);
            _input.Pump();          // 先写入缓冲
            _session.World.Tick(dt);
            ActorViewSpawner.SyncAll(_session);
        }

        void OnDamage(DamageAppliedEvent e)
        {
            if (e.Amount <= 0f) return;
            Debug.Log($"[DMG] {e.Amount:F1} -> {e.Target} hp={e.HpAfter:F1} dead={e.Died}");
        }
    }
}
