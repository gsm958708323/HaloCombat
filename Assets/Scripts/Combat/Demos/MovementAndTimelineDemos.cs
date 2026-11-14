using System;
using Combat.Core;

namespace Combat.Demos
{
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
}
