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

        protected override IEffect BakeNew() => new PlayBuffArenaCueEffect(
            CueId, AnchorKey, InstanceKey, TargetIsVictim, AtCuePoint, Loop, Stop);
    }
}
