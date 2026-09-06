using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Condition/InRange")] public sealed class CondInRangeAsset : BtNodeAsset { public override BtNode Bake() => new CondInRange(); } }
