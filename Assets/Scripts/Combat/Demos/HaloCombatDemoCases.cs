using System;
using Combat.Core;

namespace Combat.Demos
{
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
            world.Deliver(new IEffect[] { new DispelEffect(DispelMode.BySource, source:actor.Id) }, actor, actor, 0f);
            trace.Check("驱散后清理 Buff、Tag 与 Modifier",
                buffs.Count == 0 && !tags.Has(slow) && attr.ModCount == 0 && Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) <= 1e-3f,
                "Buff=0 Tag=false Modifier数量=0 Atk=10",
                $"Buff={buffs.Count} Tag={tags.Has(slow)} Modifier数量={attr.ModCount} Atk={attr.GetFinal(AttrId.Atk).ToString("F1")}",
                () => DemoTrace.Snapshot(actor));
            trace.Complete("叠层、周期、互斥与驱散验证完成");
        }
    }

    // 活动与运动用例：验证移动、跳跃、重力、受击恢复和 Dead 状态的终止语义。
}
