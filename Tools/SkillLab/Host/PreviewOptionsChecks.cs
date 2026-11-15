using Combat.Config;
using Combat.Core;

namespace SkillLab;

internal static class PreviewOptionsChecks
{
    public static void Run(string root)
    {
        var project=SkillProjectJson.SelectTemplate(SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json"))),"basic-shot");
        project.Preview.Ammo=0;project.Preview.UnlimitedAmmo=true;
        project.Skills[0].Nodes.First(n=>n.Kind=="cast").Parameters["ammoCost"]=10;
        using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
        {
            var result=(SkillCastResult)simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0});
            if(!result.Accepted||simulation.Recording.Enabled||simulation.Recording.Inputs!=0||simulation.Snapshot().Ammo!=0)throw new Exception("Unlimited preview or disabled recording failed.");
            try{simulation.CaptureReplay("basic-shot");throw new Exception("Disabled recording exported a partial replay.");}catch(ContentValidationException){}
        }
        using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),true))
        {
            var command=new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0};simulation.Execute(command);command.X=100;
            simulation.Advance(80);var file=simulation.CaptureReplay("basic-shot");
            if(file.Inputs[0].Command.X!=0||!file.Project.Preview.UnlimitedAmmo)throw new Exception("Recording lost owned input or preview options.");
            using var replay=new SkillReplayRun(file);if(!replay.Comparison.Matches)throw new Exception("Unlimited preview replay differs.");
        }
        project.Preview.UnlimitedAmmo=false;
        using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project),false))
            if(((SkillCastResult)simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId="basic-shot",X=0,Z=0})).Accepted)throw new Exception("Finite preview bypassed ammunition.");
        Console.WriteLine("PASS: preview recording disabled/enabled ownership, unlimited ammunition, finite rejection and replay options.");
    }
}
