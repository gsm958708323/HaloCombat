using Combat.Config;
using SkillLab;
using System.Collections.Concurrent;

var paths=new HostPaths(args,Directory.GetCurrentDirectory(),AppContext.BaseDirectory);var root=paths.Root;
if(args.Contains("--upgrade-effects")){EffectNodeMigration.Run(root);return;}
if(args.Contains("--split-content"))
{
    foreach(var relative in new[]{"Content/SkillLab/project.json","Assets/StreamingAssets/Combat/project.json","Tools/SkillLab/.local/project.json"})
    {
        string path=Path.Combine(root,relative);if(!File.Exists(path))continue;
        var project=SkillProjectFiles.Read(path);string before=SkillProjectJson.Write(project);SkillProjectFiles.Write(path,project);
        if(before!=SkillProjectJson.Write(SkillProjectFiles.Read(path)))throw new InvalidOperationException("Content changed while splitting: "+relative);
        Console.WriteLine("Split "+relative+" -> "+SkillProjectFiles.ReferencedPaths(path).Length+" modules");
    }
    return;
}
if(args.Contains("--self-test"))
{
    try { PreviewChecks.Run(root); }
    catch(Exception error) { Console.Error.WriteLine(error.ToString()); Environment.ExitCode=1; }
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
var dataDirectory=paths.Data;
Directory.CreateDirectory(dataDirectory);
var projectFile=Path.Combine(dataDirectory,"project.json");
var exampleFile=Path.Combine(root,"Content/SkillLab/project.json");
if(!File.Exists(projectFile))
{var initial=SkillProjectFiles.Read(exampleFile);initial.Preview=SkillPreviewDefaults.Create();SkillProjectFiles.Write(projectFile,initial);}
var jsonLock=new SemaphoreSlim(1);
var projectStore=new ProjectFileStore(projectFile);
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
    var snapshot=await projectStore.Read();context.Response.Headers.ETag=snapshot.ETag;context.Response.Headers.CacheControl="no-store";
    return Results.Text(snapshot.Json,"application/json");
});
app.MapGet("/api/examples",()=>Results.Text(SkillProjectJson.Write(SkillProjectFiles.Read(exampleFile)),"application/json"));
var runtimeFile=Path.Combine(root,"Assets/StreamingAssets/Combat/project.json");
app.MapGet("/api/runtime",()=>Results.Text(SkillProjectJson.Write(SkillProjectFiles.Read(runtimeFile)),"application/json"));
app.MapPost("/api/runtime/publish",async(HttpContext context)=>
{
    var project=await ReadProject(context);var compiled=CombatContentCompiler.Compile(project);
    if(compiled.Runtime==null||project.Runtime.Actors.Length==0)throw new ContentValidationException("$.runtime.actors","正式内容需要角色定义。");
    project.Editor=null;
    await jsonLock.WaitAsync();
    try{SkillProjectFiles.Write(runtimeFile,project);}
    finally{jsonLock.Release();}
    return Results.Ok(new{message="正式JSON已发布；Arena按F5载入，演示场景按F1重建，训练场点击重置。"});
});
app.MapGet("/api/templates/{id}",(string id)=>
{var project=SkillProjectFiles.Read(exampleFile);return Results.Text(SkillProjectJson.Write(SkillProjectJson.SelectTemplate(project,id)),"application/json");});
app.MapPost("/api/project",async(HttpContext context)=>
{
    var document=await ReadProject(context);
    var snapshot=await projectStore.Save(document,context.Request.Headers.IfMatch);
    context.Response.Headers.ETag=snapshot.ETag;
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
    string json=SkillProjectJson.Write(imported.Project);
    bool needsLayout=imported.NeedsLayout;
    return previewDetachment
        ?Results.Ok(new{json,needsLayout,detachedReferences=imported.DetachedReferences})
        :Results.Ok(new{json,needsLayout});
});
app.MapPost("/api/export/{kind}",async(string kind,HttpContext context)=>
{var project=await ReadProject(context);return Results.Ok(SkillJsonTransfer.Export(project,kind,context.Request.Query["skillId"]));});
app.MapGet("/api/publications",async()=>
{
    await jsonLock.WaitAsync();
    try {var directory=paths.Publications;return Results.Ok(new[]{PublicationInspection.Read(directory,"f6"),PublicationInspection.Read(directory,"f7")});}
    finally{jsonLock.Release();}
});
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
app.MapPost("/api/publish/sync",async(HttpContext context)=>
{
    var project=await ReadProject(context);var directory=paths.Publications;
    await jsonLock.WaitAsync();
    try
    {
        var plan=PublicationFiles.PlanSync(project,PublicationFiles.Read(directory));
        foreach(var pair in plan){string path=Path.Combine(directory,pair.Key+".json"),temporary=path+".tmp";await File.WriteAllTextAsync(temporary,SkillPublicationJson.Write(pair.Value));File.Move(temporary,path,true);}
        return Results.Ok(new{message="已按当前工程同步现有槽位；SkillLab按F5或点击Reload载入。",slots=plan.ToDictionary(p=>p.Key,p=>new{skillId=p.Value.SkillId,revision=p.Value.Revision})});
    }
    finally{jsonLock.Release();}
});
app.MapPost("/api/publish/{slot}",async(string slot,HttpContext context)=>
{
    if(slot!="f6"&&slot!="f7")return Results.BadRequest(new{error="Expected f6 or f7."});
    var project=await ReadProject(context);
    var publication=SkillPublicationJson.Create(project,context.Request.Query["skillId"]);
    var directory=paths.Publications;Directory.CreateDirectory(directory);
    var path=Path.Combine(directory,slot+".json");
    await jsonLock.WaitAsync();
    try {var temporary=path+".tmp";await File.WriteAllTextAsync(temporary,SkillPublicationJson.Write(publication));File.Move(temporary,path,true);}
    finally{jsonLock.Release();}
    string warning=PublicationFiles.LoadWarning(directory);
    return Results.Ok(new{written=true,path,revision=publication.Revision,skillId=publication.SkillId,
        message=warning??"已发布到 "+slot.ToUpperInvariant()+"；Unity SkillLab 场景按 F5 重载，再按 F6/F7 施放。"});
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
