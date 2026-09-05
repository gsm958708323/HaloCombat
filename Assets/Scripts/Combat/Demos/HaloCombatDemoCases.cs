using System;
using Combat.Core;

namespace Combat.Demos
{
    // 基础输入用例：验证 Tag 的计数语义，以及输入缓冲的有效时间窗口。
    public static class TagInputDemo
    {
        public static void Run()
        {
            // 每个基础用例都创建独立世界，避免前一个用例的状态污染当前验证。
            var world = NewWorld();
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var tags = actor.GetComp<TagComp>();
            var input = actor.GetComp<InputBufferComp>();

            var trace = new DemoTrace("TagInput", CombatCategories.TagInput, world);
            trace.Step("初始化 Tag 与输入缓冲", () => DemoTrace.Snapshot(actor));

            // Tag 是可叠加计数；移除足够层数后 Has 应返回 false。
            tags.Add(CommonTags.Grounded, 1, TagSource.Debug);
            tags.Add(CommonTags.Cancel, 1, TagSource.Debug);
            trace.Check("添加 Grounded 与 Cancel", tags.Has(CommonTags.Grounded) && tags.Stack(CommonTags.Cancel) == 1,
                "Grounded=true Cancel层数=1", $"Grounded={tags.Has(CommonTags.Grounded)} Cancel层数={tags.Stack(CommonTags.Cancel)}",
                () => DemoTrace.Snapshot(actor));
            tags.Remove(CommonTags.Cancel, 1, TagSource.Debug);
            trace.Check("移除 Cancel 后归零", !tags.Has(CommonTags.Cancel), "Cancel=false",
                $"Cancel={tags.Has(CommonTags.Cancel)}", () => DemoTrace.Snapshot(actor));

            // 输入缓冲默认窗口为 0.2 秒；推进 0.25 秒后应过期。
            input.Push(InputToken.Attack);
            trace.Check("写入 Attack 输入", input.TryPeek(out _), "缓冲存在", $"存在={input.HasBuffered}",
                () => $"token=Attack {DemoTrace.Snapshot(actor)}");
            trace.AdvanceFor("推进输入有效窗口之外", 0.25f, 1,
                () => $"buffered={input.HasBuffered} {DemoTrace.Snapshot(actor)}");
            trace.Check("输入缓冲自动过期", !input.TryPeek(out _), "Attack 不可读取",
                $"可读取={input.HasBuffered}", () => DemoTrace.Snapshot(actor));
            trace.Complete("Tag 计数与输入窗口验证完成");
        }

        static CombatWorld NewWorld()
            => DemoWorld.Create(out _, out _, new FixedRandom(0f));
    }

    // 属性用例：验证 Add/Mul/Override 的计算顺序、Modifier 来源清理和 HP 上限钳制。
    public static class AttributeDemo
    {
        public static void Run()
        {
            // 使用 stake 作为纯属性载体，不引入输入、连招等玩家行为组件。
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f));
            var id = world.SpawnActor(new ActorSpawnSpec("stake"));
            world.TryGetActor(id, out var actor);
            var attr = actor.GetComp<AttributeSet>();
            var trace = new DemoTrace("Attribute", CombatCategories.Attribute, world);
            trace.Step("初始化属性载体", () => $"Hp={attr.GetBase(AttrId.Hp)} Atk={attr.GetFinal(AttrId.Atk)} {DemoTrace.Snapshot(actor)}");

            // 普通 Modifier 按 (Base + Add) * Mul 计算。
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 50f, SourceId = 1 });
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Mul, Value = 1.2f, SourceId = 1 });
            trace.Check("应用 Add 与 Mul Modifier", Math.Abs(attr.GetFinal(AttrId.Atk) - 72f) < 1e-3f,
                "Atk=72", $"Atk={attr.GetFinal(AttrId.Atk).ToString("F1")}", () => DemoTrace.Snapshot(actor));

            // Override 存在时直接覆盖普通 Add/Mul 结果。
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Override, Value = 999f, SourceId = 2, Priority = 1 });
            trace.Check("应用高优先级 Override", Math.Abs(attr.GetFinal(AttrId.Atk) - 999f) < 1e-3f,
                "Atk=999", $"Atk={attr.GetFinal(AttrId.Atk).ToString("F1")}", () => DemoTrace.Snapshot(actor));

            // 按来源移除后，属性应恢复到此前的计算结果，再恢复到基础值。
            attr.RemoveBySource(2);
            trace.Check("移除 Override 后恢复 Add/Mul 结果", Math.Abs(attr.GetFinal(AttrId.Atk) - 72f) <= 1e-3f,
                "Atk=72", $"Atk={attr.GetFinal(AttrId.Atk).ToString("F1")}", () => DemoTrace.Snapshot(actor));
            attr.RemoveBySource(1);
            trace.Check("移除全部 Modifier 后恢复基础值", Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) <= 1e-3f,
                "Atk=10", $"Atk={attr.GetFinal(AttrId.Atk).ToString("F1")}", () => DemoTrace.Snapshot(actor));

            // HP 写入不能超过 MaxHp。
            attr.SetBase(AttrId.Hp, 800f);
            trace.Check("写入 HP 时限制在 MaxHp", Math.Abs(attr.GetBase(AttrId.Hp) - 100f) <= 1e-3f,
                "Hp=100", $"Hp={attr.GetBase(AttrId.Hp).ToString("F1")}", () => DemoTrace.Snapshot(actor));
            trace.Complete("属性计算与清理验证完成");
        }
    }

    // Buff 用例：验证叠层、周期事件、互斥组，以及驱散时附带状态的完整清理。
    public static class BuffDemo
    {
        public static void Run()
        {
            // 下面的三个 DurationSpec 分别代表可叠层 Burn、互斥的 Wet 和 Ignite。
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f));
            var id = world.SpawnActor(new ActorSpawnSpec("stake"));
            world.TryGetActor(id, out var actor);
            var attr = actor.GetComp<AttributeSet>();
            var tags = actor.GetComp<TagComp>();
            var buffs = actor.GetComp<BuffComp>();
            var slow = new TagId(2101);
            int periodHits = 0;
            var trace = new DemoTrace("Buff", CombatCategories.Buff, world);
            trace.Step("初始化 Buff、Modifier 与周期效果", () => DemoTrace.Snapshot(actor));
            var burn = new DurationSpec
            {
                BuffId = 1,
                Duration = 3f,
                TickInterval = 1f,
                MaxStacks = 3,
                Stack = StackPolicy.AddStack,
                Modifiers = new[] { new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 5f } },
                GrantedTags = new[] { slow },
                OnPeriod = new IEffect[] { new CallbackEffect(() => periodHits++) }
            };
            var wet = new DurationSpec
            {
                BuffId = 2,
                Duration = 5f,
                MutexGroup = 10,
                GrantedTags = new[] { new TagId(2102) },
                Modifiers = new[] { new Modifier { Attr = AttrId.MoveSpeed, Op = ModOp.Mul, Value = 0.5f } }
            };
            var ignite = new DurationSpec
            {
                BuffId = 3,
                Duration = 5f,
                MutexGroup = 10,
                Modifiers = new[] { new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 100f } }
            };

            // AddStack 只能把同一个 Buff 实例叠到 MaxStacks，不会创建第 4 层。
            for (int i = 0; i < 4; i++)
                world.Deliver(new IEffect[] { new ApplyDurationEffect(burn) }, actor, actor, 0f);
            trace.Check("Burn 叠层达到上限", buffs.StacksOf(1) == 3, "层数=3", $"层数={buffs.StacksOf(1)}",
                () => $"Atk={attr.GetFinal(AttrId.Atk).ToString("F1")} {DemoTrace.Snapshot(actor)}");

            // 当前语义：层数是运行时计数，属性 Modifier 只在实例创建时挂载一次。
            trace.Check("叠层不重复挂载 Modifier", Math.Abs(attr.GetFinal(AttrId.Atk) - 15f) <= 1e-3f && attr.ModCount == 1,
                "Atk=15 且 Modifier数量=1", $"Atk={attr.GetFinal(AttrId.Atk).ToString("F1")} Modifier数量={attr.ModCount}",
                () => DemoTrace.Snapshot(actor));
            // Buff 出生帧不立即触发周期效果；下一帧累计满 1 秒后触发一次。
            trace.AdvanceFor("推进不足一个周期", 0.5f, 1,
                () => $"周期回调次数={periodHits} {DemoTrace.Snapshot(actor)}");
            trace.Check("出生后不足周期不触发回调", periodHits == 0, "回调次数=0", $"回调次数={periodHits}",
                () => DemoTrace.Snapshot(actor));
            trace.AdvanceFor("推进至一个周期", 1f, 1,
                () => $"周期回调次数={periodHits} {DemoTrace.Snapshot(actor)}");
            trace.Check("周期效果触发一次", periodHits == 1, "回调次数=1", $"回调次数={periodHits}",
                () => DemoTrace.Snapshot(actor));
            // Wet 与 Ignite 共享互斥组 10；应用 Ignite 会移除 Wet 及其授予的 Tag。
            world.Deliver(new IEffect[] { new ApplyDurationEffect(wet) }, actor, actor, 0f);
            world.Deliver(new IEffect[] { new ApplyDurationEffect(ignite) }, actor, actor, 0f);
            trace.Check("互斥组替换 Wet", !tags.Has(new TagId(2102)), "Wet Tag=false",
                $"Wet Tag={tags.Has(new TagId(2102))}", () => $"Burn层数={buffs.StacksOf(1)} {DemoTrace.Snapshot(actor)}");
            // 按来源驱散所有 Buff，并验证 Modifier、Tag、Buff 实例都被清理。
            world.Deliver(new IEffect[] { new DispelEffect(DispelMode.BySource, BuffComp.Pack(actor)) }, actor, actor, 0f);
            trace.Check("驱散后清理 Buff、Tag 与 Modifier",
                buffs.Count == 0 && !tags.Has(slow) && attr.ModCount == 0 && Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) <= 1e-3f,
                "Buff=0 Tag=false Modifier数量=0 Atk=10",
                $"Buff={buffs.Count} Tag={tags.Has(slow)} Modifier数量={attr.ModCount} Atk={attr.GetFinal(AttrId.Atk).ToString("F1")}",
                () => DemoTrace.Snapshot(actor));
            trace.Complete("叠层、周期、互斥与驱散验证完成");
        }
    }

    // 活动与运动用例：验证移动、跳跃、重力、受击恢复和 Dead 状态的终止语义。
    public static class ActivityMotorDemo
    {
        public static void Run()
        {
            // Activity 决定当前 Actor 可以使用哪种运动策略，Locomotion 负责实际积分位置。
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f));
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var fsm = actor.GetComp<StateMachineComp>();
            var tf = actor.GetComp<TransformComp>();
            var loco = actor.GetComp<LocomotionComp>();
            var tags = actor.GetComp<TagComp>();
            var input = actor.GetComp<InputBufferComp>();
            var director = actor.GetComp<SkillDirectorComp>();
            var trace = new DemoTrace("ActivityMotor", CombatCategories.ActivityMotor, world);
            trace.Step("初始化状态机与运动组件", () => DemoTrace.Snapshot(actor));

            // Root 状态允许移动，并保持 Grounded。
            loco.RequestMoveIntent(1f, 0f);
            world.Tick(0.10f);
            trace.Check("Root 状态推进移动", tf.Position.X > 0f && tags.Has(CommonTags.Grounded),
                "位置X>0 且 Grounded=true", $"位置X={tf.Position.X.ToString("F2")} Grounded={tags.Has(CommonTags.Grounded)}",
                () => DemoTrace.Snapshot(actor));
            // Jump 不切换到独立 Activity，而是让 Root 进入 Airborne；空中仍可接 Attack。
            loco.RequestMoveIntent(0f, 0f);
            input.Push(InputToken.Jump);
            world.Tick(0.02f);
            trace.Check("Root 内进入空中状态", fsm.Current == ActivityId.Root && tags.Has(CommonTags.Airborne),
                "Activity=Root 且 Airborne=true", $"Activity={fsm.Current} Airborne={tags.Has(CommonTags.Airborne)}",
                () => DemoTrace.Snapshot(actor));
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            trace.Check("空中接续 Attack", fsm.Current == ActivityId.Attack, "Activity=Attack", $"Activity={fsm.Current}",
                () => DemoTrace.Snapshot(actor));
            trace.AdvanceFor("观察空中重力积分", 0.05f, 5,
                () => $"Y={tf.Position.Y.ToString("F2")} {DemoTrace.Snapshot(actor)}");
            float descendingY = tf.Position.Y;
            world.Tick(0.05f);
            trace.Check("空中继续受重力影响", tf.Position.Y < descendingY, "Y 下降", $"此前Y={descendingY.ToString("F2")} 当前Y={tf.Position.Y.ToString("F2")}",
                () => DemoTrace.Snapshot(actor));
            trace.AdvanceUntil("等待落地恢复 Root", () => fsm.Current == ActivityId.Root, 0.05f, 30,
                () => $"Activity={fsm.Current} {DemoTrace.Snapshot(actor)}");
            // Hit 会停止技能并在计时结束后回到 Root；Dead 则阻止返回 Root。
            fsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "Hit", HitDuration = 0.30f, IFrameDuration = 0.10f });
            trace.Check("进入 Hit 时停止技能", !director.IsPlaying && fsm.Current == ActivityId.Hit,
                "Activity=Hit 且技能停止", $"Activity={fsm.Current} playing={director.IsPlaying}",
                () => DemoTrace.Snapshot(actor));
            trace.AdvanceUntil("等待受击恢复 Root", () => fsm.Current == ActivityId.Root, 0.05f, 10,
                () => $"Activity={fsm.Current} {DemoTrace.Snapshot(actor)}");
            fsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "Kill" });
            trace.Check("Dead 成为不可逆终态", fsm.Current == ActivityId.Dead, "Activity=Dead", $"Activity={fsm.Current}",
                () => DemoTrace.Snapshot(actor));
            bool canReturn = fsm.TryEnter(ActivityId.Root, new ActivityEnterArgs { Reason = "cheat" });
            trace.Check("Dead 阻止返回 Root", !canReturn, "无法进入 Root", $"TryEnter Root={canReturn}",
                () => DemoTrace.Snapshot(actor));
            trace.Complete("活动状态、运动和终态验证完成");
        }
    }

    // Timeline 用例：验证 Clip 时间窗、Payload 定时触发、连招取消和受击中断清理。
    public static class ClipPayloadDemo
    {
        public static void Run()
        {
            // 监听 Cue 事件，确认 Timeline 的表现层 Payload 确实被触发。
            var events = new EventBus();
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f), events);
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, CombatCatalog.Burn(), world.Summons);
            int cues = 0;
            events.Subscribe<EvCue>(_ => cues++);
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var fsm = actor.GetComp<StateMachineComp>();
            var tags = actor.GetComp<TagComp>();
            var input = actor.GetComp<InputBufferComp>();
            var director = actor.GetComp<SkillDirectorComp>();
            var tf = actor.GetComp<TransformComp>();
            var loco = actor.GetComp<LocomotionComp>();
            var box = actor.GetComp<HitboxComp>();
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var hitTarget);
            hitTarget.GetComp<TransformComp>().Position = new SimVec3(0.6f, 0f, 0.6f);
            EntityId actorId = actor.Id;
            EntityId hitTargetId = hitTarget.Id;
            int hitboxHits = 0;
            events.Subscribe<EvDamage>(e =>
            {
                // 只记录本 Timeline 的明确 Source/Target，避免其它伤害事件污染命中阶段。
                if (e.Source == actorId && e.Target == hitTargetId && box.IsOpen)
                    hitboxHits++;
            });
            void Step(float dt) { loco.RequestMoveIntent(0, 0); world.Tick(dt); }
            int CountProjectiles()
            {
                int count = 0;
                var active = world.RegistryActive();
                for (int i = 0; i < active.Count; i++)
                    if (active[i].TryGetComp<ProjectileComp>(out var p) && p.OwnerId == actor.Id)
                        count++;
                return count;
            }

            var trace = new DemoTrace("ClipPayload", CombatCategories.ClipPayload, world, Step);
            trace.Step("初始化 G1 Timeline、Actor 与事件监听", () => $"{DemoTrace.Snapshot(actor)} target={DemoTrace.Snapshot(hitTarget)}");

            float x0 = tf.Position.X;
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("启动 G1", () => director.IsPlaying && director.CurrentSkill == SkillNodeId.G1,
                0.02f, 3, () => $"skill={director.CurrentSkill} playing={director.IsPlaying} {DemoTrace.Snapshot(actor)}");
            float timelineStart = world.Time.Time;
            string TimelineState(string clip)
            {
                return $"clip={clip} timelineTime={(world.Time.Time - timelineStart).ToString("F3")} skill={director.CurrentSkill} cancel={tags.Has(CommonTags.Cancel)} hitbox={box.IsOpen} pos={tf.Position.X.ToString("F3")},{tf.Position.Z.ToString("F3")}";
            }

            // Move Clip 开始后位置持续变化；范围检查能说明位移来自 Timeline，而不是普通移动输入。
            trace.AdvanceUntil("Move Clip 开始并产生位移", () => tf.Position.X > x0 + 0.001f,
                0.02f, 10, () => TimelineState("Move.Begin"));
            trace.AdvanceUntil("Cancel Tag 开始", () => tags.Has(CommonTags.Cancel),
                0.02f, 10, () => TimelineState("Cancel.Begin"));
            trace.AdvanceUntil("Hitbox Clip 开始", () => box.IsOpen,
                0.02f, 10, () => TimelineState("Hitbox.Begin"));
            trace.AdvanceUntil("Hitbox 命中明确目标", () => hitboxHits >= 1,
                0.02f, 4, () => $"{TimelineState("Hitbox.Hit")} hits={hitboxHits} targetHp={hitTarget.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}");
            trace.AdvanceUntil("Cue 与 Fireball Payload 触发", () => cues >= 1 && CountProjectiles() >= 1,
                0.02f, 8, () => $"{TimelineState("Cue.Payload")} cues={cues} projectiles={CountProjectiles()}");
            trace.AdvanceUntil("Move Clip 结束", () => world.Time.Time - timelineStart >= 0.28f && tf.Position.X >= x0 + 0.55f,
                0.02f, 10, () => TimelineState("Move.End"));
            trace.AdvanceUntil("Hitbox Clip 结束", () => !box.IsOpen,
                0.02f, 8, () => $"{TimelineState("Hitbox.End")} hits={hitboxHits}");
            trace.AdvanceUntil("Cancel Tag 结束", () => !tags.Has(CommonTags.Cancel),
                0.02f, 8, () => TimelineState("Cancel.End"));
            trace.AdvanceUntil("G1 Timeline 结束", () => !director.IsPlaying && fsm.Current == ActivityId.Root,
                0.02f, 12, () => $"{TimelineState("Timeline.End")} activity={fsm.Current}");

            float dx = tf.Position.X - x0;
            trace.Check("G1 完成并回到 Root", !director.IsPlaying && fsm.Current == ActivityId.Root && dx >= 0.50f && dx <= 0.75f,
                "Root、位移约0.6", $"Activity={fsm.Current} playing={director.IsPlaying} dx={dx.ToString("F3")}",
                () => DemoTrace.Snapshot(actor));

            // 在 Cancel 窗口内再次输入 Attack，应从 G1 解析到 G2。
            input.Push(InputToken.Attack);
            float comboTimelineStart = world.Time.Time;
            trace.AdvanceUntil("再次启动 G1 并等待 Cancel 窗口", () => director.IsPlaying && tags.Has(CommonTags.Cancel),
                0.02f, 20, () => $"skill={director.CurrentSkill} cancel={tags.Has(CommonTags.Cancel)} timelineTime={(world.Time.Time - comboTimelineStart).ToString("F3")}");
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("Cancel 窗口接续 G2", () => director.CurrentSkill == SkillNodeId.G2,
                0.02f, 3, () => $"skill={director.CurrentSkill} {DemoTrace.Snapshot(actor)}");
            trace.AdvanceUntil("G2 Timeline 结束", () => !director.IsPlaying && fsm.Current == ActivityId.Root,
                0.02f, 30, () => $"skill={director.CurrentSkill} activity={fsm.Current}");
            trace.Check("G2 接续完成", fsm.Current == ActivityId.Root && !director.IsPlaying,
                "Root 且技能停止", $"Activity={fsm.Current} playing={director.IsPlaying}", () => DemoTrace.Snapshot(actor));

            // 重新启动 G1 后立即受击，必须关闭所有活动 Clip，并清除未消费的位移。
            x0 = tf.Position.X;
            input.Push(InputToken.Attack);
            trace.AdvanceUntil("准备一个可被中断的 G1", () => director.IsPlaying,
                0.02f, 3, () => $"skill={director.CurrentSkill} {DemoTrace.Snapshot(actor)}");
            trace.AdvanceFor("推进到 G1 中段", 0.02f, 6,
                () => $"skill={director.CurrentSkill} cancel={tags.Has(CommonTags.Cancel)} hitbox={box.IsOpen}");
            fsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "Hit", HitDuration = 0.2f });
            float xHit = tf.Position.X;
            trace.Check("受击中断清理 Timeline", !director.IsPlaying && !tags.Has(CommonTags.Cancel) && !box.IsOpen,
                "技能停止、Cancel=false、Hitbox=false", $"playing={director.IsPlaying} cancel={tags.Has(CommonTags.Cancel)} hitbox={box.IsOpen}",
                () => DemoTrace.Snapshot(actor));
            trace.AdvanceUntil("受击恢复 Root 且无残留位移", () => fsm.Current == ActivityId.Root,
                0.02f, 15, () => $"Activity={fsm.Current} dx={(tf.Position.X - xHit).ToString("F3")}");
            trace.Check("中断后不再应用 Clip 位移", Math.Abs(tf.Position.X - xHit) <= 0.05f,
                "位移残留<=0.05", $"残留={Math.Abs(tf.Position.X - xHit).ToString("F3")}", () => DemoTrace.Snapshot(actor));
            trace.Complete("Timeline Clip、Payload、连招与中断验证完成");
        }
    }

    // 验证特性：近战命中扣血、单次命中去重、霸体免硬直、暴击、击杀和无敌帧。
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
            var profile = TimelineSO.G1Melee;
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
