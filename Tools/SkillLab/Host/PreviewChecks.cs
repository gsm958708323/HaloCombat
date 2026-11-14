using Combat.Config;
using Newtonsoft.Json.Linq;

namespace SkillLab;

internal static class PreviewChecks
{
    public static void Run(string root)
    {
        var json=File.ReadAllText(Path.Combine(root,"Content/SkillLab/project.json"));
        var document=SkillProjectJson.Read(json);
        var content=CombatContentCompiler.Compile(document);
        using var first=new SkillSimulation(document,content);
        Assert(first.Cast("basic-shot",0,0).Accepted,"Initial cast must succeed.");
        Assert(!first.Cast("basic-shot",0,0).Accepted,"Cooldown must refuse immediate second cast.");
        Assert(Math.Abs(first.Snapshot().Energy-90)<.001,"Rejected cast must not spend energy.");
        first.Advance(60);
        var result=first.Snapshot();
        Assert(result.Statistics.Projectiles==1,"Must create one real projectile.");
        Assert(result.Statistics.Damage==80,"Projectile must deal 80 damage through Core for seed 1.");
        var hp=result.Actors.First(a=>a.Name=="target-1").Hp.Value;
        Assert(hp<1000,"Target HP must decrease.");
        var roundTrip=SkillProjectJson.Read(SkillProjectJson.Write(document));
        using var second=new SkillSimulation(roundTrip,CombatContentCompiler.Compile(roundTrip));
        second.Cast("basic-shot",0,0);second.Cast("basic-shot",0,0);second.Advance(60);
        Assert(JToken.DeepEquals(JObject.FromObject(result),JObject.FromObject(second.Snapshot())),"Reset/roundtrip must reproduce actual state and events.");
        var invalid=JObject.Parse(json);invalid["skills"][0]["nodes"][2]["parameters"]["speed"]=-1;
        Reject(invalid.ToString(),"speed");
        invalid=JObject.Parse(json);invalid["skills"][0]["nodes"][2]["parameters"]["typo"]=1;
        Reject(invalid.ToString(),"typo");
        invalid=JObject.Parse(json);invalid["skills"][0]["edges"][0]["target"]="missing";
        Reject(invalid.ToString(),"endpoint");
        invalid=JObject.Parse(json);((JArray)invalid["skills"][0]["edges"]).Add(new JObject{
            ["id"]="cycle",["source"]="damage",["sourcePort"]="next",["target"]="projectile",["targetPort"]="execute",["order"]=0});
        Reject(invalid.ToString(),"cycles");
        CheckCharge(document);
        CheckAreas(document);
        CheckSpatial(document);
        CheckObstacles(document);
        CheckSummons(document);
        CheckBuffs(document);
        CheckProjectileBehaviors(document);
        CheckProjectileMotion(document);
        CheckDamageEvents(document);
        ProjectFileChecks.Run(root).GetAwaiter().GetResult();
        RuntimeContentChecks.Run(root);
        AmmoRecastChecks.Run(root);
        ConstructChecks.Run(root);
        ActorFeedbackChecks.Run(root);
        ComboChecks.Run(root);
        Console.WriteLine("PASS: JSON roundtrip, real Core cast/spawn/damage, cooldown/resource rejection, deterministic reset, invalid fields/references/cycles.");
    }
    static void CheckDamageEvents(SkillProjectDocument source)
    {
        var p=SkillProjectJson.Read(SkillProjectJson.Write(source));p.Preview.Targets=p.Preview.Targets.Take(1).ToArray();
        var target=p.Preview.Targets[0];target.Hp=20;target.Shield=30;target.Def=20;
        using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
        {
            simulation.Cast("event-shot",0,0);simulation.Advance(100);var state=simulation.Snapshot();
            var result=state.Events.Single(e=>e.Kind=="critical").Damage;
            Assert(state.Statistics.Damage==50&&result.Absorbed==30&&result.HpLost==20&&result.Overkill==70,"Damage stages must report actual shield/health and separate overkill.");
            Assert(state.Events.Count(e=>e.Kind=="skill-hit")==1&&state.Events.Count(e=>e.Kind=="skill-kill")==1,"A kill must route exactly one health event chain.");
        }
        target.Hp=1000;target.Shield=0;target.Def=0;
        using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
        {
            simulation.Cast("event-shot",0,0);simulation.Advance(100);var state=simulation.Snapshot();
            Assert(state.Statistics.Damage==165&&state.Events.Count(e=>e.Kind=="skill-hit")==1,"Additional event damage cannot recursively route another skill-hit.");
        }
        Console.WriteLine("PASS: typed damage stages, actual shield/health/overkill and exactly-once skill hit/critical/kill with recursion suppression.");
    }
    static void CheckProjectileMotion(SkillProjectDocument source)
    {
        var p=SkillProjectJson.Read(SkillProjectJson.Write(source));p.Preview.Targets=Array.Empty<PreviewActorDocument>();
        var skill=p.Skills.First(s=>s.Id=="accelerating-shot");skill.Nodes.First(n=>n.Kind=="cast").Parameters["delay"]=0;
        var parameters=skill.Nodes.First(n=>n.Kind=="projectile").Parameters;parameters["speed"]=2;parameters["acceleration"]=4;parameters["maxSpeed"]=6;
        using(var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
        {
            simulation.Cast("accelerating-shot",0,0);simulation.Advance(50);var body=simulation.Snapshot().Actors.Single(a=>a.Kind=="projectile");
            Assert(Math.Abs(body.X-(-1.6f))<.001&&Math.Abs(body.Speed.Value-6)<.001,"Acceleration must integrate real speed and position.");
        }
        foreach(var item in new[]{("returning-shot","Returned",0),("bouncing-shot","Ground",2)})
        {
            using var simulation=new SkillSimulation(p,CombatContentCompiler.Compile(p));simulation.Cast(item.Item1,0,0);simulation.Advance(150);
            Assert(simulation.Snapshot().Events.Single(e=>e.Kind=="projectile-end").Result==item.Item2,"Wrong motion end: "+item.Item1);
            Assert(simulation.Snapshot().Events.Count(e=>e.Kind=="projectile-ground-bounce")==item.Item3,"Ground bounce count differs.");
        }
        Console.WriteLine("PASS: exact accelerated position/speed, returned Owner capture and real gravity/restitution ground bounce lifecycle.");
    }
    static void CheckProjectileBehaviors(SkillProjectDocument source)
    {
        var project=SkillProjectJson.Read(SkillProjectJson.Write(source));
        project.Preview.Caster.X=0;project.Preview.Caster.Z=0;project.Preview.Recovery=0;
        project.Preview.Targets=Enumerable.Range(1,3).Select(i=>new PreviewActorDocument{Id="target-"+i,X=i*2,Hp=1000}).ToArray();
        foreach(string id in new[]{"chain-shot","split-shot"})
        {
            var skill=project.Skills.First(s=>s.Id==id);skill.Nodes.First(n=>n.Kind=="cast").Parameters["delay"]=0;
            skill.Nodes.First(n=>n.Kind=="projectile").Parameters["radius"]=.1f;
        }
        project.Skills.First(s=>s.Id=="chain-shot").Nodes.First(n=>n.Kind=="projectile").Parameters["chainCount"]=2;
        var split=project.Skills.First(s=>s.Id=="split-shot").Nodes.First(n=>n.Kind=="split").Parameters;
        split["count"]=2;split["spread"]=0;split["generations"]=2;
        foreach(string id in new[]{"chain-shot","split-shot"})
        {
            var publication=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(project,id)));
            using var simulation=new SkillSimulation(publication.Document.Project,publication.Content);
            simulation.Cast(id,10,0);simulation.Advance(150);var state=simulation.Snapshot();
            Assert(state.Statistics.Damage==240,"Projectile behavior real damage differs: "+id);
            Assert(state.Statistics.Projectiles==(id=="chain-shot"?1:7),"Projectile derivation count differs: "+id);
            Assert(state.Events.Where(e=>e.Kind=="damage").All(e=>e.CastId>0&&e.Skill==id),"Delayed projectile damage must keep the original cast identity.");
        }
        Console.WriteLine("PASS: published chain redirects one body, split creates seven bounded bodies, real 240 damage and persistent cast provenance.");
    }
    static void CheckBuffs(SkillProjectDocument project)
    {
        var published=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(project,"burn-shot")));
        Assert(published.Document.Project.Buffs.Length==1,"Burn publication needs the shared Buff definition.");
        using var simulation=new SkillSimulation(published.Document.Project,published.Content);
        simulation.Cast("burn-shot",0,0);simulation.Advance(40);
        var target=simulation.Snapshot().Actors.First(a=>a.Name=="target-1");
        Assert(target.Buffs.Length==1&&target.Attributes["MoveSpeed"]==2.5f&&target.Tags[3101]==1,"Buff must change actual attributes and grant its own tag.");
        simulation.Advance(80);target=simulation.Snapshot().Actors.First(a=>a.Name=="target-1");
        Assert(simulation.Snapshot().Statistics.Damage==40&&target.Hp==960,"Burn needs four real periods.");
        Assert(target.Buffs.Length==0&&target.Attributes["MoveSpeed"]==5&&!target.Tags.ContainsKey(3101),"Natural expiry must restore only its leased state.");
        using var field=new SkillSimulation(project,CombatContentCompiler.Compile(project));field.Cast("burning-field",0,0);field.Advance(150);
        Assert(field.Snapshot().Statistics.Damage==510,"Area application must stack periodic damage without duplicating modifiers.");
        Console.WriteLine("PASS: shared Buff closure/lifecycle, four real burn periods, modifiers/Tag cleanup and stacked area damage.");
    }
    static void CheckSummons(SkillProjectDocument project)
    {
        var publication=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(project,"summon-gunners")));
        Assert(publication.Document.Project.Skills.Length==2&&publication.Document.Project.SummonBlueprints.Length==1,"Published summon must contain its exact skill/blueprint closure.");
        using var simulation=new SkillSimulation(publication.Document.Project,publication.Content);
        Assert(simulation.Cast("summon-gunners",0,0).Accepted,"Summon cast must succeed.");simulation.Advance(80);
        Assert(simulation.Snapshot().Actors.Count(a=>a.Kind=="summon")==2,"Must create two real blueprint bodies.");
        Assert(simulation.Snapshot().Statistics.Damage>0&&simulation.Snapshot().Statistics.Casts>1,"Independent summon AI must cast through Core.");
        Assert(simulation.Snapshot().Events.Any(e=>(e.Kind=="damage"||e.Kind=="critical")&&e.Source.StartsWith("射击分身#")),"Damage must belong to the summoned source.");
        float damage=simulation.Snapshot().Statistics.Damage;simulation.Interrupt(true);simulation.Advance(100);
        Assert(!simulation.Snapshot().Actors.Any(a=>a.Kind=="summon"||a.Kind=="projectile"||a.Kind=="aoe"),"Owner death must clean all descendants.");
        Assert(simulation.Snapshot().Statistics.Damage==damage,"Removed summons must not continue dealing damage.");
        Console.WriteLine("PASS: summon dependency closure, two real bodies, guarded AI casts/source attribution and owner cleanup.");
    }
    static void CheckObstacles(SkillProjectDocument source)
    {
        var project=SkillProjectJson.Read(SkillProjectJson.Write(source));
        project.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=-2,Z=0,Width=.5f,Depth=4}};
        var content=CombatContentCompiler.Compile(project);
        foreach(string key in new[]{"basic-shot","ray-shot","dash-strike"})
        {
            using var simulation=new SkillSimulation(project,content);simulation.Cast(key,0,0);simulation.Advance(100);
            Assert(simulation.Snapshot().Statistics.Damage==0,"Wall must block "+key);
            if(key=="dash-strike")Assert(Math.Abs(simulation.Snapshot().Actors.First(a=>a.Kind=="caster").X+2.6501f)<.002,"Dash must stop at body clearance.");
        }
        using var blink=new SkillSimulation(project,content);
        Assert(!blink.Cast("blink-strike",-2,0).Accepted&&blink.Snapshot().Energy==100,"Blocked blink cannot spend energy.");
        Assert(blink.Cast("blink-strike",0,0).Accepted,"Blink may cross an intermediate wall to a free landing.");
        blink.Advance(50);Assert(blink.Snapshot().Actors.First(a=>a.Kind=="caster").X==0,"Blink must land exactly.");
        Console.WriteLine("PASS: continuous wall collision for movement/dash/projectile/ray and pre-cost blink landing validation.");
    }
    static void CheckSpatial(SkillProjectDocument project)
    {
        var content=CombatContentCompiler.Compile(project);
        foreach(var item in new[]{("melee-thrust",80f,1,-6f),("melee-fan",160f,2,-6f),("melee-whirl",160f,2,-6f),("dash-strike",160f,2,6f),("blink-strike",240f,3,0f)})
        {
            using var s=new SkillSimulation(project,content);Assert(s.Cast(item.Item1,0,0).Accepted,"Action cast must succeed: "+item.Item1);s.Advance(80);var state=s.Snapshot();
            Assert(Math.Abs(state.Statistics.Damage-item.Item2)<.01,"Action damage differs: "+item.Item1);
            Assert(state.Statistics.Hits==item.Item3,"Action hit budget differs: "+item.Item1);
            Assert(Math.Abs(state.Actors.First(a=>a.Kind=="caster").X-item.Item4)<.003,"Action displacement differs: "+item.Item1);
            Assert(state.Volumes.Length==0,"Completed action cannot leave a hit volume.");
        }
        Console.WriteLine("PASS: thrust/fan/whirl/dash/blink real hits, exact movement and completed volume cleanup.");
    }
    static void CheckAreas(SkillProjectDocument project)
    {
        var content=CombatContentCompiler.Compile(project);
        foreach(var item in new[]{("explosive-shot",240f,3),("ground-field",300f,15),("direct-hit",80f,1)})
        {
            using var simulation=new SkillSimulation(project,content);
            Assert(simulation.Cast(item.Item1,0,0).Accepted,"Area example must cast: "+item.Item1);
            simulation.Advance(80);var state=simulation.Snapshot();
            Assert(Math.Abs(state.Statistics.Damage-item.Item2)<.005,"Unexpected area damage: "+item.Item1);
            Assert(state.Statistics.Hits==item.Item3,"Unexpected area target/pulse count: "+item.Item1);
            Assert(!state.Actors.Any(a=>a.Kind=="aoe"),"Finite area must expire.");
        }
        Console.WriteLine("PASS: hit-point explosion 240/3 hits, finite area 300/15 hits and direct target 80/1 hit.");
    }
    static void CheckCharge(SkillProjectDocument source)
    {
        var project=SkillProjectJson.Read(SkillProjectJson.Write(source));
        project.Preview.InitialEnergy=50;project.Preview.Recovery=0;
        var node=project.Skills.First(s=>s.Id=="charged-shot").Nodes.First(n=>n.Kind=="cast");
        node.Parameters["autoRelease"]=false;node.Parameters["energyPerSecond"]=4;
        var published=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(project,"charged-shot")));
        using var simulation=new SkillSimulation(published.Document.Project,published.Content);
        Assert(simulation.Snapshot().Energy==50,"Independent initial energy must load from published JSON.");
        Assert(simulation.Cast("charged-shot",0,0).Accepted,"Charge press must succeed.");
        simulation.Advance(30);
        Assert(simulation.Snapshot().Statistics.Projectiles==0&&simulation.Snapshot().Charging,"Holding cannot emit damage.");
        simulation.Release();simulation.Advance(80);
        Assert(Math.Abs(simulation.Snapshot().Statistics.Damage-160)<.005,"Half charge must carry 2x power through the real projectile.");
        Assert(Math.Abs(simulation.Snapshot().Energy-37.6f)<.003,"Only the held phase drains charge energy.");
        Console.WriteLine("PASS: charge publication, initial energy, deferred emission, 2x projectile damage and exact held resource cost.");
    }
    static void Reject(string text,string fragment)
    {
        try{CombatContentCompiler.Compile(SkillProjectJson.Read(text));}
        catch(ContentValidationException e){Assert(e.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase),"Expected useful error path: "+e.Message);return;}
        throw new Exception("Expected rejection containing "+fragment);
    }
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
}
