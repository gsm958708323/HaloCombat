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
        GameFlow _flow;

        void Start()
        {
            if (Database == null || Spawns == null)
            {
                Debug.LogError("ArenaBootstrap missing Database/Spawns");
                enabled = false;
                return;
            }
            var baked = Database.BakeAll();
            var table = Spawns.Bake();
            ArenaSession Create()
            {
                var w = new CombatWorld(
                    new FighterActorFactory(baked),
                    new IntentQueue(),
                    new EventBus(),
                    new CombatTime(),
                    new SeededRandom(1),
                    baked.Cues,
                    baked.Motor
                );
                baked.Install(w);
                var h = new PresentHub();
                h.SetWorld(w);
                h.Cues.SetPool(new UnityVfxPool(VfxRoot != null ? VfxRoot : transform, BuildMap()));
                var s = new ArenaSession(w, h, table);
                s.Start();
                return s;
            }
            var cam =
                Rig != null
                    ? Rig.transform
                    : (Camera.main != null ? Camera.main.transform : transform);
            _flow = new GameFlow(new UnityInputSource(Actions, cam), Create);
            _flow.StartRun();
        }

        void Update()
        {
            if (_flow == null)
                return;
            _flow.TickUpdate(Time.deltaTime);
            if (PausePanel != null && _flow.Session != null)
                PausePanel.SetActive(_flow.Session.Paused);
        }

        void LateUpdate()
        {
            if (_flow == null)
                return;
            _flow.TickLate(Time.deltaTime);
            if (_flow.Session != null)
            {
                Hud?.Refresh(_flow.Session.Hub, Time.deltaTime);
                Rig?.Apply(_flow.Session.Hub);
            }
        }

        void OnDestroy()
        {
            _flow?.ConfirmResultTitle();
            _flow?.GoTitle();
        }

        Dictionary<string, GameObject> BuildMap()
        {
            var d = new Dictionary<string, GameObject>();
            if (Views?.Entries == null)
                return d;
            for (int i = 0; i < Views.Entries.Length; i++)
            {
                var e = Views.Entries[i];
                if (!string.IsNullOrEmpty(e.BlueprintId) && e.Prefab != null)
                    d[e.BlueprintId] = e.Prefab;
            }
            return d;
        }
    }
}
