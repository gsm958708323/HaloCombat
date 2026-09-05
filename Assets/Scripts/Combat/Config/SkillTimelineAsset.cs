using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Timeline")]
    public sealed class SkillTimelineAsset : ScriptableObject
    {
        public int TimelineIdValue;
        public float Duration = 0.55f;
        public TimelineClipAsset[] Clips;
        public TimelinePayloadAsset[] Payloads;
        TimelineSO _baked;

        public TimelineSO Bake()
        {
            if (_baked != null) return _baked;
            var clipAssets = Clips ?? Array.Empty<TimelineClipAsset>();
            var clips = new TimelineClip[clipAssets.Length];
            for (int i = 0; i < clipAssets.Length; i++)
            {
                var c = clipAssets[i];
                if (c == null) continue;
                clips[i] = new TimelineClip
                {
                    Start = c.Start,
                    End = c.End,
                    Kind = c.Kind,
                    MoveX = c.MoveX,
                    MoveY = c.MoveY,
                    MoveZ = c.MoveZ,
                    Steer = c.Steer,
                    HitRadius = c.HitRadius,
                    HitOffsetX = c.HitOffsetX,
                    HitOffsetY = c.HitOffsetY,
                    HitOffsetZ = c.HitOffsetZ,
                    OnHit = c.HitProfile != null ? c.HitProfile.Bake() : Array.Empty<IEffect>()
                };
            }

            var payloadAssets = Payloads ?? Array.Empty<TimelinePayloadAsset>();
            var payloads = new TimelinePayload[payloadAssets.Length];
            for (int i = 0; i < payloadAssets.Length; i++)
            {
                var p = payloadAssets[i];
                if (p == null) continue;
                payloads[i] = new TimelinePayload { Time = p.Time, Effects = BakeEffects(p.Effects) };
            }

            _baked = new TimelineSO
            {
                Id = new TimelineId(TimelineIdValue),
                Duration = Duration,
                Clips = clips,
                Payloads = payloads
            };
            return _baked;
        }

        public void ClearCache()
        {
            _baked = null;
            if (Clips != null)
                for (int i = 0; i < Clips.Length; i++)
                    if (Clips[i] != null && Clips[i].HitProfile) Clips[i].HitProfile.ClearCache();
            if (Payloads == null) return;
            for (int i = 0; i < Payloads.Length; i++)
                if (Payloads[i] != null) ClearEffects(Payloads[i].Effects);
        }

        static IEffect[] BakeEffects(EffectAsset[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<IEffect>();
            var result = new IEffect[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = source[i] != null ? source[i].Bake() : null;
            return result;
        }

        static void ClearEffects(EffectAsset[] source)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i]) source[i].ClearCache();
        }

        void OnValidate() => ClearCache();
    }
}
