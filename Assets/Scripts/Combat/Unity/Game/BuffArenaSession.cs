using System;
using System.Collections.Generic;
using Combat.Core;
using Combat.Unity.Presentation;

namespace Combat.Unity.Game
{
    /// <summary>
    /// 一局 Arena 的运行时大脑：持有 CombatWorld、表现中枢 Hub、固定步长时钟，以及刷怪 / 清尸计时。
    /// 帧序契约：每帧先 ApplyInput（写意图与输入缓冲）→ PumpLogic 按 LogicTicker 固定步长推进，
    /// 单个逻辑步是 World.Tick → 清尸 → 刷怪 → 重算敌人数 → Hub.AfterLogicTick（表现事件在逻辑之后收口）；
    /// 呈现由 PumpPresent 插值、PumpUnscaled 用 wall time 清尸体。
    /// 玩家死亡会冻结逻辑钟（TickLogic 直接返回），所以任何“死后仍要发生”的事都不能挂在逻辑时间上。
    /// 键位 → 技能令牌的映射由本类代码拥有（见 ApplyInput 与 BuffArenaIds），不在资产里。
    /// </summary>
    public sealed class BuffArenaSession : IDisposable
    {
        readonly BuffArenaData _data;
        readonly GridMovementConstraint _map;
        readonly LogicTicker _ticker = new LogicTicker();
        readonly int _enemyTeamId;
        readonly List<DeadEnemy> _deadEnemies = new List<DeadEnemy>(16);
        Action<EvEntityDead> _dead;
        bool _firstSpawn = true;
        float _spawnTimer;
        int _spawned;
        bool _playerDead;
        bool _disposed;

        struct DeadEnemy
        {
            public EntityId Id;
            public float CleanupAt;
        }

        // Corpse cleanup runs on wall time so it keeps working after the player dies,
        // which freezes the logic clock.
        float _wallTime;

        public CombatWorld World { get; private set; }
        public PresentHub Hub { get; private set; }
        /// <summary>
        /// 本会话运行的烘焙内容（含玩家的输入绑定）。给测试用：不必反射私有字段，
        /// 就能读到这一局实际生效的技能与按键映射。
        /// </summary>
        public BuffArenaData Data => _data;
        public EntityId LocalPlayerId { get; private set; }
        public bool PlayerDead => _playerDead;
        public int SpawnedEnemies => _spawned;
        public int EnemyCount { get; private set; }
        public GridMovementConstraint Map => _map;

        public BuffArenaSession(BuffArenaData data, PresentHub hub, GridMovementConstraint map)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _enemyTeamId = _data.RequireActor(BuffArenaIds.EnemyBlueprint).TeamId;
            // 世界只装配一次：弹体 / AoE 目录与 Cue 来自烘焙数据，Arena 没有召唤物。
            World = new CombatWorld(
                new BuffArenaActorFactory(_data),
                new WorldInstall
                {
                    Intents = new IntentQueue(),
                    Events = new EventBus(),
                    Time = new CombatTime(),
                    Random = new SeededRandom(_data.Seed),
                    Cues = _data.Cues,
                    Motor = _data.Motor,
                    Movement = _map,
                    Projectiles = _data.Projectiles,
                    Aoes = _data.Aoes,
                    Summons = new SummonCatalog()
                });
        }

        /// <summary>绑定世界与事件总线后生成玩家；必须在 Bootstrap 建好 Hub / Cue 池之后调用，因为生成即发事件。</summary>
        public void Start()
        {
            Hub.SetWorld(World);
            Hub.BindBus(World.Events);
            _dead = OnDead;
            World.Events.Subscribe(_dead);
            SpawnPlayer();
        }

        /// <summary>
        /// 把本帧采样转成移动 / 瞄准意图和技能输入令牌。
        /// 推送顺序沿用源工程 PlayerController，而输入缓冲只有单槽位（后来者覆盖），
        /// 所以这段 if 序列本身就是优先级：Fire5 → Fire1 → Roll → Homing → Monkey。
        /// 这里的每个令牌都必须等于目标技能资产上署名的 InputToken，对不上时技能永远不会被找到，
        /// 而且是静默的。玩家已死 / 无 Actor / 已 Dispose 时整帧丢弃输入。
        /// </summary>
        public void ApplyInput(in BuffArenaInputFrame input)
        {
            if (_disposed || _playerDead || !World.TryGetActor(LocalPlayerId, out var player) || player == null)
                return;
            if (player.TryGetComp<LocomotionComp>(out var loco))
            {
                loco.RequestMoveIntent(input.MoveX, input.MoveZ);
                if (input.AimValid)
                    loco.RequestAimYaw(input.AimYaw);
            }

            if (!player.TryGetComp<InputBufferComp>(out var buffer)) return;
            // The order mirrors the source PlayerController, and the single-slot buffer means
            // the last push wins. Q/E expose the two learned source skills that had no physical
            // button in that script. Every token here must equal the InputToken authored on the
            // skill asset it is meant to cast (see BuffArenaIds).
            if (input.Fire5Held) buffer.Push(BuffArenaIds.Fire5);
            if (input.Fire4Held) buffer.Push(BuffArenaIds.Fire4);
            if (input.Fire3Held) buffer.Push(BuffArenaIds.Fire3);
            if (input.Fire2Held) buffer.Push(BuffArenaIds.Fire2);
            if (input.Fire1Held) buffer.Push(BuffArenaIds.Fire1);
            if (input.RollHeld) buffer.Push(BuffArenaIds.RollInput);
            if (input.HomingHeld) buffer.Push(BuffArenaIds.HomingInput);
            if (input.MonkeyHeld) buffer.Push(BuffArenaIds.MonkeyInput);
        }

        /// <summary>把真实帧时长交给固定步长时钟；一帧内可能跑 0..N 个逻辑步。</summary>
        public void PumpLogic(float dt)
        {
            if (_disposed) return;
            _ticker.Accumulate(dt, TickLogic);
        }

        /// <summary>
        /// 表现层推进：用 RenderLogicTime(World.Time.Time) 取插值时刻，负 dt 夹到 0。
        /// 与逻辑解耦，玩家死后照常运行（否则死亡瞬间的表现会卡住）。
        /// </summary>
        public void PumpPresent(float dt)
        {
            if (_disposed || World == null) return;
            Hub.LateUpdate(World, dt, _ticker.RenderLogicTime(World.Time.Time));
            Hub.PumpUnscaled(dt < 0f ? 0f : dt);
        }

        /// <summary>
        /// 不走逻辑钟的推进：累加 wall time 并清理到期尸体。
        /// 用 wall time 的原因见 _wallTime 的注释：玩家死后逻辑钟冻住，
        /// 若清尸按逻辑时间计，尸体会永远留在场上，“死亡”看起来像没生效。
        /// </summary>
        public void PumpUnscaled(float dt)
        {
            if (_disposed || World == null) return;
            _wallTime += dt < 0f ? 0f : dt;
            ProcessDeadEnemies();
        }

        /// <summary>
        /// 单个固定步：World.Tick → 清尸 → 首次 / 周期刷怪 → 重算敌人数 → Hub.AfterLogicTick。
        /// AfterLogicTick 放在最后，是给表现层的收口点，保证它看到的是本步结算完之后的世界。
        /// 玩家死后整步直接跳过：逻辑钟停摆，刷怪与输入随之停止。
        /// </summary>
        void TickLogic(float step)
        {
            if (_playerDead) return;
            World.Tick(step);
            ProcessDeadEnemies();

            _spawnTimer += step;
            if (_firstSpawn || _spawnTimer >= _data.SpawnPeriod)
            {
                _firstSpawn = false;
                _spawnTimer = 0f;
                SpawnToLimit();
            }

            CountEnemies();
            Hub.AfterLogicTick(World);
        }

        /// <summary>
        /// 在导航上取随机可站点生成玩家；先 publishSpawn:false、配置完再手动 PublishSpawn，
        /// 是为了让表现层拿到位置和属性都已配好的 Actor，而不是默认状态。
        /// </summary>
        void SpawnPlayer()
        {
            var def = _data.RequireActor(BuffArenaIds.PlayerBlueprint);
            if (!_map.TryGetRandomPosition(World.Random, def.BodyRadius, false, out var position))
                throw new InvalidOperationException("Buff Arena map has no player spawn point.");
            var id = World.SpawnActor(new ActorSpawnSpec(def.BlueprintId), publishSpawn: false);
            if (!World.TryGetActor(id, out var player) || player == null)
                throw new InvalidOperationException("Unable to create Buff Arena player.");
            player.GetComp<TransformComp>().Position = position;
            ConfigurePlayer(player);
            LocalPlayerId = id;
            Hub.SetLocalPlayer(id);
            World.PublishSpawn(id, def.BlueprintId, def.ViewBlueprintId);
        }

        /// <summary>补怪到上限；单次生成失败（找不到站位）就停手，等下个周期再试。</summary>
        void SpawnToLimit()
        {
            CountEnemies();
            while (!_playerDead && EnemyCount < _data.MaxEnemies)
            {
                if (!SpawnEnemy()) break;
                EnemyCount++;
            }
        }

        /// <summary>
        /// 生成一只敌人并立即发布。index 取自累计生成数（_spawned++）而非当前存活数，
        /// 所以死掉再刷出来的下一只依然更强。随机站位 / 朝向失败时返回 false。
        /// </summary>
        bool SpawnEnemy()
        {
            var def = _data.RequireActor(BuffArenaIds.EnemyBlueprint);
            if (!_map.TryGetRandomPosition(World.Random, def.BodyRadius, false, out var position))
                return false;
            int index = _spawned++;
            var id = World.SpawnActor(new ActorSpawnSpec(def.BlueprintId), publishSpawn: false);
            if (!World.TryGetActor(id, out var enemy) || enemy == null)
                return false;
            enemy.GetComp<TransformComp>().Position = position;
            // Same world heading as before the yaw convention flip (90 - oldYaw) so a
            // given SeededRandom draw keeps producing the same spawn facing.
            enemy.GetComp<TransformComp>().YawDegrees = 90f - World.Random.Next01() * 360f;
            ConfigureEnemy(enemy, index);
            if (enemy.TryGetComp<BehaviorTreeComp>(out var ai))
                ai.Board.Target = LocalPlayerId;
            World.PublishSpawn(id, def.BlueprintId, def.ViewBlueprintId);
            return true;
        }

        /// <summary>玩家属性取数据库设定而非 Actor 定义；Atk 在这里抽一次随机，会消耗随机序列。</summary>
        void ConfigurePlayer(Actor player)
        {
            var def = _data.RequireActor(BuffArenaIds.PlayerBlueprint);
            var attr = player.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, _data.PlayerMaxHp);
            attr.SetBase(AttrId.Hp, _data.PlayerMaxHp);
            attr.SetBase(AttrId.Atk, def.Atk + (int)(World.Random.Next01() * def.AtkRandomRange));
            attr.SetBase(AttrId.MoveSpeed, def.MoveSpeed);
            attr.SetBase(AttrId.ActionSpeed, def.ActionSpeed);
            attr.SetBase(AttrId.CritRate, def.CritRate);
            player.GetComp<AmmoComp>().Set(_data.PlayerAmmoCapacity, _data.PlayerAmmoCapacity);
        }

        /// <summary>敌人属性 = 定义值 + index 递增成长；这里同样抽随机数，抽数顺序不能改。</summary>
        void ConfigureEnemy(Actor enemy, int index)
        {
            var def = _data.RequireActor(BuffArenaIds.EnemyBlueprint);
            var attr = enemy.GetComp<AttributeSet>();
            float maxHp = def.MaxHp + index * def.MaxHpPerIndex;
            attr.SetBase(AttrId.MaxHp, maxHp);
            attr.SetBase(AttrId.Hp, maxHp);
            attr.SetBase(
                AttrId.Atk,
                def.Atk + (int)(World.Random.Next01() * def.AtkRandomRange) + index * def.AtkPerIndex
            );
            attr.SetBase(AttrId.MoveSpeed, ResolveMoveSpeed(def));
            attr.SetBase(AttrId.ActionSpeed, def.ActionSpeed);
            attr.SetBase(AttrId.CritRate, def.CritRate);
        }

        /// <summary>
        /// 源工程保存的是旧速度值，生成时再换算。换算公式及其入参都留在配置里，
        /// 好让迁移前的数值能逐位复现；随机抽数顺序不能改（改了会整体错位）。
        /// </summary>
        float ResolveMoveSpeed(BuffArenaActorDef def)
        {
            if (!def.UseLegacySpeedCurve)
                return def.MoveSpeed;
            int legacySpeed =
                (int)def.LegacySpeedBase + (int)(World.Random.Next01() * def.LegacySpeedRandomRange);
            return legacySpeed * def.MoveSpeedCurveScale / (legacySpeed + def.MoveSpeedCurveDivisor)
                + def.MoveSpeedCurveOffset;
        }

        /// <summary>倒序遍历（RequestDespawn 只登记、不立即移除）；到期用 wall time 判定，见 PumpUnscaled。</summary>
        void ProcessDeadEnemies()
        {
            for (int i = _deadEnemies.Count - 1; i >= 0; i--)
            {
                var dead = _deadEnemies[i];
                if (_wallTime < dead.CleanupAt) continue;
                World.RequestDespawn(dead.Id);
                _deadEnemies.RemoveAt(i);
            }
        }

        /// <summary>
        /// 每逻辑步重算存活敌人数（排除带 Dead 标签的）。刷怪上限以存活数为准而不是累计生成数；
        /// 玩家不属敌人队伍，天然不计入。
        /// </summary>
        void CountEnemies()
        {
            EnemyCount = 0;
            var actors = World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || !actor.TryGetComp<TeamComp>(out var team) || team.TeamId != _enemyTeamId)
                    continue;
                if (actor.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) continue;
                EnemyCount++;
            }
        }

        /// <summary>
        /// 收到死亡事件：登记尸体清理时间。玩家死亡只置标志，不在这里做表现或结算——
        /// 逻辑钟随后会冻住，清尸只能靠 wall time 累加。
        /// </summary>
        void OnDead(EvEntityDead e)
        {
            // Every corpse (player included) is removed after the cleanup delay; the
            // source only spares the main actor, but leaving the body forever reads as
            // "death did nothing".
            _deadEnemies.Add(new DeadEnemy { Id = e.Id, CleanupAt = _wallTime + _data.EnemyCleanupDelay });
            if (e.Id == LocalPlayerId)
            {
                _playerDead = true;
                return;
            }
            if (!World.TryGetActor(e.Id, out var actor) || actor == null ||
                !actor.TryGetComp<TeamComp>(out var team) || team.TeamId != _enemyTeamId)
                return;
        }

        /// <summary>退订 → 解绑总线 → 释放表现池 → Shutdown 世界，并清空引用使二次 Dispose 成为 no-op。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (World != null && _dead != null)
                World.Events.Unsubscribe(_dead);
            Hub?.UnbindBus();
            Hub?.ReleaseAll();
            World?.Shutdown();
            World = null;
            Hub = null;
            _dead = null;
            _deadEnemies.Clear();
        }
    }
}
