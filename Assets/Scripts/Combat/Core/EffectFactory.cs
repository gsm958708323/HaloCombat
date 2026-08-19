using System;

namespace Combat.Core
{
    /// <summary>
    /// 调度器不 new 万能 Context；按类型组装专属 args。
    /// </summary>
    public sealed class EffectFactory
    {
        readonly IntentQueue _intents;

        public EffectFactory(IntentQueue intents)
        {
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
        }

        public Effect Create(in TimelineKey key, Actor self, TagComp tags)
        {
            switch (key.Type)
            {
                case EffectType.AddTag:
                    return new AddTagEffect(new AddTagEffectArgs(
                        tags, new TagId(key.TagValue), Math.Max(1, key.TagStacks)));

                case EffectType.RemoveTag:
                    return new RemoveTagEffect(new RemoveTagEffectArgs(
                        tags, new TagId(key.TagValue), Math.Max(1, key.TagStacks)));

                case EffectType.SpawnProjectile:
                    return new SpawnProjectileEffect(new SpawnProjectileEffectArgs(
                        _intents, self.Id, key.ProjectileSpecValue));

                case EffectType.AnimSignal:
                    return new AnimSignalEffect(new AnimSignalEffectArgs(
                        _intents, self.Id, key.AnimSignalName));

                default:
                    throw new NotSupportedException($"EffectType {key.Type}");
            }
        }
    }
}
