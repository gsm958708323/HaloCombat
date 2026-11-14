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
    }
}
