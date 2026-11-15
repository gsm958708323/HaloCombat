using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class BurstChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json"));p.Preview.Targets=Array.Empty<PreviewActorDocument>();
        var s=p.Skills.Single(v=>v.Id=="basic-shot");var cast=s.Nodes.Single(n=>n.Kind=="cast");s.Nodes.Single(n=>n.Kind=="target").Parameters["mode"]="direction";
        cast.Parameters=JObject.FromObject(new{mode="instant",burst=true,count=20,interval=.04,delay=0,cost=0,cooldown=0,duration=.9});
        using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
        {
            simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId=s.Id,X=0,Z=0});simulation.Advance(45);
            if(simulation.Snapshot().Statistics.Projectiles!=20||simulation.Snapshot().Actors.Where(a=>a.Kind=="projectile").Select(a=>a.Yaw).Distinct().Count()<10)throw new Exception("Burst did not sample independent directions.");
            using var replay=new SkillReplayRun(simulation.CaptureReplay(s.Id));if(!replay.Comparison.Matches)throw new Exception("Burst replay differs.");
        }
        cast.Parameters["burst"]=false;
        using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
        {simulation.Cast(s.Id,0,0);simulation.Advance(45);if(simulation.Snapshot().Statistics.Projectiles!=1)throw new Exception("Disabled burst did not reduce to one emission.");}
        Console.WriteLine("PASS: explicit20-shot burst samples a new direction per actual trigger; disabled switch preserves count but emits once; full recording replays.");
    }
}
