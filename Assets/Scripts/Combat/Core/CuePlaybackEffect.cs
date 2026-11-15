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
        public CuePlaybackEffect(int id,string name,EffectCenter center,CuePlayback playback,string key,bool stopWithCast,string anchor=null)
        {_id=id;_name=name;_center=center;_playback=playback;_key=key;_stopWithCast=stopWithCast;_anchor=anchor;}
        public void Apply(ref EffectContext context)
        {
            if(context.World==null||!EffectPosition.TryResolve(context,_center,out var point))return;
            var anchor=_center==EffectCenter.Target?context.Target:context.Source;
            context.World.Events.Publish(new EvCue(_id,anchor?.Id??EntityId.Invalid,_name,point,_playback,_key,_stopWithCast,context.Origin,_anchor));
        }
    }
}
