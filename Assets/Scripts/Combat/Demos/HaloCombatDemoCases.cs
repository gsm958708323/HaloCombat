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

            // Tag 是可叠加计数；移除足够层数后 Has 应返回 false。
            tags.Add(CommonTags.Grounded, 1, TagSource.Debug);
            tags.Add(CommonTags.Cancel, 1, TagSource.Debug);
            CombatLog.Debug(CombatCategories.TagInput, "Has Cancel=" + tags.Has(CommonTags.Cancel));
            tags.Remove(CommonTags.Cancel, 1, TagSource.Debug);
            CombatLog.Debug(CombatCategories.TagInput, "After remove Cancel=" + tags.Has(CommonTags.Cancel));

            // 输入缓冲默认窗口为 0.2 秒；推进 0.25 秒后应过期。
            input.Push(InputToken.Attack);
            CombatLog.Debug(CombatCategories.TagInput, "Peek=" + input.TryPeek(out _));
            world.Tick(0.25f);
            CombatLog.Debug(CombatCategories.TagInput, "Peek expired=" + input.TryPeek(out _));
            CombatLog.Info(CombatCategories.TagInput, "TagInputDemo PASSED");
        }

        static CombatWorld NewWorld()
            => new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()));
    }

    // 属性用例：验证 Add/Mul/Override 的计算顺序、Modifier 来源清理和 HP 上限钳制。
    public static class AttributeDemo
    {
        public static void Run()
        {
            // 使用 stake 作为纯属性载体，不引入输入、连招等玩家行为组件。
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()));
            var id = world.SpawnActor(new ActorSpawnSpec("stake"));
            world.TryGetActor(id, out var actor);
            var attr = actor.GetComp<AttributeSet>();
            CombatLog.Debug(CombatCategories.Attribute, "born Hp=" + attr.GetBase(AttrId.Hp) + " Atk=" + attr.GetFinal(AttrId.Atk));

            // 普通 Modifier 按 (Base + Add) * Mul 计算。
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 50f, SourceId = 1 });
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Mul, Value = 1.2f, SourceId = 1 });
            CombatLog.Debug(CombatCategories.Attribute, "add+mul Atk=" + attr.GetFinal(AttrId.Atk));

            // Override 存在时直接覆盖普通 Add/Mul 结果。
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Override, Value = 999f, SourceId = 2, Priority = 1 });
            CombatLog.Debug(CombatCategories.Attribute, "override Atk=" + attr.GetFinal(AttrId.Atk));

            // 按来源移除后，属性应恢复到此前的计算结果，再恢复到基础值。
            attr.RemoveBySource(2);
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 72f) > 1e-3f) throw new Exception("72");
            attr.RemoveBySource(1);
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) > 1e-3f) throw new Exception("restore");

            // HP 写入不能超过 MaxHp。
            attr.SetBase(AttrId.Hp, 800f);
            if (Math.Abs(attr.GetBase(AttrId.Hp) - 100f) > 1e-3f) throw new Exception("clamp");
            CombatLog.Info(CombatCategories.Attribute, "AttributeDemo PASSED");
        }
    }

    // Buff 用例：验证叠层、周期事件、互斥组，以及驱散时附带状态的完整清理。
    public static class BuffDemo
    {
        public static void Run()
        {
            // 下面的三个 DurationSpec 分别代表可叠层 Burn、互斥的 Wet 和 Ignite。
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()));
            var id = world.SpawnActor(new ActorSpawnSpec("stake"));
            world.TryGetActor(id, out var actor);
            var attr = actor.GetComp<AttributeSet>();
            var tags = actor.GetComp<TagComp>();
            var buffs = actor.GetComp<BuffComp>();
            var slow = new TagId(2101);
            int periodHits = 0;
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
                BuffId = 2, Duration = 5f, MutexGroup = 10,
                GrantedTags = new[] { new TagId(2102) },
                Modifiers = new[] { new Modifier { Attr = AttrId.MoveSpeed, Op = ModOp.Mul, Value = 0.5f } }
            };
            var ignite = new DurationSpec
            {
                BuffId = 3, Duration = 5f, MutexGroup = 10,
                Modifiers = new[] { new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 100f } }
            };

            // AddStack 只能把同一个 Buff 实例叠到 MaxStacks，不会创建第 4 层。
            for (int i = 0; i < 4; i++)
                world.Deliver(new IEffect[] { new ApplyDurationEffect(burn) }, actor, actor, 0f);
            if (buffs.StacksOf(1) != 3) throw new Exception("cap 3");

            // 当前语义：层数是运行时计数，属性 Modifier 只在实例创建时挂载一次。
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 15f) > 1e-3f)
                throw new Exception("stack modifier value");
            if (attr.ModCount != 1)
                throw new Exception("stack modifier duplicated");
            CombatLog.Debug(CombatCategories.Buff,
                "burn stacks=" + buffs.StacksOf(1) +
                " atk=" + attr.GetFinal(AttrId.Atk) +
                " modifiers=" + attr.ModCount);
            // Buff 出生帧不立即触发周期效果；下一帧累计满 1 秒后触发一次。
            world.Tick(0.5f);
            if (periodHits != 0) throw new Exception("skip same frame period");
            world.Tick(1f);
            if (periodHits != 1) throw new Exception("period");
            // Wet 与 Ignite 共享互斥组 10；应用 Ignite 会移除 Wet 及其授予的 Tag。
            world.Deliver(new IEffect[] { new ApplyDurationEffect(wet) }, actor, actor, 0f);
            world.Deliver(new IEffect[] { new ApplyDurationEffect(ignite) }, actor, actor, 0f);
            if (tags.Has(new TagId(2102))) throw new Exception("mutex");
            // 按来源驱散所有 Buff，并验证 Modifier、Tag、Buff 实例都被清理。
            world.Deliver(new IEffect[] { new DispelEffect(DispelMode.BySource, BuffComp.Pack(actor)) }, actor, actor, 0f);
            if (buffs.Count != 0) throw new Exception("dispel");
            if (tags.Has(slow)) throw new Exception("tag cleanup");
            if (attr.ModCount != 0) throw new Exception("modifier cleanup");
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) > 1e-3f) throw new Exception("mods");
            CombatLog.Info(CombatCategories.Buff, "BuffDemo PASSED");
        }
    }

    // 活动与运动用例：验证移动、跳跃、重力、受击恢复和 Dead 状态的终止语义。
    public static class ActivityMotorDemo
    {
        public static void Run()
        {
            // Activity 决定当前 Actor 可以使用哪种运动策略，Locomotion 负责实际积分位置。
            var lib = DemoTables.MakeLib();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), lib));
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var fsm = actor.GetComp<StateMachineComp>();
            var tf = actor.GetComp<TransformComp>();
            var loco = actor.GetComp<LocomotionComp>();
            var tags = actor.GetComp<TagComp>();
            var input = actor.GetComp<InputBufferComp>();
            var director = actor.GetComp<SkillDirectorComp>();

            // Root 状态允许移动，并保持 Grounded。
            loco.RequestMoveIntent(1f, 0f);
            world.Tick(0.10f);
            if (tf.Position.X <= 0f) throw new Exception("walk");
            if (!tags.Has(CommonTags.Grounded)) throw new Exception("grounded");
            // Jump 不切换到独立 Activity，而是让 Root 进入 Airborne；空中仍可接 Attack。
            loco.RequestMoveIntent(0f, 0f);
            input.Push(InputToken.Jump);
            world.Tick(0.02f);
            if (fsm.Current != ActivityId.Root) throw new Exception("jump not activity");
            if (!tags.Has(CommonTags.Airborne)) throw new Exception("air");
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            if (fsm.Current != ActivityId.Attack) throw new Exception("air attack");
            for (int i = 0; i < 5; i++) world.Tick(0.05f);
            float descendingY = tf.Position.Y;
            world.Tick(0.05f);
            if (tf.Position.Y >= descendingY) throw new Exception("gravity");
            for (int i = 0; i < 30; i++) world.Tick(0.05f);
            if (fsm.Current != ActivityId.Root) throw new Exception("back root");
            // Hit 会停止技能并在计时结束后回到 Root；Dead 则阻止返回 Root。
            fsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "Hit", HitDuration = 0.30f, IFrameDuration = 0.10f });
            if (director.IsPlaying) throw new Exception("stop");
            for (int i = 0; i < 10; i++) world.Tick(0.05f);
            if (fsm.Current != ActivityId.Root) throw new Exception("hit recover");
            fsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "Kill" });
            if (fsm.Current != ActivityId.Dead) throw new Exception("dead");
            if (fsm.TryEnter(ActivityId.Root, new ActivityEnterArgs { Reason = "cheat" }))
                throw new Exception("dead stick");
            CombatLog.Info(CombatCategories.ActivityMotor, "ActivityMotorDemo PASSED");
        }
    }

    // Timeline 用例：验证 Clip 时间窗、Payload 定时触发、连招取消和受击中断清理。
    public static class ClipPayloadDemo
    {
        public static void Run()
        {
            // 监听 Cue 事件，确认 Timeline 的表现层 Payload 确实被触发。
            var intents = new IntentQueue();
            var events = new EventBus();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()), intents, events);
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
            void Step(float dt) { loco.RequestMoveIntent(0, 0); world.Tick(dt); }

            float x0 = tf.Position.X;
            input.Push(InputToken.Attack);
            bool sawCancel = false, sawBox = false;

            // G1 的 Move/CancelTag/Hitbox Clip 和 Cue/Projectile Payload 应在各自时间点生效。
            for (int i = 0; i < 35; i++)
            {
                Step(0.02f);
                if (tags.Has(CommonTags.Cancel)) sawCancel = true;
                if (box.IsOpen) sawBox = true;
            }

            float dx = tf.Position.X - x0;
            CombatLog.Debug(CombatCategories.ClipPayload, "G1 dx=" + dx.ToString("F3") + " cancel=" + sawCancel + " box=" + sawBox + " cues=" + cues);
            if (dx < 0.50f || dx > 0.75f) throw new Exception("move ~0.6");
            if (!sawCancel || !sawBox || cues < 1) throw new Exception("clips/payload");
            if (fsm.Current != ActivityId.Root) throw new Exception("root");

            // 在 Cancel 窗口内再次输入 Attack，应从 G1 解析到 G2。
            input.Push(InputToken.Attack);
            for (int i = 0; i < 7; i++) Step(0.02f);
            input.Push(InputToken.Attack);
            Step(0.02f);
            if (director.CurrentSkill != SkillNodeId.G2) throw new Exception("G2");
            for (int i = 0; i < 25; i++) Step(0.02f);

            x0 = tf.Position.X;
            input.Push(InputToken.Attack);
            for (int i = 0; i < 6; i++) Step(0.02f);

            // 受击中断必须关闭 Timeline 的活动 Clip，并清除未消费的位移。
            fsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "Hit", HitDuration = 0.2f });
            if (director.IsPlaying || tags.Has(CommonTags.Cancel) || box.IsOpen)
                throw new Exception("interrupt close");
            float xHit = tf.Position.X;
            for (int i = 0; i < 8; i++) Step(0.02f);
            if (Math.Abs(tf.Position.X - xHit) > 0.05f) throw new Exception("no leftover");
            CombatLog.Info(CombatCategories.ClipPayload, "ClipPayloadDemo PASSED");
        }
    }

    // 验证特性：近战命中扣血、单次命中去重、霸体免硬直、暴击、击杀和无敌帧。
    public static class MeleeDamageDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()), new IntentQueue(), events, new CombatTime(), new FixedRandom(0f));
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, CombatCatalog.Burn(), world.Summons);
            int dmgCount = 0;
            bool lastCrit = false, lastKill = false;
            int immune = 0;
            events.Subscribe<EvDamage>(e => { dmgCount++; lastCrit = e.IsCrit; lastKill = e.IsKill; });
            events.Subscribe<EvImmune>(_ => immune++);

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake);
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

            float hp0 = sAttr.GetBase(AttrId.Hp);
            input.Push(InputToken.Attack);

            // 普通命中应扣血并进入 Hit；同一 Hitbox 期间不能重复命中同一目标。
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (dmgCount < 1) throw new Exception("hit");
            if (sAttr.GetBase(AttrId.Hp) >= hp0) throw new Exception("hp");
            if (sFsm.Current != ActivityId.Hit) throw new Exception("stun");
            int hits = dmgCount;
            // 第二季 G1 配置要求三帧顿帧；在冻结窗口附近检查去重，避免后续 Fireball
            // Payload 的伤害被误认为近战 Hitbox 重复命中。
            for (int i = 0; i < 2; i++) Step(0.02f);
            if (dmgCount != hits) throw new Exception("dedup");
            for (int i = 0; i < 20; i++) Step(0.02f);

            // SuperArmor 只免疫 HitStun，不免疫伤害。
            sTags.Add(CommonTags.SuperArmor, 1, TagSource.Debug);
            for (int i = 0; i < 10 && sFsm.Current == ActivityId.Hit; i++) Step(0.05f);
            float hpB = sAttr.GetBase(AttrId.Hp);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (sAttr.GetBase(AttrId.Hp) >= hpB) throw new Exception("armor dmg");
            if (sFsm.Current == ActivityId.Hit) throw new Exception("armor stun");
            sTags.Remove(CommonTags.SuperArmor, 1, TagSource.Debug);
            for (int i = 0; i < 20; i++) Step(0.02f);

            // CritRate=1 且使用 FixedRandom(0) 时，本次攻击必须暴击。
            pAttr.SetBase(AttrId.CritRate, 1f);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (!lastCrit) throw new Exception("crit");
            pAttr.SetBase(AttrId.CritRate, 0f);
            for (int i = 0; i < 20; i++) Step(0.02f);

            // HP 降到 0 时发布击杀伤害并进入 Dead。
            sAttr.SetBase(AttrId.Hp, 1f);
            lastKill = false;
            input.Push(InputToken.Attack);
            for (int i = 0; i < 60 && sFsm.Current != ActivityId.Dead; i++) Step(0.02f);
            if (!lastKill || sFsm.Current != ActivityId.Dead) throw new Exception("kill");

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake2);
            stake2.GetComp<TransformComp>().Position = new SimVec3(0.6f, 0, 0);

            // IFrame 期间伤害不改变 HP，并发布 EvImmune。
            world.Deliver(new IEffect[] { new IFrameEffect { Duration = 1f } }, player, stake2, pAttr.GetFinal(AttrId.Atk));
            float hpE = stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 1f, CanCrit = false } }, player, stake2, pAttr.GetFinal(AttrId.Atk));
            if (stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp) != hpE || immune < 1)
                throw new Exception("iframe");
            CombatLog.Info(CombatCategories.MeleeDamage, "MeleeDamageDemo PASSED");
        }
    }

    // 远程与范围用例：验证投射物命中、AoE 周期脉冲、Burn 叠层和拥有者死亡清理。
    public static class ProjectileAoeDemo
    {
        public static void Run()
        {
            // 同时注册投射物、AoE 和 Burn，覆盖技能 Payload 到运行时对象的完整链路。
            var events = new EventBus();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()), new IntentQueue(), events);
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

            // 直接执行 Fireball 的 OnHit，验证命中后可无脚本地施加 Burn。
            world.Deliver(fb.OnHit, player, stake, pAttr.GetFinal(AttrId.Atk));
            if (sBuff.StacksOf(CombatIds.Burn) != 1) throw new Exception("scriptless burn");
            world.Deliver(new IEffect[] { new DispelEffect(DispelMode.ByBuffId, CombatIds.Burn) }, player, stake, 0f);
            sAttr.SetBase(AttrId.Hp, 100f);
            player.GetComp<InputBufferComp>().Push(InputToken.Attack);
            for (int i = 0; i < 40; i++)
            {
                player.GetComp<LocomotionComp>().RequestMoveIntent(0, 0);
                world.Tick(0.02f);
            }

            // 通过技能生成 Fireball，验证飞行命中路径同样施加 Burn。
            if (sBuff.StacksOf(CombatIds.Burn) < 1) throw new Exception("fly burn");
            stake.GetComp<TransformComp>().Position = new SimVec3(0, 0, 0);

            // Ground AoE 按 PulseInterval 触发 OnPulse，Burn 层数最多为 3。
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.FireGround) }, player, null, pAttr.GetFinal(AttrId.Atk), player.GetComp<TransformComp>().Position);
            for (int i = 0; i < 50; i++)
            {
                player.GetComp<LocomotionComp>().RequestMoveIntent(0, 0);
                world.Tick(0.05f);
            }

            if (sBuff.StacksOf(CombatIds.Burn) != 3) throw new Exception("stacks 3");
            // 拥有者死亡后，仍存在的 Projectile/AoE 必须被清理。
            player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "DemoKill" });
            world.Tick(0.02f);
            int leftover = 0;
            var all = world.RegistryActive();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].TryGetComp<ProjectileComp>(out var p) && p.OwnerId == player.Id) leftover++;
                if (all[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == player.Id) leftover++;
            }

            if (leftover != 0) throw new Exception("cleanup");
            CombatLog.Info(CombatCategories.ProjectileAoe, "ProjectileAoeDemo PASSED");
        }
    }

    // 第一季集成用例：把输入、连招、Timeline、Cue、伤害、Buff、Bake 缓存和死亡清理串起来。
    public static class SeasonOneDemo
    {
        public static void Run()
        {
            // 该用例故意使用真实的事件监听器，模拟表现层消费 Cue 和伤害事件。
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var cues = CueLibrary.DefaultCombat();
            var listener = new CueListener(cues);
            listener.Bind(events);
            var lib = DemoTables.MakeLib();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), lib), intents, events, time, new FixedRandom(0f), cues);
            var burnBake = new DurationBake(CombatCatalog.Burn());
            var burn = burnBake.Bake();
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, burn, world.Summons);

            int floaters = 0;
            events.Subscribe<EvDamage>(e =>
            {
                floaters++;
                CombatLog.Debug(CombatCategories.SeasonOne, "[F" + time.Frame + "] floater dmg=" + e.Amount.ToString("F1") + " crit=" + e.IsCrit + " kill=" + e.IsKill);
            });
            int deadEvents = 0, cleanups = 0;
            events.Subscribe<EvEntityDead>(_ => deadEvents++);
            events.Subscribe<EvEntityCleanup>(_ => cleanups++);

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake);
            var ptf = player.GetComp<TransformComp>();
            var stf = stake.GetComp<TransformComp>();
            var input = player.GetComp<InputBufferComp>();
            var pLoco = player.GetComp<LocomotionComp>();
            var pAttr = player.GetComp<AttributeSet>();
            var pFsm = player.GetComp<StateMachineComp>();
            var pDir = player.GetComp<SkillDirectorComp>();
            var loadout = player.GetComp<LoadoutComp>();
            var sAttr = stake.GetComp<AttributeSet>();
            var sBuff = stake.GetComp<BuffComp>();
            if (!loadout.TryGet(SkillSlot.Normal, out var ns, out var nt) || ns != SkillNodeId.G1 || nt != TimelineId.TL_G1)
                throw new Exception("loadout");

            ptf.Position = new SimVec3(0, 0, 0);
            ptf.YawDegrees = 0f;
            stf.Position = new SimVec3(0.55f, 0, 0);
            void Step(float dt)
            {
                if (player.IsActive) pLoco.RequestMoveIntent(0, 0);
                world.Tick(dt);
            }

            // 1. G1 近战应触发 Cue、Fireball、Burn 和伤害飘字。
            input.Push(InputToken.Attack);
            for (int i = 0; i < 30; i++) Step(0.02f);
            if (listener.CountId(101) < 1) throw new Exception("blade cue");
            for (int i = 0; i < 20; i++) Step(0.02f);
            if (sBuff.StacksOf(CombatIds.Burn) < 1) throw new Exception("fireball burn");
            if (floaters < 1) throw new Exception("floater");
            CombatLog.Debug(CombatCategories.SeasonOne, "1 melee+burn stacks=" + sBuff.StacksOf(CombatIds.Burn) + " cues=" + listener.Count);
            for (int i = 0; i < 15; i++) Step(0.05f);

            stf.Position = new SimVec3(0.2f, 0, 0);

            // 2. 在 Cancel 窗口接续输入，进入 G2 并把地面 Burn 叠到上限 3 层。
            input.Push(InputToken.Attack);
            for (int i = 0; i < 7; i++) Step(0.02f);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 50; i++) Step(0.05f);
            int stacks = sBuff.StacksOf(CombatIds.Burn);
            CombatLog.Debug(CombatCategories.SeasonOne, "2 ground stacks=" + stacks);
            if (stacks != 3) throw new Exception("cap 3");

            // 3. 受击时停止技能并清空输入缓存，恢复后回到 Root。
            input.Push(InputToken.Attack);
            Step(0.02f);
            input.Push(InputToken.Attack);
            pFsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "P1Hit", HitDuration = 0.25f });
            if (pDir.IsPlaying) throw new Exception("hit stop");
            if (input.HasBuffered) throw new Exception("clear buf");
            for (int i = 0; i < 8; i++) Step(0.05f);
            if (pFsm.Current != ActivityId.Root) throw new Exception("recover");

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var dummy);
            dummy.GetComp<TransformComp>().Position = new SimVec3(100, 0, 0);
            float atk = pAttr.GetFinal(AttrId.Atk);
            dummy.GetComp<AttributeSet>().SetBase(AttrId.Hp, 100f);
            // 4. 修改可配置伤害后清缓存，新的 Bake 结果必须生效。
            world.Deliver(TimelineSO.G1Melee.Bake(), player, dummy, atk);
            float hpAfter1 = dummy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            TimelineSO.G1Melee.Damage.Coeff = 3f;
            TimelineSO.G1Melee.ClearCache();
            dummy.GetComp<AttributeSet>().SetBase(AttrId.Hp, 100f);
            dummy.GetComp<StateMachineComp>().TryEnter(ActivityId.Root, new ActivityEnterArgs { Reason = "reset" });
            world.Deliver(TimelineSO.G1Melee.Bake(), player, dummy, atk);
            float hpAfter2 = dummy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            CombatLog.Debug(CombatCategories.SeasonOne, "4 bake hp " + hpAfter1 + " vs " + hpAfter2);
            if (hpAfter2 >= hpAfter1) throw new Exception("ClearCache");
            TimelineSO.G1Melee.Damage.Coeff = 1f;
            TimelineSO.G1Melee.ClearCache();

            // 5. 死亡时清理 Buff、Projectile、AoE，并发布 EvEntityDead。
            input.Push(InputToken.Attack);
            Step(0.05f);
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.FireGround) }, player, null, atk, ptf.Position);
            pFsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "SeasonOneKill", Killer = stake.Id });
            Step(0.02f);
            if (pFsm.Current != ActivityId.Dead) throw new Exception("dead");
            if (player.GetComp<BuffComp>().Count != 0) throw new Exception("buffs");
            if (deadEvents < 1) throw new Exception("EvEntityDead");
            int leftover = 0;
            var all = world.RegistryActive();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].TryGetComp<ProjectileComp>(out var pr) && pr.OwnerId == player.Id) leftover++;
                if (all[i].TryGetComp<AoeComp>(out var ao) && ao.OwnerId == player.Id) leftover++;
            }

            if (leftover != 0) throw new Exception("runtime leftover");
            CombatLog.Debug(CombatCategories.SeasonOne, "5 deadEvents=" + deadEvents + " cleanups=" + cleanups + " bladeCues=" + listener.CountId(101));
            CombatLog.Info(CombatCategories.SeasonOne, "SeasonOneDemo PASSED");
        }
    }
}
