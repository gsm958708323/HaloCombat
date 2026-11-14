using System;
using Combat.Core;
using System.Collections.Generic;

namespace Combat.Config
{
    internal static class SkillCastCompiler
    {
        public static SkillDefinition Compile(SkillGraphDocument skill, SkillNodeDocument node, string path, IEffect[] effects, TimelineLibrary timelines,
            TimelineClip[] actionClips,TimelinePayload[] followups,TimelineDefinition authored,Dictionary<string,SkillNodeId> skills,ContentDependencies dependencies)
        {
            var c = new NodeParameters(node, path, "mode", "cost", "cooldown", "delay", "duration", "count", "interval", "sustained", "channelDuration",
                "energyPerSecond", "costPerPulse", "chargeMin", "chargeMax", "powerMin", "powerMax", "autoRelease",
                "ammoCost","fallbackSkill","recastSkill","recastProjectile","requiredProjectile","hitFeedback");
            string mode = c.Choice("mode", "instant", "instant", "channel", "charge");
            if(authored!=null)
            {
                foreach(var property in node.Parameters.Properties())
                    if(System.Array.IndexOf(new[]{"mode","cost","cooldown","count","delay","ammoCost","fallbackSkill","recastSkill","recastProjectile","requiredProjectile","hitFeedback"},property.Name)<0)
                        Fail(path+".parameters."+property.Name,"Explicit timeline owns timing and has no automatic cast schedule fields.");
                if(mode!="instant"||effects.Length>0||actionClips.Length>0||followups.Length>0||
                    c.Integer("count",1,1,1000)!=1||c.Number("delay",0,0,60)!=0)
                    Fail(path,"Explicit timeline owns its schedule; use instant mode with no cast output, delay or automatic burst.");
                if(skill.TimelineRef==null)timelines.Register(authored);
                return SkillCastRoutingCompiler.Apply(new SkillDefinition{Id=new SkillNodeId(skill.RuntimeId),Timeline=authored.Id,Key=skill.Id,Name=skill.Name,
                    Cooldown=c.Number("cooldown",0,0,3600),EnergyCost=c.Number("cost",0,0,1000000),Mode=SkillCastMode.Instant},skill.Id,c,path,skills,dependencies);
            }
            float delay = c.Number("delay", 0, 0, 60);
            float interval = c.Number("interval", .1f, .001f, 60);
            int count = mode == "instant" ? c.Integer("count", 1, 1, 1000) : 1;
            if (delay + (count - 1) * interval > 120) Fail(path, "Cast schedule exceeds 120 seconds.");
            var payloads = new List<TimelinePayload>();
            for (int i = 0; i < count; i++) payloads.Add(new TimelinePayload
            { Time = delay + i * interval, RepeatInterval = mode == "channel" ? interval : 0, Effects = effects });
            var clips=new List<TimelineClip>();float tail=0;int movements=0;
            foreach(var clip in actionClips)
            { tail=Math.Max(tail,clip.End);if(clip.Kind==ClipKind.Dash){movements++;if((mode=="channel"||count>1)&&clip.End>interval+.00001f)Fail(path,"Dash duration/startup overlaps the cast interval.");} }
            if(movements+followups.Length>1)Fail(path,"Multiple simultaneous displacements are not allowed.");
            if(actionClips.Length>0&&mode=="channel"&&tail/interval>64)Fail(path,"Too many overlapping action clips; shorten duration or increase interval.");
            foreach(var followup in followups)tail=Math.Max(tail,followup.Time);
            for(int i=0;i<count;i++)
            {
                foreach(var prototype in actionClips){var clip=prototype;clip.OnPayload=true;clip.PayloadIndex=i;clips.Add(clip);}
                foreach(var prototype in followups){var payload=prototype;payload.OnPayload=true;payload.PayloadIndex=i;payloads.Add(payload);}
            }
            float length = mode == "channel"
                ? (c.Boolean("sustained", true) ? float.PositiveInfinity : c.Number("channelDuration", 3, .02f, 120))
                : Math.Max(c.Number("duration", .3f, .02f, 120), delay + (count - 1) * interval + tail + .02f);
            if(length>120&&mode!="channel")Fail(path,"Cast action schedule exceeds 120 seconds.");
            if (mode == "channel" && delay >= length) Fail(path + ".parameters.delay", "Delay must be shorter than the channel duration.");
            ChargeDefinition charge = null;
            if (mode == "charge")
            {
                float minimum = c.Number("chargeMin", .2f, 0, 120);
                float maximum = c.Number("chargeMax", 1, .02f, 120);
                if (minimum >= maximum) Fail(path + ".parameters.chargeMin", "Minimum charge must be shorter than maximum charge.");
                float minimumPower = c.Number("powerMin", 1, .01f, 100);
                charge = new ChargeDefinition
                {
                    MinimumTime = minimum, MaximumTime = maximum, MinimumPower = minimumPower,
                    MaximumPower = c.Number("powerMax", 3, minimumPower, 100), AutoRelease = c.Boolean("autoRelease", true)
                };
            }
            timelines.Register(new TimelineDefinition { Id = new TimelineId(skill.RuntimeId), Duration = length, Payloads = payloads.ToArray(),Clips=clips.ToArray() });
            return SkillCastRoutingCompiler.Apply(new SkillDefinition
            {
                Id = new SkillNodeId(skill.RuntimeId), Timeline = new TimelineId(skill.RuntimeId), Key = skill.Id, Name = skill.Name,
                Cooldown = c.Number("cooldown", .7f, 0, 3600), EnergyCost = c.Number("cost", 10, 0, 1000000),
                Mode = (SkillCastMode)Array.IndexOf(new[] { "instant", "channel", "charge" }, mode), Charge = charge,
                EnergyPerSecond = mode == "instant" ? 0 : c.Number("energyPerSecond", mode == "channel" ? 12 : 0, 0, 1000000),
                CostPerPulse = mode == "instant" && c.Boolean("costPerPulse", false)
            },skill.Id,c,path,skills,dependencies);
        }

        static void Fail(string path, string message) => throw new ContentValidationException(path, message);
    }
}
