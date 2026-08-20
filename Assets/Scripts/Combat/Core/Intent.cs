
namespace Combat.Core
{
    // 4.5 已有：Owner + SpecValue。本步在 Service 内用 Owner 的 Transform 推出生点。
    public readonly struct SpawnProjectileIntent
    {
        public readonly EntityId Owner;
        public readonly int SpecValue;
        public SpawnProjectileIntent(EntityId owner, int specValue)
        {
            Owner = owner;
            SpecValue = specValue;
        }
    }

    public readonly struct AnimSignalIntent
    {
        public readonly EntityId Source;
        public readonly string Signal;
        public AnimSignalIntent(EntityId source, string signal)
        {
            Source = source;
            Signal = signal ?? string.Empty;
        }
    }

    public readonly struct HitIntent
    {
        public readonly EntityId Source;     // 投射物或角色
        public readonly EntityId Target;
        public readonly EntityId Owner;      // 投射物主人（伤害归属）
        public readonly int AttackSpecValue;
        public readonly int SourceSkillValue; // 可 0
        public HitIntent(
            EntityId source,
            EntityId target,
            EntityId owner,
            int attackSpecValue,
            int sourceSkillValue = 0)
        {
            Source = source;
            Target = target;
            Owner = owner;
            AttackSpecValue = attackSpecValue;
            SourceSkillValue = sourceSkillValue;
        }
    }
}