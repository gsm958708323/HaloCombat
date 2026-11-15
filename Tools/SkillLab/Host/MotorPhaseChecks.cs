using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class MotorPhaseChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Assets/StreamingAssets/Combat/project.json")));
        var skill=p.Skills.Single(s=>s.Id=="arena-controlled-shot");
        skill.Nodes=skill.Nodes.Where(n=>n.Kind!="projectile").ToArray();skill.Edges=skill.Edges.Where(e=>e.Source!="shot").ToArray();
        skill.Timeline=new TimelineDocument{RuntimeId=3015,Duration=.02f,ControlTags=new[]{1011},Clips=new[]{
            new TimelineClipDocument{Id="move",Kind="move",End=.02f,MoveX=1},
            new TimelineClipDocument{Id="contact",Kind="hitbox",End=.02f,HitRadius=.3f,Effects=new[]{"damage"}}}};
        skill.Nodes.Single(n=>n.Id=="damage").Parameters=JObject.FromObject(new{flat=10,coefficient=0,canCrit=false});
        p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=1.2f,Hp=1000}};
        var transferred=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,skill.Id)));
        using var simulation=new SkillSimulation(transferred.Document.Project,transferred.Content);
        if(!simulation.Cast(skill.Id,4,0).Accepted)throw new Exception("Motor phase probe cast rejected.");
        simulation.Move(0,1);simulation.Advance(1);var state=simulation.Snapshot();var caster=state.Actors.Single(a=>a.Kind=="caster");
        if(Math.Abs(caster.X-1)>.00001||caster.Z!=0||state.Statistics.Damage!=10||state.Statistics.Hits!=1||caster.ActionTime!=null||caster.ControlTags.Length!=0)
            throw new Exception("Final-step motion/contact/control release order changed.");
        if(state.Events.Single(e=>e.Kind=="damage").Sequence>=state.Events.Single(e=>e.Kind=="completed").Sequence)
            throw new Exception("Timeline completion preceded its final damage.");
        simulation.Advance(1);if(simulation.Snapshot().Actors.Single(a=>a.Kind=="caster").Z<=0)throw new Exception("Input did not resume on the next step.");
        Console.WriteLine("PASS: published one-step skill moves1m, deals10 damage before completion and resumes walking on the next step.");
    }
}
