namespace Combat.Core
{
    public readonly struct EvBuffLifecycle
    {
        public readonly EntityId Owner,Source,AppliedBy;
        public readonly int BuffId,InstanceId,Stacks;
        public readonly BuffEventKind Kind;
        public readonly BuffRemovalReason Reason;
        public EvBuffLifecycle(EntityId owner,EntityId source,EntityId appliedBy,int buffId,int instanceId,int stacks,BuffEventKind kind,BuffRemovalReason reason=default)
        {Owner=owner;Source=source;AppliedBy=appliedBy;BuffId=buffId;InstanceId=instanceId;Stacks=stacks;Kind=kind;Reason=reason;}
    }

    public readonly struct EvSkillStarted
    {
        public readonly EntityId Source;
        public readonly SkillNodeId Skill;
        public EvSkillStarted(EntityId source,SkillNodeId skill){Source=source;Skill=skill;}
    }
    public enum SummonEventKind {Spawned,Expired,OwnerRemoved,Killed,Removed,Rejected}
    public readonly struct EvSummonLifecycle
    {
        public readonly EntityId Id,Owner;
        public readonly int SpecId;
        public readonly SummonEventKind Kind;
        public readonly string Detail;
        public EvSummonLifecycle(EntityId id,EntityId owner,int specId,SummonEventKind kind,string detail=null)
        {Id=id;Owner=owner;SpecId=specId;Kind=kind;Detail=detail;}
    }

    public readonly struct EvObstacleHit
    {
        public readonly EntityId Source,Body;
        public readonly string Obstacle,Kind;
        public readonly SimVec3 Point,Normal;
        public EvObstacleHit(EntityId source,EntityId body,string obstacle,SimVec3 point,SimVec3 normal,string kind)
        {Source=source;Body=body;Obstacle=obstacle;Point=point;Normal=normal;Kind=kind;}
    }
    public readonly struct EvRayResolved
    {
        public readonly EntityId Source;
        public readonly SimVec3 From,To;
        public readonly float Width;
        public readonly int Hits;
        public EvRayResolved(EntityId source,SimVec3 from,SimVec3 to,float width,int hits)
        {Source=source;From=from;To=to;Width=width;Hits=hits;}
    }
    public readonly struct EvProjectileBehavior
    {
        public readonly EntityId Source,Projectile,Target;
        public readonly string Kind;
        public readonly SimVec3 Point;
        public readonly int Count,Generation;
        public readonly SkillOrigin Origin;
        public EvProjectileBehavior(EntityId source,EntityId projectile,string kind,SimVec3 point,EntityId target,int count,int generation,SkillOrigin origin)
        {Source=source;Projectile=projectile;Kind=kind;Point=point;Target=target;Count=count;Generation=generation;Origin=origin;}
    }
    public enum ProjectileEndReason {HitLimit,Wall,Range,Lifetime,ChainExhausted,Returned,Ground}
    public readonly struct EvProjectileEnded
    {
        public readonly EntityId Source,Id;
        public readonly ProjectileEndReason Reason;
        public readonly SimVec3 Point;
        public EvProjectileEnded(EntityId source,EntityId id,ProjectileEndReason reason,SimVec3 point)
        {Source=source;Id=id;Reason=reason;Point=point;}
    }

    public readonly struct EvSpatialVolume
    {
        public readonly EntityId Source;
        public readonly int Sequence;
        public readonly SimVec3 Origin;
        public readonly float Yaw,Length,Width,Angle;
        public readonly HitShape Shape;
        public readonly bool Closed;
        public EvSpatialVolume(EntityId source,int sequence,SimVec3 origin,float yaw,HitShape shape,float length,float width,float angle,bool closed)
        { Source=source;Sequence=sequence;Origin=origin;Yaw=yaw;Shape=shape;Length=length;Width=width;Angle=angle;Closed=closed; }
    }

    public readonly struct EvAreaResolved
    {
        public readonly EntityId Source;
        public readonly SimVec3 Point;
        public readonly float Radius;
        public readonly int TargetCount;
        public EvAreaResolved(EntityId source, SimVec3 point, float radius, int targetCount)
        { Source = source; Point = point; Radius = radius; TargetCount = targetCount; }
    }

    public enum AoeEventKind { Pulse, Enter, Exit, Stay, Expired, Removed }
    public readonly struct EvAoeLifecycle
    {
        public readonly EntityId Id, Source, Target;
        public readonly AoeEventKind Kind;
        public EvAoeLifecycle(EntityId id, EntityId source, EntityId target, AoeEventKind kind)
        { Id = id; Source = source; Target = target; Kind = kind; }
    }

    public readonly struct EvSkillReleased
    {
        public readonly EntityId Source;
        public readonly SkillNodeId Skill;
        public readonly float Power;
        public EvSkillReleased(EntityId source, SkillNodeId skill, float power) { Source = source; Skill = skill; Power = power; }
    }

    public readonly struct EvSkillEnded
    {
        public readonly EntityId Source;
        public readonly SkillNodeId Skill;
        public readonly DirectorStopReason Reason;
        public readonly SkillOrigin Origin;
        public EvSkillEnded(EntityId source, SkillNodeId skill, DirectorStopReason reason,SkillOrigin origin=default)
        { Source = source; Skill = skill; Reason = reason;Origin=origin; }
    }

    public readonly struct EvEntitySpawn
    {
        public readonly EntityId Id;
        public readonly string BlueprintId;
        public readonly EntityId Owner;
        public readonly string ViewKey;

        public EvEntitySpawn(EntityId id, string blueprintId, EntityId owner,string viewKey=null)
        {
            Id = id;
            BlueprintId = blueprintId ?? string.Empty;
            Owner = owner;
            ViewKey=viewKey;
        }
    }

    public readonly struct EvCue
    {
        public readonly int CueId;
        public readonly EntityId Source;
        public readonly string Name;
        public readonly SimVec3 Point;
        public readonly bool HasPoint;
        public EvCue(int cueId, EntityId source, string name,SimVec3? point=null)
        {
            CueId = cueId;
            Source = source;
            Name = name ?? string.Empty;
            Point=point??default;HasPoint=point.HasValue;
        }
    }

    public readonly struct EvDamage
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly float Amount;
        public readonly bool IsCrit;
        public readonly float ShieldAbsorb;
        public readonly bool IsKill;
        public readonly SkillOrigin Origin;
        public readonly DamageResult Result;
        public EvDamage(EntityId source, EntityId target, float amount, bool isCrit, float shieldAbsorb, bool isKill,SkillOrigin origin=default,DamageResult result=null)
        {
            Source = source;
            Target = target;
            Amount = amount;
            IsCrit = isCrit;
            ShieldAbsorb = shieldAbsorb;
            IsKill = isKill;
            Origin=origin;
            Result=result;
        }
    }

    public readonly struct EvImmune
    {
        public readonly EntityId Target;
        public readonly EntityId Source;
        public EvImmune(EntityId target, EntityId source)
        {
            Target = target;
            Source = source;
        }
    }

    public readonly struct EvHitstop
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly int LogicFrames;
        public readonly bool FreezeSource;
        public readonly bool FreezeTarget;
        public int Frames => LogicFrames;

        public EvHitstop(EntityId source, EntityId target, int logicFrames)
        {
            Source = source;
            Target = target;
            LogicFrames = logicFrames;
            FreezeSource = true;
            FreezeTarget = true;
        }
    }

    public readonly struct EvHeal
    {
        public readonly EntityId Target;
        public readonly float Amount;
        public EvHeal(EntityId target, float amount)
        {
            Target = target;
            Amount = amount;
        }
    }

    public readonly struct EvEntityDead
    {
        public readonly EntityId Id;
        public readonly EntityId Killer;
        public EvEntityDead(EntityId id, EntityId killer)
        {
            Id = id;
            Killer = killer;
        }
    }

    public readonly struct EvEntityCleanup
    {
        public readonly EntityId Id;
        public readonly string Reason;
        public EvEntityCleanup(EntityId id, string reason)
        {
            Id = id;
            Reason = reason ?? string.Empty;
        }
    }
}
