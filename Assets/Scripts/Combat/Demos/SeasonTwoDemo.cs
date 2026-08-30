using System;
using Combat.Core;

namespace Combat.Demos
{
    /// <summary>
    /// 第二季交点回归。各系统仍通过 CombatWorld 的既有入口协作，G1 默认配置不在这里改写。
    /// </summary>
    public static class SeasonTwoDemo
    {
        public static void Run()
        {
            // 先恢复 G1 的基线配置，保证本用例不受其它 Demo 修改过的可配置数据影响。
            DemoTables.ResetG1MeleeDefaults();

            // 第二季把闪避、顿帧、AI、光环和召唤物放进同一个 CombatWorld 做交点验证。
            var time = new CombatTime();
            var events = new EventBus();
            var world = new CombatWorld(
                new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()),
                new IntentQueue(), events, time, new FixedRandom(0f));
            CombatCatalog.RegisterDefaults(
                world.Projectiles, world.Aoes, CombatCatalog.Burn(), world.Summons);

            // 事件计数用来验证结算顺序和来源，而不是只检查最终位置或 HP。
            int immune = 0, hitstops = 0, damages = 0;
            int dodgeImmuneEvents = 0;
            int meleeDamageEvents = 0, meleeHitstopEvents = 0;
            int homingDamageEvents = 0, summonDamageEvents = 0;
            EntityId playerId = EntityId.Invalid;
            EntityId guardId = EntityId.Invalid;
            EntityId stakeMeleeId = EntityId.Invalid;
            EntityId stakeHomingId = EntityId.Invalid;
            EntityId stakePetId = EntityId.Invalid;
            EntityId summonId = EntityId.Invalid;
            EntityId lastDmgSrc = EntityId.Invalid;
            EntityId lastDmgDst = EntityId.Invalid;
            events.Subscribe<EvImmune>(e =>
            {
                immune++;
                if (e.Source == guardId && e.Target == playerId)
                    dodgeImmuneEvents++;
            });
            events.Subscribe<EvHitstop>(e =>
            {
                hitstops++;
                if (e.Source == playerId && e.Target == stakeMeleeId)
                    meleeHitstopEvents++;
            });
            events.Subscribe<EvDamage>(e =>
            {
                damages++;
                lastDmgSrc = e.Source;
                lastDmgDst = e.Target;
                if (e.Source == playerId && e.Target == stakeMeleeId)
                    meleeDamageEvents++;
                if (e.Source == playerId && e.Target == stakeHomingId)
                    homingDamageEvents++;
                if (e.Source == summonId && e.Target == stakePetId)
                    summonDamageEvents++;
            });

            // 准备玩家、守卫和四个相互隔离的目标桩，分别用于近战、追踪弹、光环和宠物。
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("melee_guard")), out var guard);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakeMelee);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakeHoming);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakeAura);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stakePet);
            playerId = player.Id;
            guardId = guard.Id;
            stakeMeleeId = stakeMelee.Id;
            stakeHomingId = stakeHoming.Id;
            stakePetId = stakePet.Id;

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
                // 测试只推进逻辑时间；玩家没有额外移动意图，位置变化来自技能或运动系统。
                if (player.IsActive)
                    pLoco.RequestMoveIntent(0f, 0f);
                world.Tick(dt);
            }

            int CountOwnedRuntime(EntityId ownerId)
            {
                int count = 0;
                var active = world.RegistryActive();
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].TryGetComp<ProjectileComp>(out var p) && p.OwnerId == ownerId) count++;
                    if (active[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == ownerId) count++;
                }
                return count;
            }

            var trace = new DemoTrace("SeasonTwo", CombatCategories.SeasonTwo, world, Step);
            trace.Step("初始化第二季跨系统契约场景", () => $"{DemoTrace.Snapshot(player)} guard={DemoTrace.Snapshot(guard)}");

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

            // Dodge IFrame：闪避窗内受击只发布 Immune，不得触发顿帧。
            // 暂时把守卫移出场景，避免它干扰闪避和后续顿帧的时序；后面会恢复出生点。
            gtf.Position = new SimVec3(20f, 0f, 0f);
            gBoard.Home = gtf.Position;
            pInput.Push(Season2Tokens.Dodge);
            trace.AdvanceUntil("Dodge IFrame 窗口开启", () => pTags.Has(CommonTags.Invincible),
                0.02f, 8, () => $"iframe={pTags.Has(CommonTags.Invincible)} {DemoTrace.Snapshot(player)}");

            int hs0 = hitstops;
            float hpP = pAttr.GetBase(AttrId.Hp);
            int immune0 = dodgeImmuneEvents;
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
            trace.Check("IFrame 内受击不扣血且不增加顿帧", pAttr.GetBase(AttrId.Hp) == hpP && dodgeImmuneEvents > immune0 && hitstops == hs0,
                "HP不变、Immune增加、Hitstop不增加",
                $"HP={pAttr.GetBase(AttrId.Hp).ToString("F1")} Immune={immune} Hitstop={hitstops}",
                () => DemoTrace.Snapshot(player));
            trace.AdvanceUntil("Dodge IFrame 结束并恢复 Root", () => pFsm.Current == ActivityId.Root && !pTags.Has(CommonTags.Invincible),
                0.02f, 25, () => DemoTrace.Snapshot(player));
            trace.Check("闪避结束后玩家回到 Root", pFsm.Current == ActivityId.Root,
                "Activity=Root", $"Activity={pFsm.Current}", () => DemoTrace.Snapshot(player));
            ptf.Position = new SimVec3(0f, 0f, 0f);

            // Projectile Freeze 前置：冻结前先生成并让 Homing 弹进入飞行阶段。
            float hpH0 = stakeHoming.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            // 投射物服务会在生成后的首次移动中做碰撞检测，因此先移开近处目标，
            // 让“冻结前已经飞行”可观察；验证近战和光环前再恢复目标布局。
            stakeMelee.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            world.Deliver(
                new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) },
                player, null, pAttr.GetFinal(AttrId.Atk));
            Actor bolt = null;
            trace.AdvanceUntil("生成 Homing Projectile", () =>
            {
                bolt = FindBolt();
                return bolt != null;
            }, 0.02f, 8, () => $"bolt={(bolt != null)} runtime={CountOwnedRuntime(player.Id)}");
            trace.Check("Homing Projectile 成功生成", bolt != null, "Projectile 存在", $"bolt={(bolt != null)}",
                () => bolt == null ? "无 bolt" : DemoTrace.Snapshot(bolt));
            var boltTf = bolt.GetComp<TransformComp>();
            trace.AdvanceFor("冻结前推进 Projectile 飞行", 0.02f, 4,
                () => $"position={boltTf.Position.X.ToString("F3")},{boltTf.Position.Z.ToString("F3")}");
            SimVec3 flew = boltTf.Position;
            stakeMelee.GetComp<TransformComp>().Position = new SimVec3(0.55f, 0f, 0f);
            // 光环测试桩暂时移开，避免 G1 Hitbox 同帧命中两个目标，影响近战断言。
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);

            // G1 Hitstop：近桩命中必须保留默认三帧顿帧。
            pInput.Push(InputToken.Attack);
            bool meleeHit = false;
            int hs1 = meleeHitstopEvents;
            int meleeDamage0 = meleeDamageEvents;
            trace.AdvanceUntil("G1 命中近桩并请求顿帧", () =>
            {
                meleeHit = lastDmgSrc == player.Id && lastDmgDst == stakeMelee.Id &&
                           meleeDamageEvents > meleeDamage0 && meleeHitstopEvents > hs1;
                return meleeHit;
            }, 0.02f, 20, () => $"meleeHit={meleeHit} source={lastDmgSrc} target={lastDmgDst} hitstops={hitstops} {DemoTrace.Snapshot(stakeMelee)}");
            trace.Check("G1 命中事件来源和目标正确", meleeHit,
                "Source=player、Target=stakeMelee 且 Hitstop 增加", $"source={lastDmgSrc} target={lastDmgDst} hitstops={hitstops}",
                () => DemoTrace.Snapshot(stakeMelee));

            // Projectile Freeze：命中结算当帧完成；下一 Tick 开始冻结服务，弹的位置保持不变。
            trace.AdvanceFor("进入顿帧服务阶段", 0.02f, 1,
                () => $"frame={time.Frame} InHitstop={world.InHitstop} left={world.HitstopLeft} actorPos={ptf.Position.X.ToString("F3")} projectilePos={boltTf.Position.X.ToString("F3")}");
            trace.Check("下一逻辑帧进入顿帧状态", world.InHitstop,
                "InHitstop=true", $"InHitstop={world.InHitstop} left={world.HitstopLeft}",
                () => DemoTrace.Snapshot(player));
            SimVec3 frozen = boltTf.Position;
            float frozenActorX = ptf.Position.X;
            float frozenActorZ = ptf.Position.Z;
            int freezeFrame = time.Frame;
            trace.AdvanceFor("顿帧窗口内冻结 Actor 与 Projectile", 0.02f, 2,
                () => $"frame={time.Frame} InHitstop={world.InHitstop} actorPos={ptf.Position.X.ToString("F3")} projectilePos={boltTf.Position.X.ToString("F3")}");
            trace.Check("顿帧期间 Actor 与 Projectile 位置不变", Math.Abs(boltTf.Position.X - frozen.X) <= 1e-4f &&
                Math.Abs(boltTf.Position.Z - frozen.Z) <= 1e-4f && Math.Abs(ptf.Position.X - frozenActorX) <= 1e-4f &&
                Math.Abs(ptf.Position.Z - frozenActorZ) <= 1e-4f,
                "顿帧期间 Actor 与 Projectile 位置保持不变", $"冻结前Actor={frozenActorX.ToString("F3")},{frozenActorZ.ToString("F3")} 当前Actor={ptf.Position.X.ToString("F3")},{ptf.Position.Z.ToString("F3")} 冻结前Projectile={frozen.X.ToString("F3")},{frozen.Z.ToString("F3")} 当前Projectile={boltTf.Position.X.ToString("F3")},{boltTf.Position.Z.ToString("F3")} freezeFrame={freezeFrame} currentFrame={time.Frame}",
                () => DemoTrace.Snapshot(bolt));
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            trace.Step("记录冻前飞行与冻结后位置", () => $"冻前位置={flew.X.ToString("F3")},{flew.Z.ToString("F3")} 冻中位置={boltTf.Position.X.ToString("F3")},{boltTf.Position.Z.ToString("F3")}");

            // Homing Recovery：解冻后继续追踪并命中偏置桩。
            int homingDamage0 = homingDamageEvents;
            bool homingHit = false;
            trace.AdvanceUntil("解冻后 Homing 继续追踪并命中", () =>
            {
                homingHit = stakeHoming.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hpH0 - 0.1f &&
                            homingDamageEvents > homingDamage0;
                return homingHit;
            }, 0.02f, 80, () => $"homingHit={homingHit} targetHp={stakeHoming.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")} {DemoTrace.Snapshot(stakeHoming)}");
            trace.Check("解冻后 Homing 继续追踪并命中", homingHit,
                "Homing 目标HP下降", $"homingHit={homingHit} targetHp={stakeHoming.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}",
                () => DemoTrace.Snapshot(stakeHoming));

            // Knockdown Gate：倒地是单独姿态效果；倒地期间 BT 不得继续 Play(G1)。
            gtf.Position = new SimVec3(2.2f, 0f, 0f);
            gBoard.Home = gtf.Position;
            gBoard.Returning = false;
            gBoard.ClearTarget();
            var gDir = guard.GetComp<SkillDirectorComp>();
            world.Deliver(
                new IEffect[] { new KnockdownEffect { Duration = 0.55f } },
                player, guard, 0f);
            trace.Check("Guard 进入 Knockdown 并获得 Downed Tag", guard.GetComp<StateMachineComp>().Current == ActivityId.Knockdown &&
                guard.GetComp<TagComp>().Has(CommonTags.Downed),
                "Activity=Knockdown 且 Downed=true", $"Activity={guard.GetComp<StateMachineComp>().Current} Downed={guard.GetComp<TagComp>().Has(CommonTags.Downed)}",
                () => DemoTrace.Snapshot(guard));
            trace.AdvanceFor("倒地期间推进并观察 BT 停机", 0.02f, 12,
                () => $"playing={gDir.IsPlaying} skill={gDir.CurrentSkill} {DemoTrace.Snapshot(guard)}");
            trace.Check("倒地期间行为树不再播放 G1", !gDir.IsPlaying || gDir.CurrentSkill != SkillNodeId.G1,
                "倒地期间不播放 G1", $"playing={gDir.IsPlaying} skill={gDir.CurrentSkill}",
                () => DemoTrace.Snapshot(guard));

            // Aura Occupancy：进入、离开和速度恢复。
            float baseSpd = stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed);
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            world.Deliver(
                new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) },
                player, null, 0f, SimVec3.Zero);
            trace.AdvanceFor("Aura 进入目标并施加减速", 0.05f, 1,
                () => $"stacks={stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow)} speed={stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            trace.Check("Aura 进入目标后施加减速", stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) >= 1 &&
                Math.Abs(stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed) - baseSpd * 0.5f) <= 1e-3f,
                "AuraSlow>=1 且速度为0.5倍", $"stacks={stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow)} speed={stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(stakeAura));
            stakeAura.GetComp<TransformComp>().Position = new SimVec3(10f, 0f, 0f);
            trace.AdvanceFor("目标离开 Aura 并驱散减速", 0.05f, 1,
                () => $"stacks={stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow)} speed={stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed).ToString("F2")}");
            trace.Check("Aura 离开目标后清理减速", stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow) == 0 &&
                Math.Abs(stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed) - baseSpd) <= 1e-3f,
                "AuraSlow=0 且速度恢复基础值", $"stacks={stakeAura.GetComp<BuffComp>().StacksOf(CombatIds.AuraSlow)} speed={stakeAura.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed).ToString("F2")}",
                () => DemoTrace.Snapshot(stakeAura));
            trace.Step("Aura Occupancy 契约完成", () => $"owner={player.Id} target={stakeAura.Id}");

            // Summon Ownership：召唤宠物通过自己的 BT/Timeline 命中第三桩，Source 必须是宠物。
            stakePet.GetComp<TransformComp>().Position = new SimVec3(1.3f, 0f, 0f);
            world.Deliver(
                new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) },
                player, null, pAttr.GetFinal(AttrId.Atk));
            Actor pet = null;
            trace.AdvanceUntil("创建玩家 Owner 的召唤物", () =>
            {
                pet = FindPet();
                return pet != null;
            }, 0.02f, 5, () => $"pet={(pet != null)} runtime={CountOwnedRuntime(player.Id)}");
            trace.Check("召唤物成功建立 Owner 关系", pet != null, "召唤物存在", $"pet={(pet != null)}",
                () => pet == null ? "无 pet" : DemoTrace.Snapshot(pet));
            float hpPet0 = stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            lastDmgSrc = EntityId.Invalid;
            summonId = pet.Id;
            int summonDamage0 = summonDamageEvents;
            trace.AdvanceUntil("召唤物通过自己的 BT/Timeline 命中目标", () => stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hpPet0 - 0.1f,
                0.02f, 90, () => $"targetHp={stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")} source={lastDmgSrc} target={lastDmgDst} pet={pet.Id}");
            trace.Check("召唤物攻击命中且 Source 为召唤物", stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hpPet0 &&
                summonDamageEvents > summonDamage0 && lastDmgSrc == pet.Id && lastDmgDst == stakePet.Id,
                "目标HP下降且最后伤害 Source=召唤物", $"targetHp={stakePet.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")} source={lastDmgSrc} pet={pet.Id}",
                () => $"target={stakePet.Id} {DemoTrace.Snapshot(pet)}");
            trace.Step("Summon Ownership 契约完成", () => $"owner={player.Id} pet={pet.Id}");

            // Owner Cleanup：玩家死亡只清理其 Owner 运行时对象；无 Owner 的 guard 必须存活。
            pFsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "S2-7", Killer = guard.Id });
            trace.AdvanceFor("Owner 死亡并清理召唤物、Projectile 与 AoE", 0.02f, 1,
                () => $"pet={(FindPet() != null)} playerRuntime={CountOwnedRuntime(player.Id)} {DemoTrace.Snapshot(player)}");

            int auras = 0;
            var live = world.RegistryActive();
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == player.Id)
                    auras++;
            }

            trace.Check("Owner 死亡后清理所有后代运行时对象", pFsm.Current == ActivityId.Dead && FindPet() == null && FindBolt() == null &&
                CountOwnedRuntime(player.Id) == 0 && auras == 0,
                "玩家 Dead、Pet/Projectile/AoE 均清理", $"Activity={pFsm.Current} pet={(FindPet() != null)} playerRuntime={CountOwnedRuntime(player.Id)} auras={auras}",
                () => DemoTrace.Snapshot(player));
            trace.Check("无 Owner 的 Guard 继续存活", guard.IsActive && guard.GetComp<StateMachineComp>().Current != ActivityId.Dead,
                "无 Owner 的 guard 继续存活", $"guardActive={guard.IsActive} guardActivity={guard.GetComp<StateMachineComp>().Current}",
                () => DemoTrace.Snapshot(guard));

            trace.Check("SeasonTwo 保持 G1 默认 Coeff", Math.Abs(TimelineSO.G1Melee.Damage.Coeff - 1f) <= 1e-4f,
                "SeasonTwo 不修改 G1 默认 Coeff", $"Coeff={TimelineSO.G1Melee.Damage.Coeff}", () => DemoTrace.Snapshot(player));

            CombatLog.Info(CombatCategories.SeasonTwo,
                $"[DemoSummary][SeasonTwo] 八个阶段计数 immune={immune} hitstops={hitstops} damages={damages}");
            trace.Complete($"八个跨系统契约阶段完成 immune={immune} hitstops={hitstops} damages={damages}");
        }

        public static void Regression()
        {
            // 回归入口逐个运行全部 Demo，并在每次运行前恢复共享的 G1 默认配置。
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
                Console.WriteLine($"======== REGRESS {steps[i].name} ========");
                DemoTables.ResetG1MeleeDefaults();
                steps[i].run();
            }

            Console.WriteLine("======== ALL S1+S2 REGRESSION PASSED ========");
        }
    }
}
