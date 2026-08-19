
namespace Combat.Core
{
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
}