using Combat.Config;
using Combat.Core;

namespace SkillLab;

internal static class PreviewOptionsChecks
{
    public static void Run(string root)
    {
        var project=SkillProjectJson.SelectTemplate(SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json"))),"basic-shot");
        project.Preview.Ammo=0;project.Preview.Energy=0;project.Preview.Recovery=0;project.Preview.UnlimitedAmmo=true;
        project.Skills[0].Nodes.First(n=>n.Kind=="cast").Parameters["ammoCost"]=10;
        using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
        {
            var result=(SkillCastResult)simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0});
            if(!result.Accepted||simulation.Recording.Enabled||simulation.Recording.Inputs!=0||simulation.Snapshot().Ammo!=0||simulation.Snapshot().Energy!=0)throw new Exception("Unlimited preview or disabled recording failed.");
            try{simulation.CaptureReplay("basic-shot");throw new Exception("Disabled recording exported a partial replay.");}catch(ContentValidationException){}
        }
        using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),true))
        {
            var command=new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0};simulation.Execute(command);command.X=100;
            simulation.Advance(80);var file=simulation.CaptureReplay("basic-shot");
            if(file.Inputs[0].Command.X!=0||!file.Project.Preview.UnlimitedAmmo)throw new Exception("Recording lost owned input or preview options.");
            var full=simulation.Snapshot();var light=simulation.Snapshot(full.Events.Last().Sequence,-1,false,false);
            if(light.Events.Length!=0||light.Trails.Length!=0||light.Actors.Any(a=>a.Attributes!=null)||light.Actors.Length!=full.Actors.Length)
                throw new Exception("Light preview duplicated diagnostics, trails or lost live actors.");
            using var replay=new SkillReplayRun(file);if(!replay.Comparison.Matches)throw new Exception("Unlimited preview replay differs.");
        }
        project.Preview.UnlimitedAmmo=false;
        using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
            if(((SkillCastResult)simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0})).Accepted)throw new Exception("Finite preview bypassed ammunition.");
        project.Preview.UnlimitedAmmo=true;
        var cast=project.Skills[0].Nodes.First(n=>n.Kind=="cast");cast.Parameters["cooldown"]=.05;cast.Parameters["delay"]=0;cast.Parameters["duration"]=.04;
        using(var first=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
        using(var chunked=new SkillSimulation(project,CombatContentCompiler.Compile(project),true))
        {
            var auto=new SkillSimulationCommand{Kind="auto-cast",SkillId="basic-shot",X=0,Z=0};first.Execute(auto);chunked.Execute(auto);
            for(int step=0;step<120;step++)first.Advance(1);chunked.Advance(120);
            if(first.Snapshot().Statistics.Casts!=40||chunked.Snapshot().Statistics.Casts!=40||first.Snapshot().Trails.Length!=0)
                throw new Exception("Automatic fire depends on request chunk size or retains unrecorded trails.");
            chunked.Execute(new SkillSimulationCommand{Kind="auto-cast"});
            using var replay=new SkillReplayRun(chunked.CaptureReplay("basic-shot"));
            if(!replay.Comparison.Matches)throw new Exception("Automatic fire replay differs.");
        }
        Console.WriteLine("PASS: preview recording disabled/enabled ownership, unlimited ammunition/energy, finite rejection and replay options.");
    }
}
