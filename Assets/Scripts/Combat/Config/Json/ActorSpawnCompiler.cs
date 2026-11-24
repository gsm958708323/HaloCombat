using Combat.Core;

namespace Combat.Config
{
    internal static class ActorSpawnCompiler
    {
        public static ActorSpawnStats Compile(ActorSpawnStatsDocument source,string path)
        {
            if(source==null)return null;
            N(source.HpPerSpawn,0,1000000,path+".hpPerSpawn");N(source.AttackPerSpawn,0,1000000,path+".attackPerSpawn");N(source.AttackRandomRange,0,1000000,path+".attackRandomRange");
            SpawnSpeedCurve curve=null;
            if(source.MoveSpeedCurve!=null)
            {
                var c=source.MoveSpeedCurve;string p=path+".moveSpeedCurve";
                N(c.Base,0,1000000,p+".base");N(c.RandomRange,0,1000000,p+".randomRange");N(c.Scale,0,1000,p+".scale");N(c.Divisor,.001f,1000000,p+".divisor");N(c.Offset,0,1000,p+".offset");
                curve=new SpawnSpeedCurve{Base=c.Base,RandomRange=c.RandomRange,Scale=c.Scale,Divisor=c.Divisor,Offset=c.Offset};
            }
            return new ActorSpawnStats{HpPerSpawn=source.HpPerSpawn,AttackPerSpawn=source.AttackPerSpawn,AttackRandomRange=source.AttackRandomRange,MoveSpeedCurve=curve};
        }
        public static WanderDefinition Wander(WanderDocument source,string path)
        {
            if(source==null)throw new ContentValidationException(path,"Wander timing is required.");
            N(source.FirstTurn,0,3600,path+".firstTurn");N(source.TurnIntervalMin,.02f,3600,path+".turnIntervalMin");N(source.TurnIntervalMax,source.TurnIntervalMin,3600,path+".turnIntervalMax");N(source.TurnAngle,0,360,path+".turnAngle");
            N(source.FirstShot,0,3600,path+".firstShot");N(source.ShotIntervalMin,.02f,3600,path+".shotIntervalMin");N(source.ShotIntervalMax,source.ShotIntervalMin,3600,path+".shotIntervalMax");
            return new WanderDefinition{FirstTurn=source.FirstTurn,TurnIntervalMin=source.TurnIntervalMin,TurnIntervalMax=source.TurnIntervalMax,TurnAngle=source.TurnAngle,
                FirstShot=source.FirstShot,ShotIntervalMin=source.ShotIntervalMin,ShotIntervalMax=source.ShotIntervalMax};
        }
        static void N(float value,float min,float max,string path)=>AuthoredTimelineCompiler.Number(value,min,max,path);
    }
}
