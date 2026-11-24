namespace Combat.Core
{
    public sealed class PlayPositionedCueEffect : IEffect
    {
        readonly int _id;
        readonly string _name;
        readonly EffectCenter _center;
        public PlayPositionedCueEffect(int id,string name,EffectCenter center){_id=id;_name=name;_center=center;}
        public void Apply(ref EffectContext context)
        {if(context.World!=null&&EffectPosition.TryResolve(context,_center,out var point))context.World.Events.Publish(new EvCue(_id,context.Source?.Id??EntityId.Invalid,_name,point));}
    }
}
