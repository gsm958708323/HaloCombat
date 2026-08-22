using System;

namespace Combat.Core
{
    public enum EffectType : byte
    {
        AddTag = 1,
        RemoveTag = 2,
        SpawnProjectile = 3,
        AnimSignal = 4,
        MoveOffset = 5,          // 区间或瞬时位移
        AoEBurst = 6,   // 瞬时
        PulseZone = 7,  // 生成火池 Actor（可选）
    }

    /// <summary>
    /// Time：触发点（区间=进入点）
    /// EndTime：&lt; Time 表示瞬时；&gt;= Time 表示区间 [Time, EndTime]
    /// </summary>
    [Serializable]
    public struct TimelineKey
    {
        public float Time;
        public float EndTime;   // 瞬时：设为 -1f
        public EffectType Type;
        public int TagValue;
        public int TagStacks;
        public int ProjectileSpecValue;
        public string AnimSignalName;
        // MoveOffset：区间内按秒速度；瞬时则吃一次性 Offset
        public float MoveX;
        public float MoveY;
        public float MoveZ;
        public bool MoveAsVelocity; // true: 每秒；false: 整段总位移按时长均分，或瞬时一次施加
        public bool IsInterval => EndTime >= Time;
        public int AoESpecValue;
        public int PulseZoneSpecValue;
        public static TimelineKey Instant(float time, EffectType type)
        {
            return new TimelineKey { Time = time, EndTime = -1f, Type = type };
        }
    }

    [Serializable]
    public sealed class TimelineSO
    {
        public TimelineId Id;
        public float Duration = 0.6f;
        public TimelineKey[] Keys = Array.Empty<TimelineKey>();
#if UNITY_EDITOR || DEBUG
        public void ValidateOrThrow()
        {
            if (!Id.IsValid) throw new InvalidOperationException("Timeline Id invalid");
            if (Duration <= 0f) throw new InvalidOperationException("Duration must be > 0");
            if (Keys == null) return;
            for (int i = 0; i < Keys.Length; i++)
            {
                var k = Keys[i];
                if (k.Time < 0f || k.Time > Duration)
                    throw new InvalidOperationException($"Key[{i}] Time out of range");
                if (k.IsInterval && k.EndTime > Duration)
                    throw new InvalidOperationException($"Key[{i}] EndTime > Duration");
            }
        }
#endif
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
            var tlG1 = new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.60f,
                Keys = new[]
                 {
                    // Cancel 区间 [0.05, 0.45]
                    new TimelineKey
                    {
                        Time = 0.05f, EndTime = 0.45f,
                        Type = EffectType.AddTag,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    // 前进冲量：区间速度 (3,0,0) m/s，持续 0.20s → 约 0.6m
                    new TimelineKey
                    {
                        Time = 0.10f, EndTime = 0.30f,
                        Type = EffectType.MoveOffset,
                        MoveX = 3f, MoveY = 0f, MoveZ = 0f,
                        MoveAsVelocity = true
                    },
                    new TimelineKey
                    {
                        Time = 0.20f, EndTime = -1f,
                        Type = EffectType.AnimSignal,
                        AnimSignalName = "G1_Slash"
                    },
                    new TimelineKey
                    {
                        Time = 0.25f, EndTime = -1f,
                        Type = EffectType.SpawnProjectile,
                        ProjectileSpecValue = 901
                    },
                }
            };
            var tlG2 = new TimelineSO
            {
                Id = TimelineId.TL_G2,
                Duration = 0.40f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.00f, EndTime = 0.25f,
                        Type = EffectType.AddTag,
                        TagValue = CommonTags.Cancel.Value, TagStacks = 1
                    },
                    new TimelineKey
                    {
                        Time = 0.00f, EndTime = 0.20f,
                        Type = EffectType.MoveOffset,
                        MoveX = 2f, MoveAsVelocity = true
                    },
                }
            };
#if DEBUG
            tlG1.ValidateOrThrow();
            tlG2.ValidateOrThrow();
#endif

            var library = new TimelineLibrary();
            library.Register(tlG1);
            library.Register(tlG2);
            return library;
        }
    }
}


