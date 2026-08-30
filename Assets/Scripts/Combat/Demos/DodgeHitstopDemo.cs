using System;
using Combat.Core;

namespace Combat.Demos
{
    // 闪避与顿帧用例：验证闪避无敌、倒地输入门控、近战顿帧和运行时对象冻结。
    public static class DodgeHitstopDemo
    {
        public static void Run()
        {
            // 事件计数用于区分真实伤害、免疫和顿帧请求，避免只看最终 HP。
            var events = new EventBus();
            int damage = 0, immune = 0, hitstops = 0;
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var attacker = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var target = SeasonTwoDemoSupport.Spawn(world, "stake", 0.6f, 0f);
            EntityId attackerId = attacker.Id;
            EntityId targetId = target.Id;
            events.Subscribe<EvDamage>(e =>
            {
                // 只统计本阶段两个 Actor 之间的事件，避免其它 Payload 影响计数。
                if ((e.Source == attackerId && e.Target == targetId) ||
                    (e.Source == targetId && e.Target == attackerId))
                    damage++;
            });
            events.Subscribe<EvImmune>(e =>
            {
                if (e.Source == targetId && e.Target == attackerId)
                    immune++;
            });
            events.Subscribe<EvHitstop>(e =>
            {
                if ((e.Source == attackerId && e.Target == targetId) ||
                    (e.Source == targetId && e.Target == attackerId))
                    hitstops++;
            });
            var input = attacker.GetComp<InputBufferComp>();
            var director = attacker.GetComp<SkillDirectorComp>();
            var targetAttr = target.GetComp<AttributeSet>();
            var fsm = attacker.GetComp<StateMachineComp>();
            float x0 = attacker.GetComp<TransformComp>().Position.X;
            var trace = new DemoTrace("DodgeHitstop", CombatCategories.DodgeHitstop, world, dt => SeasonTwoDemoSupport.Step(world, dt));
            trace.Step("init", "初始化闪避、近战与顿帧事件", () => DemoTrace.Snapshot(attacker) + " target=" + DemoTrace.Snapshot(target));
            // Dodge Timeline 同时负责位移和无敌帧；命中无敌目标不得造成伤害或顿帧。
            input.Push(Season2Tokens.Dodge);
            trace.AdvanceUntil("dodge-start", "闪避启动并进入无敌窗口", () => director.CurrentSkill == SkillNodeId.Dodge &&
                attacker.GetComp<TagComp>().Has(CommonTags.Invincible), 0.02f, 6,
                () => "skill=" + director.CurrentSkill + " iframe=" + attacker.GetComp<TagComp>().Has(CommonTags.Invincible) + " " + DemoTrace.Snapshot(attacker));
            float attackerHp = attacker.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            int immuneBefore = immune;
            int hitstopBefore = hitstops;
            world.Deliver(new IEffect[] { new DamageEffect { HitstopFrames = 3 } }, target, attacker, 10f);
            trace.Check("iframe-hit", "无敌期间受击不扣血也不顿帧", attacker.GetComp<AttributeSet>().GetBase(AttrId.Hp) == attackerHp &&
                immune > immuneBefore && hitstops == hitstopBefore && targetAttr.GetBase(AttrId.Hp) == 100f,
                "攻击者HP不变、Immune增加、Hitstop不增加、伤害源HP不变",
                "攻击者HP=" + attacker.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1") + " Immune=" + immune + " Hitstop=" + hitstops + " 目标HP=" + targetAttr.GetBase(AttrId.Hp).ToString("F1"),
                () => DemoTrace.Snapshot(attacker));
            trace.AdvanceUntil("dodge-end", "闪避结束并恢复 Root", () => !attacker.GetComp<TagComp>().Has(CommonTags.Invincible) &&
                fsm.Current == ActivityId.Root, 0.02f, 25, () => DemoTrace.Snapshot(attacker));
            float dx = attacker.GetComp<TransformComp>().Position.X - x0;
            trace.Check("dodge-result", "闪避位移完成且无敌窗口关闭", dx >= 1f && dx <= 1.4f,
                "位移1.0至1.4且 IFrame=false", "dx=" + dx.ToString("F2") + " iframe=" + attacker.GetComp<TagComp>().Has(CommonTags.Invincible),
                () => DemoTrace.Snapshot(attacker));

            // Downed 是 Activity 门控：倒地期间缓存的 Dodge 不能启动，恢复后才可继续输入。
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, target, attacker, 0f);
            input.Push(Season2Tokens.Dodge);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            trace.Check("downed-gate", "倒地期间阻止闪避输入", fsm.Current == ActivityId.Knockdown && director.CurrentSkill != SkillNodeId.Dodge,
                "Activity=Knockdown 且未播放 Dodge", "Activity=" + fsm.Current + " skill=" + director.CurrentSkill,
                () => DemoTrace.Snapshot(attacker));
            input.Clear();
            trace.AdvanceUntil("downed-recover", "倒地恢复后允许后续输入", () => fsm.Current == ActivityId.Root, 0.05f, 12,
                () => DemoTrace.Snapshot(attacker));
            trace.Check("downed-recover-result", "倒地恢复到 Root", fsm.Current == ActivityId.Root, "Activity=Root", "Activity=" + fsm.Current,
                () => DemoTrace.Snapshot(attacker));

            // 普通近战 Timeline 是顿帧来源；命中结算在当前帧完成，下一帧开始冻结，
            // 因此投射物和 AI 服务暂停时，攻击者也不能继续移动。
            attacker.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            target.GetComp<TransformComp>().Position = new SimVec3(0.55f, 0f, 0f);
            trace.Check("melee-setup", "准备近战顿帧场景", fsm.Current == ActivityId.Root && !input.HasBuffered &&
                !attacker.GetComp<TagComp>().Has(CommonTags.Downed) && !attacker.GetComp<TagComp>().Has(CommonTags.Stunned),
                "Activity=Root、无输入、无 Downed/Stunned",
                "Activity=" + fsm.Current + " buffered=" + input.HasBuffered + " downed=" + attacker.GetComp<TagComp>().Has(CommonTags.Downed) + " stunned=" + attacker.GetComp<TagComp>().Has(CommonTags.Stunned),
                () => DemoTrace.Snapshot(attacker));
            input.Push(InputToken.Attack);
            int damageBeforeMelee = damage;
            int hitstopBeforeMelee = hitstops;
            bool meleeHit = false;
            trace.AdvanceUntil("melee-hitstop", "近战命中并请求顿帧", () =>
            {
                meleeHit = damage > damageBeforeMelee && hitstops > hitstopBeforeMelee;
                return meleeHit;
            }, 0.02f, 20, () => "damage=" + damage + " hitstops=" + hitstops + " targetHp=" + target.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1") + " " + DemoTrace.Snapshot(attacker));
            trace.Check("melee-hitstop-result", "近战伤害事件与顿帧事件配对", meleeHit,
                "damage 和 hitstops 都增加", "damage=" + damage + " hitstops=" + hitstops,
                () => "source=" + attackerId + " target=" + targetId + " " + DemoTrace.Snapshot(target));
            float frozenX = attacker.GetComp<TransformComp>().Position.X;
            trace.AdvanceFor("hitstop-enter", "推进到顿帧服务开始", 0.02f, 1,
                () => "InHitstop=" + world.InHitstop + " left=" + world.HitstopLeft + " " + DemoTrace.Snapshot(attacker));
            trace.Check("hitstop-enter-result", "下一逻辑帧进入冻结", world.InHitstop,
                "InHitstop=true", "InHitstop=" + world.InHitstop + " left=" + world.HitstopLeft,
                () => DemoTrace.Snapshot(attacker));
            trace.AdvanceFor("hitstop-freeze", "冻结期间保持 Actor 位置", 0.02f, 2,
                () => "InHitstop=" + world.InHitstop + " left=" + world.HitstopLeft + " x=" + attacker.GetComp<TransformComp>().Position.X.ToString("F3"));
            trace.Check("hitstop-freeze-result", "顿帧期间攻击者位置不变", Math.Abs(attacker.GetComp<TransformComp>().Position.X - frozenX) < 1e-4f,
                "位置X不变", "冻结前X=" + frozenX.ToString("F3") + " 当前X=" + attacker.GetComp<TransformComp>().Position.X.ToString("F3"),
                () => DemoTrace.Snapshot(attacker));
            trace.AdvanceFor("hitstop-end", "等待顿帧结束", 0.02f, 5, () => "InHitstop=" + world.InHitstop + " left=" + world.HitstopLeft);

            // 默认 DamageEffect 不主动顿帧，显式设置 HitstopFrames 才会请求顿帧。
            int damageBeforeDefault = damage;
            int hitstopBeforeDefault = hitstops;
            world.Deliver(new IEffect[] { new DamageEffect() }, target, attacker, 10f);
            trace.Check("default-damage", "默认伤害不主动顿帧", damage == damageBeforeDefault + 1 && hitstops == hitstopBeforeDefault,
                "伤害+1 且 Hitstop 不变", "damage=" + damage + " hitstops=" + hitstops,
                () => "source=" + targetId + " target=" + attackerId + " " + DemoTrace.Snapshot(attacker));
            int damageBeforeExplicit = damage;
            int hitstopBeforeExplicit = hitstops;
            world.Deliver(new IEffect[] { new DamageEffect { HitstopFrames = 3 } }, target, attacker, 10f);
            trace.Check("explicit-damage", "显式 HitstopFrames 请求顿帧", damage == damageBeforeExplicit + 1 && hitstops == hitstopBeforeExplicit + 1,
                "伤害+1 且 Hitstop+1", "damage=" + damage + " hitstops=" + hitstops,
                () => "source=" + targetId + " target=" + attackerId + " " + DemoTrace.Snapshot(attacker));
            trace.AdvanceFor("explicit-recover", "推进显式顿帧请求后的恢复", 0.02f, 5, () => "InHitstop=" + world.InHitstop + " left=" + world.HitstopLeft);

            // 让 HomingBolt 先进入飞行状态，再请求顿帧，检查其位置在冻结期间保持不变。
            var projectile = SeasonTwoDemoSupport.Spawn(world, "fighter", -10f, 0f);
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, projectile, target, 10f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            var bodies = world.RegistryActive();
            Actor projectileActor = null;
            ProjectileComp bolt = null;
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i].TryGetComp<ProjectileComp>(out var p)) { bolt = p; projectileActor = bodies[i]; }
            trace.Check("projectile-spawn", "生成可冻结的 Homing Projectile", bolt != null, "Projectile 存在", "Projectile存在=" + (bolt != null),
                () => projectileActor == null ? "无 projectile actor" : DemoTrace.Snapshot(projectileActor));
            var projectileTf = projectileActor.GetComp<TransformComp>();
            float projectileX = projectileTf.Position.X;
            int frameBefore = world.Time.Frame;
            world.RequestHitstop(2);
            world.RequestHitstop(4);
            trace.AdvanceFor("projectile-freeze", "推进并冻结 Projectile", 0.02f, 1,
                () => "frame=" + world.Time.Frame + " left=" + world.HitstopLeft + " projectileX=" + projectileTf.Position.X.ToString("F3"));
            trace.Check("projectile-freeze-result", "顿帧期间 Projectile 位置不变", world.Time.Frame == frameBefore + 1 && world.InHitstop &&
                world.HitstopLeft == 3 && projectileTf.Position.X == projectileX,
                "帧+1、InHitstop=true、剩余3帧、位置不变",
                "frame=" + world.Time.Frame + " left=" + world.HitstopLeft + " InHitstop=" + world.InHitstop + " x=" + projectileTf.Position.X.ToString("F3"),
                () => DemoTrace.Snapshot(projectileActor));
            trace.Complete("闪避、倒地门控、近战顿帧、伤害对比与投射物冻结验证完成");
        }
    }
}
