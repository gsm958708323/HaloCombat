using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class ProjectilePolicyChecks
{
    public static void Run(string root)
    {
        foreach(string clock in new[]{"time","distance"})
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
            var skill=p.Skills.Single(s=>s.Id=="basic-shot");var body=skill.Nodes.Single(n=>n.Kind=="projectile");
            skill.Nodes.Single(n=>n.Kind=="cast").Parameters["delay"]=0;p.Preview.Caster.X=0;p.Preview.Caster.Z=0;
            body.Parameters["contactMode"]="ignore";body.Parameters["speed"]=10;body.Parameters["range"]=100;body.Parameters["spawnForward"]=0;
            body.Parameters["flightTrigger"]=clock;body.Parameters["flightInterval"]=.2f;body.Parameters["flightDistance"]=2;
            foreach(string field in new[]{"maxHits","chain","chainCount","chainRadius","chainMode"})body.Parameters.Remove(field);
            skill.Nodes=skill.Nodes.Concat(new[]{new SkillNodeDocument{Id="behavior",Kind="projectile-behavior",Parameters=JObject.FromObject(new{mode="pierce",maxHits=3})},
                new SkillNodeDocument{Id="flight-cue",Kind="cue",Parameters=JObject.FromObject(new{id=9901})}}).ToArray();
            skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument{Id="policy",Source="behavior",SourcePort="behavior",Target=body.Id,TargetPort="behavior"},
                new SkillEdgeDocument{Id="flight",Source=body.Id,SourcePort="flight",Target="flight-cue",TargetPort="execute"}}).ToArray();
            var published=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,skill.Id)));
            using var simulation=new SkillSimulation(published.Document.Project,published.Content);simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId=skill.Id,X=10,Z=0});simulation.Advance(50);
            var state=simulation.Snapshot();var events=state.Events.Where(e=>e.Kind=="projectile-flight").ToArray();
            if(events.Length!=5||events.Last().Flight.Index!=5||Math.Abs(events.Last().Flight.Distance-10)>.00001||events.Any(e=>e.Node.NodeId!=body.Id||e.CastId<=0))throw new Exception("Flight cadence or author/cast identity was lost.");
            if(state.Statistics.Hits!=0||state.Events.Count(e=>e.Kind=="cue"&&e.Result=="9901")!=5)throw new Exception("Ignored enemy contact or actual flight branch execution differed.");
            using var replay=new SkillReplayRun(simulation.CaptureReplay(skill.Id));if(!replay.Comparison.Matches)throw new Exception("Projectile flight recording did not reproduce.");
        }
        var transformed=Path.Combine(root,"Tools/SkillLab/.local/verification/projectile-policy/web-shared-behavior.json");
        if(File.Exists(transformed))CombatContentCompiler.Compile(SkillProjectJson.Read(File.ReadAllText(transformed)));
        Console.WriteLine("PASS: independent behavior data, time/distance flight execution, ignored contact, author/cast identity and recorded JSON replay.");
    }
}
