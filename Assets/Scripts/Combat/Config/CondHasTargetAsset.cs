using Combat.Core;
using UnityEngine;
namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Condition/HasTarget")]
    public sealed class CondHasTargetAsset : BtNodeAsset
    {
        public override BtNode Bake() => new CondHasTarget();
    }
}
