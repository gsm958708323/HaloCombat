using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/StopMove")] public sealed class ActStopMoveAsset : BtNodeAsset { public override BtNode Bake() => new ActStopMove(); } }
