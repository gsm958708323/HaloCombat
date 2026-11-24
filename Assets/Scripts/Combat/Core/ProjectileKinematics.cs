using System;

namespace Combat.Core
{
    public sealed class ProjectileMotionDefinition
    {
        public float Acceleration;
        public float RampTime;
        public float MaxSpeed=1000;
        public float ReturnAfter=-1;
        public float ReturnSpeed=12;
        public float CatchRadius=.4f;
        public ProjectileBounceDefinition Bounce;
        public int WallBounces;
        public bool SineReturn;
        public ScheduledBounceDefinition ScheduledBounce;
    }

    public sealed class ProjectileBounceDefinition
    {
        public float LaunchHeight=.5f;
        public float VerticalSpeed=5;
        public float Gravity=20;
        public float Restitution=.6f;
        public int Count=2;
    }
    public sealed class ScheduledBounceDefinition
    {
        public float Height;
        public float[] Phases=Array.Empty<float>();
    }

    public static class ProjectileKinematics
    {
        // The authored ramp samples once at the start of the logic step, matching the
        // source curve. Contact/reflect substeps reuse that speed for the remaining step.
        public static void SampleStep(ProjectileComp body)
        {
            if(body.Def.Motion?.SineReturn==true)body.SampledSpeed=SampledProjectileMotion.ReturnSpeed(body.Def.Speed,body.Age,body.Def.Motion.ReturnAfter);
            else if(body.Def.Motion?.RampTime>0)body.SampledSpeed=2*body.Def.Speed*body.Age/(body.Age+body.Def.Motion.RampTime);
        }
        public static double Speed(ProjectileComp body)
        {
            var motion=body.Def.Motion;
            if(motion?.SineReturn==true)return body.SampledSpeed;
            if(body.Returning)return motion.ReturnSpeed;
            if(motion?.RampTime>0)return body.SampledSpeed;
            return motion==null?body.Def.Speed:Math.Max(0,Math.Min(motion.MaxSpeed,body.Def.Speed+motion.Acceleration*body.Age));
        }

        public static double Distance(ProjectileComp body,double time)
        {
            double speed=Speed(body),a=body.Returning||body.Def.Motion?.RampTime>0||body.Def.Motion?.SineReturn==true?0:body.Def.Motion?.Acceleration??0;
            if(a==0)return speed*time;
            double limit=a>0?body.Def.Motion.MaxSpeed:0;
            double cap=Math.Max(0,(limit-speed)/a),part=Math.Min(time,cap);
            return Math.Max(0,speed*part+.5*a*part*part+limit*Math.Max(0,time-cap));
        }

        public static double Time(ProjectileComp body,double distance,double maximumTime)
        {
            if(distance<=.0000000001)return 0;
            double speed=Speed(body),a=body.Returning||body.Def.Motion?.RampTime>0||body.Def.Motion?.SineReturn==true?0:body.Def.Motion?.Acceleration??0;
            if(a==0)return speed>0?Math.Min(maximumTime,distance/speed):maximumTime;
            double limit=a>0?body.Def.Motion.MaxSpeed:0;
            double cap=Math.Max(0,(limit-speed)/a),capDistance=speed*cap+.5*a*cap*cap;
            if(distance>capDistance+.000000001)
                return limit>0?Math.Min(maximumTime,cap+(distance-capDistance)/limit):maximumTime;
            double end=Math.Sqrt(Math.Max(0,speed*speed+2*a*distance));
            return Math.Min(maximumTime,2*distance/(speed+end));
        }

        public static double GroundTime(ProjectileComp body,TransformComp transform,float ground)
        {
            if(body.Def.Motion?.ScheduledBounce!=null)
            {var phases=body.Def.Motion.ScheduledBounce.Phases;int index=phases.Length-body.GroundBouncesLeft;return index<phases.Length?Math.Max(0,phases[index]-body.Age):double.PositiveInfinity;}
            var bounce=body.Def.Motion?.Bounce;
            if(bounce==null)return double.PositiveInfinity;
            double height=Math.Max(0,transform.Position.Y-ground),v=body.VerticalSpeed,g=bounce.Gravity;
            return v>=0?(v+Math.Sqrt(v*v+2*g*height))/g:
                height>0?2*height/(Math.Sqrt(v*v+2*g*height)-v):0;
        }

        public static SimVec3 Position(ProjectileComp body,SimVec3 from,SimVec3 direction,double time,double distance)
        {
            var bounce=body.Def.Motion?.Bounce;
            double y=from.Y;
            if(bounce!=null)y+=body.VerticalSpeed*time-.5*bounce.Gravity*time*time;
            if(body.Def.Motion?.ScheduledBounce!=null)y=body.GroundY+SampledProjectileMotion.Height(body.Def.Motion.ScheduledBounce,body.Age+time);
            return new SimVec3(from.X+direction.X*(float)distance,(float)y,from.Z+direction.Z*(float)distance);
        }

        public static void Advance(ProjectileComp body,TransformComp transform,SimVec3 from,SimVec3 direction,double time,double distance)
        {
            transform.Position=Position(body,from,direction,time,distance);
            if(body.Def.Motion?.ScheduledBounce!=null&&time>0)body.VerticalSpeed=(transform.Position.Y-from.Y)/time;
            if(body.Def.Motion?.Bounce!=null)body.VerticalSpeed-=body.Def.Motion.Bounce.Gravity*time;
            body.Age+=time;body.Distance+=distance;
        }
    }
}
