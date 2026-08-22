using System;
using Combat.Core;
using UnityEngine;

namespace Combat.AoEDemo
{
    public class AoEDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();

            var aoeSpecs = new AoESpecLibrary();
            aoeSpecs.Register(new AoESpec
            {
                Id = 501,
                Shape = AoEShapeType.Circle,
                Radius = 1.5f,
                AttackSpecValue = 42,
                OffsetX = 0f,
                OffsetY = 1f,
                OffsetZ = 0f
            });

            var pulseSpecs = new PulseZoneSpecLibrary();
            pulseSpecs.Register(new PulseZoneSpec
            {
                Id = 701,
                Radius = 1.2f,
                Lifetime = 1.2f,
                Interval = 0.4f,
                AttackSpecValue = 42,
                OffsetX = 1.5f,
                OffsetY = 1f,
                AoESpecValue = 0
            });

            // 技能轴：瞬时震爆 + 丢火池
            var tl = new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.55f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.10f, EndTime = -1f,
                        Type = EffectType.AoEBurst,
                        AoESpecValue = 501
                    },
                    new TimelineKey
                    {
                        Time = 0.20f, EndTime = -1f,
                        Type = EffectType.PulseZone,
                        PulseZoneSpecValue = 701
                    },
                }
            };

            var timelines = new TimelineLibrary();
            timelines.Register(tl);

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

            var projSpecs = new ProjectileSpecLibrary(); // 本 Demo 可不注册弹
            var attackSpecs = new AttackSpecLibrary();
            attackSpecs.Register(new AttackSpec
            {
                Id = 42,
                Power = 0.5f,
                StunDuration = 0.25f,
                ApplyHitStun = true
            });

            var effects = new EffectFactory(intents, aoeSpecs);
            var factory = new CombatActorFactory(time, combos, timelines, effects, intents, projSpecs);
            var world = new CombatWorld(factory, intents, events, time);

            var projectileService = new ProjectileService(world, intents, projSpecs, factory);
            var pulseZoneService = new PulseZoneService(world, intents, pulseSpecs, aoeSpecs, factory);
            var hitDetect = new HitDetectService(world, intents, projectileService);
            var aoeService = new AoEService(world, intents);
            var damage = new DamageService(world, intents, attackSpecs, events);
            var despawn = new DespawnService(world, intents);

            int dmgCount = 0;
            events.Subscribe<DamageAppliedEvent>(e =>
            {
                if (e.Amount <= 0f) return;
                dmgCount++;
                print(
                    $"[F{time.Frame}] DMG={e.Amount:F1} tgt={e.Target} hp={e.HpAfter:F1} hit={e.EnteredHit}");
            });

            world.AddServicePhase(() => projectileService.Tick());
            world.AddServicePhase(() => pulseZoneService.Tick());
            world.AddServicePhase(() => hitDetect.Tick());
            world.AddServicePhase(() => aoeService.Tick());
            world.AddServicePhase(() => damage.Tick());
            world.AddServicePhase(() => despawn.Tick());

            var playerId = world.SpawnActor(new ActorSpawnSpec("fighter"));
            var dummyId = world.SpawnActor(new ActorSpawnSpec("dummy"));
            world.TryGetActor(playerId, out var player);
            world.TryGetActor(dummyId, out var dummy);

            player.GetComp<TransformComp>().Teleport(new SimVec3(0f, 0f, 0f));
            // 木桩在身前，落在 Burst 半径与火池偏移附近
            dummy.GetComp<TransformComp>().Teleport(new SimVec3(1.2f, 1f, 0f));
            dummy.GetComp<AttrComp>().Setup(0f, 0f, 80f);
            dummy.GetComp<HealthComp>().ResetToFull();

            float hp0 = dummy.GetComp<HealthComp>().Hp;

            player.GetComp<InputBufferComp>().Push(InputToken.Attack);

            // 跑约 2 秒：瞬时至少 1 次，火池应按 interval 多次
            for (int i = 0; i < 40; i++)
                world.Tick(0.05f);

            float hp1 = dummy.GetComp<HealthComp>().Hp;
            print($"hp {hp0} -> {hp1}, dmgEvents={dmgCount}, state={dummy.GetComp<StateMachineComp>().Current}");

            if (dmgCount < 2)
                throw new Exception("Expected burst + at least one pulse tick");
            if (hp1 >= hp0)
                throw new Exception("Expected HP reduced by AoE");

            // 火池寿命 1.2s，再过一会应被 Despawn，Active 不再含 pulse
            for (int i = 0; i < 20; i++)
                world.Tick(0.05f);

            int pulseAlive = 0;
            foreach (var a in world.CopyActiveActors())
            {
                if (a.TryGetComp<PulseZoneComp>(out _))
                    pulseAlive++;
            }
            print($"pulseAlive={pulseAlive} (expect 0)");
            if (pulseAlive != 0)
                throw new Exception("Pulse zone should despawn after lifetime");

            print("AoEDemo PASSED");
        }
    }
}
