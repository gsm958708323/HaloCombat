using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/FollowOwner")] public sealed class ActFollowOwnerAsset : BtNodeAsset { public override BtNode Bake() => new ActFollowOwner(); } }
