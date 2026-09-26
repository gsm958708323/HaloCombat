using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>
    /// Buff Arena 的唯一内容作者：运行时数据库完全由本资产烘焙而来，没有任何代码回退
    /// （对照 CodeCombatContent 那套纯代码内容）。因此内容缺项或交叉引用断裂时必须让会话启动失败，
    /// 而不是悄悄退回默认值——默认值会把“内容没配好”伪装成“游戏本来就这样”。
    /// 内容资产位于 Assets/Combat/Config/Generated，Bake() 是唯一出口。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Buff Arena Database")]
    public sealed class BuffArenaDatabaseAsset : ScriptableObject
    {
        public int PlayerAmmoCapacity = 60;
        public int MaxEnemies = 10;
        public float SpawnPeriod = 10f;
        public float EnemyCleanupDelay = 5f;
        public float BarrelSelfDamagePeriod = 5f;
        public int Seed = 1;
        public CharacterMotorAsset Motor;

        [Header("Presentation")]
        [Tooltip("Framing used when the Arena scene has to build its own camera rig at runtime.")]
        public float CameraDistance = 2.5f;
        public float CameraHeight = 2.5f;
        public float CameraBaseFov = 60f;

        [Header("Content")]
        [Tooltip("Every definition the Arena runs on. Authored here; the runtime has no code fallback.")]
        public ProjectileDefAsset[] Projectiles;
        [Tooltip("AoE definitions registered by SpecId.")]
        public AoeDefAsset[] Aoes;
        [Tooltip("Registered in order; ids come from each asset.")]
        public SkillTimelineAsset[] Timelines;
        public BuffArenaSkillAsset[] Skills;
        public BuffArenaActorDefAsset[] Actors;
        public CueLibraryAsset Cues;

        /// <summary>
        /// 最近一次 Bake() 返回 null 的原因，好让启动方说清“哪里不对”，而不是悄悄换一套内容顶上。
        /// Bake() 开头会清空它，所以只有失败的那次调用留有值。
        /// </summary>
        public string LastContentError { get; private set; }

        const string IncompleteReason =
            "the authored content is incomplete: projectiles, aoes, timelines, skills, actors "
            + "and a motor asset all have to be present and non-empty";

        /// <summary>
        /// 把编辑期资产烘焙成运行时数据库，四步顺序固定：
        /// ① 完整性检查（HasCompleteContent）→ ② 逐项烘焙（BakeCore）→ ③ null 洞检查（HasCastlessPayload，
        /// 在 BakeCore 末尾执行）→ ④ 交叉引用与数值校验（ValidateReferences）。
        /// 任一步失败都返回 null 并写入 LastContentError（第 ④ 步同时 LogError）。
        /// 三类失败原因：内容缺项、时间轴 payload 里残留烘焙不出的 null 效果槽、引用断裂 / 数值非正。
        /// 没有代码回退，调用方必须让会话启动失败（见 BuffArenaBootstrap.Start）。
        /// </summary>
        public BuffArenaData Bake()
        {
            LastContentError = null;
            if (!HasCompleteContent())
            {
                LastContentError = IncompleteReason;
                return null;
            }

            var data = BakeCore();
            if (data == null) return null;

            if (!ValidateReferences(data, out string error))
            {
                LastContentError = error;
                Debug.LogError("BuffArenaDatabase: " + error, this);
                return null;
            }

            return data;
        }

        /// <summary>
        /// 逐项把定义资产 Bake() 并注册进运行时目录（时间轴、弹体、AoE、Cue、技能、Actor），
        /// 最后做 null 洞检查。Motor 缺席时退回 SeasonOneDefaults()，但 HasCompleteContent
        /// 已在入口拦掉这种情况，这里只是防御。检测到 payload 空洞即返回 null——
        /// 带着空洞开服等于放出“有施法动作、什么也不生成”的技能。
        /// </summary>
        BuffArenaData BakeCore()
        {
            var data = new BuffArenaData
            {
                PlayerAmmoCapacity = PlayerAmmoCapacity,
                MaxEnemies = MaxEnemies,
                SpawnPeriod = SpawnPeriod,
                EnemyCleanupDelay = EnemyCleanupDelay,
                BarrelSelfDamagePeriod = BarrelSelfDamagePeriod,
                Seed = Seed,
                Motor = Motor != null ? Motor.Bake() : MotorConfig.SeasonOneDefaults()
            };

            for (int i = 0; i < Timelines.Length; i++)
                data.Timelines.Register(Timelines[i].Bake());
            data.Projectiles.Register(Projectiles[0].Bake());
            for (int i = 1; i < Projectiles.Length; i++)
                data.Projectiles.Register(Projectiles[i].Bake());
            data.Aoes.Register(Aoes[0].Bake());
            for (int i = 1; i < Aoes.Length; i++)
                data.Aoes.Register(Aoes[i].Bake());
            if (Cues != null)
                data.Cues = Cues.Bake();
            for (int i = 0; i < Skills.Length; i++)
                data.Skills.Add(Skills[i].Bake());
            var actorDefs = BakeActors();
            for (int i = 0; i < actorDefs.Length; i++)
                if (actorDefs[i] != null) data.Actors.Add(actorDefs[i]);
            if (HasCastlessPayload(data))
            {
                LastContentError = "authored timelines hold payload slots whose effect failed to bake";
                Debug.LogError(
                    "BuffArenaDatabase: " + LastContentError
                        + ". Serving them would ship castless skills, so the session refuses to start. "
                        + "Re-author the content assets.",
                    this
                );
                return null;
            }
            return data;
        }

        /// <summary>
        /// 任一时间轴 payload 槽位里出现烘焙失败（null）的 IEffect 时为 true。
        /// 效果资产经 BakeNew() 解析，所以子资产引用一旦断掉就会得到 null 槽位——
        /// 表现出来正是“技能有施法动作却什么也不生成”。payload / effects 数组本身为 null 时跳过。
        /// </summary>
        static bool HasCastlessPayload(BuffArenaData data)
        {
            foreach (var timeline in data.Timelines.All)
            {
                var payloads = timeline.Payloads;
                if (payloads == null)
                    continue;
                for (int i = 0; i < payloads.Length; i++)
                {
                    var effects = payloads[i].Effects;
                    if (effects == null)
                        continue;
                    for (int j = 0; j < effects.Length; j++)
                        if (effects[j] == null)
                            return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 校验过去靠“从代码表生成资产”天然保证的交叉引用：工厂按名字索要的蓝图存在、
        /// 每个技能指向真实时间轴、每个 fallback 技能存在，且运行期数值全部为正。
        /// 失败时 error 是给策划看的具体原因，Bake() 会原样记进 LastContentError 并 LogError。
        /// </summary>
        static bool ValidateReferences(BuffArenaData data, out string error)
        {
            string[] requiredBlueprints =
            {
                BuffArenaIds.PlayerBlueprint, BuffArenaIds.EnemyBlueprint, BuffArenaIds.BarrelBlueprint
            };
            for (int i = 0; i < requiredBlueprints.Length; i++)
            {
                bool found = false;
                for (int k = 0; k < data.Actors.Count; k++)
                {
                    if (!string.Equals(data.Actors[k].BlueprintId, requiredBlueprints[i], StringComparison.Ordinal))
                        continue;
                    found = true;
                    break;
                }
                if (found) continue;
                error = "the actor table has no '" + requiredBlueprints[i] + "' blueprint";
                return false;
            }

            for (int i = 0; i < data.Skills.Count; i++)
            {
                var skill = data.Skills[i];
                if (!data.Timelines.TryGet(skill.Timeline, out _))
                {
                    error = "skill " + skill.Id.Value + " points at missing timeline " + skill.Timeline.Value;
                    return false;
                }
                if (skill.FallbackSkill.IsValid && !data.TryGetSkill(skill.FallbackSkill, out _))
                {
                    error = "skill " + skill.Id.Value + " falls back to missing skill " + skill.FallbackSkill.Value;
                    return false;
                }
            }

            BuffArenaActorDef playerDef = null;
            for (int i = 0; i < data.Actors.Count; i++)
            {
                if (!string.Equals(data.Actors[i].BlueprintId, BuffArenaIds.PlayerBlueprint, StringComparison.Ordinal))
                    continue;
                playerDef = data.Actors[i];
                break;
            }
            if (playerDef == null || playerDef.MaxHp <= 0f)
            {
                error = "the player actor definition must have a positive MaxHp";
                return false;
            }

            if (data.PlayerAmmoCapacity <= 0 || data.MaxEnemies <= 0 ||
                data.SpawnPeriod <= 0f || data.EnemyCleanupDelay <= 0f || data.BarrelSelfDamagePeriod <= 0f)
            {
                error = "runtime settings contain a non-positive value";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>运行期需要的每个定义是否齐备：数组非空且不含 null 元素，并且 Motor 已赋值。</summary>
        public bool HasCompleteContent()
        {
            if (!HasEntries(Projectiles) || !HasEntries(Aoes) || !HasEntries(Timelines) ||
                !HasEntries(Skills) || !HasEntries(Actors))
                return false;
            return Motor != null;
        }

        static bool HasEntries<T>(T[] items) where T : UnityEngine.Object
        {
            if (items == null || items.Length == 0) return false;
            for (int i = 0; i < items.Length; i++)
                if (items[i] == null) return false;
            return true;
        }

        /// <summary>
        /// 把 Actor 资产逐个烘焙成运行时定义：失败项留 null 洞，由 BakeCore 统一丢弃。
        /// 与代码内容 BakedCombatData.ToInstall() 的一次性装配不同，SO 内容没有 ToInstall：
        /// 这些定义留在 BuffArenaData.Actors 里，由 BuffArenaActorFactory 在世界装配
        /// 以及之后每次按蓝图生成时凭 BlueprintId 取用（WorldInstall 由 BuffArenaSession 手工组装）。
        /// 少一个蓝图，ValidateReferences 会在启动时点名报错。
        /// </summary>
        BuffArenaActorDef[] BakeActors()
        {
            if (Actors == null) return Array.Empty<BuffArenaActorDef>();
            var result = new BuffArenaActorDef[Actors.Length];
            for (int i = 0; i < Actors.Length; i++)
                result[i] = Actors[i] != null ? Actors[i].Bake() : null;
            return result;
        }

        void OnValidate()
        {
            // Surface the obvious authoring mistakes in the Inspector instead of at play time.
            // 只报最明显的槽位错误（缺 Motor）：完整校验依赖多资产交叉引用，交给 Bake() 去做，
            // 免得每次 Inspector 改动都跑一遍又慢又吵的检查。
            if (Motor == null) Debug.LogError("BuffArenaDatabase: no motor asset assigned.", this);
        }
    }
}
