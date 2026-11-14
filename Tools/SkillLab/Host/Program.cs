using Combat.Config;
using SkillLab;
using System.Collections.Concurrent;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
// `dotnet run --project` does not change the working directory; find the checked-in project root.
var cursor = new DirectoryInfo(Directory.GetCurrentDirectory());
while (cursor != null && !File.Exists(Path.Combine(cursor.FullName, "Combat.csproj"))) cursor=cursor.Parent;
if(cursor != null)root=cursor.FullName;
if(args.Contains("--self-test"))
{
    try { PreviewChecks.Run(root); }
    catch(Exception error) { Console.Error.WriteLine(error.ToString()); Environment.ExitCode=1; }
    return;
}
var builder=WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:4192");
builder.WebHost.ConfigureKestrel(options=>options.Limits.MaxRequestBodySize=4*1024*1024);
var app=builder.Build();
var sessions=new ConcurrentDictionary<string,SkillSimulation>();
var dataDirectory=Path.Combine(root,"Tools/SkillLab/.local");
Directory.CreateDirectory(dataDirectory);
var projectFile=Path.Combine(dataDirectory,"project.json");
var exampleFile=Path.Combine(root,"Content/SkillLab/project.json");
if(!File.Exists(projectFile))File.Copy(exampleFile,projectFile);
var jsonLock=new SemaphoreSlim(1);
var projectStore=new ProjectFileStore(projectFile);
app.Use(async(context,next)=>
{
    // Local tool: reject cross-site writes, including requests from a public site's loopback fetch.
    string origin=context.Request.Headers.Origin;
    if(!string.IsNullOrEmpty(origin) && origin!="http://127.0.0.1:4192" && origin!="http://localhost:4192" && origin!="http://127.0.0.1:5173")
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
app.MapGet("/api/examples",()=>Results.Text(File.ReadAllText(exampleFile),"application/json"));
var runtimeFile=Path.Combine(root,"Assets/StreamingAssets/Combat/project.json");
app.MapGet("/api/runtime",()=>Results.Text(File.ReadAllText(runtimeFile),"application/json"));
app.MapPost("/api/runtime/publish",async(HttpContext context)=>
{
    var project=await ReadProject(context);var compiled=CombatContentCompiler.Compile(project);
    if(compiled.Runtime==null||project.Runtime.Actors.Length==0)throw new ContentValidationException("$.runtime.actors","正式内容需要角色定义。");
    project.Editor=null;
    await jsonLock.WaitAsync();
    try{var temporary=runtimeFile+".tmp";await File.WriteAllTextAsync(temporary,SkillProjectJson.Write(project));File.Move(temporary,runtimeFile,true);}
    finally{jsonLock.Release();}
    return Results.Ok(new{message="正式JSON已发布；Arena按F5载入，演示场景按F1重建，训练场点击重置。"});
});
app.MapGet("/api/templates/{id}",(string id)=>
{var project=SkillProjectJson.Read(File.ReadAllText(exampleFile));return Results.Text(SkillProjectJson.Write(SkillProjectJson.SelectTemplate(project,id)),"application/json");});
app.MapPost("/api/project",async(HttpContext context)=>
{
    var document=await ReadProject(context);
    var snapshot=await projectStore.Save(document,context.Request.Headers.IfMatch);
    context.Response.Headers.ETag=snapshot.ETag;
    return Results.Ok(new{saved=true});
});
app.MapPost("/api/document/validate",async(HttpContext context)=>
{var project=await ReadProject(context);SkillProjectStructure.Validate(project);return Results.Ok(new{valid=true});});
app.MapGet("/api/publications",async()=>
{
    await jsonLock.WaitAsync();
    try {var directory=Path.Combine(root,"Assets/StreamingAssets/Combat");return Results.Ok(new[]{PublicationInspection.Read(directory,"f6"),PublicationInspection.Read(directory,"f7")});}
    finally{jsonLock.Release();}
});
app.MapPost("/api/validate",async(HttpContext context)=>
{var project=await ReadProject(context);CombatContentCompiler.Compile(project);return Results.Ok(new{valid=true});});
app.MapPost("/api/preview",async(HttpContext context)=>
{
    var project=await ReadProject(context);var content=CombatContentCompiler.Compile(project);
    if(sessions.Count>=16)return Results.Problem("Close unused preview sessions.",statusCode:409);
    var session=new SkillSimulation(project,content);var id=Guid.NewGuid().ToString("N");sessions[id]=session;
    return Results.Ok(new{id,state=session.Snapshot()});
});
app.MapPost("/api/preview/{id}/command",async(string id,HttpContext context)=>
{
    if(!sessions.TryGetValue(id,out var session))return Results.NotFound();
    var text=await new StreamReader(context.Request.Body).ReadToEndAsync();
    var command=Newtonsoft.Json.Linq.JObject.Parse(text);
    lock(session)
    {
        var kind=command.Value<string>("kind");
        object result=null;
        if(kind=="step")session.Advance(command.Value<int?>("steps")??1);
        else if(kind=="cast")result=session.Cast(command.Value<string>("skillId"),command.Value<float?>("x")??0,command.Value<float?>("z")??0);
        else if(kind=="release")
        {if(command["x"]!=null&&command["z"]!=null)session.Aim(command.Value<float>("x"),command.Value<float>("z"));session.Release();}
        else if(kind=="cancel")session.CancelInput();
        else if(kind=="aim")session.Aim(command.Value<float?>("x")??0,command.Value<float?>("z")??0);
        else if(kind=="move")session.Move(command.Value<float?>("x")??0,command.Value<float?>("z")??0);
        else if(kind=="place")session.Place(command.Value<string>("actorId"),command.Value<float?>("x")??0,command.Value<float?>("z")??0);
        else if(kind=="remove")session.Remove(command.Value<string>("entityId"));
        else if(kind=="remove-buff")result=session.RemoveBuff(command.Value<string>("actorId"),command.Value<int>("instanceId"));
        else if(kind=="interrupt")session.Interrupt();
        else if(kind=="death")session.Interrupt(true);
        else if(kind=="hitstop")session.RequestHitstop(command.Value<int?>("frames")??10);
        else if(kind!="snapshot")throw new ContentValidationException("$.kind","Unknown command.");
        return Results.Ok(new{result,state=session.Snapshot()});
    }
});
app.MapDelete("/api/preview/{id}",(string id)=>
{if(sessions.TryRemove(id,out var session))lock(session)session.Dispose();return Results.NoContent();});
app.MapPost("/api/publish/{slot}",async(string slot,HttpContext context)=>
{
    if(slot!="f6"&&slot!="f7")return Results.BadRequest(new{error="Expected f6 or f7."});
    var project=await ReadProject(context);
    var publication=SkillPublicationJson.Create(project,context.Request.Query["skillId"]);
    var directory=Path.Combine(root,"Assets/StreamingAssets/Combat");Directory.CreateDirectory(directory);
    var path=Path.Combine(directory,slot+".json");
    await jsonLock.WaitAsync();
    try {var temporary=path+".tmp";await File.WriteAllTextAsync(temporary,SkillPublicationJson.Write(publication));File.Move(temporary,path,true);}
    finally{jsonLock.Release();}
    return Results.Ok(new{written=true,path,revision=publication.Revision,skillId=publication.SkillId,
        message="已发布到 "+slot.ToUpperInvariant()+"；Unity SkillLab 场景按 F5 重载，再按 F6/F7 施放。"});
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
