using System;

namespace Combat.Core
{
    /// <summary>
    /// 调度器不 new 万能 Context；按类型组装专属 args。
    /// </summary>
    public sealed class EffectFactory
    {
        readonly IntentQueue _intents;
        readonly AoESpecLibrary _aoeSpecs;
        public EffectFactory(IntentQueue intents, AoESpecLibrary aoeSpecs = null)
        {
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _aoeSpecs = aoeSpecs; // Pulse/Burst 用到时再要求非空
        }
        public Effect Create(in TimelineKey key, Actor self)
        {
            var tags = self.GetComp<TagComp>();
            switch (key.Type)
            {
                case EffectType.AddTag:
                    return new AddTagEffect(
                        tags,
                        new TagId(key.TagValue),
                        key.TagStacks,
                        key.IsInterval);
                case EffectType.RemoveTag:
                    return new RemoveTagEffect(tags, new TagId(key.TagValue), key.TagStacks);
                case EffectType.AnimSignal:
                    return new AnimSignalEffect(new AnimSignalEffectArgs(
                        _intents, self.Id, key.AnimSignalName));
                case EffectType.SpawnProjectile:
                    return new SpawnProjectileEffect(new SpawnProjectileEffectArgs(
                        _intents, self.Id, key.ProjectileSpecValue));
                case EffectType.MoveOffset:
                    {
                        var loco = self.GetComp<LocomotionComp>();
                        float dur = key.IsInterval ? (key.EndTime - key.Time) : 0f;
                        return new MoveOffsetEffect(
                            loco,
                            key.MoveX, key.MoveY, key.MoveZ,
                            key.MoveAsVelocity,
                            key.IsInterval,
                            dur);
                    }
                case EffectType.AoEBurst:
                    if (_aoeSpecs == null)
                        throw new InvalidOperationException("AoESpecLibrary not injected");
                    return new AoEBurstEffect(new AoEBurstEffectArgs(
                        _intents, self, _aoeSpecs, key.AoESpecValue));
                case EffectType.PulseZone:
                    if (_aoeSpecs == null)
                        throw new InvalidOperationException("AoESpecLibrary not injected");
                    return new SpawnPulseZoneEffect(new SpawnPulseZoneEffectArgs(
                        _intents, self.Id, key.PulseZoneSpecValue));
                default:
                    throw new NotSupportedException(key.Type.ToString());
            }
        }
    }
}
