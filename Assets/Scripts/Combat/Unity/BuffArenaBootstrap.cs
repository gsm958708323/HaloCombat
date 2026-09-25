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

        BuffArenaData _data;
        BuffArenaSession _session;
        BuffArenaInputSource _input;

        public BuffArenaSession Session => _session;

        void Start()
        {
            ValidateReferences();
            EnsureRoots();
            _data = Database.Bake();
            if (_data == null)
            {
                throw new InvalidOperationException(
                    "Buff Arena content is unusable: " + Database.LastContentError
                    + ". Author the assets under Assets/Combat/Config/Generated; there is no code fallback.");
            }
            var navigation = MapVisuals.Build();
            var hub = new PresentHub(new UnityPresentFactory(Views, null, PresentRoot, VfxRoot));
            hub.SetWorld(null);
            var cuePrefabs = BuildCueMap();
            if (cuePrefabs.Count == 0)
                throw new InvalidOperationException(
                    "Buff Arena has no visual cue prefab bound. Assign a Prefab and tick Visual Enabled on the "
                    + "cue entries of " + (Database.Cues != null ? Database.Cues.name : "(no cue asset assigned)")
                    + ".");
            hub.Cues.SetPool(new UnityVfxPool(VfxRoot, cuePrefabs, hub.ResolveAnchorPosition));
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
                Rig.Distance = Database.CameraDistance;
                Rig.Height = Database.CameraHeight;
                Rig.BaseFov = Database.CameraBaseFov;
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

        /// <summary>
        /// Cue prefab bindings come from the database's cue asset, so the key list has a single
        /// source: rebuilding the cue definitions cannot drift away from the prefab bindings.
        /// </summary>
        Dictionary<string, GameObject> BuildCueMap()
        {
            var map = new Dictionary<string, GameObject>();
            var cues = Database != null ? Database.Cues : null;
            if (cues == null || cues.Entries == null) return map;
            for (int i = 0; i < cues.Entries.Length; i++)
            {
                var entry = cues.Entries[i];
                if (!entry.VisualEnabled || entry.Prefab == null) continue;
                if (string.IsNullOrEmpty(entry.PrefabKey)) continue;
                map[entry.PrefabKey] = entry.Prefab;
            }
            return map;
        }
    }
}
