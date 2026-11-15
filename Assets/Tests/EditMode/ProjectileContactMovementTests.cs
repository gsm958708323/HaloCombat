using System;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ProjectileContactMovementTests
    {
        static SkillProjectDocument Project()
        {
            var project=SkillProjectJson.SelectTemplate(SkillProjectFiles.Read(System.IO.Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")),"basic-shot");
            project.Preview.Targets=project.Preview.Targets.Take(1).ToArray();
            project.Preview.Targets[0].CollisionRadius=.4f;project.Preview.Targets[0].Z=.4f;
            project.Skills[0].Nodes.First(n=>n.Kind=="damage").Parameters["critChance"]=0;
            return project;
        }
        [TestCase(12f,.4f,true)][TestCase(1000f,.4f,true)][TestCase(1000f,.59f,false)]
        public void SweptProjectileIncludesTheTargetRadiusAtNormalAndFastSpeeds(float speed,float offset,bool hits)
        {
            var project=Project();project.Preview.Targets[0].Z=offset;
            project.Skills[0].Nodes.First(n=>n.Kind=="projectile").Parameters["speed"]=speed;
            using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
            {simulation.Cast("basic-shot",10,0);simulation.Advance(60);Assert.AreEqual(hits?1:0,simulation.Snapshot().Statistics.Hits);}
        }
        [TestCase(true)][TestCase(false)]
        public void ShootingMovementCanBeExplicitlyAllowedOrBlocked(bool allowed)
        {
            var project=Project();project.Preview.Targets=Array.Empty<PreviewActorDocument>();
            var cast=project.Skills[0].Nodes.First(n=>n.Kind=="cast");cast.Parameters["duration"]=1;cast.Parameters["allowMovement"]=allowed;
            using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
            {simulation.Move(0,1);simulation.Cast("basic-shot",10,0);simulation.Advance(20);Assert.AreEqual(allowed?2:0,simulation.Snapshot().Actors.First(a=>a.Kind=="caster").Z,.001f);}
        }
    }
}
