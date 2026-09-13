using System;
using System.Collections.Generic;
using Combat.Core;
using Combat.Presentation;
using Combat.Unity.Game;

namespace Combat.Game
{
    public sealed class BuffArenaSession : IDisposable
    {
        readonly BuffArenaData _data;
        readonly GridMovementConstraint _map;
        readonly LogicTicker _ticker = new LogicTicker();
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
            World = new CombatWorld(
                new BuffArenaActorFactory(_data),
                new IntentQueue(),
                new EventBus(),
                new CombatTime(),
                new SeededRandom(1),
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
            // The order mirrors the source PlayerController. Q/E expose the two
            // learned source skills that had no physical button in that script.
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
            if (!_map.TryGetRandomPosition(World.Random, .25f, false, out var position))
                throw new InvalidOperationException("Buff Arena map has no player spawn point.");
            var id = World.SpawnActor(new ActorSpawnSpec("buff_player"), publishSpawn: false);
            if (!World.TryGetActor(id, out var player) || player == null)
                throw new InvalidOperationException("Unable to create Buff Arena player.");
            player.GetComp<TransformComp>().Position = position;
            ConfigurePlayer(player);
            LocalPlayerId = id;
            Hub.SetLocalPlayer(id);
            World.PublishSpawn(id, "buff_player", "buff_player_view");
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
            if (!_map.TryGetRandomPosition(World.Random, .25f, false, out var position))
                return false;
            int index = _spawned++;
            var id = World.SpawnActor(new ActorSpawnSpec("buff_enemy"), publishSpawn: false);
            if (!World.TryGetActor(id, out var enemy) || enemy == null)
                return false;
            enemy.GetComp<TransformComp>().Position = position;
            // Same world heading as before the yaw convention flip (90 - oldYaw) so a
            // given SeededRandom draw keeps producing the same spawn facing.
            enemy.GetComp<TransformComp>().YawDegrees = 90f - World.Random.Next01() * 360f;
            ConfigureEnemy(enemy, index);
            if (enemy.TryGetComp<BehaviorTreeComp>(out var ai))
                ai.Board.Target = LocalPlayerId;
            World.PublishSpawn(id, "buff_enemy", "buff_enemy_view");
            return true;
        }

        void ConfigurePlayer(Actor player)
        {
            var attr = player.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, _data.PlayerMaxHp);
            attr.SetBase(AttrId.Hp, _data.PlayerMaxHp);
            attr.SetBase(AttrId.Atk, 50f + (int)(World.Random.Next01() * 20f));
            attr.SetBase(AttrId.MoveSpeed, 3f);
            attr.SetBase(AttrId.ActionSpeed, 1f);
            attr.SetBase(AttrId.CritRate, .05f);
            player.GetComp<AmmoComp>().Set(_data.PlayerAmmoCapacity, _data.PlayerAmmoCapacity);
        }

        void ConfigureEnemy(Actor enemy, int index)
        {
            var attr = enemy.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, 50f + index * 2f);
            attr.SetBase(AttrId.Hp, 50f + index * 2f);
            attr.SetBase(AttrId.Atk, 15f + (int)(World.Random.Next01() * 15f) + index);
            int legacySpeed = 50 + (int)(World.Random.Next01() * 20f);
            attr.SetBase(AttrId.MoveSpeed, legacySpeed * 5.6f / (legacySpeed + 100f) + .2f);
            attr.SetBase(AttrId.ActionSpeed, 1f);
            attr.SetBase(AttrId.CritRate, .05f);
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
                if (actor == null || !actor.TryGetComp<TeamComp>(out var team) || team.TeamId != 2)
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
                !actor.TryGetComp<TeamComp>(out var team) || team.TeamId != 2)
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
