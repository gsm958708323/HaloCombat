using System;
using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.DamageDemo
{
    public class DamageDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var tlG1 = new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.45f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.12f, EndTime = -1f,
                        Type = EffectType.SpawnProjectile,
                        ProjectileSpecValue = 901
                    }
                }
            };
            var timelines = new TimelineLibrary();
            timelines.Register(tlG1);
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
                Speed = 12f,
                Lifetime = 2f,
                Radius = 0.35f,
                DirX = 1f,
                AttackSpecValue = 42,
                Pierce = false,
                SpawnOffsetX = 0.5f,
                SpawnOffsetY = 1.0f
            });
            var attackSpecs = new AttackSpecLibrary();
            attackSpecs.Register(new AttackSpec
            {
                Id = 42,
                Power = 1f,
                StunDuration = 0.30f,
                ApplyHitStun = true
            });
            var effects = new EffectFactory(intents);
            var factory = new CombatActorFactory(time, combos, timelines, effects, intents, projSpecs);
            var world = new CombatWorld(factory, intents, events, time);
            var projectileService = new ProjectileService(world, intents, projSpecs, factory);
            var hitDetect = new HitDetectService(world, intents, projectileService);
            var damage = new DamageService(world, intents, attackSpecs, events);
            var despawn = new DespawnService(world, intents);
            int dmgEvents = 0;
            events.Subscribe<DamageAppliedEvent>(e =>
            {
                dmgEvents++;
                print(
                    $"[F{time.Frame}] DMG {e.Amount:F1} -> {e.Target} hp={e.HpAfter:F1} dead={e.Died} hit={e.EnteredHit}");
            });
            world.AddServicePhase(() => projectileService.Tick());
            world.AddServicePhase(() => hitDetect.Tick());
            world.AddServicePhase(() => damage.Tick());
            world.AddServicePhase(() => despawn.Tick());
            var playerId = world.SpawnActor(new ActorSpawnSpec("fighter"));
            var dummyId = world.SpawnActor(new ActorSpawnSpec("dummy"));
            world.TryGetActor(playerId, out var player);
            world.TryGetActor(dummyId, out var dummy);
            player.GetComp<TransformComp>().Teleport(new SimVec3(0f, 0f, 0f));
            dummy.GetComp<TransformComp>().Teleport(new SimVec3(1.8f, 1.0f, 0f));
            var dummyHp = dummy.GetComp<HealthComp>();
            var dummyFsm = dummy.GetComp<StateMachineComp>();
            float hp0 = dummyHp.Hp;
            // A. 正常命中 → 掉血 + Hit
            player.GetComp<InputBufferComp>().Push(InputToken.Attack);
            for (int i = 0; i < 25; i++)
                world.Tick(0.05f);
            print($"A: hp {hp0} -> {dummyHp.Hp}, state={dummyFsm.Current}, dmgEvents={dmgEvents}");
            if (dummyHp.Hp >= hp0)
                throw new Exception("Expected damage");
            if (dummyFsm.Current != ActorStateId.Hit && dummyFsm.Current != ActorStateId.Root && !dummyHp.IsDead)
                throw new Exception("Expected Hit or recovered Root");
            // 等硬直结束回 Root
            for (int i = 0; i < 10; i++)
                world.Tick(0.05f);
            if (dummyFsm.Current != ActorStateId.Root && !dummyHp.IsDead)
                throw new Exception("Expected Root after stun");
            // B. 无敌：有 HitIntent 也不进 Hit、不掉血
            dummyHp.Invulnerable = true;
            float hp1 = dummyHp.Hp;
            int ev1 = dmgEvents;
            player.GetComp<InputBufferComp>().Push(InputToken.Attack);
            for (int i = 0; i < 25; i++)
                world.Tick(0.05f);
            print($"B invuln: hp={dummyHp.Hp}, state={dummyFsm.Current}, events+={dmgEvents - ev1}");
            if (Math.Abs(dummyHp.Hp - hp1) > 0.001f)
                throw new Exception("Invulnerable should take 0 hp");
            // DamageAppliedEvent 仍可能 Publish amount=0；若不想刷事件，可在 Service 里 amount<=0 直接 return。
            if (dummyFsm.Current == ActorStateId.Hit)
                throw new Exception("Invulnerable must not enter Hit");
            dummyHp.Invulnerable = false;
            // C. 打死 → Dead，且不回 Root
            // 抬高攻击力快速杀
            player.GetComp<AttrComp>().Atk = 1000f;
            player.GetComp<InputBufferComp>().Push(InputToken.Attack);
            for (int i = 0; i < 25; i++)
                world.Tick(0.05f);
            print($"C lethal: hp={dummyHp.Hp}, dead={dummyHp.IsDead}, state={dummyFsm.Current}");
            if (!dummyHp.IsDead || dummyFsm.Current != ActorStateId.Dead)
                throw new Exception("Expected Dead");
            for (int i = 0; i < 10; i++)
                world.Tick(0.05f);
            if (dummyFsm.Current != ActorStateId.Dead)
                throw new Exception("Dead must not return to Root");
            print("DamageDemo PASSED");
        }
    }
}