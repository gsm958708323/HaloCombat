using Combat.Config;
using SkillLab;
using System.Collections.Concurrent;

bool upgradeEffects = args.Contains("--upgrade-effects");
bool splitContent = args.Contains("--split-content") || args.Contains("--migrate-tracked-layout");
// Reject missing values before ASP.NET can consume the next long option as a value.
for (int i = 0; i < args.Length; i++)
{
    string argument = args[i];
    if (argument is "--upgrade-effects" or "--split-content" or "--migrate-tracked-layout")
        continue;
    if (argument.Contains('=') || !(argument.StartsWith("--", StringComparison.Ordinal) || argument.StartsWith('/')))
        continue;

    int valueIndex = i + 1;
    if (valueIndex == args.Length || args[valueIndex].StartsWith("--", StringComparison.Ordinal))
    {
        Console.Error.WriteLine($"Unknown option or missing value: {argument}");
        Environment.ExitCode = 2;
        return;
    }

    i = valueIndex;
}

var paths=new HostPaths(args,Directory.GetCurrentDirectory(),AppContext.BaseDirectory);var root=paths.Root;
if(upgradeEffects){EffectNodeMigration.Run(root);return;}
if(splitContent)
{
    foreach(var relative in new[]{"Content/SkillLab/project.json","Assets/StreamingAssets/Combat/project.json"})
    {
        string path=Path.Combine(root,relative);if(!File.Exists(path))continue;
        ProjectFileTransaction.Locked(path,()=>
        {
            var source=SkillProjectFiles.ReadAuthoring(path);
            SkillProjectFiles.Write(path,source.Project,source.MissingPositions);
            var migrated=SkillProjectFiles.ReadAuthoring(path);
            if(SkillProjectJson.Write(source.Project)!=SkillProjectJson.Write(migrated.Project)||!source.MissingPositions.SetEquals(migrated.MissingPositions))
                throw new InvalidOperationException("Content changed while splitting: "+relative);
            return true;
        });
        Console.WriteLine("Split "+relative+" -> "+SkillProjectFiles.ReferencedPaths(path).Length+" modules");
    }
    return;
}
var builder=WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore",LogLevel.Warning);
builder.WebHost.UseUrls(paths.Url);
builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=4*1024*1024);
var app=builder.Build();
app.Use(async(context,next)=>
{
    // Hashed assets may be cached; the HTML entry must discover the current hash.
    context.Response.OnStarting(()=>
    {if(context.Response.ContentType?.StartsWith("text/html",StringComparison.OrdinalIgnoreCase)==true)context.Response.Headers.CacheControl="no-store";return Task.CompletedTask;});
    await next();
});
var sessions=new ConcurrentDictionary<string,SkillSimulation>();
var exampleFile=Path.Combine(root,"Content/SkillLab/project.json");
var runtimeFile=Path.Combine(root,"Assets/StreamingAssets/Combat/project.json");
var workspaces=new WorkspaceStores(paths.Data,exampleFile,runtimeFile);
var jsonLock=new SemaphoreSlim(1);
app.Use(async(context,next)=>
{
    // Local tool: reject cross-site writes, including requests from a public site's loopback fetch.
    string origin=context.Request.Headers.Origin;
    if(!paths.AllowsOrigin(origin))
    {context.Response.StatusCode=403;return;}
    try{await next();}
    catch(ContentValidationException error){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{error=error.Message,path=error.Path});}
    catch(Newtonsoft.Json.JsonException error){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{error=error.Message});}
    catch(ProjectVersionException error){context.Response.StatusCode=error.StatusCode;await context.Response.WriteAsJsonAsync(new{error=error.Message});}
    catch(Exception error) when(error is IOException or UnauthorizedAccessException)
    {context.Response.StatusCode=500;await context.Response.WriteAsJsonAsync(new{error="磁盘操作失败："+error.Message});}
});
app.MapGet("/api/project",async(HttpContext context)=>
{
    string workspace=workspaces.Resolve(context.Request.Query["workspace"]);
    var snapshot=await workspaces.Get(workspace).Read();
    context.Response.Headers.ETag=snapshot.ETag;
    context.Response.Headers["X-Workspace"]=workspace;
    if(snapshot.NeedsLayout)context.Response.Headers["X-Needs-Layout"]="true";
    context.Response.Headers.CacheControl="no-store";
    return Results.Text(snapshot.Json,"application/json");
});
app.MapGet("/api/project/copy-source",(HttpContext context)=>
{
    string workspace=workspaces.Resolve(context.Request.Query["workspace"]);
    context.Response.Headers.CacheControl="no-store";
    return ImportCandidate(workspaces.Get(workspace).PrepareCopy(context.Request.Query["skillId"]));
});
app.MapGet("/api/examples",()=>Results.Text(SkillProjectJson.Write(SkillProjectFiles.Read(exampleFile)),"application/json"));
app.MapGet("/api/runtime",()=>Results.Text(SkillProjectJson.Write(SkillProjectFiles.Read(runtimeFile)),"application/json"));
app.MapPost("/api/runtime/publish",async(HttpContext context)=>
{
    if(context.Request.Query["workspace"]!=WorkspaceStores.Arena)
        throw new ProjectVersionException("只有 Arena 工作区可以发布正式内容。",403);
    await jsonLock.WaitAsync();
    try{workspaces.Get(WorkspaceStores.Arena).PublishArena(context.Request.Headers.IfMatch,runtimeFile);}
    finally{jsonLock.Release();}
    return Results.Ok(new{message="正式工程已发布。Arena 正式或木桩模式按 F5 重载。"});
});
app.MapGet("/api/templates/{id}",(string id)=>
{var project=SkillProjectFiles.Read(exampleFile);return Results.Text(SkillProjectJson.Write(SkillProjectJson.SelectTemplate(project,id)),"application/json");});
app.MapPost("/api/project",async(HttpContext context)=>
{
    string workspace=workspaces.Resolve(context.Request.Query["workspace"]);
    var document=await ReadProject(context);
    var snapshot=await workspaces.Get(workspace).Save(document,context.Request.Headers.IfMatch);
    context.Response.Headers.ETag=snapshot.ETag;
    context.Response.Headers["X-Workspace"]=workspace;
    return Results.Ok(new{saved=true});
});
app.MapPost("/api/document/validate",async(HttpContext context)=>
{var project=await ReadProject(context);SkillProjectStructure.Validate(project);return Results.Ok(new{valid=true});});
app.MapPost("/api/document/import",async(HttpContext context)=>
{
    var request=await context.Request.ReadFromJsonAsync<Dictionary<string,string>>();
    if(request==null||!request.TryGetValue("text",out var text))throw new ContentValidationException("$.text","Import file text is required.");
    bool previewDetachment=context.Request.Query["detachMissingReferences"]=="preview";
    var imported=previewDetachment?SkillJsonTransfer.PreviewAuthoringImport(text):SkillJsonTransfer.Import(text);
    return previewDetachment
        ?ImportCandidate(imported)
        :Results.Ok(new{json=SkillProjectJson.Write(imported.Project),needsLayout=imported.NeedsLayout});
});
app.MapPost("/api/export/{kind}",async(string kind,HttpContext context)=>
{var project=await ReadProject(context);return Results.Ok(SkillJsonTransfer.Export(project,kind,context.Request.Query["skillId"]));});
app.MapGet("/api/publications", () => Results.Ok(PublicationFiles.Inspect(paths.Publications)));
app.MapPost("/api/validate",async(HttpContext context)=>
{var project=await ReadProject(context);CombatContentCompiler.Compile(project);return Results.Ok(new{valid=true});});
app.MapPost("/api/preview",async(HttpContext context)=>
{
    var project=await ReadProject(context);
    project.Preview.UnlimitedAmmo=context.Request.Query["unlimitedAmmo"]!="false";
    var content=CombatContentCompiler.Compile(project);
    if(sessions.Count>=16)return Results.Problem("Close unused preview sessions.",statusCode:409);
    var session=new SkillSimulation(project,content,context.Request.Query["recordInputs"]=="true");var id=Guid.NewGuid().ToString("N");sessions[id]=session;
    return Results.Ok(new{id,state=session.Snapshot(),recording=session.Recording});
});
app.MapPost("/api/preview/{id}/command",async(string id,HttpContext context)=>
{
    if(!sessions.TryGetValue(id,out var session))return Results.NotFound();
    var text=await new StreamReader(context.Request.Body).ReadToEndAsync();
    var command=SkillSimulationCommands.Read(text);
    lock(session)
    {
        var result=session.Execute(command);return PreviewFrames.Response(session,context,result);
    }
});
app.MapPost("/api/preview/{id}/commands",async(string id,HttpContext context)=>
{
    if(!sessions.TryGetValue(id,out var session))return Results.NotFound();
    var text=await new StreamReader(context.Request.Body).ReadToEndAsync();
    var raw=Newtonsoft.Json.Linq.JArray.Parse(text,new Newtonsoft.Json.Linq.JsonLoadSettings{DuplicatePropertyNameHandling=Newtonsoft.Json.Linq.DuplicatePropertyNameHandling.Error});
    if(raw.Count<1||raw.Count>32)throw new ContentValidationException("$","Expected 1..32 preview commands.");
    var commands=raw.Select(value=>SkillSimulationCommands.Read(value.ToString(Newtonsoft.Json.Formatting.None))).ToArray();
    lock(session){foreach(var command in commands)session.Execute(command);return PreviewFrames.Response(session,context);}
});
app.MapGet("/api/preview/{id}/replay",(string id,HttpContext context)=>
{
    if(!sessions.TryGetValue(id,out var session))return Results.NotFound();
    lock(session)return Results.Text(SkillReplayJson.Write(session.CaptureReplay(context.Request.Query["skillId"])),"application/json");
});
app.MapPost("/api/replays",async(HttpContext context)=>
{
    var request=await context.Request.ReadFromJsonAsync<Dictionary<string,string>>();
    if(request==null||!request.TryGetValue("text",out var text))throw new ContentValidationException("$.text","Replay file text is required.");
    var document=SkillReplayJson.Read(text);
    if(sessions.Count>=16)return Results.Problem("Close unused preview sessions.",statusCode:409);
    using var run=new SkillReplayRun(document);
    if(!run.Comparison.Matches)return Results.BadRequest(new{error="复现结果不同："+run.Comparison.Differences[0].Path,comparison=run.Comparison});
    string id=Guid.NewGuid().ToString("N");var simulation=run.TakeSimulation();sessions[id]=simulation;
    return Results.Ok(new{id,state=simulation.Snapshot(),recording=simulation.Recording,comparison=run.Comparison,projectJson=SkillProjectJson.Write(ReplayAuthoringProject(document)),rootSkillId=document.RootSkillId});
});
app.MapDelete("/api/preview/{id}",(string id)=>
{if(sessions.TryRemove(id,out var session))lock(session)session.Dispose();return Results.NoContent();});
app.MapPost("/api/publish/sync", async (HttpContext context) =>
{
    var project = await ReadProject(context);
    var plan = PublicationFiles.Sync(paths.Publications, project);
    return Results.Ok(new
    {
        message = "已按当前工程同步全部现有槽位。Unity SkillLab 按 F5 或点击 Reload 载入。",
        slots = plan.ToDictionary(pair => pair.Key, pair => new { skillId = pair.Value.SkillId, revision = pair.Value.Revision })
    });
});
app.MapPost("/api/publish/{slot}", async (string slot, HttpContext context) =>
{
    if (!PublishedSkillSlots.Ordered.Contains(slot))
        return Results.BadRequest(new { error = "Expected a publication slot: " + string.Join(", ", PublishedSkillSlots.Ordered) + "." });
    var project = await ReadProject(context);
    var publication = SkillPublicationJson.Create(project, context.Request.Query["skillId"]);
    var warning = PublicationFiles.Publish(paths.Publications, slot, publication);
    var number = PublicationFiles.Number(slot);
    return Results.Ok(new
    {
        written = true, path = PublicationFiles.SlotPath(paths.Publications, slot),
        revision = publication.Revision, skillId = publication.SkillId,
        message = warning ?? $"已发布到槽位 {number}。Unity SkillLab 按 F5 重载，再按小键盘 {number} 施放。"
    });
});
var dist=Path.Combine(root,"Tools/SkillLab/Web/dist");
if(Directory.Exists(dist))
{
    var provider=new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dist);
    app.UseDefaultFiles(new DefaultFilesOptions{FileProvider=provider});
    app.UseStaticFiles(new StaticFileOptions{FileProvider=provider});
    app.MapFallback(()=>Results.File(Path.Combine(dist,"index.html"),"text/html"));
}
app.Lifetime.ApplicationStopping.Register(()=>{foreach(var pair in sessions)lock(pair.Value)pair.Value.Dispose();});
await app.RunAsync();

static IResult ImportCandidate(SkillJsonImport imported)
    =>Results.Ok(new{json=SkillProjectJson.Write(imported.Project),needsLayout=imported.NeedsLayout,detachedReferences=imported.DetachedReferences});

static async Task<SkillProjectDocument> ReadProject(HttpContext context)
    =>SkillProjectJson.Read(await new StreamReader(context.Request.Body).ReadToEndAsync());

static SkillProjectDocument ReplayAuthoringProject(SkillReplayDocument replay)
{
    // Replay executes the original document plus its journal. The authoring UI
    // resumes at the final edited configuration, rather than showing old values.
    var project=SkillProjectJson.Read(SkillProjectJson.Write(replay.Project));
    foreach(var input in replay.Inputs)
    {
        var command=input.Command;
        if(command.Kind=="configure")project=SkillProjectJson.Read(SkillProjectJson.Write(command.Project));
        if(command.Kind!="place")continue;
        var actor=project.Preview.Caster.Id==command.ActorId?project.Preview.Caster:project.Preview.Targets.FirstOrDefault(a=>a.Id==command.ActorId);
        if(actor!=null){actor.X=command.X.Value;actor.Z=command.Z.Value;}
    }
    project.Preview.UnlimitedAmmo=replay.Project.Preview.UnlimitedAmmo;return project;
}
