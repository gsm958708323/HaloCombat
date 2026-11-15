using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class CollisionTests
    {
        static SkillProjectDocument Example()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static SkillSimulation Simulation(SkillProjectDocument project)=>new SkillSimulation(project,CombatContentCompiler.Compile(project));
        static PreviewObstacleDocument Wall()=>new PreviewObstacleDocument{Id="wall",X=-2,Z=0,Width=.5f,Depth=4};

        [Test]
        public void SweptCircleStopsAtAThinWallAndReturnsTheEarliestObstacle()
        {
            var collision=new CollisionWorld(new[]{new ObstacleDefinition("far",5,0,.02f,4),new ObstacleDefinition("near",0,0,.02f,4)});
            var point=collision.Constrain(new SimVec3(-10,0,0),new SimVec3(10,0,0),.2f,out var hit);
            Assert.AreEqual("near",hit.Id);Assert.AreEqual(-.21f,hit.Point.X,.0001f);Assert.Less(point.X,-.21f);
            Assert.AreEqual(-1,hit.Normal.X);Assert.IsTrue(collision.IsFree(point,.2f));
        }

        [Test]
        public void CircularCornersAreNotTheSquareCornerOfAnExpandedBox()
        {
            var collision=new CollisionWorld(new[]{new ObstacleDefinition("box",0,0,2,2)});
            var hit=collision.Trace(new SimVec3(-3,0,-3),new SimVec3(3,0,3),.5f);
            float expected=-1-.5f/(float)Math.Sqrt(2);
            Assert.IsTrue(hit.Blocked);Assert.AreEqual(expected,hit.Point.X,.0001f);Assert.AreEqual(expected,hit.Point.Z,.0001f);
            Assert.IsFalse(collision.Trace(new SimVec3(-2,0,-1.49f),new SimVec3(-1.49f,0,-1.49f),.5f).Blocked);
        }

        [Test]
        public void ContactAllowsMovingOutAndAlongTheWallButBlocksMovingIn()
        {
            var collision=new CollisionWorld(new[]{new ObstacleDefinition("box",0,0,2,2)});
            var contact=new SimVec3(-1.5f,0,0);
            Assert.IsFalse(collision.Trace(contact,new SimVec3(-2,0,0),.5f).Blocked);
            Assert.IsFalse(collision.Trace(contact,new SimVec3(-1.5f,0,2),.5f).Blocked);
            Assert.IsTrue(collision.Trace(contact,SimVec3.Zero,.5f).Blocked);
            Assert.IsFalse(collision.IsFree(new SimVec3(float.NaN,0,0),.5f));
        }

        [Test]
        public void CollisionWorldCopiesDefinitionsAndRejectsMalformedGeometry()
        {
            var boxes=new[]{new ObstacleDefinition("box",0,0,2,2)};var collision=new CollisionWorld(boxes);
            boxes[0]=new ObstacleDefinition("changed",20,0,1,1);
            Assert.IsTrue(collision.Trace(new SimVec3(-3,0,0),new SimVec3(3,0,0)).Blocked);
            Assert.Throws<ArgumentException>(()=>new CollisionWorld(new[]{new ObstacleDefinition("bad",0,0,-1,2)}));
        }

        [Test]
        public void NormalMovementStopsWithItsBodyRadiusAndCanMoveAway()
        {
            var project=Example();project.Preview.Obstacles=new[]{Wall()};
            using(var simulation=Simulation(project))
            {
                simulation.Move(1,0);simulation.Advance(100);
                float x=simulation.Snapshot().Actors.First(a=>a.Kind=="caster").X;
                Assert.AreEqual(-2.6501f,x,.001f);
                simulation.Move(-1,0);simulation.Advance(10);
                Assert.Less(simulation.Snapshot().Actors.First(a=>a.Kind=="caster").X,x-.5f);
                StringAssert.Contains("overlaps",Assert.Throws<ContentValidationException>(()=>simulation.Place("caster",-2,0)).Message);
            }
        }

        [TestCase(20)]
        [TestCase(1000)]
        public void DashCannotCrossAThinWallOrDamageTargetsBehindIt(float speed)
        {
            var project=Example();project.Preview.Obstacles=new[]{Wall()};
            var action=project.Skills.First(s=>s.Id=="dash-strike").Nodes.First(n=>n.Kind=="delivery");action.Parameters["speed"]=speed;
            using(var simulation=Simulation(project))
            {
                simulation.Cast("dash-strike",0,0);simulation.Advance(100);
                Assert.AreEqual(-2.6501f,simulation.Snapshot().Actors.First(a=>a.Kind=="caster").X,.002f);
                Assert.AreEqual(0,simulation.Snapshot().Statistics.Damage);Assert.AreEqual(0,simulation.Snapshot().Volumes.Length);
                Assert.AreEqual(1,simulation.Snapshot().Events.Count(e=>e.Kind=="collision"&&e.Result.StartsWith("dash")));
            }
        }

        [Test]
        public void BlinkRejectsAnOccupiedLandingBeforeChargingButCanCrossAnIntermediateWall()
        {
            var project=Example();project.Preview.Obstacles=new[]{Wall()};
            using(var simulation=Simulation(project))
            {
                Assert.IsFalse(simulation.Cast("blink-strike",-2,0).Accepted);
                Assert.AreEqual(100,simulation.Snapshot().Energy);Assert.AreEqual(0,simulation.Snapshot().Cooldowns["blink-strike"]);
                Assert.IsTrue(simulation.Cast("blink-strike",0,0).Accepted);simulation.Advance(80);
                Assert.AreEqual(0,simulation.Snapshot().Actors.First(a=>a.Kind=="caster").X,.0001f);
                Assert.AreEqual(240,simulation.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void MeleeCannotReachThroughAClosedObstacle()
        {
            var project=Example();project.Preview.Obstacles=new[]{Wall()};
            using(var simulation=Simulation(project))
            {simulation.Cast("melee-thrust",0,0);simulation.Advance(80);Assert.AreEqual(0,simulation.Snapshot().Statistics.Damage);}
        }

        [Test]
        public void AuthoredWallsValidateIdentitySizeAndActorClearanceWithPaths()
        {
            var project=Example();project.Preview.Obstacles=new[]{Wall()};
            project.Preview.Obstacles[0].Width=0;
            StringAssert.Contains("obstacles[0].width",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(project)).Path);
            project.Preview.Obstacles=new[]{Wall(),Wall()};
            StringAssert.Contains("obstacles[1].id",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(project)).Path);
            project.Preview.Obstacles=new[]{Wall()};project.Preview.Obstacles[0].X=-6;
            StringAssert.Contains("preview.caster",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(project)).Path);
        }
    }
}
