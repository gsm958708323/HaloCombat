using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class WorkspaceStoreChecks
{
    public static async Task Run(string root)
    {
        string examples=Path.Combine(root,"Content/SkillLab/project.json");
        string formal=Path.Combine(root,"Assets/StreamingAssets/Combat/project.json");
        string directory=Path.Combine(Path.GetTempPath(),"halo-workspaces-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await Fresh(Path.Combine(directory,"fresh"),examples,formal);
            await Legacy(Path.Combine(directory,"legacy-lab"),examples,formal,WorkspaceStores.Lab);
            await Legacy(Path.Combine(directory,"legacy-arena"),examples,formal,WorkspaceStores.Arena);
            await Legacy(Path.Combine(directory,"known-lab-with-arena"),examples,formal,WorkspaceStores.Lab,true,"halo-skill-lab");
            await Legacy(Path.Combine(directory,"known-arena-without-arena"),examples,formal,WorkspaceStores.Arena,false,"halo-combat-runtime");
            await Legacy(Path.Combine(directory,"custom-arena"),examples,formal,WorkspaceStores.Arena,true,"custom-arena");
            await Legacy(Path.Combine(directory,"custom-lab"),examples,formal,WorkspaceStores.Lab,false,"custom-lab");
            await Publish(Path.Combine(directory,"publish"),examples,formal);
            RuntimeWithoutEditor(Path.Combine(directory,"runtime"),formal);
            Console.WriteLine("PASS: identity-first legacy routing and backup, saved-version publish, scoped closure and resource subsets, dormant and active references.");
        }
        finally { Directory.Delete(directory,true); }
    }

    static async Task Fresh(string directory,string examples,string formal)
    {
        var stores=new WorkspaceStores(directory,examples,formal);
        Require(stores.Resolve(null)==WorkspaceStores.Lab,"Fresh workspaces must default to Lab.");
        var arena=stores.Get(WorkspaceStores.Arena);
        var lab=stores.Get(WorkspaceStores.Lab);
        var arenaVersion=await arena.Read();
        var labVersion=await lab.Read();
        var arenaProject=SkillProjectJson.Read(arenaVersion.Json);
        var labProject=SkillProjectJson.Read(labVersion.Json);
        Require(arenaProject.Id==SkillProjectFiles.Read(formal).Id && arenaProject.Runtime.Arena!=null,"Arena must seed from formal content.");
        Require(labProject.Id==SkillProjectFiles.Read(examples).Id,"Lab must seed from examples.");
        try {await lab.Save(labProject,arenaVersion.ETag);throw new Exception("Lab accepted the Arena ETag.");}
        catch(ProjectVersionException error) when(error.StatusCode==409){}
        labProject.Name="Lab updated";
        var saved=await lab.Save(labProject,labVersion.ETag);
        Require(saved.ETag==(await lab.Read()).ETag && arenaVersion==(await arena.Read()),"A Lab save cannot change the Arena snapshot.");
        var selected=lab.PrepareCopy("summon-gunners");
        Require(selected.Project.Editor.SelectedSkill=="summon-gunners" && selected.Project.SummonBlueprints.Length>0 &&
                selected.Project.Skills.Length<labProject.Skills.Length && selected.Project.Runtime?.Arena==null,"Copy source must contain only the selected closure and supporting resources.");
        var rootGraph=labProject.Skills.Single(s=>s.Id=="basic-shot");
        rootGraph.Nodes=rootGraph.Nodes.Append(new SkillNodeDocument
        {
            Id="inactive-buff",Kind="buff",Enabled=false,Parameters=new JObject{["buff"]="burn"}
        }).ToArray();
        labProject.Buffs.Single(buff=>buff.Id=="burn").Nodes.First(node=>node.Kind=="damage").Parameters["flat"]=-1;
        await lab.Save(labProject,saved.ETag);
        var detached=lab.PrepareCopy("basic-shot");
        Require(detached.DetachedReferences.Single().OriginalId=="burn" &&
            detached.Project.Skills.Single().Nodes.Single(n=>n.Id=="inactive-buff").Parameters["buff"]==null,
            "Cross-copy must report and remove omitted references from the candidate.");
        Require(SkillProjectJson.Read((await lab.Read()).Json).Skills.Single(s=>s.Id=="basic-shot").Nodes.Last().Parameters["buff"]!=null,
            "Cross-copy must not modify the source draft.");
        try {lab.PrepareCopy("burn-shot");throw new Exception("Compiler accepted an invalid active Buff.");}
        catch(ContentValidationException error) when(error.Path.Contains(".parameters.flat")){}

        var cueClosure=arena.PrepareCopy("g1").Project;
        Require(cueClosure.Runtime.Cues.Any(cue=>cue.Id==101) && cueClosure.Runtime.Cues.Length<arenaProject.Runtime.Cues.Length,
            "Copy must include the required Cue subset.");
        var bodyClosure=arena.PrepareCopy("arena-barrel").Project;
        Require(bodyClosure.Runtime.Views.Select(view=>view.Id).SequenceEqual(new[]{"buff_barrel_view"}),
            "Copy must include the required body view subset.");
    }

    static async Task Legacy(string directory,string examples,string formal,string kind,bool? arenaShape=null,string identity=null)
    {
        Directory.CreateDirectory(directory);
        string legacy=Path.Combine(directory,"project.json");
        var original=JObject.Parse(SkillProjectJson.Write(SkillProjectFiles.Read((arenaShape??(kind==WorkspaceStores.Arena))?formal:examples)));
        if(identity!=null)original["id"]=identity;
        original["name"]="Legacy draft "+kind;
        // Preserve the distinction between a missing position and an explicit origin across migration.
        ((JObject)original["skills"][0]["nodes"][0]).Remove("position");
        string portable=original.ToString();
        if(kind==WorkspaceStores.Arena) File.WriteAllText(legacy,portable);
        else
        {
            var loaded=SkillJsonTransfer.Import(portable);
            SkillProjectFiles.Write(legacy,loaded.Project,SkillProjectDiskLayout.Restore(original,false));
        }
        var files=SkillProjectFiles.ReferencedPaths(legacy).Append(legacy).ToDictionary(file=>Path.GetRelativePath(directory,file),File.ReadAllBytes);
        var stores=new WorkspaceStores(directory,examples,formal);
        Require(stores.Resolve(null)==kind,"Legacy workspaces must route old callers to the migrated draft.");
        var migrated=await stores.Get(kind).Read();
        Require(migrated.NeedsLayout && SkillProjectJson.Read(migrated.Json).Name==(string)original["name"],"Legacy content and missing layout must survive migration.");
        foreach(var file in files)
        {
            Require(File.ReadAllBytes(Path.Combine(directory,file.Key)).SequenceEqual(file.Value),"The legacy input must remain intact.");
            Require(File.ReadAllBytes(Path.Combine(directory,"backups","legacy-project",file.Key)).SequenceEqual(file.Value),"Backup must be byte identical.");
        }
        var changed=SkillProjectJson.Read(migrated.Json);changed.Name="New draft after migration";
        var saved=await stores.Get(kind).Save(changed,migrated.ETag);
        var restarted=new WorkspaceStores(directory,examples,formal);
        Require(restarted.Default==kind && saved==await restarted.Get(kind).Read(),"Restart must keep the selected workspace and its edits.");
    }

    static async Task Publish(string directory,string examples,string formal)
    {
        string target=Path.Combine(directory,"formal","project.json");
        SkillProjectFiles.Write(target,SkillProjectFiles.Read(formal));
        string data=Path.Combine(directory,"data");
        var stores=new WorkspaceStores(data,examples,target);
        var arena=stores.Get(WorkspaceStores.Arena);
        var lab=stores.Get(WorkspaceStores.Lab);
        var original=await arena.Read();
        var project=SkillProjectJson.Read(original.Json);
        string before=SkillProjectJson.Write(SkillProjectFiles.Read(target));
        project.Name="Saved version";
        // Independent stores exercise the shared on-disk mutex and revision contract.
        var secondStore=new ProjectFileStore(Path.Combine(data,"arena","project.json"),WorkspaceStores.Arena);
        var saved=await secondStore.Save(project,original.ETag);
        RejectPublish(()=>arena.PublishArena(null,target),428);
        RejectPublish(()=>arena.PublishArena(original.ETag,target),409);
        RejectPublish(()=>lab.PublishArena(saved.ETag,target),403);
        Require(SkillProjectJson.Write(SkillProjectFiles.Read(target))==before,"Rejected publications must preserve formal content.");
        project.Name="Unsaved version";
        arena.PublishArena(saved.ETag,target);
        Require(SkillProjectFiles.Read(target).Name=="Saved version","Official publishing must use the saved Arena draft.");
    }

    static void RejectPublish(Action publish,int status)
    {
        try {publish();throw new Exception("Expected publish rejection.");}
        catch(ProjectVersionException error) when(error.StatusCode==status){}
    }

    static void RuntimeWithoutEditor(string directory,string formal)
    {
        string index=Path.Combine(directory,"project.json");
        var original=SkillProjectFiles.Read(formal);
        SkillProjectFiles.Write(index,original);
        string editor=SkillProjectFiles.ReferencedPaths(index).Single(file=>Path.GetFileName(file)=="editor.json");
        File.WriteAllText(editor,"invalid editor data");
        var runtime=new RuntimeProjectLoader(index);
        Require(runtime.Project.Editor==null && runtime.Content.Runtime.Arena!=null,"Runtime loader must compile when the editor shard is malformed.");
        File.Delete(editor);
        Require(new RuntimeProjectLoader(index).Project.Skills.Length==original.Skills.Length,"Runtime loader must compile when the editor shard is absent.");
    }

    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
}
