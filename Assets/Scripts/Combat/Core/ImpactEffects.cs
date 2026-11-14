using System;

namespace Combat.Core
{
    public sealed class LaunchEffect:IEffect
    {
        readonly float _speed;
        public LaunchEffect(float speed)=>_speed=speed;
        public void Apply(ref EffectContext context)
        {
            if(context.Target==null||context.Target.GetComp<TagComp>().Has(CommonTags.SuperArmor))return;
            if(context.Target.TryGetComp<LocomotionComp>(out var locomotion))locomotion.ImpulseVertical(_speed);
        }
    }
    public sealed class PullTowardPointEffect:IEffect
    {
        readonly float _distance;
        readonly EffectCenter _center;
        public PullTowardPointEffect(float distance,EffectCenter center){_distance=distance;_center=center;}
        public void Apply(ref EffectContext context)
        {
            var target=context.Target;
            if(target==null||!target.IsActive||target.GetComp<TagComp>().Has(CommonTags.Dead)||
                !target.TryGetComp<LocomotionComp>(out var locomotion)||!EffectPosition.TryResolve(context,_center,out var point))return;
            var from=target.GetComp<TransformComp>().Position;
            float dx=point.X-from.X,dz=point.Z-from.Z,distance=(float)Math.Sqrt(dx*dx+dz*dz);
            if(distance<.000001f)return;float ratio=Math.Min(distance,_distance)/distance;
            locomotion.RequestHitDelta(dx*ratio,0,dz*ratio);
        }
    }
    public readonly struct EvShield
    {
        public readonly EntityId Target;
        public readonly float Amount;
        public readonly SkillOrigin Origin;
        public EvShield(EntityId target,float amount,SkillOrigin origin){Target=target;Amount=amount;Origin=origin;}
    }
    public sealed class AddShieldEffect:IEffect
    {
        readonly float _amount;
        public AddShieldEffect(float amount)=>_amount=amount;
        public void Apply(ref EffectContext context)
        {
            var target=context.Target;if(target==null||!target.IsActive||target.GetComp<TagComp>().Has(CommonTags.Dead))return;
            var attributes=target.GetComp<AttributeSet>();float amount=Math.Max(0,_amount);
            attributes.SetBase(AttrId.Shield,attributes.GetBase(AttrId.Shield)+amount);
            context.World.Events.Publish(new EvShield(target.Id,amount,context.Origin));
        }
    }
}
