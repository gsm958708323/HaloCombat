using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    public abstract partial class EffectAsset : ScriptableObject
    {
        protected abstract IEffect BakeNew();
        public virtual IEffect Bake() => BakeNew();
        public virtual void ClearCache() { }
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Damage")]
    public sealed partial class DamageEffectAsset : EffectAsset
    {
        public float Coeff = 1f;
        public float Flat;
        public bool IgnoreDef;
        public bool CanCrit = true;
        public bool UseSnapshotAtk = true;
        public bool ScaleByBuffStacks;
        public int HitstopFrames;
        public float CritMul = 2f;

        protected override IEffect BakeNew() => new DamageEffect
        {
            Coeff = Coeff,
            Flat = Flat,
            IgnoreDef = IgnoreDef,
            CanCrit = CanCrit,
            UseSnapshotAtk = UseSnapshotAtk,
            ScaleByBuffStacks = ScaleByBuffStacks,
            HitstopFrames = HitstopFrames,
            CritMul = CritMul
        };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/HitStun")]
    public sealed partial class HitStunAsset : EffectAsset
    {
        public float Duration = 0.35f;
        public float IFrameDuration;
        protected override IEffect BakeNew() => new HitStunEffect { Duration = Duration, IFrameDuration = IFrameDuration };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Knockback")]
    public sealed partial class KnockbackAsset : EffectAsset
    {
        public float Distance = 0.4f;
        protected override IEffect BakeNew() => new KnockbackEffect { Distance = Distance };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Knockdown")]
    public sealed partial class KnockdownAsset : EffectAsset
    {
        public float Duration = 0.8f;
        protected override IEffect BakeNew() => new KnockdownEffect { Duration = Duration };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/IFrame")]
    public sealed partial class IFrameAsset : EffectAsset
    {
        public float Duration = 0.1f;
        protected override IEffect BakeNew() => new IFrameEffect { Duration = Duration };
    }

    [CreateAssetMenu(menuName = "Combat/Effects/PlayCue")]
    public sealed partial class PlayCueAsset : EffectAsset
    {
        public int CueId;
        public string CueName;
        protected override IEffect BakeNew() => new PlayCueEffect(CueId, CueName);
    }

    [CreateAssetMenu(menuName = "Combat/Effects/SpawnProjectile")]
    public sealed partial class SpawnProjectileAsset : EffectAsset
    {
        public int SpecId;
        protected override IEffect BakeNew() => new SpawnProjectileEffect(SpecId);
    }

    [CreateAssetMenu(menuName = "Combat/Effects/SpawnAoe")]
    public sealed partial class SpawnAoeAsset : EffectAsset
    {
        public int SpecId;
        protected override IEffect BakeNew() => new SpawnAoeEffect(SpecId);
    }

    [CreateAssetMenu(menuName = "Combat/Effects/SpawnSummon")]
    public sealed partial class SpawnSummonAsset : EffectAsset
    {
        public int SpecId;
        protected override IEffect BakeNew() => new SpawnSummonEffect(SpecId);
    }

    [CreateAssetMenu(menuName = "Combat/Effects/Dispel")]
    public sealed partial class DispelAsset : EffectAsset
    {
        public DispelMode Mode;
        public int Key;
        public int TagValue;
        public int MaxCount;
        protected override IEffect BakeNew() => new DispelEffect(Mode, Key, new TagId(TagValue), MaxCount);
    }

    [CreateAssetMenu(menuName = "Combat/Effects/ApplyDuration")]
    public sealed partial class ApplyDurationAsset : EffectAsset
    {
        public DurationSpecAsset Spec;
        public int Stacks = 1;
        protected override IEffect BakeNew() => Spec == null
            ? new ApplyDurationEffect(null, Stacks)
            : new ApplyDurationEffect(Spec.Bake(), Stacks);

        public override void ClearCache()
        {
            if (Spec != null) Spec.ClearCache();
        }
    }
}
