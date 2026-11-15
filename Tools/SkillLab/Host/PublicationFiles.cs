using Combat.Config;

namespace SkillLab;

internal static class PublicationFiles
{
    public static Dictionary<string,string> Read(string directory)
    {
        var result=new Dictionary<string,string>();foreach(string slot in new[]{"f6","f7"}){string path=Path.Combine(directory,slot+".json");if(File.Exists(path))result.Add(slot,File.ReadAllText(path));}return result;
    }
    public static Dictionary<string,SkillPublicationDocument> PlanSync(SkillProjectDocument project,IReadOnlyDictionary<string,string> current)
    {
        if(current.Count==0)throw new ContentValidationException("$.slots","请先发布至少一个技能槽，再同步现有槽位。");
        var result=new Dictionary<string,SkillPublicationDocument>();
        foreach(var pair in current)
        {
            var old=SkillPublicationJson.Read(pair.Value).Document;
            if(old.Project.Id!=project.Id)throw new ContentValidationException("$.slots."+pair.Key,"两槽需要来自当前工程；先为此槽选择技能并重新发布。");
            result.Add(pair.Key,SkillPublicationJson.Create(project,old.SkillId));
        }
        using(var session=new PublishedSkillSession())session.Replace(result.ToDictionary(p=>p.Key,p=>SkillPublicationJson.Write(p.Value)));
        return result;
    }
    public static string LoadWarning(string directory)
    {
        try{using var session=new PublishedSkillSession();session.Replace(Read(directory));return null;}
        catch(ContentValidationException error){return "当前磁盘两槽暂不能共同载入："+error.Message+"。同工程共享内容修改后，请点击“同步两槽”。";}
    }
}
