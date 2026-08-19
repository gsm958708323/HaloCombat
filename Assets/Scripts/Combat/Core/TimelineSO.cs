using System;

namespace Combat.Core
{
    public enum EffectType : byte
    {
        AddTag = 1,
        RemoveTag = 2,
        SpawnProjectile = 3,
        AnimSignal = 4,
        // 后续：LocomotionImpulse / HitboxEnable / AoEPulse ...
    }

    /// <summary>轴上一条键控。payload 按 EffectType 解释，避免万能 Context。</summary>
    [Serializable]
    public struct TimelineKey
    {
        public float Time;
        public EffectType Type;
        public bool FireOnce; // true=到点触发一次；false=可做区间（MVP 先做 FireOnce）

        // —— 分类型字段（配置层允许并列；运行时按 Type 读取对应字段）——
        public int TagValue;
        public int TagStacks;
        public int ProjectileSpecValue;
        public string AnimSignalName;
    }

    [Serializable]
    public sealed class TimelineSO
    {
        public TimelineId Id;
        public float Duration = 0.6f;
        public TimelineKey[] Keys = Array.Empty<TimelineKey>();
    }

    /// <summary>按 Id 取轴。MVP 内存库；商用可换 Addressables/SO 引用。</summary>
    public sealed class TimelineLibrary
    {
        readonly System.Collections.Generic.Dictionary<int, TimelineSO> _map =
            new System.Collections.Generic.Dictionary<int, TimelineSO>(32);

        public void Register(TimelineSO so)
        {
            if (so == null || !so.Id.IsValid)
                throw new ArgumentException("Invalid TimelineSO");
            _map[so.Id.Value] = so;
        }

        public bool TryGet(TimelineId id, out TimelineSO so)
            => _map.TryGetValue(id.Value, out so);

        public static TimelineLibrary Create()
        {
            // --- 配置：G1 轴 ---
            var tlG1 = new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.6f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.05f, Type = EffectType.AddTag, FireOnce = true,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    new TimelineKey
                    {
                        Time = 0.20f, Type = EffectType.AnimSignal, FireOnce = true,
                        AnimSignalName = "G1_Slash"
                    },
                    new TimelineKey
                    {
                        Time = 0.25f, Type = EffectType.SpawnProjectile, FireOnce = true,
                        ProjectileSpecValue = 901
                    },
                    new TimelineKey
                    {
                        Time = 0.45f, Type = EffectType.RemoveTag, FireOnce = true,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                }
            };

            var tlG2 = new TimelineSO
            {
                Id = TimelineId.TL_G2,
                Duration = 0.5f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.05f, Type = EffectType.AddTag, FireOnce = true,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    new TimelineKey
                    {
                        Time = 0.40f, Type = EffectType.RemoveTag, FireOnce = true,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                }
            };

            var library = new TimelineLibrary();
            library.Register(tlG1);
            library.Register(tlG2);
            return library;
        }
    }
}


