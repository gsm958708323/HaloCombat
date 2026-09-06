using Combat.Core;
using UnityEngine;
namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Condition/OwnerDead")]
    public sealed class CondOwnerDeadAsset : BtNodeAsset
    {
        public override BtNode Bake() => new CondOwnerDead();
    }
}
