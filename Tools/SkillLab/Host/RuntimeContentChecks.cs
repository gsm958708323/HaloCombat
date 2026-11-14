using Combat.Config;
using Combat.Core;

namespace SkillLab;

internal static class RuntimeContentChecks
{
    public static void Run(string root)
    {
        var loaded=new RuntimeProjectLoader(Path.Combine(root,"Assets/StreamingAssets/Combat/project.json"));
        if(loaded.Content.Runtime.Actors.Count<1)throw new Exception("Missing runtime actors.");
        var world=loaded.CreateWorld(new FixedRandom(.5f));
        try
        {
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("fighter")),out var player);
            world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("stake")),out var target);target.GetComp<TransformComp>().Position=new SimVec3(.55f,0,0);
            float damage=0;world.Events.Subscribe<EvDamage>(e=>damage+=e.Amount);player.GetComp<InputBufferComp>().Push(InputToken.Attack);
            for(int i=0;i<100;i++)world.Tick(.02f);
            if(damage<=0)throw new Exception("Disk JSON runtime did not deal actual damage.");
            if(!loaded.Content.Timelines.TryGet(TimelineId.TL_G1,out var timeline)||timeline.Clips.Length!=3)throw new Exception("Authored timeline was not retained.");
            Console.WriteLine("PASS: actual runtime project JSON, actor data, Combo input, authored Timeline clips/payloads and real damage="+damage);
        }
        finally{world.Shutdown();}
        CheckArena(loaded);
    }
    static void CheckArena(RuntimeProjectLoader loaded)
    {
        var content=new ArenaProject(loaded.Project);
        using var a=content.Create();using var b=content.Create();a.Start();b.Start();
        var input=new ArenaInputFrame{MoveZ=.5f,AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0),PrimaryHeld=true,Pressed=new[]{"Fire1"}};
        a.ApplyInput(input);b.ApplyInput(input);a.Advance(120);b.Advance(120);
        var left=a.World.RegistryActive();var right=b.World.RegistryActive();
        if(a.SpawnedEnemies!=10||left.Count!=right.Count)throw new Exception("Arena spawn/replay mismatch.");
        for(int i=0;i<left.Count;i++)
        {var x=left[i].GetComp<TransformComp>();var y=right[i].GetComp<TransformComp>();if(x.Position.X!=y.Position.X||x.Position.Z!=y.Position.Z)throw new Exception("Arena fixed-input replay diverged.");}
        a.SetPaused(true);int frame=a.World.Time.Frame;a.Advance(10);if(a.World.Time.Frame!=frame)throw new Exception("Paused Arena advanced.");
        Console.WriteLine("PASS: formal JSON Arena session, 10 configured enemies, 120-step deterministic input and paused clock.");
        using var training=content.Create(true);training.Start();float damage=0;int hits=0,casts=0;
        training.World.Events.Subscribe<EvDamage>(e=>{damage+=e.Amount;hits++;});training.World.Events.Subscribe<EvSkillStarted>(e=>casts++);
        input.MoveZ=0;training.ApplyInput(input);training.Advance(90);input.PrimaryHeld=false;input.Pressed=Array.Empty<string>();training.ApplyInput(input);training.Advance(30);
        if(damage<=0||training.SpawnedEnemies!=0)throw new Exception("Training Arena did not deliver primary attacks to its configured target.");
        Console.WriteLine("PASS: Arena training replay frame="+training.World.Time.Frame+" / damage="+damage+" / hits="+hits+" / casts="+casts+" / spawned="+training.SpawnedEnemies+" / actors="+training.World.RegistryActive().Count);
    }
}
