using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    internal static class ArenaCompiler
    {
        public static ArenaDefinition Compile(ArenaDocument document,CombatContentSnapshot content,IReadOnlyDictionary<string,RuntimeActorDefinition> actors)
        {
            if(document==null)return null;const string p="$.runtime.arena";
            RuntimeActorDefinition Actor(string id,string role,string field)
            {if(!actors.TryGetValue(id??"",out var actor)||actor.Role!=role)throw new ContentValidationException(p+"."+field,"Expected existing "+role+" actor.");return actor;}
            var player=Actor(document.Player,"player","player");Actor(document.Enemy,"ai","enemy");Actor(document.TrainingTarget,"target","trainingTarget");
            N(document.HalfWidth,3,1000,p+".halfWidth");N(document.HalfDepth,3,1000,p+".halfDepth");N(document.PlayerX,-document.HalfWidth,document.HalfWidth,p+".playerX");N(document.PlayerZ,-document.HalfDepth,document.HalfDepth,p+".playerZ");
            N(document.PlayerYaw,-36000,36000,p+".playerYaw");N(document.SpawnDistance,.1f,Math.Min(document.HalfWidth,document.HalfDepth),p+".spawnDistance");
            N(document.SpawnPeriod,.02f,3600,p+".spawnPeriod");N(document.CleanupDelay,0,120,p+".cleanupDelay");
            N(document.CameraDistance,.1f,100,p+".cameraDistance");N(document.CameraHeight,.1f,100,p+".cameraHeight");N(document.CameraFov,10,120,p+".cameraFov");
            N(document.CameraFocusHeight,0,20,p+".cameraFocusHeight");
            N(document.CameraDownedFocusHeight,0,20,p+".cameraDownedFocusHeight");N(document.CameraSpringHz,0,100,p+".cameraSpringHz");
            if(document.MaxEnemies<1||document.MaxEnemies>128)throw new ContentValidationException(p+".maxEnemies","Expected1..128.");
            if(document.Obstacles==null||document.Obstacles.Length>128)throw new ContentValidationException(p+".obstacles","Expected at most128 obstacles.");
            if(document.Obstacles.Any(o=>o!=null&&Array.IndexOf(new[]{"arena-left","arena-right","arena-front","arena-back"},o.Id)>=0))throw new ContentValidationException(p+".obstacles","Arena boundary obstacle ids are reserved.");
            var obstacleScene=new PreviewDocument{Caster=new PreviewActorDocument{Id="player",X=document.PlayerX,Z=document.PlayerZ,CollisionRadius=player.CollisionRadius},Obstacles=document.Obstacles};
            SkillSceneValidation.Validate(obstacleScene);
            var obstacles=SkillSceneValidation.Obstacles(obstacleScene).ToList();
            obstacles.Add(new ObstacleDefinition("arena-left",-document.HalfWidth-1,0,2,document.HalfDepth*2+4));
            obstacles.Add(new ObstacleDefinition("arena-right",document.HalfWidth+1,0,2,document.HalfDepth*2+4));
            obstacles.Add(new ObstacleDefinition("arena-front",0,document.HalfDepth+1,document.HalfWidth*2+4,2));
            obstacles.Add(new ObstacleDefinition("arena-back",0,-document.HalfDepth-1,document.HalfWidth*2+4,2));
            var position=new SimVec3(document.PlayerX,content.Runtime?.Data.Motor.GroundY??0,document.PlayerZ);
            if(!new CollisionWorld(obstacles.ToArray()).IsFree(position,player.CollisionRadius))throw new ContentValidationException(p+".playerX","Player spawn overlaps the arena boundary.");
            if(document.Bindings==null||document.Bindings.Length>8)throw new ContentValidationException(p+".bindings","Expected up to8 actions.");
            var actions=new HashSet<string>();var bindings=new List<ArenaBinding>();
            foreach(var b in document.Bindings)
            {
                if(b==null||Array.IndexOf(new[]{"Fire1","Fire2","Fire3","Fire4","Fire5","Roll","Homing","Monkey"},b.Action)<0||!actions.Add(b.Action))throw new ContentValidationException(p+".bindings","Unknown or duplicate input action.");
                if(string.IsNullOrWhiteSpace(b.Input)||!content.Runtime.Data.Combo.Entries.Any(e=>e.Input.Action==b.Input&&(e.PreSkills==null||e.PreSkills.Length==0)))throw new ContentValidationException(p+".bindings["+b.Action+"].input","Input has no root Combo entry.");
                bindings.Add(new ArenaBinding(b.Action,new InputToken(b.Input)));
            }
            if(!actions.Contains("Fire1"))throw new ContentValidationException(p+".bindings","Primary Fire1 binding is required.");
            return new ArenaDefinition{Player=document.Player,Enemy=document.Enemy,TrainingTarget=document.TrainingTarget,Seed=document.Seed,MaxEnemies=document.MaxEnemies,
                SpawnPeriod=document.SpawnPeriod,CleanupDelay=document.CleanupDelay,HalfWidth=document.HalfWidth,HalfDepth=document.HalfDepth,SpawnDistance=document.SpawnDistance,
                PlayerPosition=position,PlayerYaw=document.PlayerYaw,Bindings=bindings.ToArray(),Obstacles=obstacles.ToArray(),CameraDistance=document.CameraDistance,CameraHeight=document.CameraHeight,CameraFov=document.CameraFov,CameraFocusHeight=document.CameraFocusHeight,
                CameraDownedFocusHeight=document.CameraDownedFocusHeight,CameraSpringHz=document.CameraSpringHz,CameraShakeEnabled=document.CameraShakeEnabled};
        }
        static void N(float value,float min,float max,string path)=>AuthoredTimelineCompiler.Number(value,min,max,path);
    }
}
