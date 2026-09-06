using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/MoveToward")] public sealed class ActMoveTowardAsset : BtNodeAsset { public override BtNode Bake() => new ActMoveToward(); } }
