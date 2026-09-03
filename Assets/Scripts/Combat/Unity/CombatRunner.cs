using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    [DisallowMultipleComponent]
    public sealed class CombatRunner : MonoBehaviour
    {
        public enum SceneKind { V1, V2, V3, V4 }

        [SerializeField] SceneKind _scene = SceneKind.V1;
        [SerializeField] CombatDatabaseAsset _database;
        [SerializeField] bool _useSoDatabase;
        [SerializeField] float _logicHz = 50f;
        [SerializeField] bool _pauseLogic;
        [SerializeField] bool _enableCombatKeys = true;

        SimulationClock _clock;
        CombatWorld _world;
        PresentationRouter _router;
        InputAdapter _input;
        UnityHitstopOverlay _overlay;
        ViewRegistry _views;
        EntityId _player;

        public CombatWorld World => _world;
        public SceneKind Scene => _scene;

        void Awake() => BuildWorld();

        public void Configure(SceneKind scene) => _scene = scene;

        public void BuildWorld()
        {
            if (_router != null && _world != null) _router.Unbind(_world.Events);
            _views?.ReleaseAll();

            float dt = 1f / Mathf.Max(1f, _logicHz);
            _clock = new SimulationClock(dt, 4);
            ICombatContent content = _useSoDatabase && _database != null
                ? (ICombatContent)new SoCombatContent(_database)
                : new CodeCombatContent();
            var baked = content.Bake();
            var report = CombatValidator.Validate(baked);
            if (report.HasError) Debug.LogError(report.ToString(), this);

            _world = new CombatWorld(
                new FighterActorFactory(baked),
                new IntentQueue(),
                new EventBus(),
                new CombatTime(),
                new SeededRandom(1),
                baked.Cues,
                baked.Motor);
            baked.Install(_world);

            _views = new ViewRegistry(new UnityViewFactory());
            _overlay = new UnityHitstopOverlay();
            _router = new PresentationRouter(
                _world,
                _views,
                new UnityCuePlayer(_database != null ? _database.Cues : null),
                new UnityFloater(),
                _overlay,
                null);
            _router.Bind(_world.Events);
            _input = new InputAdapter { EnableCombatKeys = _scene != SceneKind.V1 && _enableCombatKeys };
            SetupScene();
        }

        void SetupScene()
        {
            _player = _world.SpawnActor(new ActorSpawnSpec("fighter"));
            if (_world.TryGetActor(_player, out var player))
            {
                player.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
                player.GetComp<TransformComp>().YawDegrees = 0f;
            }

            if (_scene == SceneKind.V1) return;
            SpawnStake(new SimVec3(0.55f, 0f, 0f));
            if (_scene == SceneKind.V3)
                SpawnStake(new SimVec3(3f, 0f, 2.2f));
            if (_scene == SceneKind.V4)
            {
                var guardId = _world.SpawnActor(new ActorSpawnSpec("melee_guard"));
                if (_world.TryGetActor(guardId, out var guard))
                {
                    guard.GetComp<TransformComp>().Position = new SimVec3(2.2f, 0f, 0f);
                    guard.GetComp<BehaviorTreeComp>().Board.Home = new SimVec3(2.2f, 0f, 0f);
                    guard.GetComp<BehaviorTreeComp>().Board.LeashRange = 8f;
                }
                SpawnStake(new SimVec3(1.3f, 0f, 0f));
            }
        }

        void SpawnStake(SimVec3 position)
        {
            var id = _world.SpawnActor(new ActorSpawnSpec("stake"));
            if (_world.TryGetActor(id, out var stake))
                stake.GetComp<TransformComp>().Position = position;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) { BuildWorld(); return; }
            if (Input.GetKeyDown(KeyCode.F2)) _pauseLogic = !_pauseLogic;
            DebugKeys();
            if (_pauseLogic) return;

            _input.SampleUnity();
            int steps = _clock.BeginFrame(Time.unscaledDeltaTime);
            _world.TryGetActor(_player, out var player);
            for (int i = 0; i < steps; i++)
            {
                _input.PumpLogic(player);
                _world.Tick(_clock.LogicDt);
            }
            _overlay.TickWall(Time.unscaledDeltaTime);
        }

        void DebugKeys()
        {
            if (!_world.TryGetActor(_player, out var player) || !player.IsActive) return;
            if (Input.GetKeyDown(KeyCode.H))
                _world.Deliver(new IEffect[] { new DamageEffect { Coeff = 1f, CanCrit = false, HitstopFrames = 3 } }, player, player, 50f);

            if (Input.GetKeyDown(KeyCode.N) && _scene == SceneKind.V4)
            {
                var actors = _world.RegistryActive();
                for (int i = 0; i < actors.Count; i++)
                {
                    if (!actors[i].TryGetComp<BehaviorTreeComp>(out _)) continue;
                    _world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.55f } }, player, actors[i], 0f);
                    break;
                }
            }

            if (Input.GetKeyDown(KeyCode.L) && _scene == SceneKind.V4)
                _world.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, player, null, player.GetComp<AttributeSet>().GetFinal(AttrId.Atk));
            if (Input.GetKeyDown(KeyCode.U) && _scene == SceneKind.V3)
                _world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, player, null, 0f, player.GetComp<TransformComp>().Position);
            if (Input.GetKeyDown(KeyCode.I) && _scene == SceneKind.V3)
                _world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, player, null, player.GetComp<AttributeSet>().GetFinal(AttrId.Atk));
            if (Input.GetKeyDown(KeyCode.Backspace) && _scene == SceneKind.V4)
                player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "debug-kill" });
        }

        void LateUpdate()
        {
            if (_world == null) return;
            float alpha = _pauseLogic || _world.InHitstop ? 1f : _clock.Alpha;
            _router.SampleLate(alpha);
            ScaleAoes();
        }

        void ScaleAoes()
        {
            var actors = _world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].TryGetComp<AoeComp>(out var aoe) || aoe.Def == null) continue;
                var gameObject = GameObject.Find("aoe_" + actors[i].Id.Index);
                if (gameObject != null)
                    gameObject.transform.localScale = new Vector3(2f * aoe.Def.Radius, 0.05f, 2f * aoe.Def.Radius);
            }
        }

        void OnGUI()
        {
            if (_world == null) return;
            _overlay.OnGUI();
            string text = "F" + _world.Time.Frame + " pause=" + _pauseLogic + " hitstop=" + _world.InHitstop + " scene=" + _scene;
            if (_world.TryGetActor(_player, out var player) && player.IsActive)
            {
                var tags = player.GetComp<TagComp>();
                var state = player.GetComp<StateMachineComp>();
                var transform = player.GetComp<TransformComp>();
                text += "\nact=" + state.Current + " gnd=" + tags.Has(CommonTags.Grounded) +
                        " cancel=" + tags.Has(CommonTags.Cancel) + " iframe=" + tags.Has(CommonTags.Invincible) +
                        " pos=" + transform.Position.X.ToString("F2") + "," + transform.Position.Y.ToString("F2");
            }
            text += "\nWASD Space | J atk K dodge | F1 rebuild F2 pause";
            if (_scene == SceneKind.V3) text += " | U aura I homing";
            if (_scene == SceneKind.V4) text += " | L summon N knockdown Backspace die";
            GUI.Label(new Rect(10, 10, 900, 140), text);
        }

        void OnDestroy()
        {
            if (_router != null && _world != null) _router.Unbind(_world.Events);
            _views?.ReleaseAll();
        }
    }
}
