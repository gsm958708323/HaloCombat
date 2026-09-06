using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/MoveTowardHome")] public sealed class ActMoveTowardHomeAsset : BtNodeAsset { public override BtNode Bake() => new ActMoveTowardHome(); } }
