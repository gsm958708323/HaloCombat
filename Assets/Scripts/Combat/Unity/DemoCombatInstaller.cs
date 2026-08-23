using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class DemoCombatSession
    {
        public CombatWorld World { get; }
        public CombatTime Time { get; }
        public IntentQueue Intents { get; }
        public EventBus Events { get; }
        public EntityId PlayerId { get; private set; }
        public EntityId DummyMeleeId { get; private set; }
        public EntityId DummyRangedId { get; private set; }

        readonly CombatActorFactory _factory;

        public DemoCombatSession()
        {
            Time = new CombatTime();
            Intents = new IntentQueue();
            Events = new EventBus();

            // ---------- Specs ----------
            var timelines = BuildTimelines();
            var combos = BuildCombos();
            var projSpecs = BuildProjectiles();
            var aoeSpecs = BuildAoE();
            var pulseSpecs = BuildPulse();
            var attackSpecs = BuildAttacks();

            var effects = new EffectFactory(Intents, aoeSpecs);
            _factory = new CombatActorFactory(Time, combos, timelines, effects, Intents, projSpecs);

            World = new CombatWorld(_factory, Intents, Events, Time);

            var projectileService = new ProjectileService(World, Intents, projSpecs, _factory);
            var pulseZoneService = new PulseZoneService(World, Intents, pulseSpecs, aoeSpecs, _factory);
            var hitDetect = new HitDetectService(World, Intents, projectileService);
            var aoeService = new AoEService(World, Intents);
            var damage = new DamageService(World, Intents, attackSpecs, Events);
            var despawn = new DespawnService(World, Intents);

            // 帧序锁死（与课程一致）
            World.AddServicePhase(() => projectileService.Tick());
            World.AddServicePhase(() => pulseZoneService.Tick());
            World.AddServicePhase(() => hitDetect.Tick());
            World.AddServicePhase(() => aoeService.Tick());
            World.AddServicePhase(() => damage.Tick());
            World.AddServicePhase(() => despawn.Tick());
        }

        public void SpawnDemoActors()
        {
            PlayerId = World.SpawnActor(new ActorSpawnSpec("fighter"));
            DummyMeleeId = World.SpawnActor(new ActorSpawnSpec("dummy"));
            DummyRangedId = World.SpawnActor(new ActorSpawnSpec("dummy"));

            World.TryGetActor(PlayerId, out var player);
            World.TryGetActor(DummyMeleeId, out var melee);
            World.TryGetActor(DummyRangedId, out var ranged);

            player.GetComp<TransformComp>().Teleport(new SimVec3(0f, 0f, 0f));
            player.GetComp<TeamComp>().SetTeam(0);
            player.GetComp<AttrComp>().BaseAtk = 18f;
            player.GetComp<AttrComp>().BaseDef = 1f;
            player.GetComp<AttrComp>().BaseMaxHp = 100f;
            player.GetComp<HealthComp>().ResetToFull();

            // Buff：开场攻击强化（验收属性总成）
            player.GetComp<BuffComp>().Apply(new BuffApplyArgs(
                new BuffTypeId(9001),
                stacks: 1,
                duration: 999f,
                source: TagSource.Debug,
                refreshIfExist: true));
            // 属性侧登记 flat（与 Buff 联动的最小接法，见 4.11.5）
            player.GetComp<AttrComp>().AddFlat(new BuffTypeId(9001), 7f);

            melee.GetComp<TransformComp>().Teleport(new SimVec3(1.4f, 0f, 0f));
            melee.GetComp<TeamComp>().SetTeam(1);
            melee.GetComp<AttrComp>().Setup(0f, 0f, 60f);
            melee.GetComp<HealthComp>().ResetToFull();

            ranged.GetComp<TransformComp>().Teleport(new SimVec3(4.5f, 1f, 0f));
            ranged.GetComp<TeamComp>().SetTeam(1);
            ranged.GetComp<AttrComp>().Setup(0f, 0f, 45f);
            ranged.GetComp<HealthComp>().ResetToFull();
        }

        static TimelineLibrary BuildTimelines()
        {
            var lib = new TimelineLibrary();

            // G1：Cancel 区间 + 轴位移 + 出弹
            lib.Register(new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.55f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.05f, EndTime = 0.42f,
                        Type = EffectType.AddTag,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    new TimelineKey
                    {
                        Time = 0.08f, EndTime = 0.28f,
                        Type = EffectType.MoveOffset,
                        MoveX = 3.5f, MoveAsVelocity = true
                    },
                    new TimelineKey
                    {
                        Time = 0.18f, EndTime = -1f,
                        Type = EffectType.AnimSignal,
                        AnimSignalName = "G1_Slash"
                    },
                    new TimelineKey
                    {
                        Time = 0.22f, EndTime = -1f,
                        Type = EffectType.SpawnProjectile,
                        ProjectileSpecValue = 901
                    },
                }
            });

            // G2：取消技 + 周身爆 + 丢火池
            lib.Register(new TimelineSO
            {
                Id = TimelineId.TL_G2,
                Duration = 0.50f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.00f, EndTime = 0.30f,
                        Type = EffectType.AddTag,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    new TimelineKey
                    {
                        Time = 0.05f, EndTime = 0.20f,
                        Type = EffectType.MoveOffset,
                        MoveX = 2.0f, MoveAsVelocity = true
                    },
                    new TimelineKey
                    {
                        Time = 0.10f, EndTime = -1f,
                        Type = EffectType.AoEBurst,
                        AoESpecValue = 501
                    },
                    new TimelineKey
                    {
                        Time = 0.16f, EndTime = -1f,
                        Type = EffectType.PulseZone,
                        PulseZoneSpecValue = 701
                    },
                }
            });

            return lib;
        }

        static ComboTableSO BuildCombos()
        {
            return new ComboTableSO
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
                    },
                    new ComboEntry
                    {
                        PreSkills = new[] { SkillNodeId.G1 },
                        Input = InputToken.Attack,
                        RequiredTags = new[] { CommonTags.Cancel.Value },
                        Priority = 10,
                        ToSkill = SkillNodeId.G2,
                        Timeline = TimelineId.TL_G2
                    },
                }
            };
        }

        static ProjectileSpecLibrary BuildProjectiles()
        {
            var lib = new ProjectileSpecLibrary();
            lib.Register(new ProjectileSpec
            {
                Id = ProjectileSpecId.Bolt901,
                Speed = 14f,
                Lifetime = 2f,
                Radius = 0.3f,
                DirX = 1f,
                AttackSpecValue = 42,
                Pierce = false,
                SpawnOffsetX = 0.7f,
                SpawnOffsetY = 1.0f
            });
            return lib;
        }

        static AoESpecLibrary BuildAoE()
        {
            var lib = new AoESpecLibrary();
            lib.Register(new AoESpec
            {
                Id = 501,
                Shape = AoEShapeType.Circle,
                Radius = 1.6f,
                AttackSpecValue = 43,
                OffsetX = 0.8f,
                OffsetY = 0.5f
            });
            return lib;
        }

        static PulseZoneSpecLibrary BuildPulse()
        {
            var lib = new PulseZoneSpecLibrary();
            lib.Register(new PulseZoneSpec
            {
                Id = 701,
                Radius = 1.3f,
                Lifetime = 2.0f,
                Interval = 0.45f,
                AttackSpecValue = 44,
                OffsetX = 1.6f,
                OffsetY = 0.2f
            });
            return lib;
        }

        static AttackSpecLibrary BuildAttacks()
        {
            var lib = new AttackSpecLibrary();
            lib.Register(new AttackSpec { Id = 42, Power = 1.0f, StunDuration = 0.30f, ApplyHitStun = true });
            lib.Register(new AttackSpec { Id = 43, Power = 0.8f, StunDuration = 0.28f, ApplyHitStun = true });
            lib.Register(new AttackSpec { Id = 44, Power = 0.35f, StunDuration = 0.15f, ApplyHitStun = true });
            return lib;
        }
    }
}
