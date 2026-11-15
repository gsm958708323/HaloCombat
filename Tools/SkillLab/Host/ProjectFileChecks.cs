using Combat.Config;

namespace SkillLab;

internal static class ProjectFileChecks
{
    public static async Task Run(string root)
    {
        var directory=Path.Combine(root,"Tools/SkillLab/.local/verification/project-store-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"project.json");var source=SkillProjectFiles.Read(Path.Combine(root,"Content/SkillLab/project.json"));
        try
        {
            SkillProjectFiles.Write(path,source);var store=new ProjectFileStore(path);var original=await store.Read();
            var module=SkillProjectFiles.ReferencedPaths(path).First(p=>p.EndsWith("basic-shot.json"));
            string beforeModule=File.ReadAllText(module);File.WriteAllText(module,beforeModule.Replace("普通射击","磁盘外部修改"));
            Require((await store.Read()).ETag!=original.ETag,"A module change must invalidate the project ETag.");
            await Reject(()=>store.Save(source,original.ETag),409);File.WriteAllText(module,beforeModule);
            await Reject(()=>store.Save(source,""),428);Require((await store.Read()).Json==original.Json,"Missing ETag must not overwrite disk.");
            source.Skills=[];source.Buffs=[];source.SummonBlueprints=[];var empty=await store.Save(source,original.ETag);
            Require(SkillProjectJson.Read(empty.Json).Skills.Length==0,"Empty project must persist.");
            await Reject(()=>store.Save(source,original.ETag),409);Require((await store.Read()).ETag==empty.ETag,"Stale write must retain disk.");
            var left=SkillProjectJson.Read(empty.Json);left.Name="left";var right=SkillProjectJson.Read(empty.Json);right.Name="right";
            async Task<bool> Attempt(SkillProjectDocument p){try{await store.Save(p,empty.ETag);return true;}catch(ProjectVersionException error) when(error.StatusCode==409){return false;}}
            var outcomes=await Task.WhenAll(Attempt(left),Attempt(right));Require(outcomes.Count(value=>value)==1,"Only one concurrent writer can commit an ETag.");
            var last=await store.Read();var invalid=SkillProjectJson.Read(last.Json);invalid.Id="";
            try {await store.Save(invalid,last.ETag);throw new Exception("Invalid document was saved.");}catch(ContentValidationException){}
            Require((await store.Read()).Json==last.Json,"Rejected structure must leave disk untouched.");
            Console.WriteLine("PASS: empty drafts, conditional atomic save, concurrent ETag conflict, bad document preserves disk.");
        }
        finally {Directory.Delete(directory,true);}
    }
    static async Task Reject(Func<Task<ProjectFileSnapshot>> action,int status)
    {
        try{await action();throw new Exception("Expected save rejection.");}
        catch(ProjectVersionException error){Require(error.StatusCode==status,"Unexpected rejection status.");}
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
