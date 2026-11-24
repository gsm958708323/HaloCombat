using System;

namespace Combat.Core
{
    public enum ProjectileContactMode {Behavior,End,Continue,Ignore}
    public enum ProjectileFlightClock {Time,Distance}
    public sealed class ProjectileFlightDefinition
    {
        public ProjectileFlightClock Clock;
        public float Interval;
    }
    public readonly struct EvProjectileFlight
    {
        public readonly EntityId Owner,Projectile;
        public readonly SkillOrigin Origin;
        public readonly ProjectileFlightClock Clock;
        public readonly long Index;
        public readonly double Age,Distance;
        public readonly SimVec3 Point;
        public EvProjectileFlight(Actor actor,ProjectileComp body,long index)
        {Owner=body.OwnerId;Projectile=actor.Id;Origin=body.Family.Origin;Clock=body.Def.Flight.Clock;Index=index;Age=body.Age;Distance=body.Distance;Point=actor.GetComp<TransformComp>().Position;}
    }
    internal static class ProjectileFlightCadence
    {
        const int MaxPerStep=64;
        static double Clock(ProjectileComp body)=>body.Def.Flight.Clock==ProjectileFlightClock.Time?body.Age:body.Distance;
        static double Next(ProjectileComp body)=>body.NextFlightIndex*(double)body.Def.Flight.Interval;
        // A float fixed step has time roundoff. Convert that tolerance into distance
        // units so an exact authored distance does not slip to the following step.
        static double Tolerance(ProjectileComp body)=>.0000001*(body.Def.Flight.Clock==ProjectileFlightClock.Time?1:Math.Max(1,ProjectileKinematics.Speed(body)));
        public static double Bound(ProjectileComp body,double duration,bool moving=true)
        {
            if(body.Def.Flight==null||body.FlightLimited||!moving&&body.Def.Flight.Clock==ProjectileFlightClock.Distance)return duration;
            double remaining=Math.Max(0,Next(body)-Clock(body));
            if(body.Def.Flight.Clock==ProjectileFlightClock.Time)return Math.Min(duration,remaining);
            double distance=ProjectileKinematics.Distance(body,duration);
            return distance<=.000000001?duration:ProjectileKinematics.Time(body,Math.Min(remaining,distance),duration);
        }
        public static void Deliver(CombatWorld world,Actor actor,ProjectileComp body)
        {
            if(body.Def?.Flight==null||!actor.IsActive||body.Exhausted)return;
            if(body.FlightFrame!=world.Time.Frame){body.FlightFrame=world.Time.Frame;body.FlightDelivered=0;body.FlightLimited=false;}
            if(body.FlightLimited){body.NextFlightIndex=Math.Max(body.NextFlightIndex,(long)Math.Floor((Clock(body)+Tolerance(body))/body.Def.Flight.Interval)+1);return;}
            while(actor.IsActive&&!body.Exhausted&&Clock(body)+Tolerance(body)>=Next(body))
            {
                if(body.FlightDelivered++>=MaxPerStep)
                {
                    body.FlightLimited=true;world.Events.Publish(new EvProjectileBehavior(body.OwnerId,actor.Id,"flight-limit",actor.GetComp<TransformComp>().Position,
                        EntityId.Invalid,MaxPerStep,body.Generation,body.Family.Origin));Deliver(world,actor,body);return;
                }
                var e=new EvProjectileFlight(actor,body,body.NextFlightIndex++);world.Events.Publish(e);
                if(!actor.IsActive||actor.Id!=e.Projectile||body.Exhausted||body.Def?.Flight==null)return;
                world.TryGetActor(body.OwnerId,out var owner);
                float attack=body.Def.SnapshotAtk?body.SnapshotAtk:owner!=null&&owner.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):body.SnapshotAtk;
                world.Deliver(body.Def.OnFlight,owner,null,attack,e.Point,LocomotionComp.ForwardFromYaw(actor.GetComp<TransformComp>().YawDegrees),power:body.Power,origin:e.Origin);
                if(actor.Id!=e.Projectile||body.Def?.Flight==null)return;
            }
        }
    }
}
