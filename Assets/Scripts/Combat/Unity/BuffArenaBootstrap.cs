using System;
using System.Collections.Generic;
using Combat.Config;
using Combat.Core;
using Combat.Unity.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.Unity.Game
{
    /// <summary>
    /// Arena 场景的唯一入口，启动顺序是硬契约：
    /// ValidateReferences（场景槽位）→ EnsureRoots（缺的根节点 / 相机 / HUD 现建）
    /// → Database.Bake()（内容不可用立即抛，没有代码回退）→ MapVisuals.Build()（导航与地面）
    /// → PresentHub 及 Cue / Floater 池 → BuffArenaSession → BuffArenaInputSource → session.Start()。
    /// 顺序不能换：Hub 与 Cue 池必须先于 Session.Start()，因为首次生成当场就会用到这些池子。
    /// </summary>
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

        /// <summary>
        /// 装配一局。任一必需品缺失都抛 InvalidOperationException 而不是降级运行：
        /// Arena 的验收依赖“内容坏了就响亮地失败”，静默降级会把配错的内容伪装成正常游戏。
        /// </summary>
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

        /// <summary>
        /// 逻辑帧：以玩家当前世界位置为瞄准射线起点采样输入，再交给 Session 按固定步长推进逻辑；
        /// HUD 在逻辑之后刷新，所以读到的是本帧推进后的状态。
        /// </summary>
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

        /// <summary>
        /// 表现帧：先 PumpUnscaled（wall time 清尸，见 BuffArenaSession）再 PumpPresent 插值，
        /// 最后刷 HUD、让相机跟随。玩家死后逻辑停摆，这一路仍要走，否则画面会冻住。
        /// </summary>
        void LateUpdate()
        {
            _session?.PumpUnscaled(Time.deltaTime);
            _session?.PumpPresent(Time.deltaTime);
            Hud?.Refresh(_session != null ? _session.Hub : null, Time.deltaTime);
            Rig?.Apply(_session != null ? _session.Hub : null);
        }

        /// <summary>Dispose 会退订事件、释放表现池并 Shutdown 世界；随后置 null 防止 LateUpdate 再次访问。</summary>
        void OnDestroy()
        {
            _session?.Dispose();
            _session = null;
        }

        /// <summary>只校验 Inspector 槽位是否赋值；内容层面的完整性由 Database.Bake() 负责，二者不能互相替代。</summary>
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

        /// <summary>补齐运行时根节点；相机缺席时现建一套带 CameraRig 的 Main Camera，取景参数来自数据库。</summary>
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

        // 根节点挂在本对象下，随场景卸载一起收走，避免运行时对象泄漏回编辑器场景。
        Transform CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>
        /// Cue 的 Prefab 绑定现场从数据库的 cue 资产读出，键只有这一个来源：
        /// 重建 cue 定义时不会与 Prefab 绑定漂移。这里与 UnityPresentFactory 持有的
        /// ViewPrefabTable 是运行期仅有的两处仍然直接读 SO 的例外——它们都是美术绑定表，
        /// 不是内容数据，没有进烘焙数据库的必要。
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
