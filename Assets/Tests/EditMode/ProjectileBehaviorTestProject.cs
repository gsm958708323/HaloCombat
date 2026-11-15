using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Combat.Tests
{
    static class ProjectileBehaviorTestProject
    {
        public static SkillProjectDocument Create(string id="basic-shot")
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            p.Skills=new[]{p.Skills.First(s=>s.Id==id)};p.Buffs=Array.Empty<BuffDocument>();p.SummonBlueprints=Array.Empty<SummonBlueprintDocument>();
            p.Preview.Energy=100;p.Preview.InitialEnergy=100;p.Preview.Recovery=0;p.Preview.Caster.X=0;p.Preview.Caster.Z=0;
            p.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            var cast=Node(p,"cast").Parameters;cast["delay"]=0;cast["cooldown"]=0;cast["duration"]=.1f;
            Node(p,"damage").Parameters["critChance"]=0;
            var projectile=Node(p,"projectile").Parameters;projectile["speed"]=10;projectile["range"]=100;projectile["radius"]=.1f;
            Targets(p,(2,0),(4,0),(6,0),(8,0));
            return p;
        }
        public static SkillNodeDocument Node(SkillProjectDocument p,string kind)=>p.Skills[0].Nodes.First(n=>n.Kind==kind);
        public static SkillSimulation Run(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        public static void Targets(SkillProjectDocument p,params (float x,float z)[] positions)
            =>p.Preview.Targets=positions.Select((v,i)=>new PreviewActorDocument{Id="target-"+(i+1),X=v.x,Z=v.z,Hp=1000,Atk=0,Def=0}).ToArray();
        public static void Cue(SkillProjectDocument p,string source,string port,int value,int order=0)
        {
            string id="cue-"+value;
            p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{new SkillNodeDocument{Id=id,Kind="cue",Parameters=JObject.FromObject(new{id=value})}}).ToArray();
            p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id=id,Source=source,SourcePort=port,Target=id,TargetPort="execute",Order=order}}).ToArray();
        }
    }
}
