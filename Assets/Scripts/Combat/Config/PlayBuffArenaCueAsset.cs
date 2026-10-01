using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    // Own file on purpose. Unity only mints a MonoScript for the class whose name matches
    // its file name; a ScriptableObject class without one serialises as m_Script: 0 and
    // deserialises to null after a domain reload.
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
        [Tooltip("勾选后把 Buff 层数拼进 InstanceKey，让每层各挂一份同特效实例，用粒子密度区分层数。")]
        public bool StacksAsTier;
        [Tooltip("层数分级的层数上限，应与 Buff 的 MaxStacks 一致。")]
        public int MaxTier = 3;

        protected override IEffect BakeNew() => new PlayBuffArenaCueEffect(
            CueId, AnchorKey, InstanceKey, TargetIsVictim, AtCuePoint, Loop, Stop, StacksAsTier, MaxTier);
    }
}
