using Combat.Config;

namespace SkillLab;

internal static class EffectNodeMigration
{
    public static void Run(string root)
    {
        var backup=Path.Combine(root,"Tools/SkillLab/.local/backups/effect-nodes-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        var changes=new List<(string Path,SkillProjectDocument Document)>();
        foreach(string relative in new[]{"Content/SkillLab/project.json","Assets/StreamingAssets/Combat/project.json","Tools/SkillLab/.local/project.json"})
        {
            string path=Path.Combine(root,relative);if(!File.Exists(path))continue;
            var project=SkillProjectFiles.Read(path);CombatContentCompiler.Compile(project);
            foreach(var file in SkillProjectFiles.ReferencedPaths(path).Append(path).Distinct())
            {
                string destination=Path.Combine(backup,Path.GetRelativePath(root,file));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination);
            }
            changes.Add((path,project));
        }
        foreach(var change in changes)
        {
            SkillProjectFiles.Write(change.Path,change.Document);
            var read=SkillProjectFiles.Read(change.Path);CombatContentCompiler.Compile(read);
            if(SkillProjectJson.Write(read)!=SkillProjectJson.Write(change.Document))throw new Exception("Migration roundtrip differed: "+change.Path);
            Console.WriteLine("Migrated "+Path.GetRelativePath(root,change.Path)+": "+read.Skills.Length+" skills");
        }
        Console.WriteLine("Original content backup: "+backup);
    }
}
