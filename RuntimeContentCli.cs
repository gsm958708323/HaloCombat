using System;
using System.Linq;
using Combat.Config;
using Combat.Core;

namespace Combat
{
    internal static class RuntimeContentCli
    {
        public static int Run(string[] args)
        {
            if(args.Length<2||args.Length>3){Console.Error.WriteLine("Usage: json <project.json> [skill-id]");return 2;}
            try
            {
                var loaded=new RuntimeProjectLoader(args[1]);var runtime=loaded.Content.Runtime;
                Console.WriteLine("JSON validated: "+loaded.Path+" / "+loaded.Project.Skills.Length+" skills / "+runtime.Actors.Count+" actor blueprints");
                var player=runtime.Actors.Values.FirstOrDefault(a=>a.Role=="player");var target=runtime.Actors.Values.FirstOrDefault(a=>a.Role=="target");
                if(player==null||target==null)throw new InvalidOperationException("Runtime smoke requires a player and target blueprint.");
                var world=loaded.CreateWorld(new SeededRandom(1));
                try
                {
                    world.TryGetActor(world.SpawnActor(new ActorSpawnSpec(player.Id)),out var actor);
                    world.TryGetActor(world.SpawnActor(new ActorSpawnSpec(target.Id)),out var stake);stake.GetComp<TransformComp>().Position=new SimVec3(.55f,0,0);
                    var skill=loaded.Content.Skills.All.FirstOrDefault(s=>s.Key==(args.Length==3?args[2]:loaded.Project.Skills[0].Id));
                    if(skill==null)throw new InvalidOperationException("Unknown skill id.");
                    int hits=0;float damage=0;world.Events.Subscribe<EvDamage>(e=>{hits++;damage+=e.Amount;});
                    if(!actor.GetComp<SkillCastComp>().TryCast(skill.Id,new SkillAim(stake.Id,stake.GetComp<TransformComp>().Position,0)))throw new InvalidOperationException("Initial cast rejected.");
                    for(int i=0;i<100;i++)world.Tick(.02f);
                    Console.WriteLine("JSON runtime: "+skill.Key+" / hits="+hits+" / actual damage="+damage+" / player x="+actor.GetComp<TransformComp>().Position.X);
                    Console.WriteLine("JSON RUNTIME PASSED");return 0;
                }
                finally{world.Shutdown();}
            }
            catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
        }
    }
}
