using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Effects/PlayCue")]
    public sealed class PlayCueAsset : EffectAsset
    {
        public int CueId;
        public string CueName;
        protected override IEffect BakeNew() => new PlayCueEffect(CueId, CueName);
    }
}
