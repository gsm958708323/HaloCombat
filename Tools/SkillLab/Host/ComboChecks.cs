using Combat.Config;

namespace SkillLab;

internal static class ComboChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Assets/StreamingAssets/Combat/project.json")));
        p.Preview.Caster.X=0;p.Preview.Caster.Atk=10;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=.55f,Hp=1000}};
        using var s=new SkillSimulation(p,CombatContentCompiler.Compile(p));s.Input("ConfirmAttack",4,0);s.Advance(10);
        var before=s.Snapshot();if(!before.Actors.Single(a=>a.Kind=="caster").ComboConfirmed||before.Statistics.Damage<=0)throw new Exception("Physical Combo contact did not confirm its active cast.");
        s.Input("ConfirmAttack",4,0);s.Advance(4);var after=s.Snapshot();
        if(after.Statistics.Casts!=2||after.Actors.Single(a=>a.Kind=="caster").CurrentSkill!="g2"||after.Actors.Single(a=>a.Kind=="caster").BufferedInput!=null)throw new Exception("Actual Combo input did not start and consume the confirmed transition.");
        Console.WriteLine("PASS: shared preview input, captured Hitbox origin, active-cast confirmation, guarded G1/G2 transition and success-only input consumption.");
    }
}
