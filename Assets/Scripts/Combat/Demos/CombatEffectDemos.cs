using System;
using Combat.Core;

namespace Combat.Demos
{
    public static class MeleeDamageDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            var world = DemoWorld.Create(out _, out var time, new FixedRandom(0f), events);
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, CombatCatalog.Burn(), world.Summons);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake);
            EntityId playerId = player.Id;
            EntityId stakeId = stake.Id;
            EntityId iframeTargetId = EntityId.Invalid;
            int dmgCount = 0;
            bool lastCrit = false, lastKill = false;
            int immune = 0;
            events.Subscribe<EvDamage>(e =>
            {
                // 只统计明确的玩家到目标事件，排除同一世界中的其它 Payload 伤害。
                if (e.Source != playerId || (e.Target != stakeId && e.Target != iframeTargetId)) return;
                if (e.Target == stakeId)
                {
                    dmgCount++;
                    lastCrit = e.IsCrit;
                    lastKill = e.IsKill;
                }
            });
            events.Subscribe<EvImmune>(e =>
            {
                if (e.Source == playerId && e.Target == iframeTargetId)
                    immune++;
            });
            player.GetComp<TransformComp>().Position = new SimVec3(0, 0, 0);
            stake.GetComp<TransformComp>().Position = new SimVec3(0.6f, 0, 0);
            var input = player.GetComp<InputBufferComp>();
            var pAttr = player.GetComp<AttributeSet>();
            var sAttr = stake.GetComp<AttributeSet>();
            var sTags = stake.GetComp<TagComp>();
            var sFsm = stake.GetComp<StateMachineComp>();
            void Step(float dt)
            {
                player.GetComp<LocomotionComp>().RequestMoveIntent(0, 0);
                world.Tick(dt);
            }
            var trace = new DemoTrace("MeleeDamage", CombatCategories.MeleeDamage, world, Step);
            trace.Step("初始化近战命中与目标事件过滤", () => $"{DemoTrace.Snapshot(player)} target={DemoTrace.Snapshot(stake)}");

            float hp0 = sAttr.GetBase(AttrId.Hp);
            input.Push(InputToken.Attack);

            // 普通命中应扣血并进入 Hit；同一 Hitbox 期间不能重复命中同一目标。
            trace.AdvanceUntil("等待普通 Hitbox 命中", () => dmgCount >= 1,
                0.02f, 15, () => $"过滤后 damage={dmgCount} {DemoTrace.Snapshot(stake)}");
            trace.Check("普通命中扣血并进入 Hit", sAttr.GetBase(AttrId.Hp) < hp0 && sFsm.Current == ActivityId.Hit,
                "目标HP下降且 Activity=Hit", $"hp={sAttr.GetBase(AttrId.Hp).ToString("F1")} Activity={sFsm.Current}",
                () => $"source={playerId} target={stakeId} {DemoTrace.Snapshot(stake)}");
            int hits = dmgCount;
            // 第二季 G1 配置要求三帧顿帧；在冻结窗口附近检查去重，避免后续 Fireball
            // Payload 的伤害被误认为近战 Hitbox 重复命中。
            trace.AdvanceFor("推进同一 Hitbox 的剩余窗口", 0.02f, 2,
                () => $"过滤后 damage={dmgCount} 目标HP={sAttr.GetBase(AttrId.Hp).ToString("F1")}");
            trace.Check("同一 Hitbox 不重复命中目标", dmgCount == hits,
                "命中次数不增加", $"命中次数={dmgCount}（此前={hits})", () => DemoTrace.Snapshot(stake));
            trace.AdvanceUntil("等待目标从 Hit 恢复", () => sFsm.Current == ActivityId.Root, 0.02f, 30,
                () => DemoTrace.Snapshot(stake));

            // SuperArmor 只免疫 HitStun，不免疫伤害。
            sTags.Add(CommonTags.SuperArmor, 1, TagSource.Debug);
            trace.Step("开启 SuperArmor", () => DemoTrace.Snapshot(stake));
            float hpB = sAttr.GetBase(AttrId.Hp);
            int armorDamageBefore = dmgCount;
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("SuperArmor 下等待再次命中", () => dmgCount > armorDamageBefore,
                0.02f, 15, () => $"damage={dmgCount} {DemoTrace.Snapshot(stake)}");
            trace.Check("SuperArmor 免硬直但不免伤害", sAttr.GetBase(AttrId.Hp) < hpB && sFsm.Current != ActivityId.Hit,
                "HP下降且不在 Hit", $"hp={sAttr.GetBase(AttrId.Hp).ToString("F1")} Activity={sFsm.Current}",
                () => DemoTrace.Snapshot(stake));
            sTags.Remove(CommonTags.SuperArmor, 1, TagSource.Debug);
            trace.AdvanceFor("关闭 SuperArmor 并等待技能结束", 0.02f, 20, () => DemoTrace.Snapshot(stake));

            // CritRate=1 且使用 FixedRandom(0) 时，本次攻击必须暴击。
            pAttr.SetBase(AttrId.CritRate, 1f);
            lastCrit = false;
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("CritRate=1 时等待暴击", () => lastCrit,
                0.02f, 20, () => $"crit={lastCrit} {DemoTrace.Snapshot(stake)}");
            trace.Check("命中事件标记为暴击", lastCrit, "IsCrit=true", $"IsCrit={lastCrit}",
                () => DemoTrace.Snapshot(stake));
            pAttr.SetBase(AttrId.CritRate, 0f);
            trace.AdvanceFor("恢复普通暴击率", 0.02f, 20, () => DemoTrace.Snapshot(player));

            // HP 降到 0 时发布击杀伤害并进入 Dead。
            sAttr.SetBase(AttrId.Hp, 1f);
            lastKill = false;
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("等待致死命中", () => sFsm.Current == ActivityId.Dead,
                0.02f, 60, () => $"IsKill={lastKill} {DemoTrace.Snapshot(stake)}");
            trace.Check("击杀事件与 Dead 状态同时成立", lastKill && sFsm.Current == ActivityId.Dead,
                "IsKill=true 且 Activity=Dead", $"IsKill={lastKill} Activity={sFsm.Current}",
                () => DemoTrace.Snapshot(stake));

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake2);
            stake2.GetComp<TransformComp>().Position = new SimVec3(0.6f, 0, 0);
            iframeTargetId = stake2.Id;

            // IFrame 期间伤害不改变 HP，并发布 EvImmune。
            world.Deliver(new IEffect[] { new IFrameEffect { Duration = 1f } }, player, stake2, pAttr.GetFinal(AttrId.Atk));
            float hpE = stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 1f, CanCrit = false } }, player, stake2, pAttr.GetFinal(AttrId.Atk));
            trace.Check("无敌帧阻止伤害并发布 EvImmune", stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp) == hpE && immune >= 1,
                "HP不变且 Immune>=1", $"HP变化={(stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp) - hpE).ToString("F1")} Immune={immune}",
                () => DemoTrace.Snapshot(stake2));
            trace.Complete("普通命中、去重、霸体、暴击、击杀与无敌帧验证完成");
        }
    }

    // 远程与范围用例：验证投射物命中、AoE 周期脉冲、Burn 叠层和拥有者死亡清理。
    public static class ProjectileAoeDemo
    {
        public static void Run()
        {
            // 同时注册投射物、AoE 和 Burn，覆盖技能 Payload 到运行时对象的完整链路。
            var events = new EventBus();
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f), events);
            var burn = CombatCatalog.Burn();
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, burn, world.Summons);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake);
            player.GetComp<TransformComp>().Position = new SimVec3(0, 0, 0);
            player.GetComp<TransformComp>().YawDegrees = 0f;
            stake.GetComp<TransformComp>().Position = new SimVec3(2.5f, 0, 0);
            var pAttr = player.GetComp<AttributeSet>();
            var sBuff = stake.GetComp<BuffComp>();
            var sAttr = stake.GetComp<AttributeSet>();
            world.Projectiles.TryGet(CombatIds.Fireball, out var fb);
            void Step(float dt)
            {
                if (player.IsActive)
                    player.GetComp<LocomotionComp>().RequestMoveIntent(0, 0);
                world.Tick(dt);
            }
            int CountOwnedRuntime()
            {
                int count = 0;
                var active = world.RegistryActive();
                for (int i = 0; i < active.Count; i++)
                {
                    if (active[i].TryGetComp<ProjectileComp>(out var p) && p.OwnerId == player.Id) count++;
                    if (active[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == player.Id) count++;
                }
                return count;
            }
            var trace = new DemoTrace("ProjectileAoe", CombatCategories.ProjectileAoe, world, Step);
            trace.Step("初始化 Projectile、AoE 与 Burn 注册", () => $"{DemoTrace.Snapshot(player)} target={DemoTrace.Snapshot(stake)}");

            // 直接执行 Fireball 的 OnHit，验证命中后可无脚本地施加 Burn。
            world.Deliver(fb.OnHit, player, stake, pAttr.GetFinal(AttrId.Atk));
            trace.Check("直接执行 Fireball OnHit", sBuff.StacksOf(CombatIds.Burn) == 1,
                "Burn层数=1", $"Burn层数={sBuff.StacksOf(CombatIds.Burn)}", () => DemoTrace.Snapshot(stake));
            world.Deliver(new IEffect[] { new DispelEffect(DispelMode.ByBuffId, CombatIds.Burn) }, player, stake, 0f);
            sAttr.SetBase(AttrId.Hp, 100f);
            player.GetComp<InputBufferComp>().Push(InputToken.Attack);
            trace.AdvanceUntil("技能 Payload 生成 Fireball", () => CountOwnedRuntime() > 0,
                0.02f, 30, () => $"runtime={CountOwnedRuntime()} {DemoTrace.Snapshot(player)}");

            // 通过技能生成 Fireball，验证飞行命中路径同样施加 Burn。
            trace.AdvanceUntil("等待 Fireball 实际飞行命中", () => sBuff.StacksOf(CombatIds.Burn) >= 1,
                0.02f, 60, () => $"Burn层数={sBuff.StacksOf(CombatIds.Burn)} Hp={sAttr.GetBase(AttrId.Hp).ToString("F1")} {DemoTrace.Snapshot(stake)}");
            trace.Check("飞行命中同样施加 Burn", sBuff.StacksOf(CombatIds.Burn) >= 1,
                "Burn层数>=1", $"Burn层数={sBuff.StacksOf(CombatIds.Burn)}", () => DemoTrace.Snapshot(stake));
            stake.GetComp<TransformComp>().Position = new SimVec3(0, 0, 0);

            // Ground AoE 按 PulseInterval 触发 OnPulse，Burn 层数最多为 3。
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.FireGround) }, player, null, pAttr.GetFinal(AttrId.Atk), player.GetComp<TransformComp>().Position);
            trace.AdvanceFor("推进 Ground AoE 的 Pulse 周期", 0.05f, 50,
                () => $"Burn层数={sBuff.StacksOf(CombatIds.Burn)} runtime={CountOwnedRuntime()} {DemoTrace.Snapshot(stake)}");
            trace.Check("AoE Pulse 叠加 Burn 至上限", sBuff.StacksOf(CombatIds.Burn) == 3,
                "Burn层数=3", $"Burn层数={sBuff.StacksOf(CombatIds.Burn)}", () => DemoTrace.Snapshot(stake));
            // 拥有者死亡后，仍存在的 Projectile/AoE 必须被清理。
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball), new SpawnAoeEffect(CombatIds.FireGround) }, player, null,
                pAttr.GetFinal(AttrId.Atk), player.GetComp<TransformComp>().Position);
            trace.AdvanceFor("准备仍在生命周期内的 Projectile 与 AoE", 0.02f, 1,
                () => $"runtime={CountOwnedRuntime()} {DemoTrace.Snapshot(player)}");
            player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "DemoKill" });
            trace.AdvanceFor("Owner 死亡后清理后代运行时对象", 0.02f, 1,
                () => $"runtime={CountOwnedRuntime()} {DemoTrace.Snapshot(player)}");
            trace.Check("死亡清理 Projectile 与 AoE", CountOwnedRuntime() == 0,
                "Owner 后代数量=0", $"Owner 后代数量={CountOwnedRuntime()}", () => DemoTrace.Snapshot(player));
            trace.Complete("直接 OnHit、飞行命中、AoE Pulse 与死亡清理验证完成");
        }
    }

    // 第一季集成用例：把输入、连招、Timeline、Cue、伤害、Buff、Bake 缓存和死亡清理串起来。
}
