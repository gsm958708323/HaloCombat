namespace Combat.Core
{
    public enum HitFeedbackLevel : byte {None,Light,ComboConfirm,Heavy,Finisher}
    public readonly struct EvHurt
    {
        public readonly EntityId Source,Target;
        public readonly HitFeedbackLevel Feedback;
        public readonly SkillOrigin Origin;
        public EvHurt(EntityId source,EntityId target,HitFeedbackLevel feedback,SkillOrigin origin=default)
        {Source=source;Target=target;Feedback=feedback;Origin=origin;}
    }
    public sealed class HurtFeedbackEffect : IEffect
    {
        readonly HitFeedbackLevel _level;
        readonly bool _requireDamage;
        public HurtFeedbackEffect(HitFeedbackLevel level=HitFeedbackLevel.Light,bool requireHpDamage=true){_level=level;_requireDamage=requireHpDamage;}
        public void Apply(ref EffectContext context)
        {
            var target=context.Target;var damage=context.Damage;
            if(context.World==null||target==null||!target.IsActive||!target.TryGetComp<AttributeSet>(out var attributes)||attributes.GetBase(AttrId.Hp)<=0||target.GetComp<TagComp>().Has(CommonTags.Dead))return;
            if(_requireDamage&&(damage==null||damage.HpLost<=0||damage.Target!=target.Id||damage.Source!=(context.Source?.Id??EntityId.Invalid)))return;
            context.World.Events.Publish(new EvHurt(context.Source?.Id??EntityId.Invalid,target.Id,_level,context.Origin));
        }
    }
}
