using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class EditorOptimizationChecks
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static SkillProjectDocument Project(string root)
    {
        var p=SkillProjectJson.SelectTemplate(SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json")),"basic-shot");
        p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Caster.CollisionRadius=.4f;p.Preview.Obstacles=[];
        p.Preview.Targets=[new(){Id="target-1",X=3,Z=0,Hp=1000,Atk=0},new(){Id="target-2",X=5,Z=0,Hp=1000,Atk=0},new(){Id="target-3",X=7,Z=0,Hp=1000,Atk=0}];
        var s=p.Skills.Single();s.Nodes.Single(n=>n.Kind=="cast").Parameters=JObject.FromObject(new{mode="instant",cost=0,cooldown=0,delay=0});
        s.Nodes.Single(n=>n.Kind=="projectile").Parameters=JObject.FromObject(new{speed=100,range=24,radius=.1,spawnForward=0});
        s.Nodes.Single(n=>n.Kind=="damage").Parameters=JObject.FromObject(new{flat=10,coefficient=0,critChance=0});
        return p;
    }
    static void Attach(SkillProjectDocument p,string kind,object parameters)
    {
        var s=p.Skills.Single();s.Nodes=s.Nodes.Append(new SkillNodeDocument{Id=kind,Kind=kind,Parameters=JObject.FromObject(parameters)}).ToArray();
        s.Edges=s.Edges.Append(new SkillEdgeDocument{Id="hit-"+kind,Source="projectile",SourcePort="hit",Target=kind,TargetPort="execute",Order=1}).ToArray();
    }
    public static void Run(string root)
    {
        var piercing=Project(root);Attach(piercing,"pierce",new{maxHits=2});
        using(var sim=new SkillSimulation(piercing,CombatContentCompiler.Compile(piercing)))
        {sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(10);Check(sim.Snapshot().Statistics.Damage==20,"Pierce effect must stop at its actual second contact.");}
        piercing.Skills[0].Nodes.Single(n=>n.Kind=="pierce").Enabled=false;
        using(var sim=new SkillSimulation(piercing,CombatContentCompiler.Compile(piercing)))
        {sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(10);Check(sim.Snapshot().Statistics.Damage==10,"Disabling the Pierce effect must restore one hit.");}
        var chain=Project(root);Attach(chain,"chain",new{count=2,radius=6,selection="nearest"});
        using(var sim=new SkillSimulation(chain,CombatContentCompiler.Compile(chain)))
        {sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(10);var snap=sim.Snapshot();Check(snap.Statistics.Damage==30&&snap.Statistics.Chains==2&&snap.Statistics.Projectiles==1,"Chain effect must redirect one bounded projectile.");Check(snap.Events.Any(e=>e.Kind=="execute"&&e.Node?.NodeKind=="chain"),"Chain execution must locate its effect node.");}
        var project=Project(root);project.Skills[0].Nodes.Single(n=>n.Kind=="projectile").Parameters["speed"]=1;
        using(var sim=new SkillSimulation(project,CombatContentCompiler.Compile(project),true))
        {
            sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(5);var before=sim.Snapshot();
            var updated=SkillProjectJson.Read(SkillProjectJson.Write(project));updated.Skills[0].Nodes.Single(n=>n.Kind=="damage").Parameters["flat"]=25;
            sim.Execute(new SkillSimulationCommand{Kind="configure",Project=updated});var after=sim.Snapshot();
            Check(before.Step==after.Step&&before.Actors.Select(a=>(a.Id,a.X,a.Z,a.Hp)).SequenceEqual(after.Actors.Select(a=>(a.Id,a.X,a.Z,a.Hp))),"Configure must preserve steps and all live actors.");
            sim.Advance(150);Check(sim.Snapshot().Statistics.Damage==10,"Existing projectile must retain its captured damage definition.");
            sim.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=10,Z=0});sim.Advance(150);Check(sim.Snapshot().Statistics.Damage==35,"The next cast must use the updated damage definition.");
            using var replay=new SkillReplayRun(sim.CaptureReplay("basic-shot"));Check(replay.Comparison.Matches,"Configuration replay mismatch: "+string.Join("; ",replay.Comparison.Differences.Select(d=>d.Path)));
            updated.Skills[0].Nodes.Single(n=>n.Kind=="projectile").Parameters["speed"]=-1;var valid=sim.Snapshot();
            try{sim.ApplyConfiguration(updated);throw new Exception("Invalid update was accepted.");}catch(ContentValidationException){}
            Check(JToken.DeepEquals(JToken.FromObject(valid),JToken.FromObject(sim.Snapshot())),"Invalid configuration must leave the whole world unchanged.");
        }
        var dash=Project(root);var skill=dash.Skills[0];var action=skill.Nodes.Single(n=>n.Kind=="projectile");action.Kind="delivery";
        action.Parameters=JObject.FromObject(new{mode="displacement",movementMode="dash",distance=12,speed=20,hitRadius=1.2,startup=0});
        skill.Edges.Single(e=>e.Source==action.Id).SourcePort="apply";
        using(var sim=new SkillSimulation(dash,CombatContentCompiler.Compile(dash)))
        {
            sim.Cast("basic-shot",12,0);sim.Advance(40);var snap=sim.Snapshot();var caster=snap.Actors.Single(a=>a.Kind=="caster");var target=snap.Actors.Single(a=>a.Name=="target-1");
            Check(caster.X<target.X&&target.X-caster.X>=.7998f,"Dash must stop outside the target body.");
            Check(snap.Statistics.Damage==10,"Body obstruction must retain the contacted target hit.");
        }
        var ordinary=Project(root);ordinary.Runtime=new RuntimeContentDocument();
        ordinary.Runtime.Combos=[new(){Id="root",Input="Attack",ToSkill="basic-shot"},new(){Id="loop",Input="Attack",PreSkills=["basic-shot"],ToSkill="basic-shot",Priority=10}];
        ordinary.Skills[0].Timeline=new TimelineDocument{RuntimeId=ordinary.Skills[0].RuntimeId,Duration=.3f,
            Clips=[new(){Id="cancel",Kind="cancel",Start=.1f,End=.3f}],Payloads=[new(){Id="fire",Time=.02f,Effects=["projectile"]}]};
        ordinary.Skills[0].Nodes.Single(n=>n.Kind=="cast").Parameters=JObject.FromObject(new{cost=0,cooldown=0});
        ordinary.Skills[0].Edges=ordinary.Skills[0].Edges.Where(e=>e.Source!="cast").ToArray();
        using(var sim=new SkillSimulation(ordinary,CombatContentCompiler.Compile(ordinary)))
        {
            sim.Input("Attack",10,0);sim.Advance(6);Check(sim.Snapshot().Statistics.Casts==1,"Ordinary project root Combo must start a real cast.");
            sim.Input("Attack",10,0);sim.Advance(2);Check(sim.Snapshot().Statistics.Casts==2,"A cyclic Combo must use the authored cancel window.");
        }
        Console.WriteLine("PASS: executable chain/pierce effects, disabled effect, bounded contacts, same-world config update, captured old bodies, exact edit replay, invalid update rollback and solid dash contact.");
    }
}
