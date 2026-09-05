using System;
using Combat.Core;
using Combat.Presentation;

namespace Combat.Demos
{
    public static class SeasonThreeLessonDemo
    {
        public static void Run()
        {
            RunMotor();
            RunMelee();
            RunProjectileAoe();
            RunAiSummon();
            Console.WriteLine("SeasonThreeLessonDemo PASSED");
        }

        static void RunMotor()
        {
            var lesson = New(CombatLessonKind.Motor);
            for (int i = 0; i < 500; i++) lesson.Tick();
            Require(lesson.Get(lesson.Player).GetComp<TagComp>().Has(CommonTags.Grounded), "V1 final grounded");
            Require(lesson.Get(lesson.Player).GetComp<TransformComp>().Position.X != -2f, "V1 moved");
        }

        static void RunMelee()
        {
            var lesson = New(CombatLessonKind.Melee);
            int damages = 0, hitstops = 0, cues = 0, g2Cues = 0;
            lesson.World.Events.Subscribe<EvDamage>(_ => damages++);
            lesson.World.Events.Subscribe<EvHitstop>(_ => hitstops++);
            lesson.World.Events.Subscribe<EvCue>(e => { cues++; if (e.CueId == 102) g2Cues++; });
            for (int i = 0; i < 500; i++) lesson.Tick();
            Require(damages > 0, "V2 damage");
            Require(hitstops > 0, "V2 hitstop");
            Require(cues > 0, "V2 cue");
            Require(g2Cues > 0, "V2 cancel G2 cue");
            Require(lesson.Get(lesson.Target).GetComp<AttributeSet>().GetBase(AttrId.Hp) < 1000f, "V2 target hp");
        }

        static void RunProjectileAoe()
        {
            var lesson = New(CombatLessonKind.ProjectileAoe);
            int spawns = 0, damage = 0;
            lesson.World.Events.Subscribe<EvEntitySpawn>(_ => spawns++);
            lesson.World.Events.Subscribe<EvDamage>(_ => damage++);
            for (int i = 0; i < 500; i++) lesson.Tick();
            Require(spawns >= 4, "V3 projectile and aoe spawns");
            Require(damage > 0, "V3 damage");
        }

        static void RunAiSummon()
        {
            var lesson = New(CombatLessonKind.AiSummon);
            int spawns = 0, cleanup = 0;
            lesson.World.Events.Subscribe<EvEntitySpawn>(_ => spawns++);
            lesson.World.Events.Subscribe<EvEntityCleanup>(_ => cleanup++);
            for (int i = 0; i < 500; i++) lesson.Tick();
            Require(spawns >= 3, "V4 summon descendants");
            Require(cleanup > 0, "V4 cleanup");
            Require(!HasSummon(lesson.World, lesson.Player), "V4 summon removed after owner death");
            Require(lesson.Get(lesson.Target) != null && lesson.Get(lesson.Target).IsActive, "V4 enemy survives");
        }

        static CombatLesson New(CombatLessonKind kind)
        {
            var world = DemoWorld.Create(out _, out _, new FixedRandom(0f));
            return new CombatLesson(world, kind);
        }

        static bool HasSummon(CombatWorld world, EntityId owner)
        {
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].TryGetComp<SummonComp>(out var summon) && summon.OwnerId == owner) return true;
            return false;
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new Exception("SeasonThreeLessonDemo: " + message);
        }
    }
}


