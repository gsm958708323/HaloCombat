using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/Patrol")] public sealed class ActPatrolAsset : BtNodeAsset { public override BtNode Bake() => new ActPatrol(); } }
