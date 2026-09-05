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
        CombatLesson _lesson;
        bool _lessonAuto;
        float _lessonAccumulator;
        float _lessonSpeed = 0.55f;
        bool _showDebug;

        public CombatWorld World => _world;
        public SceneKind Scene => _scene;
        public CombatDatabaseAsset Database => _database;
        public bool IsUsingSoDatabase => _useSoDatabase && _database != null;
        public EntityId PlayerId => _player;
        public CombatLesson Lesson => _lesson;
        public bool LessonAuto => _lessonAuto;
        public float LessonSpeed => _lessonSpeed;

        void Awake()
        {
            BuildWorld();
            EnsureLessonPresentation();
        }

        public void Configure(SceneKind scene) => _scene = scene;
        public void Configure(SceneKind scene, CombatDatabaseAsset database, bool useSoDatabase = true)
        {
            _scene = scene;
            _database = database;
            _useSoDatabase = useSoDatabase;
        }

        public bool TryGetPlayer(out Actor player)
        {
            player = null;
            return _world != null && _world.TryGetActor(_player, out player) && player != null;
        }

        public void QueueInput(InputToken token) => _input?.Queue(token);

        /// <summary>
        /// Injects an input token directly into the player's logical input
        /// buffer. This is intended for deterministic tests and tooling; the
        /// normal keyboard/gamepad path still goes through InputAdapter.Update.
        /// </summary>
        public void InjectInput(InputToken token)
        {
            if (!TryGetPlayer(out var player) ||
                !player.TryGetComp<InputBufferComp>(out var buffer)) return;
            buffer.Push(token);
        }

        /// <summary>
        /// Advances the pure-C# simulation by a deterministic number of logic
        /// steps. This is a test/tooling seam; the normal player loop remains
        /// driven by Update and the SimulationClock.
        /// </summary>
        public void StepLogicForTests(int steps = 1)
        {
            if (_world == null) return;
            if (steps < 0) throw new System.ArgumentOutOfRangeException(nameof(steps));
            float dt = 1f / Mathf.Max(1f, _logicHz);
            for (int i = 0; i < steps; i++)
            {
                _world.TryGetActor(_player, out var player);
                _input?.PumpLogic(player);
                _world.Tick(dt);
            }
        }

        public void DebugSpawnHoming()
        {
            if (!TryGetPlayer(out var player) || _scene != SceneKind.V3) return;
            _world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, player, null,
                player.GetComp<AttributeSet>().GetFinal(AttrId.Atk));
        }

        public void DebugSpawnAura()
        {
            if (!TryGetPlayer(out var player) || _scene != SceneKind.V3) return;
            _world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, player, null, 0f,
                player.GetComp<TransformComp>().Position);
        }

        public void DebugSpawnSummon()
        {
            if (!TryGetPlayer(out var player) || _scene != SceneKind.V4) return;
            _world.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, player, null,
                player.GetComp<AttributeSet>().GetFinal(AttrId.Atk));
        }

        public void DebugKillPlayer()
        {
            if (!TryGetPlayer(out var player)) return;
            player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead,
                new ActivityEnterArgs { Reason = "test-kill" });
        }

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
            _lesson = new CombatLesson(_world, LessonKind(_scene), _player, FindLessonTarget());
            _lessonAccumulator = 0f;
        }

        void EnsureLessonPresentation()
        {
            if (GetComponent<CombatLessonDirector>() == null) gameObject.AddComponent<CombatLessonDirector>();
            if (GetComponent<CombatLessonVfx>() == null) gameObject.AddComponent<CombatLessonVfx>();
            CombatStageView.Ensure(Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>());
        }

        EntityId FindLessonTarget()
        {
            if (_scene == SceneKind.V1) return EntityId.Invalid;
            var actors = _world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (_scene == SceneKind.V4 && actor.TryGetComp<BehaviorTreeComp>(out _) &&
                    !actor.TryGetComp<SummonComp>(out _)) return actor.Id;
                if (_scene != SceneKind.V4 && actor.TryGetComp<HealthComp>(out _) &&
                    !actor.TryGetComp<InputBufferComp>(out _) && !actor.TryGetComp<BehaviorTreeComp>(out _)) return actor.Id;
            }
            return EntityId.Invalid;
        }

        static CombatLessonKind LessonKind(SceneKind scene)
        {
            switch (scene)
            {
                case SceneKind.V1: return CombatLessonKind.Motor;
                case SceneKind.V2: return CombatLessonKind.Melee;
                case SceneKind.V3: return CombatLessonKind.ProjectileAoe;
                default: return CombatLessonKind.AiSummon;
            }
        }

        public void SetLessonAuto(bool value) { _lessonAuto = value; _lessonAccumulator = 0f; }
        public void SetLessonSpeed(float value) { _lessonSpeed = Mathf.Clamp(value, 0.15f, 1.5f); }
        public void StepLesson()
        {
            if (_lesson == null || _lesson.Finished) return;
            _lesson.Tick(true);
        }
        public void ReplayLesson()
        {
            BuildWorld();
            _lessonAuto = true;
            _lessonAccumulator = 0f;
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
            // V3 keeps its homing target far enough away for the projectile to
            // be observed in flight by automated tests and in the demo.
            if (_scene != SceneKind.V3)
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
            if (Input.GetKeyDown(KeyCode.F3)) _lessonAuto = !_lessonAuto;
            if (Input.GetKeyDown(KeyCode.F4)) ReplayLesson();
            if (Input.GetKeyDown(KeyCode.F5)) _showDebug = !_showDebug;
            DebugKeys();
            if (_pauseLogic) return;

            if (_lessonAuto && _lesson != null)
            {
                _lessonAccumulator += Time.unscaledDeltaTime * _lessonSpeed;
                while (_lessonAccumulator >= CombatLesson.Delta && !_lesson.Finished)
                {
                    _lessonAccumulator -= CombatLesson.Delta;
                    _lesson.Tick(true);
                }
                _overlay.TickWall(Time.unscaledDeltaTime);
                return;
            }

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
            if (_lessonAuto) return;
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
            if (!_showDebug || _world == null) return;
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
            text += "\nWASD Space | J atk K dodge | F1 rebuild F2 pause F3 lesson F4 replay F5 debug";
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
