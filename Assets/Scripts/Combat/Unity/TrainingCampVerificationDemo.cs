using System;
using Combat.Core;

namespace Combat.TrainingCamp
{
    /// <summary>Deterministic, scene-free smoke verification for the validation sandbox.</summary>
    public static class TrainingCampVerificationDemo
    {
        public static bool Run(Action<string> output = null)
        {
            string[] names = { "Motor", "InputBuffer", "Combo", "Damage", "Buff", "Projectile", "AOE", "AI", "SummonOwnership", "Cleanup" };
            Func<bool>[] checks = { Motor, InputBuffer, Combo, Damage, Buff, Projectile, Aoe, Ai, Summon, Cleanup };
            bool all = true;
            for (int i = 0; i < checks.Length; i++)
            {
                bool pass = false;
                try { pass = checks[i](); }
                catch { pass = false; }
                output?.Invoke("[" + (pass ? "PASS" : "FAIL") + "] " + names[i]);
                all &= pass;
            }
            output?.Invoke(all ? "TRAINING CAMP PASSED" : "TRAINING CAMP FAILED");
            return all;
        }

        static CombatWorld World()
        {
            var baked = new CodeCombatContent().Bake();
            var world = new CombatWorld(new FighterActorFactory(baked), new IntentQueue(), new EventBus(), new CombatTime(), new FixedRandom(0f), baked.Cues, baked.Motor);
            baked.Install(world);
            return world;
        }
        static Actor Spawn(CombatWorld w, string bp, float x, float z)
        {
            var id = w.SpawnActor(new ActorSpawnSpec(bp));
            w.TryGetActor(id, out var a);
            a.GetComp<TransformComp>().Position = new SimVec3(x, 0f, z);
            return a;
        }
        static void Tick(CombatWorld w, int count, float dt = 0.05f)
        { for (int i = 0; i < count; i++) w.Tick(dt); }
        static bool Motor()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0); var l = a.GetComp<LocomotionComp>();
            l.RequestMoveIntent(1, 0); w.Tick(.1f); bool moved = a.GetComp<TransformComp>().Position.X > 0;
            l.ImpulseJump(); w.Tick(.05f); bool air = a.GetComp<TagComp>().Has(CommonTags.Airborne);
            Tick(w, 30); return moved && air && a.GetComp<TagComp>().Has(CommonTags.Grounded);
        }
        static bool InputBuffer()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0); var b = a.GetComp<InputBufferComp>();
            b.SetBufferWindow(.1f); b.Push(InputToken.Attack); w.Tick(.11f); return !b.HasBuffered;
        }
        static bool Combo()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0); var b = a.GetComp<InputBufferComp>();
            b.Push(InputToken.Attack); w.Tick(.05f); bool g1 = a.GetComp<SkillDirectorComp>().CurrentSkill == SkillNodeId.G1;
            Tick(w, 3); b.Push(InputToken.Attack); w.Tick(.01f);
            return g1 && a.GetComp<SkillDirectorComp>().CurrentSkill == SkillNodeId.G2;
        }
        static bool Damage()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0); var d = Spawn(w, "stake", 1, 0);
            float before = d.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            w.Deliver(new IEffect[] { new DamageEffect { Flat = 7, HitstopFrames = 2 }, new PlayCueEffect(1, "Validation") }, a, d, 0);
            w.Tick(.02f);
            return d.GetComp<AttributeSet>().GetBase(AttrId.Hp) < before && w.InHitstop;
        }
        static bool Buff()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0); var d = Spawn(w, "stake", 1, 0); var buffs = d.GetComp<BuffComp>();
            var burn = CombatCatalog.Burn(); w.Deliver(new IEffect[] { new ApplyDurationEffect(burn, 4) }, a, d, 10);
            bool stacked = buffs.StacksOf(CombatIds.Burn) == 3; w.Deliver(new IEffect[] { new DispelEffect(DispelMode.ByBuffId, CombatIds.Burn) }, a, d, 0);
            return stacked && buffs.Count == 0;
        }
        static bool Projectile()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0); var target = Spawn(w, "stake", 3, 0);
            float hp = target.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            w.Deliver(new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball) }, a, null, 10); w.Tick(.05f);
            bool spawned = false; foreach (var x in w.RegistryActive()) spawned |= x.TryGetComp<ProjectileComp>(out _);
            bool hitState = false;
            for (int i = 0; i < 50 && target.GetComp<AttributeSet>().GetBase(AttrId.Hp) >= hp; i++)
            {
                w.Tick(.05f);
                hitState |= target.GetComp<StateMachineComp>().Current == ActivityId.Hit;
            }
            return spawned && target.GetComp<AttributeSet>().GetBase(AttrId.Hp) < hp && hitState &&
                   target.GetComp<TransformComp>().Position.X > 3f;
        }
        static bool Aoe()
        {
            var w = World(); var a = Spawn(w, "fighter", 0, 0);
            w.Deliver(new IEffect[] { new SpawnAoeEffect(CombatIds.AuraField) }, a, null, 0, new SimVec3(0, 0, 0)); w.Tick(.05f);
            foreach (var x in w.RegistryActive()) if (x.TryGetComp<AoeComp>(out var aoe) && aoe.Def != null) return aoe.Def.TrackOccupancy;
            return false;
        }
        static bool Ai()
        {
            var w = World(); var p = Spawn(w, "fighter", 0, 0); var e = Spawn(w, "melee_ai", 2, 0);
            e.GetComp<BehaviorTreeComp>().Board.Target = p.Id; Tick(w, 3);
            return e.GetComp<BehaviorTreeComp>().Board.Target == p.Id;
        }
        static bool Summon()
        {
            var w = World(); var p = Spawn(w, "fighter", 0, 0);
            w.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon) }, p, null, 10); w.Tick(.05f);
            foreach (var x in w.RegistryActive()) if (x.TryGetComp<SummonComp>(out var s)) return s.OwnerId == p.Id;
            return false;
        }
        static bool Cleanup()
        {
            var w = World(); var p = Spawn(w, "fighter", 0, 0);
            w.Deliver(new IEffect[] { new SpawnSummonEffect(CombatIds.MeleeSummon), new SpawnAoeEffect(CombatIds.AuraField) }, p, null, 10); w.Tick(.05f);
            w.CleanupByOwner(p.Id); w.Tick(.05f);
            foreach (var x in w.RegistryActive()) if (x.TryGetComp<SummonComp>(out var s) && s.OwnerId == p.Id) return false;
            return true;
        }
    }
}
