using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/Hurt Feedback")]
    public sealed class HurtFeedbackAsset : EffectAsset
    {
        protected override IEffect BakeNew() => new HurtFeedbackEffect();
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Refill Ammo")]
    public sealed class RefillAmmoAsset : EffectAsset
    {
        public int Amount = 60;
        protected override IEffect BakeNew() => new RefillAmmoEffect(Amount);
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Spawn Barrel")]
    public sealed class SpawnBarrelAsset : EffectAsset
    {
        [Tooltip("Forward offset from the caster, in metres.")]
        public float ForwardOffset = .55f;
        public float MaxHp = 5f;
        protected override IEffect BakeNew() => new SpawnBuffArenaBarrelEffect
        {
            ForwardOffset = ForwardOffset,
            MaxHp = MaxHp
        };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Barrel Explosion")]
    public sealed class BarrelExplosionAsset : EffectAsset
    {
        public float Radius = 2.2f;
        public float DamageCoeff = .15f;
        protected override IEffect BakeNew() => new BarrelExplosionEffect
        {
            Radius = Radius,
            DamageCoeff = DamageCoeff
        };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Pull To Point")]
    public sealed class PullToPointAsset : EffectAsset
    {
        public float Strength = 1f;
        protected override IEffect BakeNew() => new PullToPointEffect { Strength = Strength };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Play Buff Arena Cue")]
    public sealed class PlayBuffArenaCueAsset : EffectAsset
    {
        public int CueId;
        public string AnchorKey = "";
        public string InstanceKey = "";
        public bool TargetIsVictim;
        public bool AtCuePoint;
        public bool Loop;
        public bool Stop;

        protected override IEffect BakeNew() => new PlayBuffArenaCueEffect(
            CueId, AnchorKey, InstanceKey, TargetIsVictim, AtCuePoint, Loop, Stop);
    }
}
