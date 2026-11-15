using Combat.Config;

namespace SkillLab;

internal static class TimelineControlChecks
{
    public static void Run(string root)
    {
        string file=Path.Combine(root,"Content/SkillLab/project.json");var p=SkillProjectFiles.Read(file);
        p.Preview.Caster.X=0;p.Preview.Caster.ActionSpeed=2;p.Preview.Recovery=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();
        using var s=new SkillSimulation(p,CombatContentCompiler.Compile(p));s.Move(0,1);
        if(!s.Cast("controlled-shot",4,0).Accepted)throw new Exception("Controlled timeline rejected.");s.Advance(5);
        var snapshot=s.Snapshot();var actor=snapshot.Actors.Single(a=>a.Kind=="caster");
        if(actor.ActionRate!=2||Math.Abs(actor.ActionTime.Value-.2f)>.0001||actor.Z!=0||snapshot.Statistics.Projectiles!=1)throw new Exception("Timeline control/speed projection mismatch.");
        s.Advance(5);if(Math.Abs(s.Snapshot().Actors.Single(a=>a.Kind=="caster").X-2)>.0001)throw new Exception("Fast timeline changed its total displacement.");
        s.Advance(5);actor=s.Snapshot().Actors.Single(a=>a.Kind=="caster");if(actor.ControlTags.Length!=0||actor.CurrentSkill!=null)throw new Exception("Finished Timeline kept its controls.");
        Console.WriteLine("PASS:2x authored Timeline, .2-second payload in5 steps, independent walk lock, exact2m movement and complete lease cleanup.");
    }
}
