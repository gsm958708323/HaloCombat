using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class ProjectileMotionJsonTests
    {
        [TestCase("acceleration",1001)]
        [TestCase("maxSpeed",0)]
        [TestCase("returnAfter",-1)]
        [TestCase("returnSpeed",0)]
        [TestCase("catchRadius",0)]
        [TestCase("launchHeight",-1)]
        [TestCase("verticalSpeed",-1)]
        [TestCase("gravity",0)]
        [TestCase("restitution",2)]
        [TestCase("groundBounces",33)]
        [TestCase("wallBounces",65)]
        public void InvalidMotionFieldsLocateTheExactField(string field,int value)
        {
            var p=Create();Node(p,"projectile").Parameters[field]=value;
            StringAssert.Contains("parameters."+field,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [Test]
        public void UnknownTrajectoryAndMaximumBelowInitialSpeedAreRejected()
        {
            var p=Create();var parameters=Node(p,"projectile").Parameters;parameters["trajectory"]="unknown";
            StringAssert.Contains("trajectory",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            parameters["trajectory"]="accelerate";parameters["maxSpeed"]=1;
            StringAssert.Contains("maxSpeed",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [Test]
        public void MotionDefinitionsAndLifecycleBranchesRoundtripInPublication()
        {
            foreach(string id in new[]{"accelerating-shot","returning-shot","bouncing-shot","ricochet-shot","homing-shot"})
            {
                var p=Create(id);var projectile=Node(p,"projectile");
                Cue(p,projectile.Id,"bounce",71);Cue(p,projectile.Id,"returned",72);Cue(p,projectile.Id,"ground",73);
                var a=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,id)));
                var b=SkillPublicationJson.Read(SkillPublicationJson.Write(a.Document));
                Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(a.Document),JObject.FromObject(b.Document)));
                a.Content.Projectiles.TryGet(projectile.RuntimeId,out var first);b.Content.Projectiles.TryGet(projectile.RuntimeId,out var second);
                Assert.AreNotSame(first,second);Assert.AreEqual(1,second.OnBounce.Length);Assert.AreEqual(1,second.OnReturned.Length);Assert.AreEqual(1,second.OnGround.Length);
                if(first.Motion!=null)Assert.AreNotSame(first.Motion,second.Motion);
                if(first.Motion?.Bounce!=null)Assert.AreNotSame(first.Motion.Bounce,second.Motion.Bounce);
            }
        }

        [TestCase("bounce")]
        [TestCase("returned")]
        [TestCase("ground")]
        public void MotionLifecyclePortsCannotInventAProjectileHitForSplit(string port)
        {
            var p=Create("split-shot");var split=Node(p,"split");
            p.Skills[0].Edges=p.Skills[0].Edges.Where(e=>e.Target!=split.Id).Concat(new[]{new SkillEdgeDocument{
                Id="invalid",Source=Node(p,"projectile").Id,SourcePort=port,Target=split.Id,TargetPort="execute"}}).ToArray();
            StringAssert.Contains("projectile hit",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
    }
}
