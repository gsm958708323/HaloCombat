using System;

namespace Combat.Core
{
    // Authored curves sample the logic-step age; collision substeps reuse that sample.
    public static class SampledProjectileMotion
    {
        public static double ReturnSpeed(float speed,double age,float turnTime)
        {
            double period=Math.Max(.001,turnTime);
            double phase=age+.0000001<period?Math.Max(0,age)/period*Math.PI:Math.Min(Math.Max(0,age-period)/period*Math.PI,.5);
            return speed*(Math.Sin(phase)+.1);
        }
        public static double Height(ScheduledBounceDefinition curve,double age)
        {
            double start=0;
            foreach(float end in curve.Phases)
            {
                double duration=end-start;
                if(age<=end+.0000001)
                {
                    double t=Math.Max(0,Math.Min(1,(age-start)/duration)),scale=duration/curve.Phases[0];
                    return 4*curve.Height*scale*scale*t*(1-t);
                }
                start=end;
            }
            return 0;
        }
    }
}
