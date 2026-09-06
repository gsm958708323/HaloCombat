using System;
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
        public ProceduralArenaVisuals ArenaVisuals;
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
            ValidateReferences();
            _baked = Database.BakeAll();
            _table = Spawns.Bake();
            if (_table.Entries == null || _table.Entries.Length == 0)
                throw new InvalidOperationException("ArenaBootstrap requires a non-empty ArenaSpawnTableSO.");
            var playerOptions = _baked.Characters.PlayerOptions();
            if (playerOptions.Count == 0)
                throw new InvalidOperationException("CombatDatabase has no selectable player characters.");
            var playerIds = new string[playerOptions.Count];
            for (int i = 0; i < playerOptions.Count; i++) playerIds[i] = playerOptions[i].BlueprintId;

            ArenaSession Create(string playerBlueprintId)
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
                var s = new ArenaSession(w, h, _table, playerBlueprintId);
                s.Start();
                return s;
            }
            EnsurePresentationRoots();
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
            if (UnityEngine.Object.FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("ArenaKeyLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            _flow = new GameFlow(
                new UnityInputSource(Actions, cam),
                Create,
                store,
                playerIds,
                "swordsman");
            PauseMenu?.Bind(_flow);
            return _flow;
        }

        void ValidateReferences()
        {
            var missing = new List<string>();
            if (Database == null) missing.Add(nameof(Database));
            if (Spawns == null) missing.Add(nameof(Spawns));
            if (Actions == null) missing.Add(nameof(Actions));
            if (Views == null) missing.Add(nameof(Views));
            if (missing.Count == 0) return;
            var message = "ArenaBootstrap missing required configuration: " + string.Join(", ", missing);
            Debug.LogError(message, this);
            throw new InvalidOperationException(message);
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

        void EnsurePresentationRoots()
        {
            if (PresentRoot == null)
            {
                var go = new GameObject("PresentRoot"); go.transform.SetParent(transform, false); PresentRoot = go.transform;
            }
            if (VfxRoot == null)
            {
                var go = new GameObject("VfxRoot"); go.transform.SetParent(transform, false); VfxRoot = go.transform;
            }
            if (FloaterRoot == null)
            {
                var go = new GameObject("FloaterRoot"); go.transform.SetParent(transform, false); FloaterRoot = go.transform;
            }
            if (Hud == null)
            {
                var go = new GameObject("CombatHud"); go.transform.SetParent(transform, false); Hud = go.AddComponent<HudView>();
            }
            if (ArenaVisuals == null)
            {
                ArenaVisuals = GetComponent<ProceduralArenaVisuals>();
                if (ArenaVisuals == null) ArenaVisuals = gameObject.AddComponent<ProceduralArenaVisuals>();
            }
            ArenaVisuals.Build();
            if (Rig == null && Camera.main == null)
            {
                var cameraObject = new GameObject("ArenaCamera");
                var camera = cameraObject.AddComponent<Camera>(); camera.tag = "MainCamera";
                Rig = cameraObject.AddComponent<CameraRig>(); Rig.Cam = camera; Rig.Distance = 8f; Rig.Height = 6f; Rig.BaseFov = 52f;
            }
        }
    }
}
