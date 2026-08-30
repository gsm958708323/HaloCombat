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
            events.Subscribe<EvDamage>(_ => damage++);
            events.Subscribe<EvImmune>(_ => immune++);
            events.Subscribe<EvHitstop>(_ => hitstops++);
            var world = SeasonTwoDemoSupport.NewWorld(events);
            var attacker = SeasonTwoDemoSupport.Spawn(world, "fighter", 0f, 0f);
            var target = SeasonTwoDemoSupport.Spawn(world, "stake", 0.6f, 0f);
            var input = attacker.GetComp<InputBufferComp>();
            var director = attacker.GetComp<SkillDirectorComp>();
            var targetAttr = target.GetComp<AttributeSet>();
            var fsm = attacker.GetComp<StateMachineComp>();
            float x0 = attacker.GetComp<TransformComp>().Position.X;
            // Dodge Timeline 同时负责位移和无敌帧；命中无敌目标不得造成伤害或顿帧。
            input.Push(Season2Tokens.Dodge);
            SeasonTwoDemoSupport.Step(world, 0.06f);
            SeasonTwoDemoSupport.Assert(director.CurrentSkill == SkillNodeId.Dodge, "dodge starts");
            SeasonTwoDemoSupport.Assert(attacker.GetComp<TagComp>().Has(CommonTags.Invincible), "dodge iframe tag");
            float attackerHp = attacker.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            world.Deliver(new IEffect[] { new DamageEffect { HitstopFrames = 3 } }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(attacker.GetComp<AttributeSet>().GetBase(AttrId.Hp) == attackerHp, "dodge immune");
            SeasonTwoDemoSupport.Assert(immune > 0 && hitstops == 0, "iframe events");
            SeasonTwoDemoSupport.Assert(targetAttr.GetBase(AttrId.Hp) == 100f, "dodge does not damage source");
            for (int i = 0; i < 25; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            float dx = attacker.GetComp<TransformComp>().Position.X - x0;
            SeasonTwoDemoSupport.Assert(!attacker.GetComp<TagComp>().Has(CommonTags.Invincible) &&
                fsm.Current == ActivityId.Root && dx >= 1f && dx <= 1.4f, "dodge iframe closes and moves");

            // Downed 是 Activity 门控：倒地期间缓存的 Dodge 不能启动，恢复后才可继续输入。
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, target, attacker, 0f);
            input.Push(Season2Tokens.Dodge);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Knockdown &&
                director.CurrentSkill != SkillNodeId.Dodge, "downed blocks dodge");
            input.Clear();
            for (int i = 0; i < 12; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Root, "downed recovery before melee");

            // 普通近战 Timeline 是顿帧来源；命中结算在当前帧完成，下一帧开始冻结，
            // 因此投射物和 AI 服务暂停时，攻击者也不能继续移动。
            attacker.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            target.GetComp<TransformComp>().Position = new SimVec3(0.55f, 0f, 0f);
            SeasonTwoDemoSupport.Assert(fsm.Current == ActivityId.Root && !input.HasBuffered &&
                !attacker.GetComp<TagComp>().Has(CommonTags.Downed) && !attacker.GetComp<TagComp>().Has(CommonTags.Stunned),
                "melee setup");
            input.Push(InputToken.Attack);
            int damageBeforeMelee = damage;
            int hitstopBeforeMelee = hitstops;
            bool meleeHit = false;
            for (int i = 0; i < 20; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.02f);
                if (damage > damageBeforeMelee && hitstops > hitstopBeforeMelee)
                {
                    meleeHit = true;
                    break;
                }
            }
            SeasonTwoDemoSupport.Assert(meleeHit, "melee hitstop (damage=" + damage + ", hitstops=" + hitstops +
                ", targetHp=" + target.GetComp<AttributeSet>().GetBase(AttrId.Hp) + ", attackerState=" + fsm.Current + ")");
            float frozenX = attacker.GetComp<TransformComp>().Position.X;
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(world.InHitstop, "freeze starts next tick");
            for (int i = 0; i < 2; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.02f);
                SeasonTwoDemoSupport.Assert(Math.Abs(attacker.GetComp<TransformComp>().Position.X - frozenX) < 1e-4f,
                    "hitstop freezes attacker");
            }
            for (int i = 0; i < 5; i++) SeasonTwoDemoSupport.Step(world, 0.02f);

            // 默认 DamageEffect 不主动顿帧，显式设置 HitstopFrames 才会请求顿帧。
            int damageBeforeDefault = damage;
            int hitstopBeforeDefault = hitstops;
            world.Deliver(new IEffect[] { new DamageEffect() }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(damage == damageBeforeDefault + 1 && hitstops == hitstopBeforeDefault,
                "default damage no hitstop");
            int damageBeforeExplicit = damage;
            int hitstopBeforeExplicit = hitstops;
            world.Deliver(new IEffect[] { new DamageEffect { HitstopFrames = 3 } }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(damage == damageBeforeExplicit + 1 && hitstops == hitstopBeforeExplicit + 1,
                "explicit hitstop");
            for (int i = 0; i < 5; i++) SeasonTwoDemoSupport.Step(world, 0.02f);

            // 让 HomingBolt 先进入飞行状态，再请求顿帧，检查其位置在冻结期间保持不变。
            var projectile = SeasonTwoDemoSupport.Spawn(world, "fighter", -10f, 0f);
            world.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }, projectile, target, 10f);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            var bodies = world.RegistryActive();
            Actor projectileActor = null;
            ProjectileComp bolt = null;
            for (int i = 0; i < bodies.Count; i++)
                if (bodies[i].TryGetComp<ProjectileComp>(out var p)) { bolt = p; projectileActor = bodies[i]; }
            SeasonTwoDemoSupport.Assert(bolt != null, "projectile for hitstop");
            var projectileTf = projectileActor.GetComp<TransformComp>();
            float projectileX = projectileTf.Position.X;
            int frameBefore = world.Time.Frame;
            world.RequestHitstop(2);
            world.RequestHitstop(4);
            SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(world.Time.Frame == frameBefore + 1 && world.InHitstop &&
                world.HitstopLeft == 3 && projectileTf.Position.X == projectileX, "hitstop clock and freeze");
            CombatLog.Info(CombatCategories.DodgeHitstop, "DodgeHitstopDemo PASSED");
        }
    }
}
