using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    internal static class AuthoredTimelineCompiler
    {
        public static TimelineDefinition Compile(TimelineDocument source,EffectGraphIndex graph,SkillEffectCompiler effects)
        {
            string path=graph.Path+".timeline";
            if(source.RuntimeId<=0)Fail(path+".runtimeId","Expected positive timeline identity.");
            Number(source.Duration,.001f,120,path+".duration");
            if(source.Animation!=null&&(string.IsNullOrWhiteSpace(source.Animation)||source.Animation.Length>80))Fail(path+".animation","Expected a nonempty animation semantic key up to80 characters.");
            if(source.ControlTags==null||source.ControlTags.Length>6||source.ControlTags.Distinct().Count()!=source.ControlTags.Length||source.ControlTags.Any(tag=>tag<1011||tag>1016))Fail(path+".controlTags","Expected unique control tags1011..1016.");
            if(source.Clips==null||source.Payloads==null||source.Clips.Length>128||source.Payloads.Length>256)Fail(path,"Invalid timeline arrays.");
            var ids=new HashSet<string>();
            var clips=source.Clips.Select(c=>
            {
                if(c==null)Fail(path+".clips","Null clip.");Identity(c.Id,ids,path+".clips");string p=path+".clips["+c.Id+"]";
                Number(c.Start,0,source.Duration,p+".start");Number(c.End,c.Start+.000001f,source.Duration,p+".end");
                Number(c.MoveX,-1000,1000,p+".moveX");Number(c.MoveY,-1000,1000,p+".moveY");Number(c.MoveZ,-1000,1000,p+".moveZ");Number(c.Steer,0,1,p+".steer");
                int kind=Array.IndexOf(new[]{"cancel","move","hitbox","iframe"},c.Kind);if(kind<0)Fail(p+".kind","Unknown clip kind.");
                Number(c.HitRadius,kind==2?.001f:0,100,p+".hitRadius");Number(c.HitOffsetX,-1000,1000,p+".hitOffsetX");Number(c.HitOffsetY,-1000,1000,p+".hitOffsetY");Number(c.HitOffsetZ,-1000,1000,p+".hitOffsetZ");
                if(kind!=2&&c.Effects?.Length>0)Fail(p+".effects","Only hitbox clips deliver effects.");
                if(!string.IsNullOrEmpty(c.Channel))
                {
                    if(kind!=0)Fail(p+".channel","Only cancel clips may specify a window channel.");
                    if(string.IsNullOrWhiteSpace(c.Channel)||c.Channel.Length>64)Fail(p+".channel","Expected a nonempty cancel channel up to 64 characters.");
                }
                return new TimelineClip{Start=c.Start,End=c.End,Kind=(ClipKind)(kind+1),CancelChannel=string.IsNullOrEmpty(c.Channel)?null:c.Channel,MoveX=c.MoveX,MoveY=c.MoveY,MoveZ=c.MoveZ,Steer=c.Steer,
                    HitRadius=c.HitRadius,HitOffsetX=c.HitOffsetX,HitOffsetY=c.HitOffsetY,HitOffsetZ=c.HitOffsetZ,OnHit=effects.Entries(c.Effects,p+".effects")};
            }).ToArray();
            var payloads=source.Payloads.Select(p=>
            {
                if(p==null)Fail(path+".payloads","Null payload.");Identity(p.Id,ids,path+".payloads");string q=path+".payloads["+p.Id+"]";
                Number(p.Time,0,source.Duration,q+".time");Number(p.RepeatInterval,0,120,q+".repeatInterval");
                return new TimelinePayload{Time=p.Time,RepeatInterval=p.RepeatInterval,Effects=effects.Entries(p.Effects,q+".effects")};
            }).ToArray();
            return new TimelineDefinition{Id=new TimelineId(source.RuntimeId),Duration=source.Duration,Animation=source.Animation,Clips=clips,Payloads=payloads,ControlTags=source.ControlTags.Select(tag=>new TagId(tag)).ToArray(),ScaleWithActionSpeed=source.ScaleWithActionSpeed};
        }
        static void Identity(string id,HashSet<string> ids,string path){if(string.IsNullOrWhiteSpace(id)||!ids.Add(id))Fail(path,"Missing or duplicate clip/payload identity.");}
        internal static void Number(float value,float minimum,float maximum,string path)
        {if(float.IsNaN(value)||float.IsInfinity(value)||value<minimum||value>maximum)Fail(path,"Expected finite number in ["+minimum+", "+maximum+"].");}
        static void Fail(string path,string reason)=>throw new ContentValidationException(path,reason);
    }
}
