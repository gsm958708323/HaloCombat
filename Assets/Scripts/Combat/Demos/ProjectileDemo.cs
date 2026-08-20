using System;
using Combat.Core;
using UnityEngine;

namespace Combat.ProjectileDemo
{
    public class ProjectileDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            // --- 轴：到点发射 901 ---
            var tlG1 = new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.50f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.05f, EndTime = 0.40f,
                        Type = EffectType.AddTag,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    new TimelineKey
                    {
                        Time = 0.15f, EndTime = -1f,
                        Type = EffectType.SpawnProjectile,
                        ProjectileSpecValue = 901
                    },
                }
            };
            var library = new TimelineLibrary();
            library.Register(tlG1);
            var combos = new ComboTableSO
            {
                Entries = new[]
                {
                    new ComboEntry
                    {
                        PreSkills = Array.Empty<SkillNodeId>(),
                        Input = InputToken.Attack,
                        RequiredTags = Array.Empty<int>(),
                        Priority = 0,
                        ToSkill = SkillNodeId.G1,
                        Timeline = TimelineId.TL_G1
                    }
                }
            };
            var projSpecs = new ProjectileSpecLibrary();
            projSpecs.Register(new ProjectileSpec
            {
                Id = ProjectileSpecId.Bolt901,
                Speed = 10f,
                Lifetime = 2f,
                Radius = 0.35f,
                DirX = 1f,
                AttackSpecValue = 42,
                Pierce = false,
                SpawnOffsetX = 0.6f,
                SpawnOffsetY = 1.0f
            });
            var effects = new EffectFactory(intents);
            var factory = new CombatActorFactory(time, combos, library, effects, intents, projSpecs);
            var world = new CombatWorld(factory, intents, events, time);
            var projectileService = new ProjectileService(world, intents, projSpecs, factory);
            var hitDetect = new HitDetectService(world, intents, projectileService);
            var despawn = new DespawnService(world, intents);
            int hitCount = 0;
            world.AddServicePhase(() => projectileService.Tick());
            world.AddServicePhase(() => hitDetect.Tick());
            world.AddServicePhase(() =>
            {
                intents.Drain<HitIntent>(hit =>
                {
                    hitCount++;
                    print(
                        $"[F{time.Frame}] HIT src={hit.Source} tgt={hit.Target} owner={hit.Owner} atk={hit.AttackSpecValue}");
                });
            });
            world.AddServicePhase(() => despawn.Tick());
            var playerId = world.SpawnActor(new ActorSpawnSpec("fighter"));
            var dummyId = world.SpawnActor(new ActorSpawnSpec("dummy"));
            world.TryGetActor(playerId, out var player);
            world.TryGetActor(dummyId, out var dummy);
            // 木桩放在玩家前方 2m
            dummy.GetComp<TransformComp>().Teleport(new SimVec3(2.0f, 1.0f, 0f));
            player.GetComp<TransformComp>().Teleport(new SimVec3(0f, 0f, 0f));
            var input = player.GetComp<InputBufferComp>();
            var fsm = player.GetComp<StateMachineComp>();
            input.Push(InputToken.Attack);
            world.Tick(0.05f); // 开招
            // 推到发射点之后，再推几帧让子弹飞到木桩
            for (int i = 0; i < 20; i++)
                world.Tick(0.05f);
            print($"hits={hitCount}, activeActors={world.Registry.ActiveCount}, playerState={fsm.Current}");
            if (hitCount < 1)
                throw new Exception("Expected at least one HitIntent");
            // 再等一会儿：非穿透弹应已销毁；寿命也会清
            for (int i = 0; i < 10; i++)
                world.Tick(0.05f);
            bool projStillThere = false;
            foreach (var id in projectileService.ActiveProjectiles)
            {
                if (world.TryGetActor(id, out _))
                    projStillThere = true;
            }
            print($"projectileAlive={projStillThere} (expect false)");
            if (projStillThere)
                throw new Exception("Non-pierce projectile should despawn after hit");
            // 轴应结束回 Root
            if (fsm.Current != ActorStateId.Root)
                print($"Warn: state={fsm.Current} (若 duration 已过应为 Root)");
            print("ProjectileDemo PASSED");
        }
    }


}