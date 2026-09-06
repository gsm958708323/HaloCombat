using Combat.Core;
using UnityEngine;
namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Action/Wait")]
    public sealed class ActWaitAsset : BtNodeAsset
    {
        public float Duration = .5f;
        public override BtNode Bake() => new ActWait(Duration);
    }
}
