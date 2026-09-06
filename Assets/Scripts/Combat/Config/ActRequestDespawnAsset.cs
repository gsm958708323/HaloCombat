using Combat.Core;
using UnityEngine;
namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Action/RequestDespawn")]
    public sealed class ActRequestDespawnAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActRequestDespawn();
    }
}
