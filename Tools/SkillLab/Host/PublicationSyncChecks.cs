using Combat.Config;

namespace SkillLab;

internal static class PublicationSyncChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json"));
        var slots=new Dictionary<string,string>{{"f6",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"basic-shot"))},{"f7",SkillPublicationJson.Write(SkillPublicationJson.Create(p,"summon-gunners"))}};
        p.Skills.Single(s=>s.Id=="basic-shot").Nodes.Single(n=>n.Kind=="projectile").Parameters["speed"]=9;
        var plan=PublicationFiles.PlanSync(p,slots);
        if(plan["f6"].SkillId!="basic-shot"||plan["f7"].SkillId!="summon-gunners")throw new Exception("Sync lost the selected slot roots.");
        using(var session=new PublishedSkillSession())session.Replace(plan.ToDictionary(e=>e.Key,e=>SkillPublicationJson.Write(e.Value)));
        var empty=new Dictionary<string,string>();try{PublicationFiles.PlanSync(p,empty);throw new Exception("Empty sync should fail.");}catch(ContentValidationException){}
        var other=SkillProjectJson.Read(SkillProjectJson.Write(p));other.Id="other-project";try{PublicationFiles.PlanSync(other,slots);throw new Exception("Foreign project sync should fail.");}catch(ContentValidationException){}
        var directory=Directory.CreateTempSubdirectory("halo-slot-replace-");
        try
        {
            foreach(var slot in slots)File.WriteAllText(Path.Combine(directory.FullName,slot.Key+".json"),slot.Value);
            string f6=Path.Combine(directory.FullName,"f6.json"), f7=Path.Combine(directory.FullName,"f7.json");
            File.WriteAllText(f6,SkillPublicationJson.Write(SkillPublicationJson.Create(other,"basic-shot")));
            var warning=PublicationFiles.LoadWarning(directory.FullName,"f6",other.Id);
            if(warning==null||!warning.Contains("F7")||warning.Contains("同步两槽")||File.ReadAllText(f7)!=slots["f7"])
                throw new Exception("A mixed pair must ask to republish the other slot without changing it.");
            File.WriteAllText(f7,SkillPublicationJson.Write(SkillPublicationJson.Create(other,"summon-gunners")));
            if(PublicationFiles.LoadWarning(directory.FullName,"f7",other.Id)!=null)
                throw new Exception("Replacing the second slot from the same workspace must resolve the mixed pair.");
        }
        finally {directory.Delete(true);}
        Console.WriteLine("PASS: slot sync preserves roots, refuses foreign projects, and explicit sequential replacement resolves a mixed pair.");
    }
}
