using System;
using Combat.Core;

namespace Combat.Demos
{
    public static class SeasonOneDemo
    {
        public static void Run()
        {
            DemoTables.ResetG1MeleeDefaults();
            // 该用例故意使用真实的事件监听器，模拟表现层消费 Cue 和伤害事件。
            var time = new CombatTime();
            var events = new EventBus();
            var cues = CueLibrary.DefaultCombat();
            var listener = new CueListener(cues);
            listener.Bind(events);
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f), events, time);

            int floaters = 0;
            int deadEvents = 0, cleanups = 0;

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake);
            EntityId playerId = player.Id;
            EntityId stakeId = stake.Id;
            events.Subscribe<EvDamage>(e =>
            {
                // 只消费本主线玩家到目标桩的伤害事件，避免其它运行时对象污染飘字计数。
                if (e.Source != playerId || e.Target != stakeId) return;
                floaters++;
                CombatLog.Debug(CombatCategories.SeasonOne, $"[帧{time.Frame}] 伤害飘字 伤害={e.Amount.ToString("F1")} 暴击={e.IsCrit} 击杀={e.IsKill}");
            });
            events.Subscribe<EvEntityDead>(e =>
            {
                if (e.Id == playerId) deadEvents++;
            });
            events.Subscribe<EvEntityCleanup>(e =>
            {
                if (e.Id == playerId) cleanups++;
            });
            var ptf = player.GetComp<TransformComp>();
            var stf = stake.GetComp<TransformComp>();
            var input = player.GetComp<InputBufferComp>();
            var pLoco = player.GetComp<LocomotionComp>();
            var pAttr = player.GetComp<AttributeSet>();
            var pFsm = player.GetComp<StateMachineComp>();
            var pDir = player.GetComp<SkillDirectorComp>();
            var pTags = player.GetComp<TagComp>();
            var loadout = player.GetComp<LoadoutComp>();
            var sAttr = stake.GetComp<AttributeSet>();
            var sBuff = stake.GetComp<BuffComp>();
            bool loadoutValid = loadout.TryGet(SkillSlot.Normal, out var ns, out var nt) &&
                ns == SkillNodeId.G1 && nt == TimelineId.TL_G1;

            ptf.Position = new SimVec3(0, 0, 0);
            ptf.YawDegrees = 0f;
            stf.Position = new SimVec3(0.55f, 0, 0);
            void Step(float dt)
            {
                if (player.IsActive) pLoco.RequestMoveIntent(0, 0);
                world.Tick(dt);
            }
            var trace = new DemoTrace("SeasonOne", CombatCategories.SeasonOne, world, Step);
            trace.Step("初始化 Season One 主线与 Loadout", () => $"{DemoTrace.Snapshot(player)} target={DemoTrace.Snapshot(stake)}");
            trace.Check("Loadout 将 Normal 映射到 G1 Timeline", loadoutValid,
                "Normal=G1 且 Timeline=TL_G1", $"Normal={ns} Timeline={nt}", () => DemoTrace.Snapshot(player));

            // 1. G1 近战应触发 Cue、Fireball、Burn 和伤害飘字。
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("G1 触发近战 Cue", () => listener.CountId(101) >= 1,
                0.02f, 30, () => $"bladeCue={listener.CountId(101)} {DemoTrace.Snapshot(player)}");
            trace.AdvanceUntil("G1 Payload 生成 Fireball 并施加 Burn", () => sBuff.StacksOf(CombatIds.Burn) >= 1,
                0.02f, 30, () => $"Burn层数={sBuff.StacksOf(CombatIds.Burn)} {DemoTrace.Snapshot(stake)}");
            trace.Check("G1 同时完成 Cue、Burn 与伤害事件", listener.CountId(101) >= 1 && sBuff.StacksOf(CombatIds.Burn) >= 1 && floaters >= 1,
                "Cue、Burn 与伤害事件均发生",
                $"bladeCue={listener.CountId(101)} Burn层数={sBuff.StacksOf(CombatIds.Burn)} floaters={floaters}",
                () => DemoTrace.Snapshot(stake));
            trace.AdvanceFor("推进 G1 完整结束", 0.05f, 15, () => DemoTrace.Snapshot(player));

            stf.Position = new SimVec3(0.2f, 0, 0);

            // 2. 在 Cancel 窗口接续输入，进入 G2 并把地面 Burn 叠到上限 3 层。
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("再次启动 G1 并进入 Cancel 窗口", () => pDir.IsPlaying && pTags.Has(CommonTags.Cancel),
                0.02f, 20, () => $"skill={pDir.CurrentSkill} cancel={pTags.Has(CommonTags.Cancel)}");
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("Cancel 接续进入 G2", () => pDir.CurrentSkill == SkillNodeId.G2,
                0.02f, 3, () => $"skill={pDir.CurrentSkill} {DemoTrace.Snapshot(player)}");
            trace.AdvanceFor("推进 G2 Ground AoE Pulse", 0.05f, 50,
                () => $"Burn层数={sBuff.StacksOf(CombatIds.Burn)} {DemoTrace.Snapshot(stake)}");
            int stacks = sBuff.StacksOf(CombatIds.Burn);
            trace.Check("G2 Ground AoE 将 Burn 叠到上限", stacks == 3, "Ground AoE Burn层数=3", $"Burn层数={stacks}", () => DemoTrace.Snapshot(stake));

            // 3. 受击时停止技能并清空输入缓存，恢复后回到 Root。
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("准备可被中断的技能", () => pDir.IsPlaying, 0.02f, 3,
                () => $"skill={pDir.CurrentSkill} {DemoTrace.Snapshot(player)}");
            input.Push(InputToken.Attack);
            pFsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "P1Hit", HitDuration = 0.25f });
            trace.Check("受击中断停止技能并清空输入", !pDir.IsPlaying && !input.HasBuffered && pFsm.Current == ActivityId.Hit,
                "技能停止、输入清空、Activity=Hit", $"playing={pDir.IsPlaying} buffered={input.HasBuffered} Activity={pFsm.Current}",
                () => DemoTrace.Snapshot(player));
            trace.AdvanceUntil("受击恢复 Root", () => pFsm.Current == ActivityId.Root, 0.05f, 8,
                () => DemoTrace.Snapshot(player));
            trace.Check("受击结束后恢复 Root", pFsm.Current == ActivityId.Root, "Activity=Root", $"Activity={pFsm.Current}",
                () => DemoTrace.Snapshot(player));

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var dummy);
            dummy.GetComp<TransformComp>().Position = new SimVec3(100, 0, 0);
            float atk = pAttr.GetFinal(AttrId.Atk);
            dummy.GetComp<AttributeSet>().SetBase(AttrId.Hp, 100f);
            // 4. 修改可配置伤害后清缓存，新的 Bake 结果必须生效。
            var profile = TimelineDefinition.G1Melee;
            var profileDamage = profile.Damage;
            float oldCoeff = profileDamage.Coeff;
            trace.Step("准备 Bake 缓存对比", () => $"Coeff={oldCoeff} {DemoTrace.Snapshot(dummy)}");
            world.Deliver(profile.Bake(), player, dummy, atk);
            float hpAfter1 = dummy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            profileDamage.Coeff = 3f;
            profile.ClearCache();
            dummy.GetComp<AttributeSet>().SetBase(AttrId.Hp, 100f);
            dummy.GetComp<StateMachineComp>().TryEnter(ActivityId.Root, new ActivityEnterArgs { Reason = "reset" });
            world.Deliver(profile.Bake(), player, dummy, atk);
            float hpAfter2 = dummy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            trace.Check("清除 Bake 缓存后使用新的伤害系数", hpAfter2 < hpAfter1,
                "清缓存后新 Coeff 生效", $"原配置后HP={hpAfter1.ToString("F1")} 新配置后HP={hpAfter2.ToString("F1")}",
                () => DemoTrace.Snapshot(dummy));

            // 5. 死亡时清理 Buff、Projectile、AoE，并发布 EvEntityDead。
            input.Push(InputToken.Attack);
            trace.AdvanceFor("准备玩家的活动 Timeline 与 AoE", 0.05f, 1,
                () => $"skill={pDir.CurrentSkill} {DemoTrace.Snapshot(player)}");
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.FireGround) }, player, null, atk, ptf.Position);
            pFsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "SeasonOneKill", Killer = stake.Id });
            trace.AdvanceFor("玩家死亡并清理运行时对象", 0.02f, 1,
                () => $"deadEvents={deadEvents} cleanups={cleanups} {DemoTrace.Snapshot(player)}");
            int leftover = 0;
            var all = world.RegistryActive();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].TryGetComp<ProjectileComp>(out var pr) && pr.OwnerId == player.Id) leftover++;
                if (all[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == player.Id) leftover++;
            }

            trace.Check("玩家死亡时清理 Buff 与 Owner 后代", pFsm.Current == ActivityId.Dead && player.GetComp<BuffComp>().Count == 0 &&
                deadEvents >= 1 && leftover == 0,
                "Activity=Dead、Buff=0、EvEntityDead已发布、Owner后代=0",
                $"Activity={pFsm.Current} Buff={player.GetComp<BuffComp>().Count} deadEvents={deadEvents} leftover={leftover}",
                () => DemoTrace.Snapshot(player));
            trace.Complete("G1 主线、G2 AoE、受击中断、Bake 缓存与死亡清理验证完成");
        }
    }
}
