using System;
using Combat.Core;

namespace Combat.Demos
{
    public static class TagInputDemo
    {
        public static void Run()
        {
            var world = NewWorld();
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var tags = actor.GetComp<TagComp>();
            var input = actor.GetComp<InputBufferComp>();
            tags.Add(CommonTags.Grounded, 1, TagSource.Debug);
            tags.Add(CommonTags.Cancel, 1, TagSource.Debug);
            CombatLog.Debug(CombatCategories.TagInput, "Has Cancel=" + tags.Has(CommonTags.Cancel));
            tags.Remove(CommonTags.Cancel, 1, TagSource.Debug);
            CombatLog.Debug(CombatCategories.TagInput, "After remove Cancel=" + tags.Has(CommonTags.Cancel));
            input.Push(InputToken.Attack);
            CombatLog.Debug(CombatCategories.TagInput, "Peek=" + input.TryPeek(out _));
            world.Tick(0.25f);
            CombatLog.Debug(CombatCategories.TagInput, "Peek expired=" + input.TryPeek(out _));
            CombatLog.Info(CombatCategories.TagInput, "TagInputDemo PASSED");
        }

        static CombatWorld NewWorld()
            => new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()));
    }

    public static class AttributeDemo
    {
        public static void Run()
        {
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()));
            var id = world.SpawnActor(new ActorSpawnSpec("stake"));
            world.TryGetActor(id, out var actor);
            var attr = actor.GetComp<AttributeSet>();
            CombatLog.Debug(CombatCategories.Attribute, "born Hp=" + attr.GetBase(AttrId.Hp) + " Atk=" + attr.GetFinal(AttrId.Atk));
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Add, Value = 50f, SourceId = 1 });
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Mul, Value = 1.2f, SourceId = 1 });
            CombatLog.Debug(CombatCategories.Attribute, "add+mul Atk=" + attr.GetFinal(AttrId.Atk));
            attr.AddMod(new Modifier { Attr = AttrId.Atk, Op = ModOp.Override, Value = 999f, SourceId = 2, Priority = 1 });
            CombatLog.Debug(CombatCategories.Attribute, "override Atk=" + attr.GetFinal(AttrId.Atk));
            attr.RemoveBySource(2);
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 72f) > 1e-3f) throw new Exception("72");
            attr.RemoveBySource(1);
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) > 1e-3f) throw new Exception("restore");
            attr.SetBase(AttrId.Hp, 800f);
            if (Math.Abs(attr.GetBase(AttrId.Hp) - 100f) > 1e-3f) throw new Exception("clamp");
            CombatLog.Info(CombatCategories.Attribute, "AttributeDemo PASSED");
        }
    }

    public static class BuffDemo
    {
        public static void Run()
        {
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

            for (int i = 0; i < 4; i++)
                world.Deliver(new IEffect[] { new ApplyDurationEffect(burn) }, actor, actor, 0f);
            if (buffs.StacksOf(1) != 3) throw new Exception("cap 3");
            world.Tick(0.5f);
            if (periodHits != 0) throw new Exception("skip same frame period");
            world.Tick(1f);
            if (periodHits != 1) throw new Exception("period");
            world.Deliver(new IEffect[] { new ApplyDurationEffect(wet) }, actor, actor, 0f);
            world.Deliver(new IEffect[] { new ApplyDurationEffect(ignite) }, actor, actor, 0f);
            if (tags.Has(new TagId(2102))) throw new Exception("mutex");
            world.Deliver(new IEffect[] { new DispelEffect(DispelMode.BySource, BuffComp.Pack(actor)) }, actor, actor, 0f);
            if (buffs.Count != 0) throw new Exception("dispel");
            if (Math.Abs(attr.GetFinal(AttrId.Atk) - 10f) > 1e-3f) throw new Exception("mods");
            CombatLog.Info(CombatCategories.Buff, "BuffDemo PASSED");
        }
    }

    public static class ActivityMotorDemo
    {
        public static void Run()
        {
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

            loco.RequestMoveIntent(1f, 0f);
            world.Tick(0.10f);
            if (tf.Position.X <= 0f) throw new Exception("walk");
            if (!tags.Has(CommonTags.Grounded)) throw new Exception("grounded");
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

    public static class ClipPayloadDemo
    {
        public static void Run()
        {
            var intents = new IntentQueue();
            var events = new EventBus();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()), intents, events);
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, CombatCatalog.Burn());
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

            input.Push(InputToken.Attack);
            for (int i = 0; i < 7; i++) Step(0.02f);
            input.Push(InputToken.Attack);
            Step(0.02f);
            if (director.CurrentSkill != SkillNodeId.G2) throw new Exception("G2");
            for (int i = 0; i < 25; i++) Step(0.02f);

            x0 = tf.Position.X;
            input.Push(InputToken.Attack);
            for (int i = 0; i < 6; i++) Step(0.02f);
            fsm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { Reason = "Hit", HitDuration = 0.2f });
            if (director.IsPlaying || tags.Has(CommonTags.Cancel) || box.IsOpen)
                throw new Exception("interrupt close");
            float xHit = tf.Position.X;
            for (int i = 0; i < 8; i++) Step(0.02f);
            if (Math.Abs(tf.Position.X - xHit) > 0.05f) throw new Exception("no leftover");
            CombatLog.Info(CombatCategories.ClipPayload, "ClipPayloadDemo PASSED");
        }
    }

    public static class MeleeDamageDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()), new IntentQueue(), events, new CombatTime(), new FixedRandom(0f));
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, CombatCatalog.Burn());
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
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (dmgCount < 1) throw new Exception("hit");
            if (sAttr.GetBase(AttrId.Hp) >= hp0) throw new Exception("hp");
            if (sFsm.Current != ActivityId.Hit) throw new Exception("stun");
            int hits = dmgCount;
            for (int i = 0; i < 8; i++) Step(0.02f);
            if (dmgCount != hits) throw new Exception("dedup");
            for (int i = 0; i < 20; i++) Step(0.02f);

            sTags.Add(CommonTags.SuperArmor, 1, TagSource.Debug);
            for (int i = 0; i < 10 && sFsm.Current == ActivityId.Hit; i++) Step(0.05f);
            float hpB = sAttr.GetBase(AttrId.Hp);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (sAttr.GetBase(AttrId.Hp) >= hpB) throw new Exception("armor dmg");
            if (sFsm.Current == ActivityId.Hit) throw new Exception("armor stun");
            sTags.Remove(CommonTags.SuperArmor, 1, TagSource.Debug);
            for (int i = 0; i < 20; i++) Step(0.02f);

            pAttr.SetBase(AttrId.CritRate, 1f);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (!lastCrit) throw new Exception("crit");
            pAttr.SetBase(AttrId.CritRate, 0f);
            for (int i = 0; i < 20; i++) Step(0.02f);

            sAttr.SetBase(AttrId.Hp, 1f);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 12; i++) Step(0.02f);
            if (!lastKill || sFsm.Current != ActivityId.Dead) throw new Exception("kill");

            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake2);
            stake2.GetComp<TransformComp>().Position = new SimVec3(0.6f, 0, 0);
            world.Deliver(new IEffect[] { new IFrameEffect { Duration = 1f } }, player, stake2, pAttr.GetFinal(AttrId.Atk));
            float hpE = stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            world.Deliver(new IEffect[] { new DamageEffect { Coeff = 1f, CanCrit = false } }, player, stake2, pAttr.GetFinal(AttrId.Atk));
            if (stake2.GetComp<AttributeSet>().GetBase(AttrId.Hp) != hpE || immune < 1)
                throw new Exception("iframe");
            CombatLog.Info(CombatCategories.MeleeDamage, "MeleeDamageDemo PASSED");
        }
    }

    public static class ProjectileAoeDemo
    {
        public static void Run()
        {
            var events = new EventBus();
            var world = new CombatWorld(new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()), new IntentQueue(), events);
            var burn = CombatCatalog.Burn();
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, burn);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")), out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")), out var stake);
            player.GetComp<TransformComp>().Position = new SimVec3(0, 0, 0);
            player.GetComp<TransformComp>().YawDegrees = 0f;
            stake.GetComp<TransformComp>().Position = new SimVec3(2.5f, 0, 0);
            var pAttr = player.GetComp<AttributeSet>();
            var sBuff = stake.GetComp<BuffComp>();
            var sAttr = stake.GetComp<AttributeSet>();
            world.Projectiles.TryGet(CombatIds.Fireball, out var fb);
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

            if (sBuff.StacksOf(CombatIds.Burn) < 1) throw new Exception("fly burn");
            stake.GetComp<TransformComp>().Position = new SimVec3(0, 0, 0);
            world.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.FireGround) }, player, null, pAttr.GetFinal(AttrId.Atk), player.GetComp<TransformComp>().Position);
            for (int i = 0; i < 50; i++)
            {
                player.GetComp<LocomotionComp>().RequestMoveIntent(0, 0);
                world.Tick(0.05f);
            }

            if (sBuff.StacksOf(CombatIds.Burn) != 3) throw new Exception("stacks 3");
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

    public static class SeasonOneDemo
    {
        public static void Run()
        {
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
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, burn);

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

            input.Push(InputToken.Attack);
            for (int i = 0; i < 30; i++) Step(0.02f);
            if (listener.CountId(101) < 1) throw new Exception("blade cue");
            for (int i = 0; i < 20; i++) Step(0.02f);
            if (sBuff.StacksOf(CombatIds.Burn) < 1) throw new Exception("fireball burn");
            if (floaters < 1) throw new Exception("floater");
            CombatLog.Debug(CombatCategories.SeasonOne, "1 melee+burn stacks=" + sBuff.StacksOf(CombatIds.Burn) + " cues=" + listener.Count);
            for (int i = 0; i < 15; i++) Step(0.05f);

            stf.Position = new SimVec3(0.2f, 0, 0);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 7; i++) Step(0.02f);
            input.Push(InputToken.Attack);
            for (int i = 0; i < 50; i++) Step(0.05f);
            int stacks = sBuff.StacksOf(CombatIds.Burn);
            CombatLog.Debug(CombatCategories.SeasonOne, "2 ground stacks=" + stacks);
            if (stacks != 3) throw new Exception("cap 3");

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
