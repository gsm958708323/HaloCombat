using System;
using System.Collections.Generic;
using Combat.Config;
using Combat.Core;
using Combat.Unity.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    public sealed class BuffArenaBootstrap : MonoBehaviour
    {
        public ViewPrefabTable Views;
        public InputActionAsset Actions;
        public BuffArenaDatabaseAsset Database;
        public BuffArenaMapVisuals MapVisuals;
        public CameraRig Rig;
        public Transform PresentRoot;
        public Transform VfxRoot;
        public Transform FloaterRoot;
        public BuffArenaHudView Hud;

        public GameObject MuzzleFlashPrefab;
        public GameObject HeartPrefab;
        public GameObject RollFirePrefab;
        public GameObject HitPrefab;
        public GameObject ShieldPrefab;
        public GameObject ExplosionPrefab;
        public GameObject StarPrefab;
        public GameObject ShockwavePrefab;

        BuffArenaData _data;
        BuffArenaSession _session;
        BuffArenaInputSource _input;

        public BuffArenaSession Session => _session;

        void Start()
        {
            ValidateReferences();
            EnsureRoots();
            _data = BuffArenaContent.Build(Database.Bake());
            var navigation = MapVisuals.Build();
            var hub = new PresentHub(new UnityPresentFactory(Views, null, PresentRoot, VfxRoot));
            hub.SetWorld(null);
            hub.Cues.SetPool(new UnityVfxPool(VfxRoot, BuildCueMap(), hub.ResolveAnchorPosition));
            hub.Floaters.SetPool(new UnityFloaterPool(FloaterRoot, Camera.main));
            _session = new BuffArenaSession(_data, hub, navigation);
            _input = new BuffArenaInputSource(Actions, Camera.main);
            _session.Start();
        }

        void Update()
        {
            if (_session == null || _input == null) return;
            Vector3 origin = Vector3.zero;
            if (_session.World != null && _session.World.TryGetActor(_session.LocalPlayerId, out var player) &&
                player != null && player.TryGetComp<TransformComp>(out var tf))
                origin = new Vector3(tf.Position.X, tf.Position.Y, tf.Position.Z);
            _session.ApplyInput(_input.Sample(origin));
            _session.PumpLogic(Time.deltaTime);
            Hud?.Refresh(_session.Hub, Time.deltaTime);
        }

        void LateUpdate()
        {
            _session?.PumpUnscaled(Time.deltaTime);
            _session?.PumpPresent(Time.deltaTime);
            Hud?.Refresh(_session != null ? _session.Hub : null, Time.deltaTime);
            Rig?.Apply(_session != null ? _session.Hub : null);
        }

        void OnDestroy()
        {
            _session?.Dispose();
            _session = null;
        }

        void ValidateReferences()
        {
            var missing = new List<string>();
            if (Views == null) missing.Add(nameof(Views));
            if (Actions == null) missing.Add(nameof(Actions));
            if (Database == null) missing.Add(nameof(Database));
            if (MapVisuals == null) missing.Add(nameof(MapVisuals));
            if (missing.Count > 0)
                throw new InvalidOperationException("Buff Arena scene is missing: " + string.Join(", ", missing));
        }

        void EnsureRoots()
        {
            if (PresentRoot == null) PresentRoot = CreateRoot("PresentRoot");
            if (VfxRoot == null) VfxRoot = CreateRoot("VfxRoot");
            if (FloaterRoot == null) FloaterRoot = CreateRoot("FloaterRoot");
            if (Rig == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                var camera = go.AddComponent<Camera>();
                Rig = go.AddComponent<CameraRig>();
                Rig.Cam = camera;
                Rig.Distance = 2.5f;
                Rig.Height = 2.5f;
                Rig.BaseFov = 60f;
            }
            if (Hud == null)
            {
                var go = new GameObject("BuffArenaHud");
                go.transform.SetParent(transform, false);
                Hud = go.AddComponent<BuffArenaHudView>();
            }
        }

        Transform CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        Dictionary<string, GameObject> BuildCueMap()
        {
            return new Dictionary<string, GameObject>
            {
                ["fx_muzzle"] = MuzzleFlashPrefab,
                ["fx_heart"] = HeartPrefab,
                ["fx_roll_fire"] = RollFirePrefab,
                ["fx_hit"] = HitPrefab,
                ["fx_shield"] = ShieldPrefab,
                ["fx_explosion"] = ExplosionPrefab,
                ["fx_star"] = StarPrefab,
                ["fx_shockwave"] = ShockwavePrefab
            };
        }
    }
}
