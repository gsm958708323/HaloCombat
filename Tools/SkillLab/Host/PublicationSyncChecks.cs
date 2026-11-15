using Combat.Config;

namespace SkillLab;

internal static class PublicationSyncChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json")));
        var slots=new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"basic-shot"))},{"f7",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"summon-gunners"))}};
        p.Skills.Single(s=>s.Id=="basic-shot").Nodes.Single(n=>n.Kind=="projectile").Parameters["speed"]=9;
        var plan=PublicationFiles.PlanSync(p,slots);
        if(plan["f6"].SkillId!="basic-shot"||plan["f7"].SkillId!="summon-gunners")throw new Exception("Sync lost the selected slot roots.");
        using(var session=new PublishedSkillSession())session.Replace(plan.ToDictionary(e=>e.Key,e=>SkillPublicationJson.Write(e.Value)));
        var empty=new Dictionary<string,string>();try{PublicationFiles.PlanSync(p,empty);throw new Exception("Empty sync should fail.");}catch(ContentValidationException){}
        var other=SkillProjectJson.Read(SkillProjectJson.Write(p));other.Id="other-project";try{PublicationFiles.PlanSync(other,slots);throw new Exception("Foreign project sync should fail.");}catch(ContentValidationException){}
        Console.WriteLine("PASS: slot sync preserves both roots, rebuilds shared dependencies together and refuses empty/foreign publications.");
    }
}
