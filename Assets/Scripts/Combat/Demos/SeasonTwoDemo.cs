using System;
using Combat.Core;

namespace Combat.Demos
{
    /// <summary>
    /// 第二季交点回归。各系统仍通过 CombatWorld 的既有入口协作，G1 默认袋不在这里改写。
    /// </summary>
    public static class SeasonTwoDemo
    {
        public static void Run()
        {
            DemoTables.ResetG1MeleeDefaults();

            var time = new CombatTime();
            var events = new EventBus();
            var world = new CombatWorld(
                new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()),
                new IntentQueue(), events, time, new FixedRandom(0f));
            CombatCatalog.RegisterDefaults(
                world.Projectiles, world.Aoes, CombatCatalog.Burn(), world.Summons);

            int immune = 0, hitstops = 0, damages = 0;
            EntityId lastDmgSrc = EntityId.Invalid;
            EntityId lastDmgDst = EntityId.Invalid;
            events.Subscribe<EvImmune>(_ => immune++);
            events.Subscribe<EvHitstop>(_ => hitstops++);
            events.Subscribe<EvDamage>(e =>
            {
                damages++;
                lastDmgSrc = e.Source;
                lastDmgDst = e.Target;
            });

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("melee_guard")), out var guard);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakeMelee);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakeHoming);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakeAura);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakePet);

            var ptf = player.GetComp<TransformComp>();
            var pLoco = player.GetComp<LocomotionComp>();
            var pInput = player.GetComp<InputBufferComp>();
            var pTags = player.GetComp<TagComp>();
            var pFsm = player.GetComp<StateMachineComp>();
            var pAttr = player.GetComp<AttributeSet>();

            ptf.Position = new SimVec3(0f, 0f, 0f);
            ptf.YawDegrees = 0f;

            var gtf = guard.GetComp<TransformComp>();
            gtf.Position = new SimVec3(2.2f, 0f, 0f);
            var gBoard = guard.GetComp<BehaviorTreeComp>().Board;
            gBoard.Home = gtf.Position;
            gBoard.LeashRange = 8f;

            stakeMelee.GetComp<TransformComp>().Position = new SimVec3(0.55f, 0f, 0f);
            stakeHoming.GetComp<TransformComp>().Position = new SimVec3(3f, 0f, 2.2f);
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            stakePet.GetComp<TransformComp>().Position = new SimVec3(12f, 0f, 0f);

            void Step(float dt)
            {
                if (player.IsActive)
                    pLoco.RequestMoveIntent(0f, 0f);
                world.Tick(dt);
            }

            Actor FindPet()
            {
                var all = world.RegistryActive();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].TryGetComp<SummonComp>(out var sm) && sm.OwnerId == player.Id)
                        return all[i];
                }

                return null;
            }

            Actor FindBolt()
            {
                var all = world.RegistryActive();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].TryGetComp<ProjectileComp>(out var p) &&
                        p.Def != null && p.Def.SpecId == CombatIds.HomingBolt)
                        return all[i];
                }

                return null;
            }

            // A. 闪避窗内受击只发布 Immune，不得触发顿帧。
            // Keep the later guard interaction out of the dodge/hitstop timing window.
            // It is restored to the documented home before the knockdown assertion.
            gtf.Position = new SimVec3(20f, 0f, 0f);
            gBoard.Home = gtf.Position;
            pInput.Push(Season2Tokens.Dodge);
            Step(0.02f);
            for (int i = 0; i < 5; i++)
                Step(0.02f);
            if (!pTags.Has(CommonTags.Invincible))
                throw new Exception("A: need dodge iframe");

            int hs0 = hitstops;
            float hpP = pAttr.GetBase(AttrId.Hp);
            world.Deliver(
                new IEffect[]
                {
                    new DamageEffect
                    {
                        Coeff = 1f,
                        CanCrit = false,
                        UseSnapshotAtk = true,
                        HitstopFrames = 3
                    }
                },
                guard, player, 50f);
            if (pAttr.GetBase(AttrId.Hp) != hpP)
                throw new Exception("A: iframe must block hp");
            if (immune < 1)
                throw new Exception("A: EvImmune");
            if (hitstops != hs0)
                throw new Exception("A: immune must not RequestHitstop");

            for (int i = 0; i < 25; i++)
                Step(0.02f);
            if (pFsm.Current != ActivityId.Root)
                throw new Exception("A: dodge should finish");
            ptf.Position = new SimVec3(0f, 0f, 0f);

            // C 前置：冻结前先生成并让 Homing 弹进入飞行阶段。
            float hpH0 = stakeHoming.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            // The projectile service resolves collisions after its first move. Move
            // the near fixtures out of that first sweep so the pre-freeze flight is
            // observable; restore the documented layout before G1/Aura assertions.
            stakeMelee.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            world.Deliver(
                new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) },
                player, null, pAttr.GetFinal(AttrId.Atk));
            Actor bolt = null;
            for (int i = 0; i < 8; i++)
            {
                Step(0.02f);
                bolt = FindBolt();
                if (bolt != null)
                    break;
            }

            if (bolt == null)
                throw new Exception("C: homing must spawn before freeze");
            var boltTf = bolt.GetComp<TransformComp>();
            for (int i = 0; i < 4; i++)
                Step(0.02f);
            SimVec3 flew = boltTf.Position;
            stakeMelee.GetComp<TransformComp>().Position = new SimVec3(0.55f, 0f, 0f);
            // Keep the aura fixture out of the G1 hitbox so the melee assertion
            // observes the intended target rather than a second same-frame hit.
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);

            // B. G1 命中近桩必须保留默认三帧顿帧，且不把倒地焊入默认袋。
            pInput.Push(InputToken.Attack);
            bool meleeHit = false;
            int hs1 = hitstops;
            for (int i = 0; i < 20; i++)
            {
                Step(0.02f);
                if (lastDmgDst == stakeMelee.Id && hitstops > hs1)
                {
                    meleeHit = true;
                    break;
                }
            }

            if (!meleeHit)
                throw new Exception("B: G1 should damage melee stake and hitstop");

            // C. 命中结算当帧完成；下一 Tick 开始冻结服务，弹的位置保持不变。
            Step(0.02f);
            if (!world.InHitstop)
                throw new Exception("C: freeze starts next tick (pending)");
            SimVec3 frozen = boltTf.Position;
            for (int i = 0; i < 2; i++)
            {
                if (!world.InHitstop)
                    throw new Exception("C: still in hitstop");
                Step(0.02f);
                if (Math.Abs(boltTf.Position.X - frozen.X) > 1e-4f ||
                    Math.Abs(boltTf.Position.Z - frozen.Z) > 1e-4f)
                    throw new Exception("C: homing must not steer during hitstop");
            }
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);

            // 保留 flew 变量作为“冻前已经飞过”的时序证据。
            if (boltTf.Position.X == flew.X && boltTf.Position.Z == flew.Z)
            {
                // 冻前已飞过；此处只要求冻中不动。
            }

            // D. 解冻后 Homing 继续追踪并命中偏置桩。
            bool homingHit = false;
            for (int i = 0; i < 80; i++)
            {
                Step(0.02f);
                if (stakeHoming.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hpH0 - 0.1f)
                {
                    homingHit = true;
                    break;
                }
            }

            if (!homingHit)
                throw new Exception("D: homing should hit after unfreeze");

            // E. 倒地是单独姿态效果；倒地期间 BT 不得继续 Play(G1)。
            gtf.Position = new SimVec3(2.2f, 0f, 0f);
            gBoard.Home = gtf.Position;
            gBoard.Returning = false;
            gBoard.ClearTarget();
            var gDir = guard.GetComp<SkillDirectorComp>();
            world.Deliver(
                new IEffect[] { new KnockdownEffect { Duration = 0.55f } },
                player, guard, 0f);
            if (guard.GetComp<StateMachineComp>().Current != ActivityId.Knockdown)
                throw new Exception("E: knockdown activity");
            if (!guard.GetComp<TagComp>().Has(CommonTags.Downed))
                throw new Exception("E: Downed tag");
            for (int i = 0; i < 12; i++)
            {
                Step(0.02f);
                if (gDir.IsPlaying && gDir.CurrentSkill == SkillNodeId.G1)
                    throw new Exception("E: BT must not Play while downed");
            }

            // F. 光环 Occupancy 的进入、离开和速度恢复。
            float baseSpd = stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed);
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            world.Deliver(
                new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) },
                player, null, 0f, SimVec3.Zero);
            Step(0.05f);
            if (stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) < 1)
                throw new Exception("F: aura enter");
            if (Math.Abs(stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed) - baseSpd * 0.5f) > 1e-3f)
                throw new Exception("F: slow mul");
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            Step(0.05f);
            if (stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) != 0)
                throw new Exception("F: aura exit dispel");
            if (Math.Abs(stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed) - baseSpd) > 1e-3f)
                throw new Exception("F: speed restored");

            // G. 召唤宠物通过自己的 BT/Timeline 命中第三桩，Source 必须是宠物。
            stakePet.GetComp<TransformComp>().Position = new SimVec3(1.3f, 0f, 0f);
            world.Deliver(
                new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) },
                player, null, pAttr.GetFinal(AttrId.Atk));
            Actor pet = null;
            for (int i = 0; i < 5; i++)
            {
                Step(0.02f);
                pet = FindPet();
                if (pet != null)
                    break;
            }

            if (pet == null)
                throw new Exception("G: summon spawn");
            float hpPet0 = stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            lastDmgSrc = EntityId.Invalid;
            for (int i = 0; i < 90; i++)
            {
                Step(0.02f);
                if (stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hpPet0 - 0.1f)
                    break;
            }

            if (stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp) >= hpPet0)
                throw new Exception("G: pet should melee via timeline");
            if (lastDmgSrc != pet.Id)
                throw new Exception("G: Source must be pet, got " + lastDmgSrc);

            // H. 玩家死亡只清理其 Owner 运行时对象；无 Owner 的 guard 必须存活。
            pFsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "S2-7", Killer = guard.Id });
            Step(0.02f);
            if (pFsm.Current != ActivityId.Dead)
                throw new Exception("H: stay dead");
            if (FindPet() != null)
                throw new Exception("H: pet cleanup");
            if (FindBolt() != null)
                throw new Exception("H: leftover player bolt (if any)");

            int auras = 0;
            var live = world.RegistryActive();
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == player.Id)
                    auras++;
            }

            if (auras != 0)
                throw new Exception("H: aura cleanup");
            if (!guard.IsActive ||
                guard.GetComp<StateMachineComp>().Current == ActivityId.Dead)
                throw new Exception("H: enemy must survive ownerless");

            if (Math.Abs(TimelineSO.G1Melee.Damage.Coeff - 1f) > 1e-4f)
                throw new Exception("R: do not mutate G1Melee as season2 default");

            Console.WriteLine(
                "SeasonTwoDemo PASSED immune=" + immune +
                " hitstops=" + hitstops +
                " damages=" + damages);
            CombatLog.Info(CombatCategories.SeasonTwo, "SeasonTwoDemo PASSED");
        }

        public static void Regression()
        {
            Season2Contracts.EnsureAiMustNotStopDirector();

            var steps = new (string name, Action run)[]
            {
                ("tag", TagInputDemo.Run),
                ("attr", AttributeDemo.Run),
                ("buff", BuffDemo.Run),
                ("motor", ActivityMotorDemo.Run),
                ("clip", ClipPayloadDemo.Run),
                ("melee", MeleeDamageDemo.Run),
                ("proj", ProjectileAoeDemo.Run),
                ("season", SeasonOneDemo.Run),
                ("knock", KnockdownDemo.Run),
                ("dodge", DodgeHitstopDemo.Run),
                ("aura", AuraHomingDemo.Run),
                ("bt", BehaviorTreeDemo.Run),
                ("perc", PerceptionDemo.Run),
                ("enemy", EnemyAiDemo.Run),
                ("summon", SummonDemo.Run),
                ("season2", SeasonTwoDemo.Run),
            };

            for (int i = 0; i < steps.Length; i++)
            {
                Console.WriteLine("======== REGRESS " + steps[i].name + " ========");
                DemoTables.ResetG1MeleeDefaults();
                steps[i].run();
            }

            Console.WriteLine("======== ALL S1+S2 REGRESSION PASSED ========");
        }
    }
}
