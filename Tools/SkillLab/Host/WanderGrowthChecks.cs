using Combat.Config;
using Combat.Core;

namespace SkillLab;

internal static class WanderGrowthChecks
{
    public static void Run(string root)
    {
        var p=SkillProjectFiles.Read(Path.Combine(root,"Assets/StreamingAssets/Combat/project.json"));p.Runtime.Arena.MaxEnemies=2;
        using var session=new ArenaProject(p).Create();var spawns=new List<float>();var casts=new List<int>();
        session.World.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId=="arena-enemy"){session.World.TryGetActor(e.Id,out var actor);spawns.Add(actor.GetComp<AttributeSet>().GetBase(AttrId.MaxHp));}});
        session.World.Events.Subscribe<EvSkillStarted>(e=>{if(e.Source!=session.PlayerId)casts.Add(session.World.Time.Frame);});
        session.Start();session.World.TryGetActor(session.PlayerId,out var player);if(player.GetComp<AttributeSet>().GetBase(AttrId.Atk)!=54)throw new Exception("Seeded player attack sampling changed.");
        session.Advance(150);if(casts.Count!=0||!spawns.SequenceEqual(new[]{50f,52f}))throw new Exception("Original spawn growth/first-shot boundary mismatch.");
        session.Advance();if(casts.Count!=2||casts.Any(frame=>frame!=151))throw new Exception("Independent periodic enemy casts did not use the original3-second start.");
        Console.WriteLine("PASS: original Arena player54 attack, cumulative enemy50/52 HP, parallel wander and two first shots at frame151.");
    }
}
