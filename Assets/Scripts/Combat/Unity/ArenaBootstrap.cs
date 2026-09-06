using System.Collections.Generic;
using Combat.Config;
using Combat.Core;
using Combat.Game;
using Combat.Presentation;
using Combat.Unity.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    public sealed class ArenaBootstrap : MonoBehaviour
    {
        public CombatDatabaseAsset Database;
        public ArenaSpawnTableSO Spawns;
        public ViewPrefabTable Views;
        public InputActionAsset Actions;
        public HudView Hud;
        public CameraRig Rig;
        public Transform VfxRoot;
        public GameObject PausePanel;
        public PauseMenuView PauseMenu;
        public bool AutoStart = true;
        public bool DriveLifecycle = true;
        public Transform PresentRoot;
        public CuePrefabTable CuePrefabs;
        public Transform FloaterRoot;
        GameFlow _flow;
        BakedCombatData _baked;
        SpawnTable _table;

        public GameFlow Flow => _flow;

        void Start()
        {
            if (!AutoStart)
                return;
            CreateFlow().StartRun();
        }

        public GameFlow CreateFlow(ISettingsStore store = null)
        {
            if (_flow != null)
                return _flow;
            if (Database == null || Spawns == null)
            {
                Debug.LogWarning("ArenaBootstrap missing Database/Spawns; using headless defaults");
            }
            _baked = Database != null ? Database.BakeAll() : new CodeCombatContent().Bake();
            _table = Spawns != null ? Spawns.Bake() : ArenaSession.DefaultArenaSpawns();
            ArenaSession Create()
            {
                var w = new CombatWorld(
                    new FighterActorFactory(_baked),
                    new IntentQueue(),
                    new EventBus(),
                    new CombatTime(),
                    new SeededRandom(1),
                    _baked.Cues,
                    _baked.Motor
                );
                _baked.Install(w);
                var root = PresentRoot != null ? PresentRoot : transform;
                var h = new PresentHub(new UnityPresentFactory(Views, root, VfxRoot));
                h.SetWorld(w);
                h.Cues.SetPool(new UnityVfxPool(VfxRoot != null ? VfxRoot : root, BuildCueMap()));
                h.Floaters.SetPool(
                    new UnityFloaterPool(FloaterRoot != null ? FloaterRoot : root, Camera.main)
                );
                var s = new ArenaSession(w, h, _table);
                s.Start();
                return s;
            }
            var cam =
                Rig != null
                    ? Rig.transform
                    : (Camera.main != null ? Camera.main.transform : transform);
            if (Camera.main == null)
            {
                var cameraObject = new GameObject("ArenaCamera");
                cameraObject.transform.position = new Vector3(0f, 4f, -7f);
                var camera = cameraObject.AddComponent<Camera>();
                Rig = cameraObject.AddComponent<CameraRig>();
                Rig.Cam = camera;
                cam = cameraObject.transform;
            }
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("ArenaKeyLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            _flow = new GameFlow(new UnityInputSource(Actions, cam), Create, store);
            PauseMenu?.Bind(_flow);
            return _flow;
        }

        void Update()
        {
            if (!DriveLifecycle)
                return;
            if (_flow == null)
                return;
            _flow.TickUpdate(Time.deltaTime);
            if (PausePanel != null && _flow.Session != null)
                PausePanel.SetActive(_flow.Session.Paused);
        }

        void LateUpdate()
        {
            if (!DriveLifecycle)
                return;
            TickPresentation(Time.deltaTime);
        }

        public void SetExternalDriver(bool external)
        {
            DriveLifecycle = !external;
        }

        public void TickExternalLate(float dt)
        {
            if (DriveLifecycle || _flow == null)
                return;
            TickPresentation(dt);
        }

        void TickPresentation(float dt)
        {
            if (_flow == null)
                return;
            _flow.TickLate(dt);
            if (_flow.Session != null)
            {
                if (PausePanel != null)
                    PausePanel.SetActive(_flow.Session.Paused);
                Hud?.Refresh(_flow.Session.Hub, dt);
                Rig?.SetScreenShake(_flow.Settings.ScreenShake);
                Rig?.Apply(_flow.Session.Hub);
            }
        }

        void OnDestroy()
        {
            _flow?.ConfirmResultTitle();
            _flow?.GoTitle();
        }

        Dictionary<string, GameObject> BuildCueMap()
        {
            var d = new Dictionary<string, GameObject>();
            if (CuePrefabs?.Entries == null)
                return d;
            for (int i = 0; i < CuePrefabs.Entries.Length; i++)
            {
                var e = CuePrefabs.Entries[i];
                if (!string.IsNullOrEmpty(e.Key) && e.Prefab != null)
                    d[e.Key] = e.Prefab;
            }
            return d;
        }
    }
}
