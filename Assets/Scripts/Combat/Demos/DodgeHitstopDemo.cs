using Combat.Core;

namespace Combat.Demos
{
    public static class DodgeHitstopDemo
    {
        public static void Run()
        {
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
            SeasonTwoDemoSupport.Assert(!attacker.GetComp<TagComp>().Has(CommonTags.Invincible), "dodge iframe closes");
            world.Deliver(new IEffect[] { new DamageEffect() }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(damage == 1 && hitstops == 0, "default damage no hitstop");
            world.Deliver(new IEffect[] { new DamageEffect { HitstopFrames = 3 } }, target, attacker, 10f);
            SeasonTwoDemoSupport.Assert(damage == 2 && hitstops == 1, "explicit hitstop");
            for (int i = 0; i < 5; i++) SeasonTwoDemoSupport.Step(world, 0.02f);

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
