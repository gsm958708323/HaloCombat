namespace Combat.Core
{
    // Keyed presentation is an event contract. No Unity object or visual timer enters Core.
    public sealed class CuePlaybackEffect : IEffect
    {
        readonly int _id;
        readonly string _name,_key;
        readonly EffectCenter _center;
        readonly CuePlayback _playback;
        readonly bool _stopWithCast;
        readonly string _anchor;
        readonly bool _stacksAsTier;
        readonly int _maxTier;
        public CuePlaybackEffect(int id,string name,EffectCenter center,CuePlayback playback,string key,bool stopWithCast,string anchor=null,bool stacksAsTier=false,int maxTier=3)
        {_id=id;_name=name;_center=center;_playback=playback;_key=key;_stopWithCast=stopWithCast;_anchor=anchor;_stacksAsTier=stacksAsTier;_maxTier=System.Math.Max(1,maxTier);}
        public void Apply(ref EffectContext context)
        {
            if(context.World==null||!EffectPosition.TryResolve(context,_center,out var point))return;
            var anchor=_center==EffectCenter.Target?context.Target:context.Source;
            if(!_stacksAsTier){Publish(context,anchor,point,_key);return;}
            if(_playback==CuePlayback.Stop)
                for(int tier=1;tier<=_maxTier;tier++)Publish(context,anchor,point,_key+"_"+tier);
            else Publish(context,anchor,point,_key+"_"+System.Math.Min(_maxTier,System.Math.Max(1,context.BuffStacks)));
        }
        void Publish(in EffectContext context,Actor anchor,SimVec3 point,string key)
            =>context.World.Events.Publish(new EvCue(_id,anchor?.Id??EntityId.Invalid,_name,point,_playback,key,_stopWithCast,context.Origin,_anchor));
    }
}
