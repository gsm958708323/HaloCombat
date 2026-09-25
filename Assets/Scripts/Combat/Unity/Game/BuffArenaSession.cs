using System;
using System.Collections.Generic;
using Combat.Core;
using Combat.Unity.Presentation;

namespace Combat.Unity.Game
{
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
        /// The baked content this session runs on (including the player input bindings).
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
            World = new CombatWorld(
                new BuffArenaActorFactory(_data),
                new IntentQueue(),
                new EventBus(),
                new CombatTime(),
                new SeededRandom(_data.Seed),
                _data.Cues,
                _data.Motor,
                _map);
            World.ReplaceCatalogs(_data.Projectiles, _data.Aoes, new SummonCatalog());
        }

        public void Start()
        {
            Hub.SetWorld(World);
            Hub.BindBus(World.Events);
            _dead = OnDead;
            World.Events.Subscribe(_dead);
            SpawnPlayer();
        }

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

        public void PumpLogic(float dt)
        {
            if (_disposed) return;
            _ticker.Accumulate(dt, TickLogic);
        }

        public void PumpPresent(float dt)
        {
            if (_disposed || World == null) return;
            Hub.LateUpdate(World, dt, _ticker.RenderLogicTime(World.Time.Time));
            Hub.PumpUnscaled(dt < 0f ? 0f : dt);
        }

        public void PumpUnscaled(float dt)
        {
            if (_disposed || World == null) return;
            _wallTime += dt < 0f ? 0f : dt;
            ProcessDeadEnemies();
        }

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

        void SpawnToLimit()
        {
            CountEnemies();
            while (!_playerDead && EnemyCount < _data.MaxEnemies)
            {
                if (!SpawnEnemy()) break;
                EnemyCount++;
            }
        }

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
        /// The source game stored a legacy speed stat and converted it when spawning. The
        /// conversion and its inputs live in configuration so the pre-migration numbers stay
        /// reproducible bit for bit; the random draw order must not change.
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
