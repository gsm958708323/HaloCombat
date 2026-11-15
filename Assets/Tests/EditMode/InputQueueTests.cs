using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class InputQueueTests
    {
        static SkillProjectDocument Project()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        static Actor Player(ArenaSession s){s.World.TryGetActor(s.PlayerId,out var p);return p;}
        static ArenaInputFrame Press(params string[] keys)=>new ArenaInputFrame{Pressed=keys,AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)};
        [Test]
        public void RenderFramePriorityAndCapacityOperateOnOneQueueAtReception()
        {
            using(var s=new ArenaProject(Project()).Create(true))
            {
                s.Start();var b=Player(s).GetComp<InputBufferComp>();
                s.ApplyInput(Press("Fire1","Fire2","Fire3","Fire4","Fire5","Roll","Homing","Monkey"));
                Assert.AreEqual(3,b.Count);Assert.IsTrue(b.TryPeek(out var token));Assert.AreEqual(new InputToken("ArenaDodge"),token);
                b.Consume();Assert.IsTrue(b.TryPeek(out token));Assert.AreEqual(new InputToken("ArenaTracked"),token);
                b.Consume();Assert.IsTrue(b.TryPeek(out token));Assert.AreEqual(new InputToken("ArenaOrb"),token);
                s.ApplyInput(Press("Fire1"));s.ApplyInput(Press("Fire2"));s.ApplyInput(Press("Fire3"));
                Assert.AreEqual(3,b.Count);b.TryPeek(out token);Assert.AreEqual(new InputToken("ArenaPrimary"),token,"The next press evicts the oldest queued press.");
            }
        }
        [Test]
        public void AllQueuedPressesExpireFromReceptionWhileTheRollIsStillPlaying()
        {
            using(var s=new ArenaProject(Project()).Create(true))
            {
                s.Start();s.ApplyInput(Press("Roll","Homing","Fire1"));s.Advance();
                Assert.AreEqual(2002,Player(s).GetComp<SkillDirectorComp>().CurrentSkill.Value);Assert.AreEqual(2,Player(s).GetComp<InputBufferComp>().Count);
                s.Advance(41);Assert.AreEqual(0,Player(s).GetComp<InputBufferComp>().Count);s.Advance(10);
                Assert.IsFalse(Player(s).GetComp<SkillDirectorComp>().IsPlaying,"An expired second press must not acquire a fresh lifetime after the roll.");
            }
        }
        [Test]
        public void EachPressKeepsItsOwnAgeAndActorHitstopFreezesTheWholeQueue()
        {
            using(var s=new ArenaProject(Project()).Create(true))
            {
                s.Start();var b=Player(s).GetComp<InputBufferComp>();b.SetBufferWindow(.2f);b.Push(new InputToken("first"));s.Advance(5);b.Push(new InputToken("second"));
                s.World.RequestActorHitstop(s.PlayerId,20);s.Advance(20);Assert.AreEqual(2,b.Count);
                s.Advance(6);Assert.AreEqual(1,b.Count);b.TryPeek(out var token);Assert.AreEqual(new InputToken("second"),token);
                s.Advance(5);Assert.AreEqual(0,b.Count);b.Push(new InputToken("new"));s.CancelInput();Assert.AreEqual(0,b.Count);
            }
        }
        [Test]
        public void ArenaUsesItsAuthoredPointEightSecondWindowAndPreviewUsesTheSameDefinition()
        {
            var p=Project();Assert.AreEqual(.8f,p.Runtime.Actors.Single(a=>a.Id==p.Runtime.Arena.Player).InputBufferSeconds);
            using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                simulation.Execute(new SkillSimulationCommand{Kind="input",Input="Unmapped",X=1,Z=0});simulation.Advance(20);
                Assert.AreEqual("Unmapped",simulation.Snapshot().Actors.Single(a=>a.Kind=="caster").BufferedInput);simulation.Advance(21);
                Assert.IsNull(simulation.Snapshot().Actors.Single(a=>a.Kind=="caster").BufferedInput);
            }
        }
        [TestCase(-1)] [TestCase(0)] [TestCase(11)] [TestCase(float.NaN)]
        public void InvalidActorInputWindowIsRejectedAtTheJsonBoundary(float window)
        {var p=Project();p.Runtime.Actors[0].InputBufferSeconds=window;Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));}
    }
}
