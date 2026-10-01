using System;
using Combat.Core;

namespace Combat.Config
{
    // Only the two [Serializable] payload/clip shapes live here now: they are inline data inside
    // SkillTimelineAsset, not assets of their own, so they need no MonoScript and can share a file.
    // The season-1 ScriptableObject authoring schema (the database asset, its combo/summon/character
    // definitions and the behaviour-tree node assets) was never wired to anything and has been removed.
    [Serializable]
    public sealed class TimelineClipAsset
    {
        public float Start;
        public float End;
        public ClipKind Kind;
        public float MoveX;
        public float MoveY;
        public float MoveZ;
        public float Steer;
        public float HitRadius;
        public float HitOffsetX;
        public float HitOffsetY;
        public float HitOffsetZ;
        public HitProfileAsset HitProfile;
    }

    [Serializable]
    public sealed class TimelinePayloadAsset
    {
        public float Time;
        public EffectAsset[] Effects;
    }
}
