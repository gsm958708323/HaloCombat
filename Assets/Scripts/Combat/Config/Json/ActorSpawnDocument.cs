namespace Combat.Config
{
    public sealed class ActorSpawnStatsDocument
    {
        public float HpPerSpawn {get;set;}
        public float AttackPerSpawn {get;set;}
        public int AttackRandomRange {get;set;}
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)]public SpawnSpeedCurveDocument MoveSpeedCurve {get;set;}
    }
    public sealed class SpawnSpeedCurveDocument
    {
        public int Base {get;set;}=50;
        public int RandomRange {get;set;}=20;
        public float Scale {get;set;}=5.6f;
        public float Divisor {get;set;}=100;
        public float Offset {get;set;}=.2f;
    }
    public sealed class WanderDocument
    {
        public float FirstTurn {get;set;}=2;
        public float TurnIntervalMin {get;set;}=1.6f;
        public float TurnIntervalMax {get;set;}=3.2f;
        public float TurnAngle {get;set;}=90;
        public float FirstShot {get;set;}=3;
        public float ShotIntervalMin {get;set;}=2;
        public float ShotIntervalMax {get;set;}=5;
    }
}
